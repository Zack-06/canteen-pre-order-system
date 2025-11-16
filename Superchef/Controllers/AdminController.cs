using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class AdminController : Controller
{
    public IActionResult Manage()
    {
        return View();
    }

    public IActionResult Add()
    {
        return View();
    }

    public IActionResult Edit()
    {
        return View();
    }
}
