using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class VariantController : Controller
{

    public IActionResult Manage(int itemId)
    {
        // show all variants of an item
        return View();
    }

    public IActionResult Add(int itemId)
    {
        // add new variant
        return View();
    }

    public IActionResult Edit(int id)
    {
        // edit variant details
        return View();
    }
}
