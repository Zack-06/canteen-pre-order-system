using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Services;

public class SystemOrderService
{
    private readonly DB db;
    private readonly PaymentService paySrv;
    private readonly IHubContext<OrderHub> orderHubContext;

    public SystemOrderService(DB db, PaymentService paySrv, IHubContext<OrderHub> orderHubContext)
    {
        this.db = db;
        this.paySrv = paySrv;
        this.orderHubContext = orderHubContext;
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
            await orderHubContext.Clients.All.SendAsync("UpdateStock", id);
        }
    }

    public async Task CancelOrder(Order order)
    {
        List<int> reloads = [];
        ProcessOrderCancellation(order, reloads);

        db.SaveChanges();
        await BroadcastStockReloads(reloads);
    }

    public async Task BulkCancelOrders(HashSet<string> ids)
    {
        var orders = db.Orders.Where(o => ids.Contains(o.Id)).ToList();
        List<int> reloads = [];

        foreach (var order in orders)
        {
            ProcessOrderCancellation(order, reloads);
        }

        db.SaveChanges();
        await BroadcastStockReloads(reloads);
    }
}