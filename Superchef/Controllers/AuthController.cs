using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

public class AuthController : Controller
{
    private readonly DB db;
    private readonly SecurityService secSrv;
    private readonly VerificationService verSrv;
    private readonly DeviceService devSrv;
    private readonly EmailService emailSrv;
    private readonly IDataProtectionProvider dp;
    private readonly IHubContext<VerificationHub> vh;

    public AuthController(DB db, SecurityService secSrv, VerificationService verSrv, DeviceService devSrv, EmailService emailSrv, IDataProtectionProvider dp, IHubContext<VerificationHub> vh)
    {
        this.db = db;
        this.secSrv = secSrv;
        this.verSrv = verSrv;
        this.devSrv = devSrv;
        this.emailSrv = emailSrv;
        this.dp = dp;
        this.vh = vh;
    }

    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginVM vm, string? ReturnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        if (ModelState.IsValid("Email") && !CheckEmailExist(vm.Email))
        {
            ModelState.AddModelError("Email", "Email is not registered.");
        }

        // Get account
        var account = db.Accounts
            .Include(a => a.AccountType)
            .FirstOrDefault(a =>
                a.Email == vm.Email &&
                !a.IsDeleted
            );

        if (account == null)
        {
            ModelState.AddModelError("Email", "Email is not registered.");
        }
        else if (account.IsBanned)
        {
            ModelState.AddModelError("Email", "Account is banned.");
        }
        else if (account.LockoutEnd != null && account.LockoutEnd > DateTime.Now)
        {
            ModelState.AddModelError("Email", "Account is locked. Please try again later.");
        }
        else if (!secSrv.VerifyPassword(account.PasswordHash, vm.Password))
        {
            account.FailedLoginAttempts++;

            var attemptsLeft = 5 - account.FailedLoginAttempts;
            if (attemptsLeft <= 0)
            {
                ModelState.AddModelError("Email", "Account is locked. Please try again later.");
                ModelState.AddModelError("Password", "Password is incorrect. No more attempts left.");
                account.FailedLoginAttempts = 0;
                account.LockoutEnd = DateTime.Now.AddMinutes(5);
            }
            else
            {
                ModelState.AddModelError("Password", $"Password is incorrect. {attemptsLeft} attempt{(attemptsLeft > 1 ? "s" : "")} left.");
            }

            db.SaveChanges();
        }

        if (ModelState.IsValid && account != null)
        {
            // Update account
            account.FailedLoginAttempts = 0;
            account.LockoutEnd = null;
            account.DeletionAt = null;
            db.SaveChanges();

            var device = devSrv.GetKnownDeviceForAccount(account.Id, await devSrv.GetCurrentDeviceInfo());

            if (device == null)
            {
                // Create device
                device = await devSrv.CreateDevice(account);
            }

            if (!device.IsVerified)
            {
                // Create short session
                devSrv.createFullShortSession(device.Id);

                // Create verification
                var verification = verSrv.CreateVerification("Login", Request.GetBaseUrl(), account.Id, device.Id);

                // Redirect to verification page
                return RedirectToAction("Verify", "Auth", new { token = verification.Token, ReturnUrl });
            }

            // Create session token in database
            var sessionToken = devSrv.createSession(device.Id, false);

            // Create cookie claim
            secSrv.SignIn(account.Id.ToString(), account.AccountType.Name, sessionToken);

            TempData["Message"] = "Welcome back, " + account.Name;

            if (ReturnUrl == null)
            {
                return RedirectToAction("Index", "Home");
            }

            return Redirect(ReturnUrl);
        }

        return View(vm);
    }

    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterVM vm, string? ReturnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        if (ModelState.IsValid("Email") && CheckEmailExist(vm.Email))
        {
            ModelState.AddModelError("Email", "Email already registered.");
        }

        if (ModelState.IsValid)
        {
            // Add New Account
            Account account = new()
            {
                Name = vm.Name,
                Email = vm.Email,
                PasswordHash = secSrv.HashPassword(vm.Password),
                FailedLoginAttempts = 0,
                IsBanned = false,
                AccountTypeId = 1
            };
            db.Accounts.Add(account);
            db.SaveChanges();

            db.Entry(account).Reference(a => a.AccountType).Load();

            // Create device
            var device = await devSrv.CreateDevice(account, true);

            // Create session token in database
            var sessionToken = devSrv.createSession(device.Id, false);

            // Create cookie claim
            secSrv.SignIn(account.Id.ToString(), account.AccountType.Name, sessionToken);


            TempData["Message"] = "Account created successfully";
            if (ReturnUrl == null)
            {
                return RedirectToAction("Index", "Home");
            }

            return Redirect(ReturnUrl);
        }
        return View(vm);
    }

    public async Task<IActionResult> Verify(string? token, string? otp, string? ReturnUrl)
    {
        ViewBag.ReturnUrl = ReturnUrl ?? "/";

        // ViewBag.Status = "check";
        // ViewBag.Status = "cross";
        // ViewBag.Status = "incorrect";
        // ViewBag.Status = "link";
        // ViewBag.Status = "expired";

        var request = db.Verifications
            .Include(v => v.Device)
            .FirstOrDefault(u => u.Token == token);

        if (request == null)
        {
            ViewBag.Status = "cross";
        }
        else if (request.ExpiresAt < DateTime.Now)
        {
            ViewBag.Status = "expired";

            // remove current expired request
            db.Verifications.Remove(request);
            db.SaveChanges();
        }
        else if (string.IsNullOrEmpty(otp))
        {
            ViewBag.Status = "link";

            var currentDevice = await devSrv.GetCurrentDeviceInfo();

            if (request.Action == "Login" && request.Device != null)
            {
                if (
                    request.Device.Address == currentDevice.Location &&
                    request.Device.DeviceOS == currentDevice.OS &&
                    request.Device.DeviceType == currentDevice.Type &&
                    request.Device.DeviceBrowser == currentDevice.Browser
                )
                {
                    ViewBag.VerificationDeviceId = request.DeviceId;
                    ViewBag.VerificationToken = token;
                }
            }
        }
        else if (otp != request.OTP)
        {
            ViewBag.Status = "incorrect";
        }
        else
        {
            ViewBag.Status = "check";

            // Update is verified
            request.IsVerified = true;
            db.SaveChanges();

            // Navigate to next page
            if (request != null)
            {
                if (request.Action == "Login" && request.DeviceId != null && request.Device != null)
                {
                    ViewBag.ReturnUrl = Url.Action(request.Action, "Auth", new { ReturnUrl = ReturnUrl });

                    // Update device is verified
                    db.Devices.Where(d => d.Id == request.DeviceId).ExecuteUpdate(s => s.SetProperty(s => s.IsVerified, true));

                    // Remove all associated verifications
                    db.Verifications.RemoveRange(db.Verifications.Where(v => v.DeviceId == request.DeviceId));
                    db.SaveChanges();

                    var currentDevice = await devSrv.GetCurrentDeviceInfo();
                    if (
                        request.Device.Address != currentDevice.Location ||
                        request.Device.DeviceOS != currentDevice.OS ||
                        request.Device.DeviceType != currentDevice.Type ||
                        request.Device.DeviceBrowser != currentDevice.Browser
                    )
                    {
                        // broadcast if device not the same
                        await vh.Clients.All.SendAsync("Verified", request.DeviceId);
                    } else
                    {
                        var error = await secSrv.ClaimSession();
                        if (error != null)
                        {
                            TempData["Message"] = error;
                        }
                    }
                }
                else
                {
                    ViewBag.ReturnUrl = Url.Action(request.Action, "Auth", new { Token = request.Token, ReturnUrl = ReturnUrl });
                }
            }
        }

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

    public async Task<IActionResult> ClaimSession()
    {
        var error = await secSrv.ClaimSession();
        if (error != null)
        {
            return BadRequest(error);
        }

        return Ok();
    }

    public IActionResult Logout()
    {
        return BadRequest();
    }

    // ==========REMOTE==========
    public bool CheckEmailExist(string email)
    {
        return db.Accounts.Any(a => a.Email == email && !a.IsDeleted);
    }

    public bool CheckEmailLogin(string email)
    {
        return CheckEmailExist(email);
    }

    public bool CheckEmailRegister(string email)
    {
        return !CheckEmailExist(email);
    }
}
