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

    public StoreController(DB db, IConfiguration configuration, ImageService imgSrv)
    {
        this.db = db;
        cf = configuration;
        this.imgSrv = imgSrv;
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
            .Where(ExpressionService.ShowStoreToCustomerExpr)
            .FirstOrDefault(s => s.Slug == slug);
        if (store == null)
        {
            return NotFound();
        }

        return View(store);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Manage(ManageStoreVM vm)
    {
        Dictionary<string, Expression<Func<Store, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Slug", a => a.Slug },
            { "Items Count", a => a.Items.Count }
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

        ViewBag.VendorName = "abc";

        return View(vm);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Select(int id, string? ReturnUrl)
    {
        var store = db.Stores
            .FirstOrDefault(
                s => s.Id == id &&
                s.AccountId == HttpContext.GetAccount()!.Id
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
    [Authorize(Roles = "Vendor,Admin")]
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

    [Authorize(Roles = "Vendor,Admin")]
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
            s.AccountId == HttpContext.GetAccount()!.Id
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
    [Authorize(Roles = "Vendor,Admin")]
    public IActionResult Edit(EditStoreVM vm)
    {
        var store = db.Stores.FirstOrDefault(s =>
            s.Id == vm.Id &&
            s.AccountId == HttpContext.GetAccount()!.Id
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
            store.SlotMaxOrders = vm.SlotMaxOrders;
            store.VenueId = vm.Venue;
            db.SaveChanges();

            TempData["Message"] = "Store updated successfully";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        vm.AvailableVenues = db.Venues.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList();

        ViewBag.ImageUrl = $"/uploads/store/{store.Image}";
        ViewBag.BannerImageUrl = store.Banner != null ? $"/uploads/banner/{store.Banner}" : null;

        return View(vm);
    }

    public IActionResult Slots(ManageSlotVM vm)
    {
        var store = db.Stores
            .Include(s => s.SlotTemplates)
            .FirstOrDefault(s => s.Id == vm.StoreId);

        vm.AvailableTypes = ["Custom", "Recurring"];
        vm.AvailableDates = [
            DateOnly.FromDateTime(DateTime.Now.AddDays(2)),
            DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            DateOnly.FromDateTime(DateTime.Now.AddDays(4))
        ];
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
        ViewBag.InitSlot = false;

        if (vm.Type == null || !vm.AvailableTypes.Contains(vm.Type))
        {
            vm.Type = vm.AvailableTypes.First();
        }

        if (vm.Date == null || !vm.AvailableDates.Contains(vm.Date.Value))
        {
            vm.Date = vm.AvailableDates.First();
        }

        if (vm.Day == null || !vm.AvailableDays.ContainsKey(vm.Day.Value))
        {
            vm.Day = vm.AvailableDays.First().Key;
        }

        // Available Slots
        if (vm.Type == "Custom")
        {
            vm.AvailableSlots = [];
            foreach (var avSlot in db.SlotTemplates.Where(s => s.DayOfWeek == (int)vm.Date.Value.DayOfWeek))
            {
                var slot = new DateTime(vm.Date.Value.Year, vm.Date.Value.Month, vm.Date.Value.Day, avSlot.StartTime.Hour, avSlot.StartTime.Minute, 0);
                if (slot > DateTime.Now)
                {
                    vm.AvailableSlots.Add(TimeOnly.FromDateTime(slot));
                }
            }
        }
        else
        {
            vm.AvailableSlots = db.SlotTemplates.Where(s => s.DayOfWeek == vm.Day).Select(s => s.StartTime).ToList();
        }

        // Enabled Slots
        if (vm.Type == "Custom" || (ViewBag.InitSlot != null && ViewBag.InitSlot == true))
        {
            vm.Slots = db.Slots.Where(s => s.StoreId == vm.StoreId && DateOnly.FromDateTime(s.StartTime) == vm.Date).Select(s => TimeOnly.FromDateTime(s.StartTime)).ToList();
        }
        else
        {
            // select
            if (store != null)
            {
                vm.Slots = store.SlotTemplates.Where(s => s.DayOfWeek == vm.Day).Select(s => s.StartTime).ToList();
            }
            else
            {
                vm.Slots = [];
            }
        }

        if (Request.IsAjax())
        {
            return PartialView("_Slots", vm);
        }

        ViewBag.StoreName = "Hainan Chicken Rice";
        return View(vm);
    }

    public IActionResult Report(int id)
    {
        // view sales report
        return View();
    }

    public IActionResult Scan(int id)
    {
        return View(id);
    }

    public IActionResult ScanChallenge(int? id, string? orderId)
    {
        if (id == null || orderId == null)
        {
            return BadRequest();
        }

        // return Ok(new
        // {
        //     status = "error",
        //     errorMessage = "Invalid order ID"
        // });

        return Ok(new
        {
            status = "success",
        });
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
            s.AccountId == HttpContext.GetAccount()!.Id
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

    [Authorize(Roles = "Vendor,Admin")]
    public IActionResult GetStripeAccountEmail(int id)
    {
        var store = db.Stores.FirstOrDefault(s => s.Id == id);
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
            s.AccountId == HttpContext.GetAccount()!.Id
        );
        if (store == null) return NotFound("Store not found");

        store.IsDeleted = true;
        db.SaveChanges();

        // delete store logic here
        // todo

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
