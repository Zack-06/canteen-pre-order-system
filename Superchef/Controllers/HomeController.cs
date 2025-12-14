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
        var vm = new HomePageVM();

        // Trending Items: Top 10 items based on quantity sold in the last 7 days
        var trendingItems = db.OrderItems
            .Where(ExpressionService.AllowCalculateOrderItemQuantityExpr)
            .Where(oi =>
                oi.Order.Status == "Completed" &&
                oi.Order.CreatedAt >= DateTime.Now.AddDays(-7)
            )
            .GroupBy(oi => oi.Variant.Item)
            .Select(g => new
            {
                Item = g.Key,
                TotalQuantity = g.Sum(oi => oi.Quantity)
            })
            .OrderByDescending(x => x.TotalQuantity)
            .Take(10)
            .Select(x => x.Item.Id)
            .ToList();
        vm.TrendingItems = db.Items
            .Include(i => i.Variants)
                .ThenInclude(v => v.OrderItems)
            .Include(i => i.Reviews)
            .Where(i => trendingItems.Contains(i.Id))
            .ToList();

        // Categories: Get all categories
        vm.Categories = db.Categories.Where(c => c.Id != 1).ToList();

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

    public IActionResult Vendor()
    {
        return View();
    }

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
