using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    public IActionResult Index()
    {
        // store details/info
        return View();
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

    public IActionResult Slots(int id, string? type = "recurring")
    {
        // manage store slots
        return View();
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

    public IActionResult ConnectStripe()
    {
        string clientId = cf["Stripe:ClientId"] ?? "";
        string redirectUri = "http://localhost:5245/store/callback";

        string url = $"https://connect.stripe.com/oauth/authorize?response_type=code&client_id={clientId}&scope=read_write&redirect_uri={redirectUri}";

        return Redirect(url);
    }

    public async Task<string> Callback(string code, string error)
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

        var service = new OAuthTokenService();
        var response = await service.CreateAsync(options);

        // Stripe account ID of the vendor
        string stripeAccountId = response.StripeUserId;

        return stripeAccountId;

        // return RedirectToAction("StripeLinkedSuccess");
    }


    // ==========Remote==========
    public IActionResult ScanChallenge(int? id, string? orderId)
    {
        if (id == null || orderId == null)
        {
            return BadRequest();
        }

        return Ok(new
        {
            status = "error",
            errorMessage = "Invalid order ID"
        });

        return Ok(new
        {
            status = "success",
        });
    }

    public bool IsSlugUnique(string slug, int? id)
    {
        return true;
    }

    public bool CheckVenue(int venue, int? id)
    {
        return true;
    }
}
