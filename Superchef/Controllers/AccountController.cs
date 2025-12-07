using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

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
    private readonly IHubContext<AccountHub> accHubCtx;

    public AccountController(DB db, IWebHostEnvironment en, DeviceService devSrv, VerificationService verSrv, SecurityService secSrv, EmailService emlSrv, ImageService imgSrv, IHubContext<AccountHub> accHubCtx)
    {
        this.db = db;
        this.en = en;
        this.devSrv = devSrv;
        this.verSrv = verSrv;
        this.secSrv = secSrv;
        this.emlSrv = emlSrv;
        this.imgSrv = imgSrv;
        this.accHubCtx = accHubCtx;
    }

    public IActionResult Index()
    {
        var model = new AccountProfileVM
        {
            Name = "John Doe",
            Email = "john@doe.com",
            PhoneNumber = "012-3456789"
        };
        return View(model);
    }

    public IActionResult ChangePassword()
    {
        return View();
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
        await accHubCtx.Clients.All.SendAsync("LogoutAll", accountId);

        TempData["Message"] = "Logged out all known devices successfully";
    }

    public IActionResult History()
    {
        return View();
    }

    public IActionResult Favourite()
    {
        return View();
    }

    // ==========REQUEST==========
    [HttpPost]
    async public Task<string> RequestChangeEmail()
    {
        // wait for 5 seconds before return
        await Task.Delay(5000);

        return "test_token";
    }

    [HttpPost]
    async public Task<string> RequestDeleteAccount()
    {
        // wait for 5 seconds before return
        await Task.Delay(5000);

        return "test_token";
    }
}
