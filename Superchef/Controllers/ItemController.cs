using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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
        var item = db.Items
            .Include(i => i.Variants)
            .Include(i => i.Store)
                .ThenInclude(s => s.Venue)
            .Include(i => i.Reviews)
                .ThenInclude(r => r.Account)
            .Where(ExpressionService.ShowItemToCustomerExpr)
            .FirstOrDefault(i => i.Slug == slug);
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
        var variant = db.Variants
            .Where(ExpressionService.ShowVariantToCustomerExpr)
            .FirstOrDefault(v => v.Id == id);

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

        var variant = db.Variants
            .Where(ExpressionService.ShowVariantToCustomerExpr)
            .FirstOrDefault(v => v.Id == vm.Variant);
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
            { "Variants Count", a => a.Variants.Count },
            { "Category", a => a.Category.Name },
            { "Creation Date", a => a.CreatedAt }
        };
        ViewBag.Fields = sortOptions.Keys.ToList();


        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            Console.WriteLine("Sorting");
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

        var results = db.Items.Where(i => i.StoreId == store.Id && !i.IsDeleted).AsQueryable();

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
            results = results.Where(i => i.Variants.Count() >= vm.MinVariantsCount);
        }

        if (vm.MaxVariantsCount != null)
        {
            results = results.Where(i => i.Variants.Count() <= vm.MaxVariantsCount);
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
    [Authorize(Roles = "Vendor")]
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
