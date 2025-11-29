using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class CategoryController : Controller
{
    private readonly DB db;
    public CategoryController(DB db)
    {
        this.db = db;
    }

    public IActionResult Manage(ManageCategoryVM vm)
    {
        Dictionary<string, Expression<Func<Category, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Items Count", a => a.Items.Count }
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

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        return View(vm);
    }

    public IActionResult Add()
    {
        return View();
    }

    public IActionResult Edit()
    {
        var vm = new EditCategoryVM
        {
            Id = 1,
            Name = "abc"
        };

        ViewBag.ImageUrl = "abc";

        return View(vm);
    }

    // ==========REMOTE==========
    public bool CheckNameExists(string name, int? id)
    {
        if (id != null)
        {
            return !db.Categories.Any(c => c.Id != id && c.Name == name);
        }

        return !db.Categories.Any(c => c.Name == name);
    }
}
