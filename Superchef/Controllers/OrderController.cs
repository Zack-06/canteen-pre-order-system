using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class OrderController : Controller
{
    [HttpPost]
    public bool Create()
    {
        return true;
    }

    public IActionResult Customer()
    {
        // fill up name and phone number

        return View();
    }

    public IActionResult Slot()
    {
        // select pickup time slot
        return View();
    }

    public IActionResult Confirmation()
    {
        // show order confirmation, click confirm to set status = "Confirmed"
        return View();
    }

    public IActionResult Info()
    {
        // show order info
        return View();
    }

    public IActionResult Manage(int storeId)
    {
        // show all orders in a store (vendor)
        return View();
    }

    public IActionResult Edit(int id)
    {
        // edit order details (vendor)
        return View();
    }
}
