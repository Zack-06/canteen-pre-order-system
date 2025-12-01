using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        var vm = new HomePageVM
        {
            
        };
        
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
