using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class CustomerController : Controller
{
    public IActionResult Manage()
    {
        return View();
    }

    public IActionResult Edit()
    {
        return View();
    }
}
