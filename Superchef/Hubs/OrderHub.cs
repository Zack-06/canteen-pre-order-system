using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Superchef.Hubs;

[Authorize(Roles = "Customer")]
public class OrderHub : Hub
{
    /*
    // Update variant stock count in item info page
    await Clients.All.SendAsync("UpdateStock", variantId);

    // Update slot status to enable or disable radio input
    await Clients.All.SendAsync("SlotActive", date, time, true); slot is active
    await Clients.All.SendAsync("SlotActive", date, time, false); slot is inactive
    */
}