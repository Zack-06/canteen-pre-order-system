using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Superchef.Services;

public class DeviceService
{
    private readonly DB db;
    private readonly IHttpContextAccessor ct;
    private readonly IDataProtectionProvider dp;
    private readonly IConfiguration cf;

    public DeviceService(DB db, IHttpContextAccessor ct, IDataProtectionProvider dp, IConfiguration cf)
    {
        this.db = db;
        this.ct = ct;
        this.dp = dp;
        this.cf = cf;
    }

    public async Task<DeviceInfo> GetCurrentDeviceInfo()
    {
        var info = new DeviceInfo();

        string ip = ct.HttpContext!.Connection.RemoteIpAddress!.ToString();
        if (ct.HttpContext!.Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            ip = ct.HttpContext.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0];
        }

        if (ip == "::1" || ip == "127.0.0.1")
        {
            info.Location = "Local Host";
        }
        else
        {
            try
            {
                // Get Geo Info
                using var client = new HttpClient();
                var geoApi = $"https://api.findip.net/{ip}/?token={cf["IpNet:Token"]}";
                var response = await client.GetStringAsync(geoApi);
                var json = JsonDocument.Parse(response);
                var root = json.RootElement;

                string city = root.GetProperty("city").GetProperty("names").GetProperty("en").GetString() ?? "Unknown City";
                string region = root.GetProperty("subdivisions")[0].GetProperty("names").GetProperty("en").GetString() ?? "Unknown Region";
                string country = root.GetProperty("country").GetProperty("names").GetProperty("en").GetString() ?? "Unknown Country";

                info.Location = $"{city}, {region}, {country}".Trim(' ', ',');
            }
            catch
            {
                info.Location = "Local Host";
            }
        }


        string userAgent = ct.HttpContext!.Request.Headers.UserAgent.ToString();

        // Browser
        if (userAgent.Contains("Edg"))
            info.Browser = "Microsoft Edge";
        else if (userAgent.Contains("OPR") || userAgent.Contains("Opera"))
            info.Browser = "Opera";
        else if (userAgent.Contains("Chrome"))
            info.Browser = "Google Chrome";
        else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome"))
            info.Browser = "Safari";
        else if (userAgent.Contains("Firefox"))
            info.Browser = "Mozilla Firefox";
        else if (userAgent.Contains("MSIE") || userAgent.Contains("Trident"))
            info.Browser = "Internet Explorer";

        // OS
        if (userAgent.Contains("Windows"))
            info.OS = "Windows";
        else if (userAgent.Contains("Macintosh"))
            info.OS = "MacOS";
        else if (userAgent.Contains("iPhone") || userAgent.Contains("iPad"))
            info.OS = "iOS";
        else if (userAgent.Contains("Android"))
            info.OS = "Android";
        else if (userAgent.Contains("Linux"))
            info.OS = "Linux";

        // Device
        if (
            userAgent.Contains("Mobi", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)
        )
            info.Type = "phone";
        else if (
            userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
        )
            info.Type = "tablet";


        return info;
    }

    public Device? GetKnownDeviceForAccount(int accountId, DeviceInfo deviceInfo)
    {
        return db.Devices.FirstOrDefault(
            u => u.AccountId == accountId
            && u.DeviceOS == deviceInfo.OS
            && u.DeviceType == deviceInfo.Type
            && u.DeviceBrowser == deviceInfo.Browser
            && u.Address == deviceInfo.Location
        );
    }

    public async Task<Device> CreateDevice(Account account, bool verified = false)
    {
        var deviceInfo = await GetCurrentDeviceInfo();

        // Add new device
        Device device = new()
        {
            IsVerified = verified,
            Address = deviceInfo.Location,
            DeviceOS = deviceInfo.OS,
            DeviceType = deviceInfo.Type,
            DeviceBrowser = deviceInfo.Browser,
            AccountId = account.Id
        };
        db.Devices.Add(device);
        db.SaveChanges();

        return device;
    }

    public void createFullShortSession(int deviceId)
    {
        var httpContext = ct.HttpContext;
        if (httpContext == null) return;

        // Remove all sessions for this device
        db.Sessions.RemoveRange(db.Sessions.Where(u => u.DeviceId == deviceId));

        // Create session token in database
        var sessionToken = createSession(deviceId, true);

        string protectedSessionToken = dp.CreateProtector("SessionToken").Protect(sessionToken);

        // Create short term cookie
        httpContext.Response.Cookies.Append("PreAuthSession", protectedSessionToken, new()
        {
            Expires = DateTime.Now.AddMinutes(5),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });
    }

    public string createSession(int deviceId, bool isShort)
    {
        // Generate session token
        var sessionToken = GeneratorHelper.RandomString(50);
        while (db.Sessions.Any(u => u.Token == sessionToken))
        {
            sessionToken = GeneratorHelper.RandomString(50);
        }

        // Create session
        db.Sessions.Add(new()
        {
            Token = sessionToken,
            ExpiresAt = isShort ? DateTime.Now.AddMinutes(5) : DateTime.Now.AddDays(30),
            DeviceId = deviceId
        });
        db.SaveChanges();

        return sessionToken;
    }
}

public class DeviceInfo
{
    public string Location { get; set; } = "Unknown";
    public string Browser { get; set; } = "Unknown Browser";
    public string OS { get; set; } = "Unknown OS";
    public string Type { get; set; } = "computer";
}