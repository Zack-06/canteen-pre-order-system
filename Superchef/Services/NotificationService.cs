using WebPush;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Services;

public class NotificationService
{
    private readonly DB db;
    private readonly IConfiguration cf;

    public NotificationService(DB db, IConfiguration cf)
    {
        this.db = db;
        this.cf = cf;
    }

    public async Task SendNotification(string title, string body, Account account)
    {
        account = db.Accounts
            .Include(a => a.Devices)
                .ThenInclude(d => d.Sessions)
            .FirstOrDefault(a => a.Id == account.Id)!;



        foreach (var session in account.Devices.SelectMany(d => d.Sessions))
        {
            await SendNotification(title, body, session);
        }
    }

    public async Task SendNotification(string title, string body, Store store)
    {
        store = db.Stores
            .Include(s => s.Sessions)
            .FirstOrDefault(s => s.Id == store.Id)!;

        foreach (var session in store.Sessions)
        {
            await SendNotification(title, body, session);
        }
    }

    private async Task SendNotification(string title, string body, Session session)
    {
        if (session.PushEndpoint == null || session.PushP256dh == null || session.PushAuth == null) return;

        var vapidDetails = new VapidDetails
        {
            Subject = cf["Vapid:Subject"],
            PublicKey = cf["Vapid:PublicKey"],
            PrivateKey = cf["Vapid:PrivateKey"]
        };

        var pushSubscription = new PushSubscription(session.PushEndpoint, session.PushP256dh, session.PushAuth);

        var notification = JsonSerializer.Serialize(new
        {
            title = title,
            body = body
        });

        var webPushClient = new WebPushClient();
        try
        {
            await webPushClient.SendNotificationAsync(pushSubscription, notification, vapidDetails);
        }
        catch (WebPushException ex)
        {
            // if the session is dead or the user blocked notifications
            if (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                session.PushEndpoint = null;
                session.PushP256dh = null;
                session.PushAuth = null;
                db.SaveChanges();
            }
        }
    }
}