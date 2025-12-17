using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Superchef.Controllers;

[Authorize(Roles = "Admin")]
public class CustomerController : Controller
{
    public IActionResult Manage(ManageCustomerVM vm)
    {
        Dictionary<string, Expression<Func<Account, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Email", a => a.Email },
            { "Status", a => a.IsBanned ? "Banned" : a.DeletionAt != null ? "ToDelete" : a.LockoutEnd != null ? "Timeout" : "Active" },
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
            new() { Value = "email", Text = "Search By Email" },
            new() { Value = "id", Text = "Search By Id" }
        ];
        vm.AvailableStatuses = ["Active", "ToDelete", "Timeout", "Banned"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        return View(vm);
    }

    public IActionResult Edit()
    {
        var vm = new Account
        {
            Id = 1,
            Name = "abc",
            Email = "abc@gmail.com",
            AccountTypeId = 1,
            CreatedAt = DateTime.Now,
            IsBanned = false,
            DeletionAt = null,
            LockoutEnd = null,
            IsDeleted = false,
        };

        return View(vm);
    }
}
