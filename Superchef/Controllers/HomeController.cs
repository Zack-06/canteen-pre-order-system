using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
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

        Console.WriteLine("HELLO");
        Console.WriteLine(vm.Page);

        return View(vm);
    }
}
