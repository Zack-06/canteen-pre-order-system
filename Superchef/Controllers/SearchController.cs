using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class SearchController : Controller
{
    // public IActionResult Index(SearchVM vm)
    // {
    //     ViewBag.SearchQuery = vm.Query;

    //     vm.AvailableTypes = ["Items", "Stores"];
    //     vm.Type ??= vm.AvailableTypes.First();

    //     if (Request.IsAjax())
    //     {
    //         return PartialView("_SearchResults", vm);
    //     }

    //     return View(vm);
    // }

    public IActionResult Index()
    {
        return View(new SearchVM
        {
            Query = "abc"
        });
    }
}
