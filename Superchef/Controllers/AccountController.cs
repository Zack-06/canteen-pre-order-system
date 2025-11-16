using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class AccountController : Controller
{

    public IActionResult Index()
    {
        return View();
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
}
