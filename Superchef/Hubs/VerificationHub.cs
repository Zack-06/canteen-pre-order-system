using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Hubs;

public class VerificationHub : Hub
{
    private readonly DB db;
    private readonly DeviceService devSrv;

    public VerificationHub(DB db, DeviceService devSrv)
    {
        this.db = db;
        this.devSrv = devSrv;
    }

    public async Task Initialize(string token)
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            await Clients.Caller.SendAsync("Error", "No HTTP context available");
            await Clients.Caller.SendAsync("Disconnect");
            return;
        }

        var verification = db.Verifications
            .Include(v => v.Device)
            .FirstOrDefault(v => v.Token == token);
        if (verification == null)
        {
            await Clients.Caller.SendAsync("Error", "Verification not found");
            await Clients.Caller.SendAsync("Disconnect");
            return;
        }
        else if (verification.Action != "Login")
        {
            await Clients.Caller.SendAsync("Disconnect");
            return;
        }

        if (verification.DeviceId == null || verification.Device == null)
        {
            await Clients.Caller.SendAsync("Error", "Device not found");
            await Clients.Caller.SendAsync("Disconnect");
            return;
        }

        var currentDevice = await devSrv.GetCurrentDeviceInfo();
        if (verification.Device.Address != currentDevice.Location || verification.Device.DeviceOS != currentDevice.OS || verification.Device.DeviceType != currentDevice.Type || verification.Device.DeviceBrowser != currentDevice.Browser)
        {
            await Clients.Caller.SendAsync("Error", "Device unauthorized");
            await Clients.Caller.SendAsync("Disconnect");
            return;
        }

        await Clients.Caller.SendAsync("Initialized", verification.DeviceId);
    }

    /*
    // Send error message
    await Clients.Caller.SendAsync("Error", "Verification not found");

    // Send disconnect message for clients to disconnect
    await Clients.Caller.SendAsync("Disconnect");

    // Send initialized message for clients
    await Clients.Caller.SendAsync("Initialized", deviceId);

    // Broadcast verified deviceId
    await Clients.All.SendAsync("Verified", deviceId);
    */
}