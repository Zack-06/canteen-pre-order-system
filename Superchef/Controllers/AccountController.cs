using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class AccountController : Controller
{

    public IActionResult Index()
    {
        var model = new AccountProfileVM
        {
            Name = "John Doe",
            Email = "john@doe.com",
            PhoneNumber = "012-3456789"
        };
        return View(model);
    }

    public IActionResult ChangePassword()
    {
        return View();
    }

    public IActionResult Device()
    {
        return View();
    }

    public IActionResult History()
    {
        return View();
    }

    public IActionResult Favourite()
    {
        return View();
    }

    // ==========REQUEST==========
    [HttpPost]
    async public Task<string> RequestChangeEmail()
    {
        // wait for 5 seconds before return
        await Task.Delay(5000);

        return "test_token";
    }

    [HttpPost]
    async public Task<string> RequestDeleteAccount()
    {
        // wait for 5 seconds before return
        await Task.Delay(5000);

        return "test_token";
    }
}
