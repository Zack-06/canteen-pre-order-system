using Microsoft.EntityFrameworkCore;

namespace Superchef.Services;

public class CleanupService
{
    private readonly DB db;
    private readonly SystemOrderService odrSrv;
    private readonly PaymentService paySrv;

    public CleanupService(DB db, SystemOrderService odrSrv, PaymentService paySrv)
    {
        this.db = db;
        this.odrSrv = odrSrv;
        this.paySrv = paySrv;
    }

    public async Task OrderExpiryCleanup()
    {
        // remove all expired verification reqeusts if more than 1 day
        db.Verifications.RemoveRange(db.Verifications.Where(u => u.ExpiresAt < DateTime.Now.AddDays(-1)));

        // Set HashSet
        HashSet<string> OrdersToCancel = [];

        // Cancel all expired pending orders
        OrdersToCancel.UnionWith(db.Orders.Where(b => b.ExpiresAt < DateTime.Now).Select(b => b.Id));

        // Cancel all expired unpaid orders
        // OrdersToCancel.UnionWith(db.Orders.Where(b => b.Payment != null && b.Payment.ExpiresAt < DateTime.Now).Select(b => b.Id));

        // Handle all confirmed orders
        foreach (var order in
            db.Orders
                .Where(b =>
                    (b.Status == "Confirmed" || b.Status == "Ready") &&
                    b.Slot.EndTime < DateTime.Now
                )
                .ToList()
        )
        {
            order.Status = "Completed";
            paySrv.HandlePayout(order);
        }
        db.SaveChanges();

        // Handle deletion of accounts
        var deletedUsers = db.Accounts.Where(a => a.DeletionAt < DateTime.Now && !a.IsDeleted).ToList();
        foreach (var account in deletedUsers)
        {
            account.IsDeleted = true;
            account.DeletionAt = null;
        }
    }

    public void CleanUp(Variant variant)
    {
        variant.IsDeleted = true;
        variant.IsActive = false;

        db.Carts.RemoveRange(db.Carts.Where(c => c.VariantId == variant.Id));
        db.SaveChanges();
    }

    public string? CanCleanUp(Variant variant)
    {
        variant = db.Variants
            .Include(v => v.OrderItems)
                .ThenInclude(oi => oi.Order)
            .FirstOrDefault(v => v.Id == variant.Id)!;

        if (variant.OrderItems.Any(oi => oi.Order.Status != "Canceled" && oi.Order.Status != "Completed"))
        {
            return $"Variant with Id {variant.Id} has orders in progress. Please complete or cancel them.";
        }

        return null;
    }

    public void CleanUp(Item item)
    {
        item = db.Items
            .Include(i => i.Variants)
            .Include(i => i.Keywords)
            .FirstOrDefault(i => i.Id == item.Id)!;

        item.IsDeleted = true;
        item.IsActive = false;

        foreach (var varaint in item.Variants.Where(v => !v.IsDeleted))
        {
            CleanUp(varaint);
        }

        db.Reviews.RemoveRange(db.Reviews.Where(r => r.ItemId == item.Id));

        item.Keywords.Clear();
        CleanUpKeyword();

        db.Favourites.RemoveRange(db.Favourites.Where(f => f.ItemId == item.Id));
        db.SaveChanges();
    }

    public string? CanCleanUp(Item item)
    {
        item = db.Items
            .Include(i => i.Variants)
            .FirstOrDefault(i => i.Id == item.Id)!;

        foreach (var variant in item.Variants)
        {
            var error = CanCleanUp(variant);
            if (error != null) return error;
        }

        return null;
    }

    public void CleanUpKeyword()
    {
        var orphanKeywords = db.Keywords
            .Where(k => !k.Items.Any())
            .ToList();

        db.Keywords.RemoveRange(orphanKeywords);
        db.SaveChanges();
    }

    public void CleanUp(Category category)
    {
        category = db.Categories
            .Include(c => c.Items)
            .FirstOrDefault(c => c.Id == category.Id)!;

        foreach (var item in category.Items)
        {
            item.CategoryId = 1;
        }

        db.SaveChanges();
    }

    public void CleanUp(Store store)
    {
        store = db.Stores
            .Include(s => s.Items)
            .FirstOrDefault(s => s.Id == store.Id)!;

        store.IsDeleted = true;

        foreach (var item in store.Items)
        {
            CleanUp(item);
        }

        store.SlotTemplates.Clear();
        db.SaveChanges();
    }

    public string? CanCleanUp(Store store)
    {
        store = db.Stores
            .Include(s => s.Items)
            .FirstOrDefault(s => s.Id == store.Id)!;

        foreach (var item in store.Items)
        {
            var error = CanCleanUp(item);
            if (error != null) return error;
        }

        return null;
    }

    public void CleanUp(Venue venue)
    {
        venue = db.Venues
            .Include(c => c.Stores)
            .FirstOrDefault(c => c.Id == venue.Id)!;

        foreach (var item in venue.Stores)
        {
            item.VenueId = 1;
        }

        db.SaveChanges();
    }

    public void CleanUp(Account account)
    {
        account = db.Accounts
            .Include(a => a.AccountType)
            .Include(a => a.Devices)
            .Include(a => a.Verifications)
            .Include(a => a.Carts)
            .Include(a => a.Favourites)
            .FirstOrDefault(a => a.Id == account.Id)!;

        db.Devices.RemoveRange(db.Devices.Where(d => d.AccountId == account.Id));
        db.Verifications.RemoveRange(db.Verifications.Where(v => v.AccountId == account.Id));

        if (account.AccountType.Name == "Customer")
        {
            db.Carts.RemoveRange(db.Carts.Where(c => c.AccountId == account.Id));
            db.Favourites.RemoveRange(db.Favourites.Where(f => f.AccountId == account.Id));
            account.DeletionAt = DateTime.Now.AddDays(7);
        }
        else
        {
            account.IsDeleted = true;
        }

        db.SaveChanges();
    }

    public string? CanCleanUp(Account account)
    {
        account = db.Accounts
            .Include(a => a.AccountType)
            .FirstOrDefault(a => a.Id == account.Id)!;

        if (account.AccountType.Name == "Customer")
        {
            if (account.Orders.Any(o => o.Status != "Canceled" && o.Status != "Completed"))
            {
                return "You have orders in progress. Please cancel or wait for them to be completed.";
            }
        }
        else if (account.AccountType.Name == "Vendor")
        {
            if (account.Stores.Any(s => !s.IsDeleted))
            {
                return "Please delete all stores before deleting this account.";
            }
        }

        return null;
    }
}