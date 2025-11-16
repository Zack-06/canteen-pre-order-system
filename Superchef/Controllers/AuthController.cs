using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class AuthController : Controller
{

    public IActionResult Login()
    {
        return View();
    }

    public IActionResult Register()
    {
        return View();
    }

    public IActionResult Verify()
    {
        return View();
    }

    public IActionResult ForgotPassword()
    {
        return View();
    }

    public IActionResult ResetPassword()
    {
        return View();
    }

    public IActionResult ChangeEmail()
    {
        return View();
    }

    public IActionResult DeleteAccount()
    {
        return View();
    }
}
