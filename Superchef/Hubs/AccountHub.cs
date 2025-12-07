using Microsoft.AspNetCore.SignalR;

namespace Superchef.Hubs;

public class AccountHub : Hub
{
    private readonly DB db;

    public AccountHub(DB db)
    {
        this.db = db;
    }

    public async Task Initialize()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            await Clients.Caller.SendAsync("Error", "No HTTP context available");
            return;
        }

        var account = httpContext.GetAccount();
        if (account == null)
        {
            return;
        }

        var token = httpContext.User.FindFirst("SessionToken")?.Value;
        if (token == null)
        {
            return;
        }

        var session = db.Sessions.FirstOrDefault(s => s.Token == token);
        if (session == null)
        {
            return;
        }

        var hashedToken = BCrypt.Net.BCrypt.HashPassword(token);

        await Clients.Caller.SendAsync("Initialized", account.Id, session.DeviceId, hashedToken);
    }
}