using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class StoreController : Controller
{
    public IActionResult Index()
    {
        // store details/info
        return View();
    }

    public IActionResult Add()
    {
        // add new store
        return View();
    }

    public IActionResult Edit(int id)
    {
        // edit store details
        return View();
    }

    public IActionResult Slots(int id, string? type = "recurring")
    {
        // manage store slots
        return View();
    }

    public IActionResult Report(int id)
    {
        // view sales report
        return View();
    }
}
