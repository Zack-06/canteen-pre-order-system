using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class AuthController : Controller
{

    public IActionResult Index()
    {
        return View();
    }
}
