using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

public class VenueController : Controller
{
    private readonly DB db;
    public VenueController(DB db)
    {
        this.db = db;
    }

    public IActionResult Manage(ManageVenueVM vm)
    {
        Dictionary<string, Expression<Func<Venue, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Stores Count", a => a.Stores.Count }
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

    [HttpGet]
    public IActionResult Edit(string id)
    {
        var vm = new EditVenueVM
        {
            Id = int.Parse(id),
            Name = "abc"
        };

        return View(vm);
    }

    // ==========REMOTE==========
    public bool CheckNameExists(string name, int? id)
    {
        if (id != null)
        {
            return !db.Venues.Any(v => v.Id != id && v.Name == name);
        }

        return !db.Venues.Any(v => v.Name == name);
    }
}
