using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class PaymentController : Controller
{
    public IActionResult Process()
    {
        // choose payment method + show total amount
        return View();
    }

    public IActionResult Success()
    {
        return View("Status", "success");
    }

    public IActionResult Failed()
    {
        return View("Status", "failed");
    }
}
