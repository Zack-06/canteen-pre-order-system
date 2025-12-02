using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Superchef.Controllers;

public class ItemController : Controller
{
    private readonly DB db;

    public ItemController(DB db)
    {
        this.db = db;
    }

    [HttpGet]
    [Route("Item/Info/{slug}")]
    public IActionResult Info(string slug, ItemInfoVM vm)
    {
        vm.Item = new Item
        {
            Id = 1,
            Name = "abc",
            Slug = "abc",
            Description = "abc",
            Category = new() { Id = 1, Name = "abc" },
            Keywords = [],
            Image = "",
            CreatedAt = DateTime.Now,
            Variants = [],
            CategoryId = 1,
            Favourites = [],
            Reviews = [],
            IsActive = true,
            IsDeleted = false,
            StoreId = 1,
        };
        vm.Reviews = [
            new Review { Account = new Account { Name = "abc", Image = null }, Comment = "abc", Rating = 4, CreatedAt = DateTime.Now },
            new Review { Account = new Account { Name = "abc", Image = null }, Comment = "abc", Rating = 4, CreatedAt = DateTime.Now },
            new Review { Account = new Account { Name = "abc", Image = null }, Comment = "abc", Rating = 4, CreatedAt = DateTime.Now }
        ];
        vm.TotalReviews = 10;
        vm.AverageRating = 4.5m;
        vm.TotalSold = 10;

        ViewBag.HasCommented = false;
        ViewBag.HasBought = true;
        ViewBag.Authenticated = true;

        // item info details
        return View(vm);
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
        var vm = new AddItemVM
        {
            StoreId = storeId,

            AvailableCategories = db.Categories.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };

        ViewBag.StoreName = "abc";

        return View(vm);
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var vm = new EditItemVM
        {
            Id = id,
            CreatedAt = DateTime.Now,
            Name = "abc",
            Slug = "abc",
            Description = "abc",
            Category = 1,
            Keywords = ["abcde"],
            Image = null,
            Active = false,

            AvailableCategories = db.Categories.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };

        ViewBag.StoreName = "abc";
        ViewBag.ImageUrl = "abc";

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditItemVM vm)
    {
        Console.WriteLine("KEYWORDS");
        Console.WriteLine(string.Join(",", vm.Keywords));

        return View(vm);
    }

    // ==========REMOTE==========
    public bool CheckCategory(int category, int? id)
    {
        return true;
    }

    public bool IsSlugUnique(string slug, int? id)
    {
        return true;
    }
}
