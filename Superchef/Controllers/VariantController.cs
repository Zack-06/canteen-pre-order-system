using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class VariantController : Controller
{

    public IActionResult Manage(ManageVariantVM vm)
    {
        Dictionary<string, Expression<Func<Variant, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Price", a => a.Price },
            { "Status", a => a.Status },
            { "Stock Count", a => a.Stock },
            { "Creation Date", a => a.CreatedAt }
        };
        ViewBag.Fields = sortOptions.Keys.ToList();


        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "name", Text = "Search By Name" },
            new() { Value = "id", Text = "Search By Id" }
        ];
        vm.AvailableStatuses = ["Active", "Inactive"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        ViewBag.ItemName = "abc";

        return View(vm);
    }

    public IActionResult Add(int itemId)
    {
        // add new variant
        return View();
    }

    public IActionResult Edit(int id)
    {
        var vm = new EditVariantVM
        {
            Id = id,
            CreatedAt = DateTime.Now,
            Name = "abc",
            Price = 100,
            StockCount = 0,
            Image = null,
            Active = false,
        };

        ViewBag.ItemName = "abc";

        return View(vm);
    }
}
