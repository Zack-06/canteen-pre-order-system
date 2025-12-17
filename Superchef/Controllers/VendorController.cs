using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

public class VendorController : Controller
{
    private readonly DB db;
    private readonly SecurityService secSrv;
    private readonly EmailService emlSrv;
    private readonly IHubContext<AccountHub> accHubCtx;
    public VendorController(DB db, SecurityService secSrv, EmailService emlSrv, IHubContext<AccountHub> accHubCtx)
    {
        this.db = db;
        this.secSrv = secSrv;
        this.emlSrv = emlSrv;
        this.accHubCtx = accHubCtx;
    }

    public IActionResult Manage(ManageVendorVM vm)
    {
        Dictionary<string, Expression<Func<Account, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Email", a => a.Email },
            { "Status", a => a.IsBanned ? "Banned" : a.DeletionAt != null ? "To Delete" : a.LockoutEnd != null ? "Timeout" : "Active" },
            { "Stores Count", a => a.Stores.Count(s => !s.IsDeleted) },
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
        vm.AvailableStatuses = ["Active", "To Delete", "Timeout", "Banned"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Accounts
            .Include(a => a.Stores)
            .Where(a => a.AccountType.Name == "Vendor" && !a.IsDeleted)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(a => a.Name.Contains(search));
                    break;
                case "email":
                    results = results.Where(a => a.Email.Contains(search));
                    break;
                case "id":
                    results = results.Where(a => a.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.Statuses.Count > 0)
        {
            results = results.Where(a =>
                (vm.Statuses.Contains("Banned") && a.IsBanned) ||
                (vm.Statuses.Contains("To Delete") && a.DeletionAt != null) ||
                (vm.Statuses.Contains("Timeout") && a.LockoutEnd != null) ||
                (vm.Statuses.Contains("Active") &&
                    !a.IsBanned &&
                    a.DeletionAt == null &&
                    !a.IsDeleted &&
                    a.LockoutEnd == null)
            );
        }

        if (vm.CreationFrom != null)
        {
            results = results.Where(a => a.CreatedAt >= vm.CreationFrom);
        }

        if (vm.CreationTo != null)
        {
            results = results.Where(a => a.CreatedAt <= vm.CreationTo);
        }

        if (vm.MinStoresCount != null)
        {
            results = results.Where(a => a.Stores.Count(s => !s.IsDeleted) >= vm.MinStoresCount);
        }

        if (vm.MaxStoresCount != null)
        {
            results = results.Where(a => a.Stores.Count(s => !s.IsDeleted) <= vm.MaxStoresCount);
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
        return View(new AddVendorVM());
    }

    [HttpPost]
    public IActionResult Add(AddVendorVM vm)
    {
        if (ModelState.IsValid("Email") && !CheckEmailRegister(vm.Email))
        {
            ModelState.AddModelError("Email", "Email is not registered.");
        }

        if (ModelState.IsValid)
        {
            string password = GeneratorHelper.RandomPassword(15);

            var account = new Account
            {
                Name = vm.Name.Trim(),
                Email = vm.Email.Trim(),
                AccountTypeId = 2,
                PasswordHash = secSrv.HashPassword(password),
            };
            db.Accounts.Add(account);
            db.SaveChanges();

            emlSrv.SendAccountCreatedEmail(account, password, Url.Action("Login", "Auth", null, Request.Scheme, Request.Host.Value));

            TempData["Message"] = "Vendor created successfully";
            return RedirectToAction("Edit", new { id = account.Id });
        }

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var vendor = db.Accounts
            .Include(a => a.Stores)
            .FirstOrDefault(a =>
                a.Id == id &&
                !a.IsDeleted &&
                a.AccountType.Name == "Vendor"
            );
        if (vendor == null)
        {
            return NotFound();
        }

        return View(vendor);
    }

    [HttpPost]
    public async Task<IActionResult> Ban(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Vendor");
        if (account == null)
        {
            return NotFound("Vendor not found");
        }

        if (account.IsBanned)
        {
            return BadRequest("This vendor is already banned.");
        }

        account.IsBanned = true;
        var devices = db.Devices.Where(d => d.AccountId == account.Id).ToList();
        foreach (var device in devices)
        {
            db.Verifications.RemoveRange(db.Verifications.Where(v => v.DeviceId == device.Id));
            db.Devices.Remove(device);
        }
        db.SaveChanges();

        await accHubCtx.Clients.All.SendAsync("LogoutAll", account.Id);

        TempData["Message"] = "Banned successfully!";
        return Ok();
    }

    [HttpPost]
    public IActionResult Unban(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Vendor");
        if (account == null)
        {
            return NotFound("Vendor not found");
        }

        if (!account.IsBanned)
        {
            return BadRequest("This vendor isn't banned.");
        }

        account.IsBanned = false;
        db.SaveChanges();

        TempData["Message"] = "Unbanned successfully!";
        return Ok();
    }

    [HttpPost]
    public IActionResult RevokeDeletion(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Vendor");
        if (account == null)
        {
            return NotFound("Vendor not found");
        }

        if (account.DeletionAt == null)
        {
            return BadRequest("This vendor isn't scheduled for deletion.");
        }

        account.DeletionAt = null;
        db.SaveChanges();

        TempData["Message"] = "Revoke deletion successfully!";
        return Ok();
    }

    [HttpPost]
    public IActionResult RemoveTimeout(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Vendor");
        if (account == null)
        {
            return NotFound("Vendor not found");
        }

        if (account.LockoutEnd == null)
        {
            return BadRequest("This vendor doesn't have timeout.");
        }

        account.LockoutEnd = null;
        db.SaveChanges();

        TempData["Message"] = "Remove timeout successfully!";
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> LogoutAllDevices(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Vendor");
        if (account == null)
        {
            return NotFound("Vendor not found");
        }

        var devices = db.Devices.Where(d => d.AccountId == account.Id).ToList();
        foreach (var device in devices)
        {
            db.Verifications.RemoveRange(db.Verifications.Where(v => v.DeviceId == device.Id));
            db.Devices.Remove(device);
        }
        db.SaveChanges();

        await accHubCtx.Clients.All.SendAsync("LogoutAll", account.Id);

        TempData["Message"] = "Logged out all known devices successfully";
        return Ok();
    }

    // ==========Remote==========
    public bool CheckEmailRegister(string email)
    {
        return !db.Accounts.Any(a => a.Email == email && !a.IsDeleted);
    }
}
