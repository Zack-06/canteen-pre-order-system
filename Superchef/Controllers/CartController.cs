using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class CartController : Controller
{
    public IActionResult Index()
    {
        // display stores

        return View();
    }

    public IActionResult Store(int id)
    {
        // display items for store with id

        return View();
    }
}
