using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class ItemController : Controller
{
    public IActionResult Index()
    {
        // item info details
        return View();
    }

    public IActionResult Manage(int storeId)
    {
        // show all items in a store
        return View();
    }

    public IActionResult Add(int storeId)
    {
        // add new item
        return View();
    }

    public IActionResult Edit(int id)
    {
        // edit item details
        return View();
    }
}
