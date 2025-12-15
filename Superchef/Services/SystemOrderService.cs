using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Services;

public class SystemOrderService
{
    private readonly DB db;
    private readonly PaymentService paySrv;
    private readonly IHubContext<OrderHub> fnbOrderHubContext;

    public SystemOrderService(DB db, PaymentService paySrv, IHubContext<OrderHub> fnbOrderHubContext)
    {
        this.db = db;
        this.paySrv = paySrv;
        this.fnbOrderHubContext = fnbOrderHubContext;
    }

    private void ProcessOrderCancellation(Order order, List<int> reloads)
    {
        order = db.Orders
            .Include(o => o.Payment)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
            .FirstOrDefault(o => o.Id == order.Id)!;

        foreach (var item in order.OrderItems)
        {
            if (item.Variant.IsDeleted) continue;
            item.Variant.Stock += item.Quantity;

            if (!reloads.Contains(item.Variant.Id))
            {
                reloads.Add(item.Variant.Id);
            }
        }

        if (order.Status == "Pending")
        {
            db.Orders.Remove(order);
        }
        else if (order.Status == "Confirmed")
        {
            order.Status = "Cancelled";
            order.ExpiresAt = null;

            if (order.Payment != null)
            {
                paySrv.TriggerRefund(order.Payment.StripePaymentIntentId);
            }
        }
    }

    private async Task BroadcastStockReloads(List<int> reloads)
    {
        foreach (var id in reloads)
        {
            await fnbOrderHubContext.Clients.All.SendAsync("UpdateStock", id);
        }
    }

    public async Task CancelOrder(string id)
    {
        var order = db.Orders.FirstOrDefault(o => o.Id == id);
        if (order == null) return;

        List<int> reloads = [];
        ProcessOrderCancellation(order, reloads);

        db.SaveChanges();
        await BroadcastStockReloads(reloads);
    }
}