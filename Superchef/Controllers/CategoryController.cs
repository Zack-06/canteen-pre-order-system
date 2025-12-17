using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Superchef.Controllers;

[Authorize(Roles = "Admin")]
public class CategoryController : Controller
{
    private readonly DB db;
    private readonly ImageService imgSrv;
    private readonly CleanupService clnSrv;

    public CategoryController(DB db, ImageService imgSrv, CleanupService clnSrv)
    {
        this.db = db;
        this.imgSrv = imgSrv;
        this.clnSrv = clnSrv;
    }

    public IActionResult Manage(ManageCategoryVM vm)
    {
        Dictionary<string, Expression<Func<Category, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Items Count", a => a.Items.Count(i => !i.IsDeleted) }
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

        var results = db.Categories
            .Include(c => c.Items)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(c => c.Name.Contains(search));
                    break;
                case "id":
                    results = results.Where(c => c.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.MinItemsCount != null)
        {
            results = results.Where(c => c.Items.Count(i => !i.IsDeleted) >= vm.MinItemsCount);
        }

        if (vm.MaxItemsCount != null)
        {
            results = results.Where(c => c.Items.Count(i => !i.IsDeleted) <= vm.MaxItemsCount);
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
        return View(new AddCategoryVM());
    }

    [HttpPost]
    public IActionResult Add(AddCategoryVM vm)
    {
        if (ModelState.IsValid("Name") && !IsNameUnique(vm.Name))
        {
            ModelState.AddModelError("Name", "Name has been taken.");
        }

        if (vm.Image != null)
        {
            var e = imgSrv.ValidateImage(vm.Image, 2);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        string? newImageFile = null;
        if (ModelState.IsValid && vm.Image != null)
        {
            try
            {
                newImageFile = imgSrv.SaveImage(vm.Image, "category", 300, 300, vm.ImageX, vm.ImageY, vm.ImageScale);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid && newImageFile != null)
        {
            var category = new Category
            {
                Name = vm.Name,
                Image = newImageFile
            };

            db.Categories.Add(category);
            db.SaveChanges();

            TempData["Message"] = "Category created successfully";
            return RedirectToAction("Edit", new { id = category.Id });
        }

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var category = db.Categories.FirstOrDefault(c => c.Id == id && c.Id != 1);
        if (category == null)
        {
            return NotFound();
        }

        var vm = new EditCategoryVM
        {
            Id = category.Id,
            Name = category.Name,
        };

        ViewBag.ImageUrl = category.Image;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditCategoryVM vm)
    {
        var category = db.Categories.FirstOrDefault(c => c.Id == vm.Id && c.Id != 1);
        if (category == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid("Name") && !IsNameUnique(vm.Name, vm.Id))
        {
            ModelState.AddModelError("Name", "Name has been taken.");
        }

        if (vm.Image != null)
        {
            var e = imgSrv.ValidateImage(vm.Image, 2);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        if (ModelState.IsValid && vm.Image != null)
        {
            try
            {
                var newFile = imgSrv.SaveImage(vm.Image, "category", 300, 300, vm.ImageX, vm.ImageY, vm.ImageScale);

                // remove image
                if (category.Image != null) imgSrv.DeleteImage(category.Image, "category");
                category.Image = newFile;
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            category.Name = vm.Name.Trim();
            db.SaveChanges();

            TempData["Message"] = "Category updated successfully";
            return RedirectToAction("Edit", new { id = category.Id });
        }

        ViewBag.ImageUrl = category.Image;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var category = db.Categories.FirstOrDefault(c => c.Id == id && c.Id != 1);
        if (category == null) return NotFound();

        clnSrv.CleanUp(category);

        TempData["Message"] = "Category deleted successfully";
        return Ok();
    }

    // ==========REMOTE==========
    public bool IsNameUnique(string name, int? id = null)
    {
        if (id != null)
        {
            return !db.Categories.Any(c => c.Id != id && c.Name == name);
        }

        return !db.Categories.Any(c => c.Name == name);
    }
}
