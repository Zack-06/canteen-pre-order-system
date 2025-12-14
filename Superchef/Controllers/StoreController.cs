using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace Superchef.Controllers;

public class StoreController : Controller
{
    private readonly DB db;
    private readonly IConfiguration cf;

    public StoreController(DB db, IConfiguration configuration)
    {
        this.db = db;
        cf = configuration;
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

    public IActionResult Add()
    {
        var vm = new AddStoreVM
        {
            AvailableVenues = db.Venues.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var vm = new EditStoreVM
        {
            Id = id,
            StripeAccountId = "abc",
            Name = "abc",
            Slug = "abc",
            Description = "abc",
            SlotMaxOrders = 1,
            Venue = 1,

            AvailableVenues = db.Venues.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };

        ViewBag.ImageUrl = "abc";

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
        } else
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

    public async Task<string> Callback(string code, string state, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            // User cancelled or there was an error
            return error;
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

        string storeId = state;

        return stripeAccountId + "," + storeId;

        // return RedirectToAction("StripeLinkedSuccess");
    }

    public IActionResult GetStripeAccountEmail(int id)
    {
        var store = db.Stores.FirstOrDefault(s => s.Id == id);
        if (store == null)
        {
            // return NotFound("Store not found");
        }

        // string? stripeAccountId = store.StripeAccountId;
        // if (string.IsNullOrEmpty(stripeAccountId))
        // {
        //     return NotFound("Stripe account not found");
        // }

        // test
        string? stripeAccountId = "acct_1SXR4c0YIOryk7Uo";

        var accountService = new AccountService();
        var account = accountService.Get(stripeAccountId);

        return Ok(account.Email);
    }

    // ==========Remote==========
    public bool IsSlugUnique(string slug, int? id)
    {
        return true;
    }

    public bool CheckVenue(int venue, int? id)
    {
        return true;
    }
}
