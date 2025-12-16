using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

public class SearchController : Controller
{
    private readonly DB db;
    public SearchController(DB db)
    {
        this.db = db;
    }

    public IActionResult Index(SearchVM vm)
    {
        ViewBag.SearchQuery = vm.Query;

        if (string.IsNullOrEmpty(vm.Query) && vm.Categories.Count == 0)
        {
            if (Request.IsAjax())
            {
                Response.Headers.Append("X-Redirect-Url", Url.Action("Index", "Home"));
                return new EmptyResult();
            }

            return RedirectToAction("Index", "Home");
        }

        vm.AvailableTypes = ["Items", "Stores"];
        vm.AvailableVenues = db.Venues.ToList();
        vm.AvailableCategories = db.Categories.ToList();
        vm.AvailablePrices = new()
        {
            ["all"] = "All Prices",
            ["below-3"] = "Below RM 3",
            ["3-10"] = "RM 3 - RM 10",
            ["10-20"] = "RM 10 - RM 20",
            ["above-20"] = "Above RM 20"
        };
        vm.AvailableRatings = new()
        {
            ["all"] = "All Ratings",
            ["1"] = "1 ⭐ & above",
            ["2"] = "2 ⭐ & above",
            ["3"] = "3 ⭐ & above",
            ["4"] = "4 ⭐ & above",
            ["5"] = "5 ⭐ only"
        };
        vm.AvailableStoreSortBy = new()
        {
            ["relevance"] = "Relevance",
            ["rating"] = "Rating",
            ["newest"] = "Newest Arrivals",
            ["sales"] = "Best Selling"
        };
        vm.AvailableItemSortBy = new()
        {
            ["relevance"] = "Relevance",
            ["price-asc"] = "Price: Low to High",
            ["price-desc"] = "Price: High to Low",
            ["rating"] = "Rating",
            ["newest"] = "Newest Arrivals",
            ["sales"] = "Best Selling"
        };

        if (vm.Type == null || !vm.AvailableTypes.Contains(vm.Type))
        {
            vm.Type = vm.AvailableTypes.First();
        }
        if (vm.Rating == null || !vm.AvailableRatings.ContainsKey(vm.Rating))
        {
            vm.Rating = "all";
        }
        vm.Query = vm.Query?.Trim() ?? "";

        if (vm.Type == "Stores")
        {
            if (vm.Sort == null || !vm.AvailableStoreSortBy.ContainsKey(vm.Sort))
            {
                vm.Sort = "relevance";
            }

            var results = db.Stores
                .Include(s => s.Items)
                    .ThenInclude(i => i.Reviews)
                .Where(s =>
                    !s.IsDeleted &&
                    s.Name.ToLower().Contains(vm.Query.ToLower()) ||
                    s.Description.ToLower().Contains(vm.Query.ToLower())
                )
                .AsQueryable();

            // filter venues
            if (vm.Venues.Count > 0)
            {
                results = results.Where(s => vm.Venues.Contains(s.VenueId));
            }

            // filter average rating
            if (vm.Rating != "all")
            {
                results = results.Where(s =>
                    s.Items.SelectMany(i => i.Reviews).Any() &&
                    s.Items.SelectMany(i => i.Reviews).Average(r => r.Rating) >= int.Parse(vm.Rating)
                );
            }

            // sorting
            switch (vm.Sort)
            {
                case "newest":
                    results = results.OrderByDescending(s => s.CreatedAt);
                    break;
                case "rating":
                    results = results.OrderByDescending(s =>
                        s.Items.SelectMany(i => i.Reviews).Average(r => (decimal?)r.Rating)
                        ?? 0m
                    );
                    break;
                case "sales":
                    results = results.OrderByDescending(s =>
                        s.Items
                            .SelectMany(i => i.Variants)
                                .SelectMany(v => v.OrderItems)
                            .Sum(oi => (int?)oi.Quantity)
                        ?? 0
                    );
                    break;
                default:
                    results = results
                        .OrderByDescending(s => s.Name.ToLower() == vm.Query.ToLower())
                        .ThenByDescending(s => s.Name.ToLower().Contains(vm.Query.ToLower()))
                        .ThenByDescending(s => s.Description.ToLower() == vm.Query.ToLower())
                        .ThenByDescending(s => s.Description.ToLower().Contains(vm.Query.ToLower()))
                        .ThenBy(s => s.Name); // fallback alphabetical
                    break;
            }

            vm.Results = results.ToPagedList(vm.Page, 30);
        }
        else
        {
            if (vm.Sort == null || !vm.AvailableItemSortBy.ContainsKey(vm.Sort))
            {
                vm.Sort = "relevance";
            }
            if (vm.Price == null || !vm.AvailablePrices.ContainsKey(vm.Price))
            {
                vm.Price = "all";
            }

            var results = db.Items
                .Include(i => i.Reviews)
                .Include(i => i.Variants)
                    .ThenInclude(v => v.OrderItems)
                .Where(i =>
                    i.IsActive &&
                    i.Name.ToLower().Contains(vm.Query.ToLower()) ||
                    i.Description.ToLower().Contains(vm.Query.ToLower())
                )
                .AsQueryable();

            // filter categories
            if (vm.Categories.Count > 0)
            {
                results = results.Where(i => vm.Categories.Contains(i.CategoryId));
            }

            // fitler venues
            if (vm.Venues.Count > 0)
            {
                results = results.Where(i => vm.Venues.Contains(i.Store.VenueId));
            }

            // filter average rating
            if (vm.Rating != "all")
            {
                results = results.Where(i =>
                    i.Reviews.Any() &&
                    i.Reviews.Average(r => r.Rating) >= int.Parse(vm.Rating)
                );
            }

            // filter price
            if (vm.Price != "all")
            {
                results = results.Where(i =>
                    i.Variants.Min(v => v.Price) >= int.Parse(vm.Price)
                );
            }

            // sorting
            switch (vm.Sort)
            {
                case "newest":
                    results = results.OrderByDescending(s => s.CreatedAt);
                    break;
                case "rating":
                    results = results.OrderByDescending(i =>
                        i.Reviews.Average(r => (decimal?)r.Rating)
                        ?? 0m
                    );
                    break;
                case "sales":
                    results = results.OrderByDescending(i =>
                       i.Variants.SelectMany(v => v.OrderItems).Sum(oi => (int?)oi.Quantity)
                        ?? 0
                    );
                    break;
                case "price-asc":
                    results = results.OrderBy(i => i.Variants.Min(v => v.Price));
                    break;
                case "price-desc":
                    results = results.OrderByDescending(i => i.Variants.Min(v => v.Price));
                    break;
                default:
                    results = results
                        .OrderByDescending(s => s.Keywords.Count(k => k.Word.ToLower() == vm.Query.ToLower())) // count exact matches of keyword
                        .ThenByDescending(s => s.Keywords.Count(k => k.Word.ToLower().Contains(vm.Query.ToLower()))) // count keyword contains
                        .ThenByDescending(s => s.Name.ToLower() == vm.Query.ToLower())
                        .ThenByDescending(s => s.Name.ToLower().Contains(vm.Query.ToLower()))
                        .ThenByDescending(s => s.Description.ToLower() == vm.Query.ToLower())
                        .ThenByDescending(s => s.Description.ToLower().Contains(vm.Query.ToLower()))
                        .ThenBy(s => s.Name); // fallback alphabetical
                    break;
            }

            vm.Results = results.ToPagedList(vm.Page, 30);
        }

        if (Request.IsAjax())
        {
            return PartialView("_SearchResults", vm);
        }

        return View(vm);
    }
}
