using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
