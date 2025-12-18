using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace Superchef.Controllers;

public class StoreController : Controller
{
    private readonly DB db;
    private readonly IConfiguration cf;
    private readonly ImageService imgSrv;
    private readonly CleanupService clnSrv;

    public StoreController(DB db, IConfiguration cf, ImageService imgSrv, CleanupService clnSrv)
    {
        this.db = db;
        this.cf = cf;
        this.imgSrv = imgSrv;
        this.clnSrv = clnSrv;
    }

    [HttpGet]
    [Route("Store/Info/{slug}")]
    public IActionResult Info(string slug)
    {
        var store = db.Stores
            .Include(s => s.Venue)
            .Include(s => s.Items)
                .ThenInclude(r => r.Reviews)
            .Include(s => s.Items)
                .ThenInclude(v => v.Variants)
                    .ThenInclude(oi => oi.OrderItems)
            .FirstOrDefault(s => s.Slug == slug && !s.IsDeleted);
        if (store == null)
        {
            return NotFound();
        }

        return View(store);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Manage(ManageStoreVM vm)
    {
        var vendor = db.Accounts.FirstOrDefault(a => a.Id == vm.Id && !a.IsDeleted && a.AccountType.Name == "Vendor");
        if (vendor == null)
        {
            return NotFound();
        }

        Dictionary<string, Expression<Func<Store, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Slug", a => a.Slug },
            { "Items Count", a => a.Items.Count },
            { "Venue", a => a.Venue.Name }
        };
        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "name", Text = "Search By Name" },
            new() { Value = "slug", Text = "Search By Slug" },
            new() { Value = "id", Text = "Search By Id" }
        ];
        vm.AvailableVenues = db.Venues.ToList();

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Stores
            .Include(s => s.Venue)
            .Include(s => s.Items)
            .Where(s => s.AccountId == vendor.Id && !s.IsDeleted)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(s => s.Name.Contains(search));
                    break;
                case "slug":
                    results = results.Where(s => s.Slug.Contains(search));
                    break;
                case "id":
                    results = results.Where(s => s.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.Venues.Count > 0)
        {
            results = results.Where(s => vm.Venues.Contains(s.VenueId));
        }

        if (vm.MinItemsCount != null)
        {
            results = results.Where(s => s.Items.Count(i => !i.IsDeleted) >= vm.MinItemsCount);
        }

        if (vm.MaxItemsCount != null)
        {
            results = results.Where(s => s.Items.Count(i => !i.IsDeleted) <= vm.MaxItemsCount);
        }

        // Sort
        results = vm.Dir == "asc"
            ? results.OrderBy(sortOptions[vm.Sort])
            : results.OrderByDescending(sortOptions[vm.Sort]);

        vm.Results = results.ToPagedList(vm.Page, 10);

        if (Request.IsAjax())
        {
            return PartialView("_Manage", vm);
        }

        ViewBag.VendorName = vendor.Name;

        return View(vm);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Select(int id, string? ReturnUrl)
    {
        var store = db.Stores
            .FirstOrDefault(s =>
                s.Id == id &&
                s.AccountId == HttpContext.GetAccount()!.Id &&
                !s.IsDeleted
            );

        if (store == null)
        {
            return NotFound();
        }

        HttpContext.Session.SetInt32("StoreId", store.Id);

        if (ReturnUrl != null)
        {
            return Redirect(ReturnUrl);
        }

        return RedirectToAction("Edit", "Store");
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Add()
    {
        var vm = new AddStoreVM
        {
            AvailableVenues = db.Venues.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };
        vm.Venue = int.Parse(vm.AvailableVenues.First().Value);

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Vendor")]
    public IActionResult Add(AddStoreVM vm)
    {
        if (ModelState.IsValid("Name") && !IsNameUnique(vm.Name, vm.Venue))
        {
            ModelState.AddModelError("Name", "Name has been taken in this venue.");
        }

        if (ModelState.IsValid("Slug") && !IsSlugUnique(vm.Slug))
        {
            ModelState.AddModelError("Slug", "Slug has been taken.");
        }

        if (ModelState.IsValid("Venue") && !CheckVenue(vm.Venue))
        {
            ModelState.AddModelError("Venue", "Venue is invalid.");
        }

        if (vm.Image != null)
        {
            var e = imgSrv.ValidateImage(vm.Image, 5);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        string? newImageFile = null;
        if (ModelState.IsValid && vm.Image != null)
        {
            try
            {
                newImageFile = imgSrv.SaveImage(vm.Image, "store", 700, 700, vm.ImageX, vm.ImageY, vm.ImageScale);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid && newImageFile != null)
        {
            var store = new Store
            {
                Name = vm.Name,
                Slug = vm.Slug,
                Description = vm.Description,
                Image = newImageFile,
                Banner = null,
                SlotMaxOrders = vm.SlotMaxOrders,
                StripeAccountId = null,
                VenueId = vm.Venue,
                AccountId = HttpContext.GetAccount()!.Id
            };

            db.Stores.Add(store);
            db.SaveChanges();

            TempData["Message"] = "Store created successfully";
            return RedirectToAction("Edit", "Store", new { id = store.Id });
        }

        vm.AvailableVenues = db.Venues.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList();

        return View(vm);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Edit(int? id)
    {
        if (id == null)
        {
            var sessionStoreId = HttpContext.Session.GetInt32("StoreId");
            if (sessionStoreId == null)
            {
                TempData["Message"] = "Please choose a store first";
                return RedirectToAction("Vendor", "Home", new { ReturnUrl = Url.Action("Edit") });
            }

            return RedirectToAction("Edit", new { id = sessionStoreId });
        }

        var store = db.Stores.FirstOrDefault(s =>
            s.Id == id &&
            s.AccountId == HttpContext.GetAccount()!.Id &&
            !s.IsDeleted
        );
        if (store == null)
        {
            TempData["Message"] = "Store not found! Please choose a store";
            return RedirectToAction("Vendor", "Home", new { ReturnUrl = Url.Action("Edit") });
        }

        var vm = new EditStoreVM
        {
            Id = store.Id,
            VendorId = store.AccountId,
            StripeAccountId = store.StripeAccountId,
            Name = store.Name,
            Slug = store.Slug,
            Description = store.Description,
            SlotMaxOrders = store.SlotMaxOrders,
            Venue = store.VenueId,

            AvailableVenues = db.Venues.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };

        ViewBag.ImageUrl = $"/uploads/store/{store.Image}";
        ViewBag.BannerImageUrl = store.Banner != null ? $"/uploads/banner/{store.Banner}" : null;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Vendor")]
    public IActionResult Edit(EditStoreVM vm)
    {
        var store = db.Stores.FirstOrDefault(s =>
            s.Id == vm.Id &&
            s.AccountId == HttpContext.GetAccount()!.Id &&
            !s.IsDeleted
        );
        if (store == null)
        {
            TempData["Message"] = "Store not found! Please choose a store";
            return RedirectToAction("Vendor", "Home", new { ReturnUrl = Url.Action("Edit") });
        }

        if (ModelState.IsValid("Name") && !IsNameUnique(vm.Name, vm.Venue, vm.Id))
        {
            ModelState.AddModelError("Name", "Name has been taken in this venue.");
        }

        if (ModelState.IsValid("Slug") && !IsSlugUnique(vm.Slug, vm.Id))
        {
            ModelState.AddModelError("Slug", "Slug has been taken.");
        }

        if (ModelState.IsValid("Venue") && !CheckVenue(vm.Venue))
        {
            ModelState.AddModelError("Venue", "Venue is invalid.");
        }

        if (vm.Image != null)
        {
            var e = imgSrv.ValidateImage(vm.Image, 5);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        if (vm.BannerImage != null && !vm.BannerRemoveImage)
        {
            var e = imgSrv.ValidateImage(vm.BannerImage, 10);
            if (e != "") ModelState.AddModelError("BannerImage", e);
        }

        if (ModelState.IsValid && vm.Image != null)
        {
            try
            {
                var newFile = imgSrv.SaveImage(vm.Image, "store", 700, 700, vm.ImageX, vm.ImageY, vm.ImageScale);

                // remove image
                if (store.Image != null) imgSrv.DeleteImage(store.Image, "store");
                store.Image = newFile;
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            if (vm.BannerRemoveImage)
            {
                // remove image
                if (store.Banner != null)
                {
                    imgSrv.DeleteImage(store.Banner, "banner");
                    store.Banner = null;
                    db.SaveChanges();
                }
            }
            else if (vm.BannerImage != null)
            {
                try
                {
                    var newFile = imgSrv.SaveImage(vm.BannerImage, "banner", 2025, 675, vm.BannerImageX, vm.BannerImageY, vm.BannerImageScale);

                    // remove image
                    if (store.Banner != null) imgSrv.DeleteImage(store.Banner, "banner");
                    store.Banner = newFile;
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("BannerImage", ex.Message);
                }
            }
        }

        if (ModelState.IsValid)
        {
            store.Name = vm.Name.Trim();
            store.Slug = vm.Slug;
            store.Description = vm.Description.Trim();
            store.VenueId = vm.Venue;
            store.SlotMaxOrders = vm.SlotMaxOrders;

            var tmpNow = DateTime.Now;
            foreach (var slot in store.Slots)
            {
                if (DateOnly.FromDateTime(slot.StartTime) == DateOnly.FromDateTime(tmpNow.AddDays(3)))
                {
                    slot.MaxOrders = vm.SlotMaxOrders;
                }
            }

            db.SaveChanges();

            TempData["Message"] = "Store updated successfully";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        vm.AvailableVenues = db.Venues.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList();

        ViewBag.ImageUrl = $"/uploads/store/{store.Image}";
        ViewBag.BannerImageUrl = store.Banner != null ? $"/uploads/banner/{store.Banner}" : null;

        return View(vm);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult SetupSlots(SetupSlotVM vm)
    {
        var store = db.Stores
            .FirstOrDefault(s =>
                s.Id == vm.Id &&
                !s.IsDeleted &&
                s.AccountId == HttpContext.GetAccount()!.Id &&
                !s.HasPublishedFirstSlots
            );
        if (store == null)
        {
            return NotFound();
        }

        var tmpNow = DateTime.Now;
        List<DateOnly> availableDates = [
            DateOnly.FromDateTime(tmpNow),
            DateOnly.FromDateTime(tmpNow.AddDays(1)),
            DateOnly.FromDateTime(tmpNow.AddDays(2))
        ];

        vm.AvailableSlots = [];
        foreach (var date in availableDates)
        {
            vm.AvailableSlots.Add(
                date,
                db.SlotTemplates
                    .Where(s => s.DayOfWeek == (int)date.DayOfWeek)
                    .Select(s => new DateTime(
                        date.Year, date.Month, date.Day,
                        s.StartTime.Hour, s.StartTime.Minute, 0
                    ))
                    .ToList()
            );
        }

        if (Request.Method == "GET")
        {
            vm.Slots = [];

            ViewBag.StoreName = store.Name;
            return View(vm);
        }

        // Validate Slots
        foreach (var slot in vm.Slots)
        {
            var date = DateOnly.FromDateTime(slot);
            if (!vm.AvailableSlots.ContainsKey(date))
            {
                return BadRequest("Invalid date! Refresh the page and try again");
            }

            if (!vm.AvailableSlots[date].Contains(slot))
            {
                return BadRequest("Invalid slot! Refresh the page and try again");
            }
        }

        // Add slots
        foreach (var slotDateTime in vm.Slots)
        {
            var slot = new Slot
            {
                StartTime = slotDateTime,
                EndTime = slotDateTime.AddMinutes(30),
                MaxOrders = store.SlotMaxOrders,
                StoreId = vm.Id
            };
            db.Slots.Add(slot);
        }
        
        store.HasPublishedFirstSlots = true;
        db.SaveChanges();

        TempData["Message"] = "Slots published successfully";
        return Ok(Url.Action("Slots", "Store", new { id = vm.Id }));
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Slots(ManageSlotVM vm)
    {
        var store = db.Stores
            .Include(s => s.SlotTemplates)
            .FirstOrDefault(s =>
                s.Id == vm.Id &&
                !s.IsDeleted &&
                s.AccountId == HttpContext.GetAccount()!.Id
            );
        if (store == null)
        {
            return NotFound();
        }

        if (!store.HasPublishedFirstSlots)
        {
            return RedirectToAction("SetupSlots", "Store", new { id = vm.Id });
        }

        var tmpNow = DateTime.Now;

        vm.AvailableTypes = ["Custom", "Recurring", "Live"];
        if (vm.Type == "Live")
        {
            vm.AvailableDates = [
                DateOnly.FromDateTime(tmpNow),
                DateOnly.FromDateTime(tmpNow.AddDays(1)),
                DateOnly.FromDateTime(tmpNow.AddDays(2))
            ];
        }
        else
        {
            vm.AvailableDates = [
                DateOnly.FromDateTime(tmpNow.AddDays(3)),
                DateOnly.FromDateTime(tmpNow.AddDays(4)),
                DateOnly.FromDateTime(tmpNow.AddDays(5))
            ];
        }

        vm.AvailableDays = new()
        {
            [0] = "Sun",
            [1] = "Mon",
            [2] = "Tue",
            [3] = "Wed",
            [4] = "Thu",
            [5] = "Fri",
            [6] = "Sat"
        };

        if (vm.Type == null || !vm.AvailableTypes.Contains(vm.Type))
        {
            if (Request.Method == "POST")
            {
                return BadRequest("Invalid type! Refresh the page and try again");
            }

            vm.Type = vm.AvailableTypes.First();
        }

        // Available Slots
        if (vm.Type == "Custom" || vm.Type == "Live")
        {
            if (vm.Date == null || !vm.AvailableDates.Contains(vm.Date.Value))
            {
                vm.Date = vm.AvailableDates.First();
            }

            vm.AvailableSlots = [];
            foreach (var avSlot in db.SlotTemplates.Where(s => s.DayOfWeek == (int)vm.Date.Value.DayOfWeek))
            {
                var slot = new DateTime(vm.Date.Value.Year, vm.Date.Value.Month, vm.Date.Value.Day, avSlot.StartTime.Hour, avSlot.StartTime.Minute, 0);
                if (slot > tmpNow.AddMinutes(-30))
                {
                    vm.AvailableSlots.Add(avSlot.StartTime);
                }
            }
        }
        else
        {
            if (vm.Day == null || !vm.AvailableDays.ContainsKey(vm.Day.Value))
            {
                vm.Day = vm.AvailableDays.First().Key;
            }

            vm.AvailableSlots = db.SlotTemplates.Where(s => s.DayOfWeek == vm.Day).Select(s => s.StartTime).ToList();
        }

        if (Request.Method == "GET")
        {
            // Selected Slots
            if (vm.Type == "Custom" || vm.Type == "Live")
            {
                vm.Slots = db.Slots.Where(s => s.StoreId == vm.Id && DateOnly.FromDateTime(s.StartTime) == vm.Date).Select(s => TimeOnly.FromDateTime(s.StartTime)).ToList();
            }
            else
            {
                vm.Slots = store.SlotTemplates.Where(s => s.DayOfWeek == vm.Day).Select(s => s.StartTime).ToList();
            }

            if (Request.IsAjax())
            {
                return PartialView("_Slots", vm);
            }

            ViewBag.StoreName = store.Name;
            return View(vm);
        }

        if (vm.Type == "Live")
        {
            return BadRequest("Live slots cannot be modified");
        }

        // Validate Slots
        foreach (var slot in vm.Slots)
        {
            if (!vm.AvailableSlots.Contains(slot))
            {
                return BadRequest("Invalid slot! Refresh the page and try again");
            }
        }

        // Remove Slots
        if (vm.Type == "Custom")
        {
            if (vm.Date == null || !vm.AvailableDates.Contains(vm.Date.Value))
            {
                return BadRequest("Invalid date! Refresh the page and try again");
            }

            // Remove slots
            var pendingRemovals = db.Slots
                .Where(s =>
                    s.StoreId == vm.Id &&
                    DateOnly.FromDateTime(s.StartTime) == vm.Date
                )
                .ToList();
            db.Slots.RemoveRange(pendingRemovals);

            // Add slots
            foreach (var slotTime in vm.Slots)
            {
                var slotDateTime = new DateTime(vm.Date.Value.Year, vm.Date.Value.Month, vm.Date.Value.Day, slotTime.Hour, slotTime.Minute, 0);
                var slot = new Slot
                {
                    StartTime = slotDateTime,
                    EndTime = slotDateTime.AddMinutes(30),
                    MaxOrders = store.SlotMaxOrders,
                    StoreId = vm.Id
                };
                db.Slots.Add(slot);
            }
        }
        else
        {
            if (vm.Day == null || !vm.AvailableDays.ContainsKey(vm.Day.Value))
            {
                return BadRequest("Invalid day! Refresh the page and try again");
            }

            store.SlotTemplates.RemoveAll(s => s.DayOfWeek == vm.Day);

            // Add slots
            foreach (var slotTime in vm.Slots)
            {
                var recurringSlot = db.SlotTemplates.FirstOrDefault(s => s.DayOfWeek == vm.Day && s.StartTime == slotTime);
                if (recurringSlot == null) continue;

                store.SlotTemplates.Add(recurringSlot);
            }
        }

        if (!store.HasPublishedFirstSlots)
        {
            store.HasPublishedFirstSlots = true;
            TempData["Message"] = "Slots published successfully";
        }
        db.SaveChanges();
        return Ok();
    }

    public IActionResult GetRecurringSlots(int id, DateOnly date)
    {
        if (!Request.IsAjax()) return NotFound();

        var store = db.Stores
            .Include(s => s.SlotTemplates)
            .FirstOrDefault(s => s.Id == id && !s.IsDeleted);
        if (store == null)
        {
            return NotFound("Store not found");
        }

        var recurringSlots = store.SlotTemplates
            .Where(s => s.DayOfWeek == (int)date.DayOfWeek)
            .Select(s => FormatHelper.ToDateTimeFormat(DateTime.Today.Add(s.StartTime.ToTimeSpan()), "h:mm tt"))
            .ToList();
        return Json(recurringSlots);
    }

    public IActionResult Report(int id)
    {
        // view sales report
        return View();
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Scan(int? id)
    {
        if (id == null)
        {
            var sessionStoreId = HttpContext.Session.GetInt32("StoreId");
            if (sessionStoreId == null)
            {
                TempData["Message"] = "Please choose a store first";
                return RedirectToAction("Vendor", "Home", new { ReturnUrl = Url.Action("Scan") });
            }

            return RedirectToAction("Scan", new { id = sessionStoreId });
        }

        var store = db.Stores.FirstOrDefault(s =>
            s.Id == id &&
            !s.IsDeleted &&
            s.AccountId == HttpContext.GetAccount()!.Id
        );
        if (store == null)
        {
            return NotFound();
        }

        return View(id);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult ScanChallenge(int id, string orderId)
    {
        var store = db.Stores.FirstOrDefault(s =>
            s.Id == id &&
            !s.IsDeleted &&
            s.AccountId == HttpContext.GetAccount()!.Id
        );
        if (store == null)
        {
            return NotFound("Store not found");
        }

        if (string.IsNullOrEmpty(orderId))
        {
            return BadRequest("Invalid Order Id");
        }

        var order = db.Orders.FirstOrDefault(o =>
            o.Id == orderId &&
            o.Status != "Preparing"
        );
        if (order == null)
        {
            return BadRequest("Order not found");
        }

        if (order.StoreId != store.Id)
        {
            return Unauthorized("Order is not from this store");
        }

        return Ok();
    }

    public IActionResult ConnectStripe(int id)
    {
        string clientId = cf["Stripe:ClientId"] ?? "";
        string redirectUri = "http://localhost:5245/store/callback";
        string state = id.ToString();

        string url = $"https://connect.stripe.com/oauth/authorize?response_type=code&client_id={clientId}&scope=read_write&redirect_uri={redirectUri}&state={state}";

        return Redirect(url);
    }

    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> Callback(string code, string state, string error)
    {
        var storeId = int.Parse(state);

        if (!string.IsNullOrEmpty(error))
        {
            // User cancelled or there was an error
            TempData["Message"] = $"Stripe connect failed!";
            return RedirectToAction("Edit", "Store", new { Id = storeId });
        }

        var options = new OAuthTokenCreateOptions
        {
            GrantType = "authorization_code",
            Code = code
        };

        var authService = new OAuthTokenService();
        var response = await authService.CreateAsync(options);

        // Stripe account ID of the vendor
        string stripeAccountId = response.StripeUserId;

        var store = db.Stores.FirstOrDefault(s =>
            s.Id == storeId &&
            s.AccountId == HttpContext.GetAccount()!.Id &&
            !s.IsDeleted
        );
        if (store == null)
        {
            TempData["Message"] = "Stripe connect failed! Store not found";
            return RedirectToAction("Edit", "Store", new { Id = storeId });
        }

        store.StripeAccountId = stripeAccountId;
        db.SaveChanges();

        TempData["Message"] = "Stripe connect success!";
        return RedirectToAction("Edit", "Store", new { Id = storeId });
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult GetStripeAccountEmail(int id)
    {
        var store = db.Stores.FirstOrDefault(s =>
            s.Id == id &&
            s.AccountId == HttpContext.GetAccount()!.Id &&
            !s.IsDeleted
        );
        if (store == null)
        {
            return NotFound("Store not found");
        }

        string? stripeAccountId = store.StripeAccountId;
        if (string.IsNullOrEmpty(stripeAccountId))
        {
            return NotFound("Stripe account not found");
        }

        var accountService = new AccountService();
        Stripe.Account account;
        try
        {
            account = accountService.Get(stripeAccountId);
        }
        catch (StripeException ex)
        {
            return BadRequest($"Stripe error: {ex.Message}");
        }

        return Ok(account.Email);
    }

    [HttpPost]
    [Authorize(Roles = "Vendor")]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var store = db.Stores.FirstOrDefault(s =>
            s.Id == id &&
            s.AccountId == HttpContext.GetAccount()!.Id &&
            !s.IsDeleted
        );
        if (store == null) return NotFound("Store not found");

        var error = clnSrv.CanCleanUp(store);
        if (error != null) return BadRequest(error);

        clnSrv.CleanUp(store);

        TempData["Message"] = "Store deleted successfully";
        return Ok();
    }

    // ==========Remote==========
    public bool IsSlugUnique(string slug, int? id = null)
    {
        if (id == null)
        {
            return !db.Stores.Any(s => s.Slug == slug && !s.IsDeleted);
        }

        return !db.Stores.Any(s => s.Slug == slug && s.Id != id && !s.IsDeleted);
    }

    public bool IsNameUnique(string name, int venue, int? id = null)
    {
        if (id == null)
        {
            return !db.Stores.Any(s => s.Name == name.Trim() && !s.IsDeleted && s.VenueId == venue);
        }

        return !db.Stores.Any(s => s.Name == name.Trim() && s.Id != id && !s.IsDeleted && s.VenueId == venue);
    }

    public bool CheckVenue(int venue)
    {
        return db.Venues.Any(v => v.Id == venue);
    }
}
