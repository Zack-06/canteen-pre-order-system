using Microsoft.EntityFrameworkCore;

namespace Superchef.Services;

public class InactiveService
{
    private readonly DB db;

    public InactiveService(DB db)
    {
        this.db = db;
    }

    public void SetInactive(Item item)
    {
        item = db.Items
            .Include(i => i.Variants)
            .FirstOrDefault(i => i.Id == item.Id)!;
        
        db.Favourites.RemoveRange(db.Favourites.Where(f => f.ItemId == item.Id));
        item.IsActive = false;
        foreach (var variant in item.Variants)
        {
            SetInactive(variant);
        }
        db.SaveChanges();
    }

    public void SetInactive(Variant variant)
    {
        db.Carts.RemoveRange(db.Carts.Where(c => c.VariantId == variant.Id));
        variant.IsActive = false;
        db.SaveChanges();
    }
}