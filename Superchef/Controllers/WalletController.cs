using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class WalletController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Reload()
    {
        return View();
    }
}
