using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

public class HomeController : Controller
{
    private readonly DB db;
    public HomeController(DB db)
    {
        this.db = db;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Admin", "Home");
            }
            else if (User.IsInRole("Vendor"))
            {
                return RedirectToAction("Vendor", "Home");
            }
        }

        var vm = new HomePageVM();

        // Trending Items: Top 10 items based on quantity sold in the last 7 days
        vm.TrendingItems = db.Items
            .Include(i => i.Variants)
                .ThenInclude(v => v.OrderItems)
            .Include(i => i.Reviews)
            .Where(ExpressionService.ShowItemToCustomerExpr)
            .Select(i => new
            {
                Item = i,
                TotalQuantity = i.Variants
                    .SelectMany(v => v.OrderItems)
                    .Where(oi =>
                        oi.Order.Status == "Completed" &&
                        oi.Order.CreatedAt >= DateTime.Now.AddDays(-7)
                    )
                    .Sum(oi => (int?)oi.Quantity)
                    ?? 0
            })
            .OrderByDescending(x => x.TotalQuantity)
            .Take(10)
            .Select(x => x.Item)
            .ToList();

        // Categories: Get all categories
        vm.Categories = db.Categories.Where(c => c.Name != "Others").ToList();

        // Order Again Items: Last 10 items ordered by the user
        if (User.Identity?.IsAuthenticated == true)
        {
            var accountId = User.Identity.Name;
            var orderAgainItems = db.OrderItems
                .Where(ExpressionService.AllowCalculateOrderItemQuantityExpr)
                .Where(oi =>
                    oi.Order.Status == "Completed" &&
                    oi.Order.AccountId.ToString() == accountId
                )
                .GroupBy(oi => oi.Variant.Item)
                .Select(g => new
                {
                    Item = g.Key,
                    OrderDate = g.Max(oi => oi.Order.CreatedAt),
                })
                .OrderByDescending(x => x.OrderDate)
                .Take(10)
                .Select(x => x.Item.Id)
                .ToList();
            vm.OrderAgainItems = db.Items
                .Include(i => i.Variants)
                    .ThenInclude(v => v.OrderItems)
                .Include(i => i.Reviews)
                .Where(i => orderAgainItems.Contains(i.Id))
                .ToList();
        }

        // Daily Discover: Random 30 active items
        vm.DailyDiscoverItems = db.Items
            .Include(i => i.Variants)
                .ThenInclude(v => v.OrderItems)
            .Include(i => i.Reviews)
            .Where(ExpressionService.ShowItemToCustomerExpr)
            .OrderBy(r => Guid.NewGuid())
            .Take(30)
            .ToList();

        return View(vm);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Vendor(string? ReturnUrl)
    {
        HttpContext.Session.Remove("StoreId");

        var stores = db.Stores
            .Include(s => s.Items)
                .ThenInclude(r => r.Reviews)
            .Where(s => s.AccountId == HttpContext.GetAccount()!.Id)
            .ToList();

        ViewBag.ReturnUrl = ReturnUrl;

        return View(stores);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Admin(AdminHomePageVM vm)
    {
        vm.TotalSales = 0;
        vm.TotalSalesStat = 0;
        vm.TotalOrders = 0;
        vm.TotalOrdersStat = 0;
        vm.TotalCustomers = 0;
        vm.TotalCustomersStat = 0;
        vm.SalesPerformanceMonths = new List<string>();
        vm.SalesPerformanceOrders = new List<int>();
        vm.LoginDevices = new List<string>();
        vm.LoginDevicesCount = new List<int>();

        return View(vm);
    }
}
