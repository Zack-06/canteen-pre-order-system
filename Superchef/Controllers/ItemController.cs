using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

public class ItemController : Controller
{
    private readonly DB db;
    private readonly ImageService imgSrv;
    private readonly CleanupService clnSrv;

    public ItemController(DB db, ImageService imgSrv, CleanupService clnSrv)
    {
        this.db = db;
        this.imgSrv = imgSrv;
        this.clnSrv = clnSrv;
    }

    [HttpGet]
    [Route("Item/Info/{slug}")]
    public IActionResult Info(string slug, ItemInfoVM vm)
    {
        var item = db.Items
            .Include(i => i.Variants)
            .Include(i => i.Store)
                .ThenInclude(s => s.Venue)
            .Include(i => i.Reviews)
                .ThenInclude(r => r.Account)
            .FirstOrDefault(i => i.Slug == slug && i.IsActive);
        if (item == null)
        {
            return NotFound();
        }

        var acc = HttpContext.GetAccount();

        // reviews
        List<Review> reviews = [];
        foreach (var review in item.Reviews.OrderBy(r => r.CreatedAt).ToList())
        {
            if (acc?.Id == review.AccountId) continue;
            if (vm.FilterRating != "all" && review.Rating.ToString() != vm.FilterRating) continue;
            reviews.Add(review);
        }

        if (Request.IsAjax())
        {
            return PartialView("_Reviews", reviews);
        }

        // info
        vm.Item = item;
        vm.Reviews = reviews;

        // stats
        vm.TotalReviews = db.Reviews.Count(r => r.ItemId == item.Id);
        vm.AverageRating = db.Reviews.Where(r => r.ItemId == item.Id).Select(r => (decimal?)r.Rating).Average() ?? 0m;
        vm.TotalSold = db.OrderItems
            .Where(oi =>
                oi.Variant.ItemId == item.Id &&
                oi.Order.Status == "Completed"
            )
            .Sum(oi => oi.Quantity);

        // own review
        ViewBag.HasCommented = false;
        var ownReview = acc != null ? db.Reviews.FirstOrDefault(r => r.AccountId == acc.Id && r.ItemId == item.Id) : null;
        if (ownReview != null)
        {
            vm.NewReview = new()
            {
                Id = item.Id,
                Rating = ownReview.Rating,
                Comment = ownReview.Comment
            };
            ViewBag.HasCommented = true;
        }
        ViewBag.HasBought = acc != null && db.OrderItems.Any(oi => oi.Order.AccountId == acc.Id && oi.Variant.ItemId == item.Id && oi.Order.Status == "Completed");

        if (acc != null)
        {
            ViewBag.IsFavourite = db.Favourites.Any(f => f.AccountId == acc.Id && f.ItemId == item.Id);
        }

        return View(vm);
    }

    public IActionResult VariantStockCount(int id)
    {
        var variant = db.Variants.FirstOrDefault(v => v.Id == id &&v.IsActive);

        if (variant == null)
        {
            return NotFound();
        }

        return Json(new
        {
            stock = FormatService.ToStockCountFormat(variant.Stock)
        });
    }

    [Authorize]
    [HttpPost]
    public IActionResult AddToCart(AddToCartVM vm)
    {
        if (vm.Variant == null)
        {
            return BadRequest("Please select a variant");
        }

        var variant = db.Variants.FirstOrDefault(v => v.Id == vm.Variant && v.IsActive);
        if (variant == null)
        {
            return BadRequest("Variant does not exist");
        }

        if (vm.Quantity == null)
        {
            return BadRequest("Quantity is required");
        }

        if (vm.Quantity < 1)
        {
            return BadRequest("Quantity must be greater than 1");
        }

        if (vm.Quantity > 10)
        {
            return BadRequest("Quantity must be less than 10");
        }

        if (variant.Stock < vm.Quantity)
        {
            return BadRequest("Not enough stock");
        }

        // add to cart logic
        var cartItem = db.Carts.FirstOrDefault(ci =>
            ci.AccountId == HttpContext.GetAccount()!.Id &&
            ci.VariantId == variant.Id
        );
        if (cartItem == null)
        {
            db.Carts.Add(new Cart
            {
                AccountId = HttpContext.GetAccount()!.Id,
                VariantId = variant.Id,
                Quantity = (int)vm.Quantity
            });
        }
        else if (cartItem.Quantity + vm.Quantity > 10)
        {
            return BadRequest("You have reached the maximum quantity per variant");
        }
        else if (cartItem.Quantity + vm.Quantity > variant.Stock)
        {
            return BadRequest("Not enough stock");
        }
        else
        {
            cartItem.Quantity += (int)vm.Quantity;
        }
        db.SaveChanges();

        return Ok();
    }

    [Authorize]
    [HttpPost]
    public IActionResult ToggleFavourite(int id, bool isDelete)
    {
        var item = db.Items.FirstOrDefault(i => i.Id == id);
        if (item == null)
        {
            return NotFound();
        }

        var fav = db.Favourites.FirstOrDefault(f => f.AccountId == HttpContext.GetAccount()!.Id && f.ItemId == item.Id);
        if (isDelete)
        {
            if (fav != null)
            {
                db.Favourites.Remove(fav);
            }
        }
        else if (fav == null)
        {
            db.Favourites.Add(new Favourite
            {
                AccountId = HttpContext.GetAccount()!.Id,
                ItemId = item.Id
            });
        }
        else
        {
            db.Favourites.Remove(fav);
        }
        db.SaveChanges();

        return Content(fav == null ? "added" : "removed");
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Manage(ManageItemVM vm)
    {
        if (vm.Id == null)
        {
            var sessionStoreId = HttpContext.Session.GetInt32("StoreId");
            if (sessionStoreId == null)
            {
                TempData["Message"] = "Please choose a store first";
                return RedirectToAction("Vendor", "Home", new { ReturnUrl = Url.Action("Manage") });
            }

            return RedirectToAction("Manage", new { id = sessionStoreId });
        }

        var store = db.Stores.FirstOrDefault(s =>
            s.Id == vm.Id &&
            s.AccountId == HttpContext.GetAccount()!.Id &&
            !s.IsDeleted
        );
        if (store == null)
        {
            return NotFound();
        }

        Dictionary<string, Expression<Func<Item, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Slug", a => a.Slug },
            { "Status", a => a.IsActive ? "Active" : "Inactive" },
            { "Variants Count", a => a.Variants.Count(v => !v.IsDeleted) },
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
        vm.AvailableStatuses = ["All", "Active", "Inactive"];
        vm.AvailableCategories = db.Categories.ToList();

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }
        if (vm.Status == null || !vm.AvailableStatuses.Contains(vm.Status))
        {
            vm.Status = vm.AvailableStatuses.First();
        }

        var results = db.Items
            .Include(i => i.Category)
            .Include(i => i.Variants.Where(v => !v.IsDeleted))
            .Where(i => i.StoreId == store.Id && !i.IsDeleted)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(i => i.Name.Contains(search));
                    break;
                case "slug":
                    results = results.Where(i => i.Slug.Contains(search));
                    break;
                case "id":
                    results = results.Where(i => i.Id.ToString().Contains(search));
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

        if (vm.Categories.Count > 0)
        {
            results = results.Where(i => vm.Categories.Contains(i.CategoryId));
        }

        if (vm.MinVariantsCount != null)
        {
            results = results.Where(i => i.Variants.Count(v => !v.IsDeleted) >= vm.MinVariantsCount);
        }

        if (vm.MaxVariantsCount != null)
        {
            results = results.Where(i => i.Variants.Count(v => !v.IsDeleted) <= vm.MaxVariantsCount);
        }

        if (vm.CreationFrom != null)
        {
            results = results.Where(i => i.CreatedAt >= vm.CreationFrom);
        }

        if (vm.CreationTo != null)
        {
            results = results.Where(i => i.CreatedAt <= vm.CreationTo);
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

        ViewBag.StoreName = store.Name;

        return View(vm);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Add(int id)
    {
        var store = db.Stores
            .FirstOrDefault(s =>
                s.Id == id &&
                s.AccountId == HttpContext.GetAccount()!.Id &&
                !s.IsDeleted
            );
        if (store == null)
        {
            return NotFound();
        }

        var vm = new AddItemVM
        {
            Id = store.Id,
            AvailableCategories = db.Categories.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };

        ViewBag.StoreName = store.Name;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Vendor")]
    public IActionResult Add(AddItemVM vm)
    {
        var store = db.Stores
            .FirstOrDefault(s =>
                s.Id == vm.Id &&
                !s.IsDeleted
            );
        if (store == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid("Slug") && !IsSlugUnique(vm.Slug))
        {
            ModelState.AddModelError("Slug", "Slug has been taken.");
        }

        if (ModelState.IsValid("Category") && !CheckCategory(vm.Category))
        {
            ModelState.AddModelError("Category", "Category is invalid.");
        }

        if (ModelState.IsValid("Keywords") && !CheckKeywords(vm.Keywords))
        {
            ModelState.AddModelError("Keywords", "Some keywords are invalid.");
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
                newImageFile = imgSrv.SaveImage(vm.Image, "item", 500, 500, vm.ImageX, vm.ImageY, vm.ImageScale);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid && newImageFile != null)
        {
            var item = new Item
            {
                Name = vm.Name,
                Slug = vm.Slug,
                Description = vm.Description,
                CategoryId = vm.Category,
                Image = newImageFile,
                StoreId = vm.Id
            };

            foreach (var word in vm.Keywords)
            {
                var keyword = db.Keywords.FirstOrDefault(k => k.Word == word);
                if (keyword == null)
                {
                    keyword = new Keyword { Word = word };
                    db.Keywords.Add(keyword);
                }

                keyword.Items.Add(item);
            }

            db.Items.Add(item);
            db.SaveChanges();

            TempData["Message"] = "Item created successfully";
            return RedirectToAction("Edit", new { id = item.Id });
        }

        vm.AvailableCategories = db.Categories.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList();

        ViewBag.StoreName = store.Name;

        return View(vm);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Edit(int id)
    {
        var item = db.Items
            .Include(i => i.Keywords)
            .FirstOrDefault(i =>
                i.Id == id &&
                !i.IsDeleted &&
                i.Store.AccountId == HttpContext.GetAccount()!.Id
            );
        if (item == null)
        {
            return NotFound();
        }

        var vm = new EditItemVM
        {
            Id = id,
            CreatedAt = DateTime.Now,
            Name = item.Name,
            Slug = item.Slug,
            Description = item.Description,
            Category = item.CategoryId,
            Keywords = item.Keywords.Select(k => k.Word).ToList(),
            Active = item.IsActive,

            AvailableCategories = db.Categories.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };

        ViewBag.ImageUrl = item.Image;
        ViewBag.StoreId = item.StoreId;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Vendor")]
    public IActionResult Edit(EditItemVM vm)
    {
        var item = db.Items
            .Include(i => i.Variants.Where(v => !v.IsDeleted))
            .Include(i => i.Store)
            .Include(i => i.Keywords)
            .FirstOrDefault(i =>
                i.Id == vm.Id &&
                !i.IsDeleted &&
                i.Store.AccountId == HttpContext.GetAccount()!.Id
            );
        if (item == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid("Slug") && !IsSlugUnique(vm.Slug, vm.Id))
        {
            ModelState.AddModelError("Slug", "Slug has been taken.");
        }

        if (ModelState.IsValid("Category") && !CheckCategory(vm.Category))
        {
            ModelState.AddModelError("Category", "Category is invalid.");
        }

        if (ModelState.IsValid("Keywords") && !CheckKeywords(vm.Keywords))
        {
            ModelState.AddModelError("Keywords", "Some keywords are invalid.");
        }

        if (vm.Image != null)
        {
            var e = imgSrv.ValidateImage(vm.Image, 3);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        if (ModelState.IsValid("Active") && vm.Active)
        {
            if (item.Store.StripeAccountId == null)
            {
                ModelState.SetModelValue("Active", new ValueProviderResult("false"));
                ModelState.AddModelError("Active", "Item activation failed. The store has not connected a Stripe account yet.");
            }
            else if (!item.Store.HasPublishedFirstSlots)
            {
                ModelState.SetModelValue("Active", new ValueProviderResult("false"));
                ModelState.AddModelError("Active", "Item activation failed. The store has not published initial slots yet.");
            } else if (!item.Variants.Any(v => !v.IsDeleted))
            {
                ModelState.SetModelValue("Active", new ValueProviderResult("false"));
                ModelState.AddModelError("Active", "Item activation failed. The item has no variants.");
            }
        }

        if (ModelState.IsValid && vm.Image != null)
        {
            try
            {
                var newFile = imgSrv.SaveImage(vm.Image, "item", 500, 500, vm.ImageX, vm.ImageY, vm.ImageScale);

                // remove image
                if (item.Image != null) imgSrv.DeleteImage(item.Image, "item");
                item.Image = newFile;
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            item.Name = vm.Name.Trim();
            item.Slug = vm.Slug;
            item.Description = vm.Description.Trim();
            item.CategoryId = vm.Category;
            item.IsActive = vm.Active;

            item.Keywords.Clear();
            foreach (var word in vm.Keywords)
            {
                var keyword = db.Keywords.FirstOrDefault(k => k.Word == word);
                if (keyword == null)
                {
                    keyword = new Keyword { Word = word };
                    db.Keywords.Add(keyword);
                }

                keyword.Items.Add(item);
            }
            db.SaveChanges();

            clnSrv.CleanUpKeyword();

            if (item.IsActive == false)
            {
                foreach (var variant in item.Variants)
                {
                    variant.IsActive = false;
                }
                db.SaveChanges();
            }

            TempData["Message"] = "Item updated successfully";
            return RedirectToAction("Edit", new { id = item.Id });
        }

        ViewBag.ImageUrl = item.Image;
        ViewBag.StoreId = item.StoreId;

        vm.AvailableCategories = db.Categories.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList();

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Vendor")]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var item = db.Items.FirstOrDefault(i => 
            i.Id == id &&
            !i.IsDeleted &&
            i.Store.AccountId == HttpContext.GetAccount()!.Id
        );
        if (item == null) return NotFound();

        var error = clnSrv.CanCleanUp(item);
        if (error != null) return BadRequest(error);

        clnSrv.CleanUp(item);
        return Ok();
    }

    // ==========REMOTE==========
    public bool CheckCategory(int category)
    {
        return db.Categories.Any(c => c.Id == category);
    }

    public bool IsSlugUnique(string slug, int? id = null)
    {
        if (id == null)
        {
            return !db.Items.Any(i => i.Slug == slug && !i.IsDeleted);
        }

        return !db.Items.Any(i => i.Slug == slug && i.Id != id && !i.IsDeleted);
    }

    public bool CheckKeywords(List<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return true;
        }

        var existingKeywords = new List<string>();

        foreach (var keyword in keywords)
        {
            if (keyword.Length < 3 || keyword.Length > 30)
            {
                return false;
            }

            if (!Regex.IsMatch(keyword.ToLower(), @"^[a-z0-9-]+$"))
            {
                return false;
            }

            if (existingKeywords.Contains(keyword.ToLower()))
            {
                return false;
            }

            existingKeywords.Add(keyword.ToLower());
        }

        return true;
    }
}
