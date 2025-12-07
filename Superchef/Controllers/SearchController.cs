using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class SearchController : Controller
{
    public IActionResult Index()
    {
        var vm = new SearchVM
        {
            Query = "abc",
            AvailableTypes = new List<string> { "Items", "Stores" }
        };

        ViewBag.SearchQuery = vm.Query;

        if (vm.Type == null || !vm.AvailableTypes.Contains(vm.Type))
        {
            vm.Type = vm.AvailableTypes.First();
        }

        return View(vm);
    }
}
