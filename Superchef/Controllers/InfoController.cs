using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class InfoController : Controller
{
    public IActionResult About()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }
}