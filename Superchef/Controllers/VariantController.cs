using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

[Authorize(Roles = "Vendor,Admin")]
public class VariantController : Controller
{
    private readonly DB db;
    private readonly ImageService imgSrv;
    private readonly CleanupService clnSrv;
    private readonly InactiveService inaSrv;

    public VariantController(DB db, ImageService imgSrv, CleanupService clnSrv, InactiveService inaSrv)
    {
        this.db = db;
        this.imgSrv = imgSrv;
        this.clnSrv = clnSrv;
        this.inaSrv = inaSrv;
    }

    public IActionResult Manage(ManageVariantVM vm)
    {
        var item = db.Items
            .Include(i => i.Store)
            .FirstOrDefault(i =>
                i.Id == vm.Id &&
                !i.IsDeleted
            );
        if (item == null)
        {
            return NotFound();
        }

        var acc = HttpContext.GetAccount()!;
        if (acc.AccountType.Name == "Vendor" && item.Store.AccountId != acc.Id)
        {
            return Unauthorized("You are not authorized to access this item");
        }

        Dictionary<string, Expression<Func<Variant, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Price", a => a.Price },
            { "Status", a => a.IsActive ? "Active" : "Inactive" },
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
        vm.AvailableStatuses = ["All", "Active", "Inactive"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }
        if (vm.Status == null || !vm.AvailableStatuses.Contains(vm.Status))
        {
            vm.Status = vm.AvailableStatuses.First();
        }

        var results = db.Variants
            .Where(v => v.ItemId == vm.Id && !v.IsDeleted)
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
        if (vm.Status != "All")
        {
            if (vm.Status == "Active")
            {
                results = results.Where(i => i.IsActive);
            }
            else if (vm.Status == "Inactive")
            {
                results = results.Where(i => !i.IsActive);
            }
        }

        if (vm.MinPrice != null)
        {
            results = results.Where(v => v.Price >= vm.MinPrice);
        }

        if (vm.MaxPrice != null)
        {
            results = results.Where(v => v.Price <= vm.MaxPrice);
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

        ViewBag.ItemName = item.Name;

        return View(vm);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Add(int id)
    {
        var item = db.Items.FirstOrDefault(i =>
            i.Id == id &&
            !i.IsDeleted &&
            i.Store.AccountId == HttpContext.GetAccount()!.Id
        );
        if (item == null)
        {
            return NotFound();
        }

        var vm = new AddVariantVM
        {
            Id = id
        };

        ViewBag.ItemName = item.Name;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Vendor")]
    public IActionResult Add(AddVariantVM vm)
    {
        var item = db.Items.FirstOrDefault(i =>
            i.Id == vm.Id &&
            !i.IsDeleted &&
            i.Store.AccountId == HttpContext.GetAccount()!.Id
        );
        if (item == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid("Name") && !IsNameUniqueWhenCreate(vm.Name, vm.Id))
        {
            ModelState.AddModelError("Name", "Name has been taken in this item.");
        }

        if (vm.Image != null)
        {
            var e = imgSrv.ValidateImage(vm.Image, 3);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        string? newImageFile = null;
        if (ModelState.IsValid && vm.Image != null)
        {
            try
            {
                newImageFile = imgSrv.SaveImage(vm.Image, "variant", 500, 500, vm.ImageX, vm.ImageY, vm.ImageScale);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid && newImageFile != null)
        {
            var variant = new Variant
            {
                Name = vm.Name,
                ItemId = vm.Id,
                Price = vm.Price,
                Stock = vm.StockCount,
                Image = newImageFile
            };

            db.Variants.Add(variant);
            db.SaveChanges();

            TempData["Message"] = "Variant created successfully";
            return RedirectToAction("Edit", new { id = variant.Id });
        }

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var variant = db.Variants
            .Include(v => v.Item)
                .ThenInclude(i => i.Store)
            .FirstOrDefault(v =>
                v.Id == id &&
                !v.IsDeleted
            );
        if (variant == null)
        {
            return NotFound();
        }

        var acc = HttpContext.GetAccount()!;
        if (acc.AccountType.Name == "Vendor" && variant.Item.Store.AccountId != acc.Id)
        {
            return Unauthorized("You are not authorized to access this item");
        }

        var vm = new EditVariantVM
        {
            Id = variant.Id,
            ItemId = variant.ItemId,
            CreatedAt = variant.CreatedAt,
            Name = variant.Name,
            Price = variant.Price,
            StockCount = variant.Stock,
            Active = variant.IsActive,
        };

        ViewBag.ItemName = variant.Item.Name;
        ViewBag.ImageUrl = variant.Image;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditVariantVM vm)
    {
        var variant = db.Variants
            .Include(v => v.Item)
                .ThenInclude(i => i.Store)
            .FirstOrDefault(v =>
                v.Id == vm.Id &&
                !v.IsDeleted
            );
        if (variant == null)
        {
            return NotFound();
        }

        var acc = HttpContext.GetAccount()!;
        if (acc.AccountType.Name == "Vendor" && variant.Item.Store.AccountId != acc.Id)
        {
            return Unauthorized("You are not authorized to access this item");
        }

        if (ModelState.IsValid("Name") && !IsNameUniqueWhenEdit(vm.Name, vm.Id, vm.ItemId))
        {
            ModelState.AddModelError("Name", "Name has been taken in this item.");
        }

        if (vm.Image != null)
        {
            var e = imgSrv.ValidateImage(vm.Image, 3);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        if (ModelState.IsValid("Active") && vm.Active)
        {
            if (!variant.Item.IsActive)
            {
                ModelState.SetModelValue("Active", new ValueProviderResult("false"));
                ModelState.AddModelError("Active", "Variant activation failed. The item is inactive.");
            }
        }

        if (ModelState.IsValid && vm.Image != null)
        {
            try
            {
                var newFile = imgSrv.SaveImage(vm.Image, "variant", 500, 500, vm.ImageX, vm.ImageY, vm.ImageScale);

                // remove image
                if (variant.Image != null) imgSrv.DeleteImage(variant.Image, "variant");
                variant.Image = newFile;
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            variant.Name = vm.Name.Trim();
            variant.Price = vm.Price;
            variant.Stock = vm.StockCount;
            variant.IsActive = vm.Active;

            if (acc.AccountType.Name == "Admin")
            {
                db.AuditLogs.Add(new()
                {
                    Action = "update",
                    Entity = "variant",
                    EntityId = variant.Id,
                    AccountId = HttpContext.GetAccount()!.Id
                });
            }

            db.SaveChanges();

            if (variant.IsActive == false)
            {
                inaSrv.SetInactive(variant);
            }

            TempData["Message"] = "Variant updated successfully";
            return RedirectToAction("Edit", new { id = variant.Id });
        }

        ViewBag.ItemName = variant.Item.Name;
        ViewBag.ImageUrl = variant.Image;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var variant = db.Variants
            .Include(v => v.Item)
                .ThenInclude(i => i.Store)
            .FirstOrDefault(v =>
                v.Id == id &&
                !v.IsDeleted
            );
        if (variant == null) return NotFound("Variant not found");

        var acc = HttpContext.GetAccount()!;
        if (acc.AccountType.Name == "Vendor" && variant.Item.Store.AccountId != acc.Id)
        {
            return Unauthorized("You are not authorized to access this item");
        }

        var error = clnSrv.CanCleanUp(variant);
        if (error != null) return BadRequest(error);

        clnSrv.CleanUp(variant);

        if (acc.AccountType.Name == "Admin")
        {
            db.AuditLogs.Add(new()
            {
                Action = "delete",
                Entity = "variant",
                EntityId = variant.Id,
                AccountId = HttpContext.GetAccount()!.Id
            });
            db.SaveChanges();
        }

        TempData["Message"] = "Variant deleted successfully";
        return Ok();
    }

    // ==========Remote==========
    public bool IsNameUniqueWhenCreate(string name, int id)
    {
        return !db.Variants.Any(v => v.Name == name && v.ItemId == id && !v.IsDeleted);
    }

    public bool IsNameUniqueWhenEdit(string name, int id, int itemId)
    {
        return !db.Variants.Any(v => v.Name == name && v.Id != id && v.ItemId == itemId && !v.IsDeleted);
    }
}
