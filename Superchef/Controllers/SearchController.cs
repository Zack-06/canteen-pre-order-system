using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class SearchController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
