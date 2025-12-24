using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly DeviceService devSrv;
    private readonly VerificationService verSrv;
    private readonly SecurityService secSrv;
    private readonly EmailService emlSrv;
    private readonly ImageService imgSrv;
    private readonly CleanupService clnSrv;
    private readonly IHubContext<AccountHub> accHubCtx;

    public AccountController(DB db, IWebHostEnvironment en, DeviceService devSrv, VerificationService verSrv, SecurityService secSrv, EmailService emlSrv, ImageService imgSrv, CleanupService clnSrv, IHubContext<AccountHub> accHubCtx)
    {
        this.db = db;
        this.en = en;
        this.devSrv = devSrv;
        this.verSrv = verSrv;
        this.secSrv = secSrv;
        this.emlSrv = emlSrv;
        this.imgSrv = imgSrv;
        this.clnSrv = clnSrv;
        this.accHubCtx = accHubCtx;
    }

    public IActionResult Index()
    {
        var acc = HttpContext.GetAccount();
        if (acc == null) return RedirectToAction("Index", "Home");

        var vm = new AccountProfileVM
        {
            Name = acc.Name,
            Email = acc.Email,
            PhoneNumber = acc.PhoneNumber,
            RemoveImage = false,
            ImageScale = 1,
            ImageX = 0,
            ImageY = 0
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult Index(AccountProfileVM vm)
    {
        var acc = HttpContext.GetAccount();
        if (acc == null) return RedirectToAction("Index", "Home");

        if (vm.Image != null && !vm.RemoveImage)
        {
            var e = imgSrv.ValidateImage(vm.Image, 1);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        if (ModelState.IsValid)
        {
            if (vm.RemoveImage)
            {
                // remove image
                if (acc.Image != null)
                {
                    imgSrv.DeleteImage(acc.Image, "account");
                    acc.Image = null;
                }
            }
            else if (vm.Image != null)
            {
                try
                {
                    var newFile = imgSrv.SaveImage(vm.Image, "account", 200, 200, vm.ImageX, vm.ImageY, vm.ImageScale);

                    // remove image
                    if (acc.Image != null) imgSrv.DeleteImage(acc.Image, "account");
                    acc.Image = newFile;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("Image", ex.Message);
                }
            }
        }

        if (ModelState.IsValid)
        {
            // update name & phone number
            acc.Name = vm.Name.Trim();
            acc.PhoneNumber = vm.PhoneNumber;
            db.SaveChanges();

            TempData["Message"] = "Account updated successfully";
            return RedirectToAction("Index");
        }

        vm.Email = acc.Email;
        return View(vm);
    }

    public IActionResult ChangePassword()
    {
        var acc = HttpContext.GetAccount();
        if (acc == null) return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordVM vm)
    {
        var acc = HttpContext.GetAccount();
        if (acc == null) return RedirectToAction("Index", "Home");

        if (!secSrv.VerifyPassword(acc.PasswordHash, vm.CurrentPassword))
        {
            ModelState.AddModelError("CurrentPassword", "Incorrect password");
        }

        if (ModelState.IsValid("NewPassword") && secSrv.VerifyPassword(acc.PasswordHash, vm.NewPassword))
        {
            ModelState.AddModelError("NewPassword", "Cannot use the same password as before.");
        }

        if (ModelState.IsValid)
        {
            // Remove sessions
            var sessions = db.Sessions.Where(s => s.Device.AccountId == acc.Id);
            foreach (var session in sessions)
            {
                db.Sessions.Remove(session);
            }

            // Update password
            acc.PasswordHash = secSrv.HashPassword(vm.NewPassword);

            // Save changes
            db.SaveChanges();

            // Send email notification
            emlSrv.SendPasswordChangedEmail(acc, Url.Action("ForgotPassword", null, null, Request.Scheme, Request.Host.Value));

            await accHubCtx.Clients.All.SendAsync("LogoutAll", acc.Id, HttpContext.GetDeviceId());

            TempData["Message"] = "Password reset successfully. Please login again";
            return RedirectToAction("Login", "Auth");
        }

        return View(vm);
    }

    public async Task<IActionResult> Device()
    {
        var deviceInfo = await devSrv.GetCurrentDeviceInfo();

        ViewBag.DeviceType = deviceInfo.Type;
        ViewBag.DeviceOS = deviceInfo.OS;
        ViewBag.DeviceBrowser = deviceInfo.Browser;
        ViewBag.DeviceAddress = deviceInfo.Location;

        var devices = db.Devices
                        .Where(d => d.AccountId.ToString() == User.Identity!.Name)
                        .Where(d => !(
                            d.DeviceOS == deviceInfo.OS
                            && d.DeviceType == deviceInfo.Type
                            && d.DeviceBrowser == deviceInfo.Browser
                            && d.Address == deviceInfo.Location
                        ))
                        .ToList();
        return View(devices);
    }

    [HttpPost]
    public async Task<IActionResult> LogoutDevice(int? id)
    {
        var device = db.Devices.FirstOrDefault(d => d.Id == id && d.AccountId.ToString() == User.Identity!.Name);
        if (device != null)
        {
            db.Verifications.RemoveRange(db.Verifications.Where(v => v.DeviceId == device.Id));
            db.Devices.Remove(device);
            db.SaveChanges();

            await accHubCtx.Clients.All.SendAsync("LogoutDevice", device.Id);
        }

        var deviceInfo = await devSrv.GetCurrentDeviceInfo();

        var devices = db.Devices
            .Where(d => d.AccountId.ToString() == User.Identity!.Name)
            .Where(d => !(
                d.DeviceOS == deviceInfo.OS
                && d.DeviceType == deviceInfo.Type
                && d.DeviceBrowser == deviceInfo.Browser
                && d.Address == deviceInfo.Location
            ))
            .ToList();

        return PartialView("_Device", devices);
    }

    [HttpPost]
    public async void LogoutAllDevices()
    {
        var accountId = User.Identity!.Name;

        var devices = db.Devices.Where(d => d.AccountId.ToString() == accountId).ToList();
        foreach (var device in devices)
        {
            db.Verifications.RemoveRange(db.Verifications.Where(v => v.DeviceId == device.Id));
            db.Devices.Remove(device);
        }
        db.SaveChanges();
        await accHubCtx.Clients.All.SendAsync("LogoutAll", accountId, HttpContext.GetDeviceId());

        TempData["Message"] = "Logged out all known devices successfully";
    }

    [Authorize(Roles = "Customer")]
    public IActionResult History(HistoryVM vm)
    {
        vm.Options = new()
        {
            ["all"] = "All",
            ["pending"] = "Pending",
            ["confirmed"] = "Confirmed",
            ["preparing"] = "Preparing",
            ["to-pickup"] = "To Pickup",
            ["completed"] = "Completed",
            ["cancelled"] = "Cancelled"
        };

        if (string.IsNullOrEmpty(vm.Option) || !vm.Options.ContainsKey(vm.Option))
        {
            vm.Option = vm.Options.First().Key;
        }

        // Get query
        var orders = db.Orders
            .Include(o => o.Slot)
            .Include(o => o.Store)
            .Include(o => o.OrderItems)
            .Where(o => o.AccountId == HttpContext.GetAccount()!.Id)
            .AsQueryable();

        if (vm.Option != "all")
        {
            // Apply order filters
            if (orders != null)
            {
                orders = orders.Where(o => o.Status == vm.Options[vm.Option]);
            }
        }

        // Get results
        vm.Results = [];
        if (orders != null)
        {
            vm.Results = orders
                .OrderByDescending(o => o.CreatedAt)
                .ToList();
        }

        if (Request.IsAjax())
        {
            return PartialView("_History", vm.Results);
        }

        return View(vm);
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Favourite()
    {
        var acc = HttpContext.GetAccount();

        var itemIds = db.Favourites
            .Where(f => f.AccountId == acc!.Id)
            .Select(f => f.ItemId)
            .ToList();

        var items = db.Items
            .Include(i => i.Reviews)
            .Include(i => i.Variants)
                .ThenInclude(v => v.OrderItems)
            .Where(i => itemIds.Contains(i.Id))
            .ToList();

        return View(items);
    }

    // ==========REQUEST==========
    [HttpPost]
    public IActionResult RequestChangeEmail()
    {
        var account = HttpContext.GetAccount()!;
        if (account.Email == "superchef.system@gmail.com")
        {
            return BadRequest("You cannot change default system email");
        }

        // Create verification
        var verification = verSrv.CreateVerification("ChangeEmail", Request.GetBaseUrl(), HttpContext.GetAccount()!.Id);

        return Ok(verification.Token);
    }

    [HttpPost]
    public IActionResult RequestDeleteAccount()
    {
        var account = HttpContext.GetAccount()!;
        var error = clnSrv.CanCleanUp(account);
        if (error != null)
        {
            return BadRequest(error);
        }

        // Create verification
        var verification = verSrv.CreateVerification("DeleteAccount", Request.GetBaseUrl(), HttpContext.GetAccount()!.Id);

        return Ok(verification.Token);
    }

    [HttpPost]
    public IActionResult SaveSubscription(PushSubscriptionVM vm)
    {
        var token = User.FindFirst("SessionToken");
        if (token == null)
        {
            return BadRequest("No session token found");
        }

        var session = db.Sessions.FirstOrDefault(s => s.Token == token.Value);
        if (session == null)
        {
            return BadRequest("No session found");
        }

        session.PushEndpoint = vm.Endpoint;
        session.PushP256dh = vm.P256dh;
        session.PushAuth = vm.Auth;
        db.SaveChanges();

        return Ok();
    }

    [HttpGet]
    [Authorize]
    public IActionResult GetIdentity()
    {
        var name = User.Identity?.Name ?? "Anonymous";
        var claimsCount = User.Claims.Count();

        return Ok(new
        {
            User = name,
            ClaimsCount = claimsCount,
            IsAuthenticated = User.Identity?.IsAuthenticated
        });
    }
}
