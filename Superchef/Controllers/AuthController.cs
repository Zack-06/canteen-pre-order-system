using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

public class AuthController : Controller
{
    private readonly DB db;
    private readonly IDataProtectionProvider dp;
    private readonly SecurityService secSrv;
    private readonly VerificationService verSrv;
    private readonly DeviceService devSrv;
    private readonly EmailService emlSrv;
    private readonly IHubContext<VerificationHub> verHubCtx;
    private readonly IHubContext<AccountHub> accHubCtx;

    public AuthController(DB db, IDataProtectionProvider dp, SecurityService secSrv, VerificationService verSrv, DeviceService devSrv, EmailService emlSrv, IHubContext<VerificationHub> verHubCtx, IHubContext<AccountHub> accHubCtx)
    {
        this.db = db;
        this.dp = dp;
        this.secSrv = secSrv;
        this.verSrv = verSrv;
        this.devSrv = devSrv;
        this.emlSrv = emlSrv;
        this.verHubCtx = verHubCtx;
        this.accHubCtx = accHubCtx;
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

        if (ModelState.IsValid("Email") && !CheckEmailLogin(vm.Email))
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

        if (ModelState.IsValid("Email") && !CheckEmailRegister(vm.Email))
        {
            ModelState.AddModelError("Email", "Email already registered.");
        }

        if (ModelState.IsValid)
        {
            bool success = await verSrv.VerifyRecaptcha(vm.RepatchaToken);
            if (!success)
            {
                ModelState.AddModelError("RepatchaToken", "Recaptcha verification failed. Please try again.");
            }
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

            TempData["Message"] = "Welcome, " + account.Name;
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
                        await verHubCtx.Clients.All.SendAsync("Verified", request.DeviceId);
                    }
                    else
                    {
                        var (error, message) = await secSrv.ClaimSession();
                        if (error != null)
                        {
                            TempData["Message"] = error;
                        }
                        else if (message != null)
                        {
                            TempData["Message"] = message;
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

    [HttpPost]
    public IActionResult ForgotPassword(ForgotPasswordVM vm)
    {
        if (ModelState.IsValid("Email") && !CheckEmailRegister(vm.Email))
        {
            ModelState.AddModelError("Email", "Email is not registered.");
        }

        if (ModelState.IsValid)
        {
            // Get account
            var acc = db.Accounts.FirstOrDefault(a => a.Email == vm.Email && !a.IsDeleted);

            // Create verification
            var verification = verSrv.CreateVerification("ResetPassword", Request.GetBaseUrl(), acc!.Id);

            // Redirect to verify request
            return RedirectToAction("Verify", new { token = verification.Token });
        }

        return View(vm);
    }

    public IActionResult ResetPassword(string? token)
    {
        var request = verSrv.GetVerificationRequest(token, "ResetPassword");
        if (request == null)
        {
            return RedirectToAction("Verify", new { token });
        }

        ViewBag.ExpiredTimestamp = new DateTimeOffset(request.ExpiresAt).ToUnixTimeMilliseconds();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordVM vm, string? token)
    {
        var request = verSrv.GetVerificationRequest(token, "ResetPassword");
        if (request == null)
        {
            return RedirectToAction("Verify", new { token });
        }

        if (ModelState.IsValid("Password") && secSrv.VerifyPassword(request.Account.PasswordHash, vm.Password))
        {
            ModelState.AddModelError("Password", "Cannot change password to the same password");
        }

        if (ModelState.IsValid)
        {
            // Remove sessions
            var sessions = db.Sessions.Where(s => s.Device.AccountId == request.Account.Id);
            foreach (var session in sessions)
            {
                db.Sessions.Remove(session);
            }

            // Update password
            request.Account.PasswordHash = secSrv.HashPassword(vm.Password);
            db.Verifications.Remove(request);
            db.SaveChanges();

            // Send email notification
            emlSrv.SendPasswordChangedEmail(request.Account, Url.Action("ForgotPassword", null, null, Request.Scheme, Request.Host.Value));

            await accHubCtx.Clients.All.SendAsync("LogoutAll", request.AccountId);
            TempData["Message"] = "Password reset successfully. Please login again";
            return RedirectToAction("Login");
        }

        ViewBag.ExpiredTimestamp = new DateTimeOffset(request.ExpiresAt).ToUnixTimeMilliseconds();
        return View(vm);
    }

    public IActionResult ChangeEmail(string? token)
    {
        var request = verSrv.GetVerificationRequest(token, "ChangeEmail");
        if (request == null)
        {
            return RedirectToAction("Verify", new { token });
        }


        ViewBag.ExpiredTimestamp = new DateTimeOffset(request.ExpiresAt).ToUnixTimeMilliseconds();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ChangeEmail(ChangeEmailVM vm, string? token)
    {
        var request = verSrv.GetVerificationRequest(token, "ChangeEmail");
        if (request == null)
        {
            return RedirectToAction("Verify", new { token });
        }

        if (ModelState.IsValid("Email"))
        {
            if (vm.Email == request.Account.Email)
            {
                ModelState.AddModelError("Email", "Cannot change to the same email.");
            }
            else if (!CheckEmailRegister(vm.Email))
            {
                ModelState.AddModelError("Email", "Email already registered.");
            }
        }

        if (ModelState.IsValid)
        {
            // Remove sessions
            var sessions = db.Sessions.Where(s => s.Device.AccountId == request.Account.Id);
            foreach (var session in sessions)
            {
                db.Sessions.Remove(session);
            }

            // Update email
            string originalEmail = request.Account.Email;
            request.Account.Email = vm.Email;
            db.Verifications.Remove(request);
            db.SaveChanges();

            // Send email notification
            emlSrv.SendEmailChangedEmail(request.Account, originalEmail, Url.Action("Contact", "Info", null, Request.Scheme, Request.Host.Value));

            await accHubCtx.Clients.All.SendAsync("LogoutAll", request.AccountId);
            TempData["Message"] = "Email changed successfully. Please login again";
            return RedirectToAction("Login");
        }


        ViewBag.ExpiredTimestamp = new DateTimeOffset(request.ExpiresAt).ToUnixTimeMilliseconds();
        return View(vm);
    }

    public async Task<IActionResult> DeleteAccount(string? token)
    {
        var request = verSrv.GetVerificationRequest(token, "DeleteAccount");
        if (request == null)
        {
            return RedirectToAction("Verify", new { token });
        }

        db.Entry(request).Reference(r => r.Account).Load();
        db.Entry(request.Account).Reference(a => a.AccountType).Load();

        ViewBag.RequestAccountType = request.Account.AccountType.Name;
        ViewBag.ExpiredTimestamp = new DateTimeOffset(request.ExpiresAt).ToUnixTimeMilliseconds();

        if (Request.Method == "POST")
        {
            // Remove sessions
            var sessions = db.Sessions.Where(s => s.Device.AccountId == request.Account.Id);
            foreach (var session in sessions)
            {
                db.Sessions.Remove(session);
            }

            // Remove devices
            var devices = db.Devices.Where(d => d.AccountId == request.Account.Id);
            foreach (var device in devices)
            {
                db.Devices.Remove(device);
            }

            var message = "Account scheduled for deletion";
            if (ViewBag.RequestAccountType == "Customer" || ViewBag.RequestAccountType == "Vendor")
            {
                request.Account.DeletionAt = DateTime.Now.AddDays(7);
            }
            else
            {
                message = "Account deleted successfully";
                db.Accounts.Remove(request.Account);
            }
            db.Verifications.Remove(request);
            db.SaveChanges();

            await accHubCtx.Clients.All.SendAsync("LogoutAll", request.AccountId);
            TempData["Message"] = message;
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ClaimSession()
    {
        var (error, message) = await secSrv.ClaimSession();
        if (error != null)
        {
            return BadRequest(error);
        }

        TempData["Message"] = message;
        return Ok();
    }

    [HttpPost]
    public async Task Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var token = User.FindFirst("SessionToken")?.Value;
            var accountId = User.Identity.Name;

            if (token != null && accountId != null)
            {
                var session = db.Sessions.FirstOrDefault(s => s.Device.AccountId.ToString() == accountId && s.Token == token);

                if (session != null)
                {
                    await accHubCtx.Clients.All.SendAsync("Logout", session.Token);

                    db.Sessions.Remove(session);
                    db.SaveChanges();
                }
            }
        }

        secSrv.SignOut();
        TempData["Message"] = "Logged out successfully";
    }

    // ==========REMOTE==========
    public bool CheckEmailLogin(string email)
    {
        return db.Accounts.Any(a => a.Email == email && !a.IsDeleted);
    }

    public bool CheckEmailRegister(string email)
    {
        return !db.Accounts.Any(a => a.Email == email && !a.IsDeleted);
    }
}
