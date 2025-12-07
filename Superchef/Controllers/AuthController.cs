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

    public IActionResult Verify(string? token, string? otp, string? ReturnUrl)
    {
        ViewBag.ReturnUrl = ReturnUrl ?? "/";

        // ViewBag.Status = "check";
        // ViewBag.Status = "cross";
        // ViewBag.Status = "incorrect";
        // ViewBag.Status = "link";
        // ViewBag.Status = "expired";
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
        ViewBag.ExpiredTimestamp = new DateTimeOffset(DateTime.Now.AddMinutes(2)).ToUnixTimeMilliseconds();
        return View();
    }

    // ==========REMOTE==========
    public bool CheckEmailLogin(string email)
    {
        return true;
    }

    public bool CheckEmailRegister(string email)
    {
        return true;
    }
}
