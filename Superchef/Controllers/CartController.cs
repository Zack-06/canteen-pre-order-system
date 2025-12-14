using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly DB db;
    public CartController(DB db)
    {
        this.db = db;
    }

    // display stores
    public IActionResult Index()
    {

        var storeIds = db.Carts
            .Where(c => c.AccountId == HttpContext.GetAccount()!.Id)
            .Select(c => c.Variant.Item.StoreId)
            .Distinct()
            .ToList();

        var stores = db.Stores
            .Include(s => s.Items)
                .ThenInclude(i => i.Reviews)
            .Where(s => storeIds.Contains(s.Id))
            .ToList();

        var itemsCount = new Dictionary<int, int>();
        foreach (var store in stores)
        {
            itemsCount.Add(
                store.Id, 
                db.Carts.Count(c => 
                    c.AccountId == HttpContext.GetAccount()!.Id && 
                    c.Variant.Item.StoreId == store.Id
                )
            );
        }

        return View((stores, itemsCount));
    }

    public IActionResult Store(int id)
    {
        // display items for store with id

        return View();
    }
}
