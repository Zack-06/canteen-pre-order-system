using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Superchef.Middlewares;

public class AuthSessionMiddleware
{
    private readonly RequestDelegate next;
    private readonly string loginPath;

    public AuthSessionMiddleware(RequestDelegate next, IOptionsMonitor<CookieAuthenticationOptions> options)
    {
        this.next = next;
        loginPath = options.Get("Cookies").LoginPath;
    }

    public async Task InvokeAsync(HttpContext context, DB db, DeviceService devSrv)
    {
        var endpoint = context.GetEndpoint();
        var requiresAuth = endpoint?.Metadata?.GetMetadata<IAuthorizeData>() != null;
        var logout = false;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var token = context.User.FindFirst("SessionToken")?.Value;
            var accountId = context.User.Identity.Name;
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

            if (token == null || accountId == null || role == null)
            {
                await context.SignOutAsync();
                logout = true;
            }
            else
            {
                var currentDevice = await devSrv.GetCurrentDeviceInfo();

                var session = db.Sessions
                    .Include(s => s.Device)
                    .FirstOrDefault(
                        s => s.Token == token &&

                        // Verify device info
                        s.Device.AccountId.ToString() == accountId &&
                        s.Device.Address == currentDevice.Location &&
                        s.Device.DeviceOS == currentDevice.OS &&
                        s.Device.DeviceType == currentDevice.Type &&
                        s.Device.DeviceBrowser == currentDevice.Browser &&

                        s.ExpiresAt > DateTime.Now
                    );

                if (session == null)
                {
                    await context.SignOutAsync();
                    logout = true;
                }
                else
                {
                    var acc = db.Accounts
                        .Include(a => a.AccountType)
                        .FirstOrDefault(a =>
                            a.Id == int.Parse(accountId) &&
                            a.AccountType.Name == role &&
                            !a.IsDeleted
                        );
                    if (acc == null || !session.Device.IsVerified)
                    {
                        db.Sessions.Remove(session);
                        db.SaveChanges();

                        await context.SignOutAsync();
                        logout = true;
                    }
                    else
                    {
                        context.Items["Account"] = acc;
                    }
                }
            }
        }

        if (requiresAuth && logout)
        {
            context.Response.Redirect(loginPath);
            return;
        }

        await next(context);
    }
}
