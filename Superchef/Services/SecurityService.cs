using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Services;

public class SecurityService
{
    private readonly IHttpContextAccessor ct;
    private readonly DB db;
    private readonly IDataProtectionProvider dp;
    private readonly DeviceService devSrv;

    public SecurityService(IHttpContextAccessor ct, DB db, IDataProtectionProvider dp, DeviceService devSrv)
    {
        this.ct = ct;
        this.db = db;
        this.dp = dp;
        this.devSrv = devSrv;
    }

    private readonly PasswordHasher<object> ph = new();

    public string HashPassword(string password)
    {
        return ph.HashPassword(0, password);
    }

    public bool VerifyPassword(string hash, string password)
    {
        return ph.VerifyHashedPassword(0, hash, password) == PasswordVerificationResult.Success;
    }

    public void SignIn(string accountId, string role, string sessionToken)
    {
        // Claim, identity and principal
        List<Claim> claims = [
            new(ClaimTypes.Name, accountId),
            new(ClaimTypes.Role, role),
            new("SessionToken", sessionToken)
        ];

        ClaimsIdentity identity = new(claims, "Cookies");

        ClaimsPrincipal principal = new(identity);

        // Remember me
        AuthenticationProperties properties = new()
        {
            IsPersistent = true
        };

        // Sign in
        ct.HttpContext!.SignInAsync(principal, properties);
    }

    public void SignOut()
    {
        // Sign out
        ct.HttpContext!.SignOutAsync();
    }

    public async Task<string?> ClaimSession()
    {
        var httpContext = ct.HttpContext;
        if (httpContext == null)
        {
            return "No HTTP context available";
        }

        var protectedSessionToken = httpContext.Request.Cookies["PreAuthSession"];

        if (string.IsNullOrEmpty(protectedSessionToken))
        {
            return "Invalid session cookie";
        }

        try
        {
            // decrypt
            string sessionToken = dp.CreateProtector("SessionToken").Unprotect(protectedSessionToken);

            // remove cookie
            httpContext.Response.Cookies.Delete("PreAuthSession");

            var currentDevice = await devSrv.GetCurrentDeviceInfo();

            // get session
            var session = db.Sessions
                .Include(s => s.Device)
                .Include(s => s.Device.Account)
                .Include(s => s.Device.Account.AccountType)
                .FirstOrDefault(s =>
                    s.Token == sessionToken &&
                    s.ExpiresAt > DateTime.Now &&
                    s.Device.Address == currentDevice.Location &&
                    s.Device.DeviceOS == currentDevice.OS &&
                    s.Device.DeviceType == currentDevice.Type &&
                    s.Device.DeviceBrowser == currentDevice.Browser
                );
            if (session == null)
            {
                return "Session expired or invalid device";
            }

            // extend session
            session.ExpiresAt = DateTime.Now.AddDays(30);
            db.SaveChanges();

            // claim session
            SignIn(session.Device.AccountId.ToString(), session.Device.Account.AccountType.Name, session.Token);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return "Invalid session signature";
        }

        return null;
    }
}