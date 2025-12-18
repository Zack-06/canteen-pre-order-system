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
            .Where(i => i.IsActive)
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
                .Where(oi =>
                    oi.Variant.Item.IsActive &&
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
            .Where(i => i.IsActive)
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
            .Where(s =>
                s.AccountId == HttpContext.GetAccount()!.Id &&
                !s.IsDeleted
            )
            .ToList();

        ViewBag.ReturnUrl = ReturnUrl;

        return View(stores);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Admin(AdminHomePageVM vm)
    {
        var activityLogs = db.AuditLogs
            .Include(a => a.Account)
            .OrderByDescending(a => a.CreatedAt);

        var results = activityLogs.ToPagedList(vm.Page, 10);
        if (results.Count == 0)
        {
            vm.Page = 1;
            results = activityLogs.ToPagedList(vm.Page, 10);
        }
        vm.ActivityLogs = results;

        if (Request.IsAjax())
        {
            return PartialView("_Admin", vm);
        }

        var tmpNow = DateTime.Now;
        vm.DisplayMonth = FormatHelper.ToDateTimeFormat(tmpNow, "MMMM");

        var firstDayCurrentMonth = new DateTime(tmpNow.Year, tmpNow.Month, 1);
        var firstDayLastMonth = firstDayCurrentMonth.AddMonths(-1);
        var lastDayLastMonth = firstDayLastMonth.AddDays(-1);

        // Sales
        var currentSales = db.Orders
            .Where(o =>
                o.Status == "Completed" &&
                o.CreatedAt >= firstDayCurrentMonth)
            .SelectMany(o => o.OrderItems)
            .Sum(oi => oi.Quantity * oi.Price);

        var lastMonthSales = db.Orders
            .Where(o =>
                o.Status == "Completed" &&
                o.CreatedAt >= firstDayLastMonth &&
                o.CreatedAt <= lastDayLastMonth)
            .SelectMany(o => o.OrderItems)
            .Sum(oi => oi.Quantity * oi.Price);

        vm.TotalSales = currentSales;
        vm.TotalSalesStat = CalculateHelper.CalculatePercentageChange(lastMonthSales, currentSales);

        // Orders
        var currentOrders = db.Orders.Count(o =>
                o.Status == "Completed" &&
                o.CreatedAt >= firstDayCurrentMonth
            );

        var lastMonthOrders = db.Orders.Count(o =>
            o.Status == "Completed" &&
            o.CreatedAt >= firstDayLastMonth &&
            o.CreatedAt <= lastDayLastMonth
        );

        vm.TotalOrders = currentOrders;
        vm.TotalOrdersStat = CalculateHelper.CalculatePercentageChange(lastMonthOrders, currentOrders);

        // Customers
        var currentCustomers = db.Accounts.Count(a =>
            a.AccountType.Name == "Customer" &&
            a.CreatedAt >= firstDayCurrentMonth
        );

        var lastMonthCustomers = db.Accounts.Count(a =>
            a.AccountType.Name == "Customer" &&
            a.CreatedAt >= firstDayLastMonth &&
            a.CreatedAt <= lastDayLastMonth
        );

        vm.TotalCustomers = currentCustomers;
        vm.TotalCustomersStat = CalculateHelper.CalculatePercentageChange(lastMonthCustomers, currentCustomers);

        // Sales Performance
        vm.SalesPerformanceMonths = [];
        vm.SalesPerformanceOrders = [];
        for (int i = 4; i >= 0; i--)
        {
            var monthDate = DateTime.Now.AddMonths(-i);
            var firstDayOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            var count = db.Orders.Count(o => o.CreatedAt >= firstDayOfMonth && o.CreatedAt <= lastDayOfMonth && o.Status == "Completed");

            vm.SalesPerformanceMonths.Add(monthDate.ToString("MMM"));
            vm.SalesPerformanceOrders.Add(count);
        }

        // Login Devices
        vm.LoginDevices = ["Phone", "Computer", "Tablet"];
        vm.LoginDevicesCount = [];
        foreach (var device in vm.LoginDevices)
        {
            var count = db.Devices.Count(d => d.DeviceType.ToLower() == device.ToLower());
            vm.LoginDevicesCount.Add(count);
        }

        return View(vm);
    }
}
