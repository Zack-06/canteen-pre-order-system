using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Superchef.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly DB db;
    private readonly SecurityService secSrv;
    private readonly EmailService emlSrv;
    private readonly CleanupService clnSrv;
    private readonly IHubContext<AccountHub> accHubCtx;
    public AdminController(DB db, SecurityService secSrv, EmailService emlSrv, CleanupService clnSrv, IHubContext<AccountHub> accHubCtx)
    {
        this.db = db;
        this.secSrv = secSrv;
        this.emlSrv = emlSrv;
        this.clnSrv = clnSrv;
        this.accHubCtx = accHubCtx;
    }

    public IActionResult Manage(ManageAdminVM vm)
    {
        Dictionary<string, Expression<Func<Account, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Email", a => a.Email },
            { "Status", a => a.LockoutEnd != null ? "Timeout" : "Active" },
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
        vm.AvailableStatuses = ["All", "Active", "Timeout"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }
        if (vm.Status == null || !vm.AvailableStatuses.Contains(vm.Status))
        {
            vm.Status = vm.AvailableStatuses.First();
        }

        var results = db.Accounts
            .Where(a => a.AccountType.Name == "Admin" && !a.IsDeleted)
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
        if (vm.Status != "All")
        {
            if (vm.Status == "Active")
            {
                results = results.Where(a => a.LockoutEnd == null);
            }
            else if (vm.Status == "Timeout")
            {
                results = results.Where(a => a.LockoutEnd != null);
            }
        }

        if (vm.CreationFrom != null)
        {
            results = results.Where(a => a.CreatedAt >= vm.CreationFrom);
        }

        if (vm.CreationTo != null)
        {
            results = results.Where(a => a.CreatedAt <= vm.CreationTo);
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
        return View(new AddAdminVM());
    }

    [HttpPost]
    public IActionResult Add(AddAdminVM vm)
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
                AccountTypeId = 3,
                PasswordHash = secSrv.HashPassword(password),
            };
            db.Accounts.Add(account);
            db.SaveChanges();

            emlSrv.SendAccountCreatedEmail(account, password, Url.Action("Login", "Auth", null, Request.Scheme, Request.Host.Value));

            TempData["Message"] = "Admin created successfully";
            return RedirectToAction("Edit", new { id = account.Id });
        }

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var admin = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Admin");
        if (admin == null)
        {
            return NotFound();
        }

        return View(admin);
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = db.Accounts.FirstOrDefault(a =>
            a.Id == id &&
            !a.IsDeleted &&
            a.AccountType.Name == "Admin" &&
            a.Email != "superchef.system@gmail.com"
        );
        if (account == null) return NotFound("Admin not found");

        var error = clnSrv.CanCleanUp(account);
        if (error != null) return BadRequest(error);

        clnSrv.CleanUp(account);

        TempData["Message"] = "Admin deleted successfully";
        return Ok();
    }

    [HttpPost]
    public IActionResult RemoveTimeout(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Admin");
        if (account == null)
        {
            return NotFound("Admin not found");
        }

        if (account.LockoutEnd == null)
        {
            return BadRequest("This admin doesn't have timeout");
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

        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Admin");
        if (account == null)
        {
            return NotFound("Admin not found");
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

    // ==========REMOTE==========
    public bool CheckEmailRegister(string email)
    {
        return !db.Accounts.Any(a => a.Email == email && !a.IsDeleted);
    }
}
