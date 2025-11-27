using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Superchef.Controllers;

public class StoreController : Controller
{
    private readonly IConfiguration cf;

    public StoreController(IConfiguration configuration)
    {
        cf = configuration;
    }

    public IActionResult Index()
    {
        // store details/info
        return View();
    }

    public IActionResult Manage()
    {
        return View();
    }

    public IActionResult Add()
    {
        // add new store
        return View();
    }

    public IActionResult Edit(int id)
    {
        // edit store details
        return View();
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
}
