using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

[Authorize(Roles = "Admin")]
public class VenueController : Controller
{
    private readonly DB db;
    private readonly CleanupService clnSrv;

    public VenueController(DB db, CleanupService clnSrv)
    {
        this.db = db;
        this.clnSrv = clnSrv;
    }

    public IActionResult Manage(ManageVenueVM vm)
    {
        Dictionary<string, Expression<Func<Venue, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Stores Count", a => a.Stores.Count(s => !s.IsDeleted) }
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

        var results = db.Venues
            .Include(v => v.Stores)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(v => v.Name.Contains(search));
                    break;
                case "id":
                    results = results.Where(v => v.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.MinStoresCount != null)
        {
            results = results.Where(v => v.Stores.Count(s => !s.IsDeleted) >= vm.MinStoresCount);
        }

        if (vm.MaxStoresCount != null)
        {
            results = results.Where(v => v.Stores.Count(s => !s.IsDeleted) <= vm.MaxStoresCount);
        }

        // Sort
        results = vm.Dir == "asc"
            ? results.OrderBy(sortOptions[vm.Sort])
            : results.OrderByDescending(sortOptions[vm.Sort]);
        
        vm.Results = results.ToPagedList(vm.Page, 10);

        if (Request.IsAjax())
        {
            return PartialView("_Manage", vm);
        }

        return View(vm);
    }

    public IActionResult Add()
    {
        return View(new AddVenueVM());
    }

    [HttpPost]
    public IActionResult Add(AddVenueVM vm)
    {
        if (ModelState.IsValid("Name") && !IsNameUnique(vm.Name))
        {
            ModelState.AddModelError("Name", "Name already exists.");
        }

        if (ModelState.IsValid)
        {
            var venue = new Venue
            {
                Name = vm.Name
            };

            db.Venues.Add(venue);

            db.AuditLogs.Add(new()
            {
                Action = "create",
                Entity = "venue",
                EntityId = venue.Id,
                AccountId = HttpContext.GetAccount()!.Id
            });

            db.SaveChanges();

            TempData["Message"] = "Venue created successfully";
            return RedirectToAction("Edit", new { id = venue.Id });
        }

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var venue = db.Venues.FirstOrDefault(v => v.Id == id && v.Id != 1);
        if (venue == null)
        {
            return NotFound();
        }

        var vm = new EditVenueVM
        {
            Id = venue.Id,
            Name = venue.Name
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditVenueVM vm)
    {
        var venue = db.Venues.FirstOrDefault(v => v.Id == vm.Id && v.Id != 1);
        if (venue == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid("Name") && !IsNameUnique(vm.Name, vm.Id))
        {
            ModelState.AddModelError("Name", "Name already exists.");
        }

        if (ModelState.IsValid)
        {
            venue.Name = vm.Name;

            db.AuditLogs.Add(new()
            {
                Action = "update",
                Entity = "venue",
                EntityId = venue.Id,
                AccountId = HttpContext.GetAccount()!.Id
            });

            db.SaveChanges();

            TempData["Message"] = "Venue updated successfully";
            return RedirectToAction("Edit", new { id = venue.Id });
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var venue = db.Venues.FirstOrDefault(v => v.Id == id && v.Id != 1);
        if (venue == null) return NotFound();

        clnSrv.CleanUp(venue);

        db.AuditLogs.Add(new()
        {
            Action = "delete",
            Entity = "venue",
            EntityId = venue.Id,
            AccountId = HttpContext.GetAccount()!.Id
        });
        db.SaveChanges();

        TempData["Message"] = "Venue deleted successfully";
        return Ok();
    }

    // ==========REMOTE==========
    public bool IsNameUnique(string name, int? id = null)
    {
        if (id != null)
        {
            return !db.Venues.Any(v => v.Id != id && v.Name == name);
        }

        return !db.Venues.Any(v => v.Name == name);
    }
}
