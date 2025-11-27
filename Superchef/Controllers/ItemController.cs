using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class ItemController : Controller
{
    private readonly DB db;

    public ItemController(DB db)
    {
        this.db = db;
    }

    public IActionResult Index()
    {
        // item info details
        return View();
    }

    public IActionResult Manage(ManageItemVM vm)
    {
        Dictionary<string, Expression<Func<Item, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Slug", a => a.Slug },
            { "Variants Count", a => a.Variants.Count },
            { "Category", a => a.Category.Name },
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
            new() { Value = "slug", Text = "Search By Slug" },
            new() { Value = "id", Text = "Search By Id" }
        ];
        vm.AvailableStatuses = ["Active", "Inactive"];
        vm.AvailableCategories = db.Categories.ToList();

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        ViewBag.StoreName = "abc";

        return View(vm);
    }

    public IActionResult Add(int storeId)
    {
        // add new item
        return View();
    }

    public IActionResult Edit(int id)
    {
        // edit item details
        return View();
    }
}
