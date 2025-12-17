using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

namespace Superchef.Controllers;

public class OrderController : Controller
{
    private readonly DB db;
    private readonly SystemOrderService sysOrderSrv;
    private readonly IHubContext<OrderHub> orderHubContext;

    public OrderController(DB db, SystemOrderService sysOrderSrv, IHubContext<OrderHub> orderHubContext)
    {
        this.db = db;
        this.sysOrderSrv = sysOrderSrv;
        this.orderHubContext = orderHubContext;
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Customer(string id)
    {
        var order = db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefault(o =>
                o.Id == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.Status == "Pending"
            );
        if (order == null)
        {
            return NotFound();
        }

        var vm = new OrderCustomerVM
        {
            Id = id,
            Name = order.Name,
            ContactNumber = order.PhoneNumber
        };

        ViewBag.TotalPrice = order.OrderItems.Sum(i => (decimal?)i.Price * i.Quantity) ?? 0m;
        ViewBag.TotalItems = order.OrderItems.Sum(i => (int?)i.Quantity) ?? 0;

        if (order.ExpiresAt != null)
        {
            ViewBag.ExpiredTimestamp = ViewBag.ExpiredTimestamp = new DateTimeOffset(order.ExpiresAt.Value).ToUnixTimeMilliseconds();
        }

        return View(vm);
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    public IActionResult Customer(OrderCustomerVM vm)
    {
        var order = db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefault(o =>
                o.Id == vm.Id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.Status == "Pending"
            );
        if (order == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            order.Name = vm.Name.Trim();
            order.PhoneNumber = vm.ContactNumber;
            db.Orders.Update(order);
            db.SaveChanges();

            return RedirectToAction("Slot", new { id = order.Id });
        }

        ViewBag.TotalPrice = order.OrderItems.Sum(i => (decimal?)i.Price * i.Quantity) ?? 0m;
        ViewBag.TotalItems = order.OrderItems.Sum(i => (int?)i.Quantity) ?? 0;

        if (order.ExpiresAt != null)
        {
            ViewBag.ExpiredTimestamp = ViewBag.ExpiredTimestamp = new DateTimeOffset(order.ExpiresAt.Value).ToUnixTimeMilliseconds();
        }

        return View(vm);
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Slot(OrderSlotVM vm)
    {
        var order = db.Orders
            .Include(o => o.Slot)
            .Include(o => o.OrderItems)
            .FirstOrDefault(o =>
                o.Id == vm.Id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.Status == "Pending"
            );
        if (order == null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(order.PhoneNumber))
        {
            return RedirectToAction("Customer", new { id = order.Id });
        }

        var tmpNow = DateTime.Now;
        var startOrderTime = tmpNow.AddMinutes(8 + 30);
        vm.AvailableDates = [
            DateOnly.FromDateTime(tmpNow),
            DateOnly.FromDateTime(tmpNow.AddDays(1)),
            DateOnly.FromDateTime(tmpNow.AddDays(2))
        ];

        if (vm.Date == null || !vm.AvailableDates.Contains(vm.Date.Value))
        {
            vm.Date = DateOnly.FromDateTime(order.Slot.StartTime);
        }

        foreach (var avSlot in db.SlotTemplates.Where(s => s.DayOfWeek == (int)vm.Date.Value.DayOfWeek))
        {
            var slot = new DateTime(vm.Date.Value.Year, vm.Date.Value.Month, vm.Date.Value.Day, avSlot.StartTime.Hour, avSlot.StartTime.Minute, 0);
            if (slot > tmpNow.AddMinutes(8 + 30))
            {
                vm.AvailableSlots.Add(avSlot.StartTime);
            }
        }

        vm.EnabledSlots = db.Slots
             .Where(s =>
                 s.StoreId == order.StoreId &&
                 DateOnly.FromDateTime(s.StartTime) == vm.Date &&
                 s.MaxOrders > s.Orders.Count
             )
             .Select(s => TimeOnly.FromDateTime(s.StartTime))
             .ToList();

        vm.Slot = null;
        if (vm.Date == DateOnly.FromDateTime(order.Slot.StartTime))
        {
            vm.Slot = TimeOnly.FromDateTime(order.Slot.StartTime);
        }

        if (Request.IsAjax())
        {
            return PartialView("_Slot", vm);
        }

        ViewBag.TotalPrice = order.OrderItems.Sum(i => (decimal?)i.Price * i.Quantity) ?? 0m;
        ViewBag.TotalItems = order.OrderItems.Sum(i => (int?)i.Quantity) ?? 0;

        if (order.ExpiresAt != null)
        {
            ViewBag.ExpiredTimestamp = ViewBag.ExpiredTimestamp = new DateTimeOffset(order.ExpiresAt.Value).ToUnixTimeMilliseconds();
        }

        return View(vm);
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    public async Task<IActionResult> SelectSlot(OrderSlotVM vm)
    {
        var order = db.Orders
            .Include(o => o.Slot)
            .Include(o => o.OrderItems)
            .FirstOrDefault(o =>
                o.Id == vm.Id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.Status == "Pending"
            );
        if (order == null)
        {
            return NotFound("Order not found");
        }

        var tmpNow = DateTime.Now;
        vm.AvailableDates = [
            DateOnly.FromDateTime(tmpNow),
            DateOnly.FromDateTime(tmpNow.AddDays(1)),
            DateOnly.FromDateTime(tmpNow.AddDays(2))
        ];

        if (vm.Date == null || !vm.AvailableDates.Contains(vm.Date.Value))
        {
            return BadRequest("Date invalid");
        }

        if (vm.Slot == null)
        {
            return BadRequest("Slot invalid");
        }

        if (DateOnly.FromDateTime(order.Slot.StartTime) == vm.Date && TimeOnly.FromDateTime(order.Slot.StartTime) == vm.Slot)
        {
            return Ok();
        }

        var startOrderTime = tmpNow.AddMinutes(8 + 30);
        var endOrderDate = tmpNow.AddDays(2);

        // Get latest slot start time for that day
        var templates = db.SlotTemplates
            .Where(s => s.DayOfWeek == (int)endOrderDate.DayOfWeek)
            .Select(s => s.StartTime)
            .ToList();

        var endOrderTimeSpan = templates.Count != 0 ? templates.Max() : TimeOnly.MinValue;

        var endOrderDateTime = new DateTime(
            endOrderDate.Year, endOrderDate.Month, endOrderDate.Day,
            endOrderTimeSpan.Hour, endOrderTimeSpan.Minute, 0
        );

        var availableSlots = db.Slots
            .Where(s =>
                s.StoreId == order.StoreId &&
                s.MaxOrders > s.Orders.Count &&
                s.StartTime >= startOrderTime &&
                s.StartTime <= endOrderDateTime
            )
            .ToList();

        foreach (var slot in availableSlots)
        {
            if (DateOnly.FromDateTime(slot.StartTime) == vm.Date && TimeOnly.FromDateTime(slot.StartTime) == vm.Slot)
            {
                var previousSlotId = order.SlotId;
                order.SlotId = slot.Id;
                db.SaveChanges();

                var currentSlot = db.Slots.Include(s => s.Orders).FirstOrDefault(s => s.Id == slot.Id)!;
                var previousSlot = db.Slots.Include(s => s.Orders).FirstOrDefault(s => s.Id == previousSlotId)!;

                await orderHubContext.Clients.All.SendAsync(
                    "SlotActive",
                    DateOnly.FromDateTime(currentSlot.StartTime),
                    TimeOnly.FromDateTime(currentSlot.StartTime).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture),
                    currentSlot.MaxOrders > currentSlot.Orders.Count
                );
                await orderHubContext.Clients.All.SendAsync(
                    "SlotActive",
                    DateOnly.FromDateTime(previousSlot.StartTime),
                    TimeOnly.FromDateTime(previousSlot.StartTime).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture),
                    previousSlot.MaxOrders > previousSlot.Orders.Count
                );

                return Ok();
            }
        }

        return BadRequest("Slot not available");
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Confirmation(string id)
    {
        var order = db.Orders
            .Include(o => o.Slot)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
                    .ThenInclude(v => v.Item)
            .Include(o => o.Store)
                .ThenInclude(s => s.Venue)
            .FirstOrDefault(o =>
                o.Id == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.Status == "Pending"
            );
        if (order == null)
        {
            return NotFound();
        }

        if (order.PhoneNumber == null)
        {
            return RedirectToAction("Customer", new { id = order.Id });
        }

        if (Request.Method == "GET")
        {
            ViewBag.TotalPrice = order.OrderItems.Sum(i => (decimal?)i.Price * i.Quantity) ?? 0m;
            ViewBag.TotalItems = order.OrderItems.Sum(i => (int?)i.Quantity) ?? 0;

            if (order.ExpiresAt != null)
            {
                ViewBag.ExpiredTimestamp = ViewBag.ExpiredTimestamp = new DateTimeOffset(order.ExpiresAt.Value).ToUnixTimeMilliseconds();
            }

            return View(order);
        }

        var baseUrl = Request.GetBaseUrl();
        var options = new SessionCreateOptions
        {
            SuccessUrl = baseUrl + "/Order/Success",
            CancelUrl = baseUrl + "/Order/Failed",
            LineItems = [],
            Mode = "payment",
            PaymentMethodTypes = ["card", "fpx", "grabpay"],
            CustomerEmail = HttpContext.GetAccount()!.Email,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["OrderId"] = order.Id.ToString()
                }
            }
        };

        // Fill line items
        foreach (var item in order.OrderItems)
        {
            options.LineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    UnitAmount = (long)(item.Price * 100),
                    Currency = "myr",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = item.Variant.Name,
                    }
                },
                Quantity = item.Quantity
            });
        }

        var stripeSession = new SessionService().Create(options);
        return Redirect(stripeSession.Url);
    }

    public IActionResult Success()
    {
        return View("Status", "success");
    }

    public IActionResult Failed()
    {
        return View("Status", "failed");
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Info(string id)
    {
        var order = db.Orders
            .Include(o => o.Payment)
            .Include(o => o.Slot)
            .Include(o => o.Store)
                .ThenInclude(s => s.Venue)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
                    .ThenInclude(v => v.Item)
            .FirstOrDefault(o =>
                o.Id == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.Status != "Pending"
            );
        if (order == null)
        {
            return NotFound();
        }

        // show order info
        return View(order);
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Manage(ManageOrderVM vm)
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

        Dictionary<string, Expression<Func<Order, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Customer Name", a => a.Name },
            { "Customer Id", a => a.AccountId },
            { "Status", a => a.Status },
            { "Pickup At", a => a.Slot.StartTime },
            { "Created At", a => a.CreatedAt }
        };
        ViewBag.Fields = sortOptions.Keys.ToList();


        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.Last();
            vm.Dir = "desc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "id", Text = "Search By Id" },
            new() { Value = "customer_name", Text = "Search By Customer Name" },
            new() { Value = "customer_id", Text = "Search By Customer Id" },
        ];
        vm.AvailableStatuses = ["Confirmed", "Preparing", "To Pickup", "Completed", "Cancelled"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Orders
            .Include(o => o.Slot)
            .Where(o => o.StoreId == store.Id && o.Status != "Pending")
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "id":
                    results = results.Where(o => o.Id.ToString().Contains(search));
                    break;
                case "customer_name":
                    results = results.Where(o => o.Name.Contains(search));
                    break;
                case "customer_id":
                    results = results.Where(o => o.AccountId.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.Statuses.Count > 0)
        {
            results = results.Where(o => vm.Statuses.Contains(o.Status));
        }

        if (vm.CreationFrom != null)
        {
            results = results.Where(o => o.CreatedAt >= vm.CreationFrom);
        }

        if (vm.CreationTo != null)
        {
            results = results.Where(o => o.CreatedAt <= vm.CreationTo);
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
    public IActionResult Edit(string id)
    {
        var order = db.Orders
            .Include(o => o.Payment)
            .Include(o => o.Slot)
            .Include(o => o.Store)
                .ThenInclude(s => s.Venue)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
                    .ThenInclude(v => v.Item)
            .FirstOrDefault(o =>
                o.Id == id &&
                o.Store.AccountId == HttpContext.GetAccount()!.Id &&
                o.Status != "Pending"
            );

        if (order == null)
        {
            return NotFound("Order not found");
        }

        return View(order);
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    public IActionResult Reorder(string id)
    {
        var order = db.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
            .FirstOrDefault(o =>
                o.Id == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.Status != "Pending"
            );

        if (order == null)
        {
            return NotFound("Order not found");
        }

        var acc = db.Accounts
            .Include(a => a.Carts)
            .FirstOrDefault(a => a.Id == order.AccountId);

        var variantIds = order.OrderItems.Select(oi => oi.VariantId).ToList();
        db.Carts.RemoveRange(db.Carts.Where(c =>
            c.AccountId == order.AccountId &&
            variantIds.Contains(c.VariantId)
        ));

        List<Cart> newItems = [];
        foreach (var item in order.OrderItems)
        {
            if (!item.Variant.IsActive) continue;

            newItems.Add(new Cart
            {
                AccountId = order.AccountId,
                VariantId = item.VariantId,
                Quantity = item.Quantity
            });
        }

        db.Carts.AddRange(newItems);
        db.SaveChanges();

        if (newItems.Count == 0)
        {
            return BadRequest("Reorder failed due to all items in the order are no longer available.");
        }

        if (newItems.Count < order.OrderItems.Count)
        {
            TempData["Message"] = "Some items in the order are no longer available.";
        }

        return Ok(order.StoreId);
    }

    [Authorize(Roles = "Customer,Vendor")]
    public async Task<IActionResult> Cancel(string id)
    {
        var order = db.Orders
            .Include(o => o.Store)
            .FirstOrDefault(o =>
                o.Id == id &&
                (o.Status == "Pending" || o.Status == "Confirmed")
            );

        if (order == null)
        {
            return NotFound("Order not found");
        }

        var acc = HttpContext.GetAccount()!;

        if (acc.AccountType.Name == "Customer")
        {
            if (order.AccountId != acc.Id)
            {
                return Unauthorized("You are not authorized to cancel this order");
            }
        }
        else if (acc.AccountType.Name == "Vendor")
        {
            if (order.Status == "Pending")
            {
                return Unauthorized("You are not authorized to cancel this order");
            }

            if (order.Store.AccountId != acc.Id)
            {
                return Unauthorized("You are not authorized to cancel this order");
            }
        } else
        {
            if (order.Status == "Pending")
            {
                return Unauthorized("You are not authorized to cancel this order");
            }
        }

        await sysOrderSrv.CancelOrder(order);
        return Ok();
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult MarkReady(string id)
    {
        var order = db.Orders
            .FirstOrDefault(o =>
                o.Id == id &&
                o.Store.AccountId == HttpContext.GetAccount()!.Id
            );

        if (order == null)
        {
            return NotFound("Order not found");
        }

        if (order.Status != "Preparing")
        {
            return BadRequest("Order unable to be marked as ready");
        }
        
        order.Status = "To Pickup";
        db.SaveChanges();

        // todo: notify customer

        return Ok();
    }

    [Authorize(Roles = "Vendor")]
    public IActionResult Complete(string id)
    {
        var order = db.Orders
            .FirstOrDefault(o =>
                o.Id == id &&
                o.Store.AccountId == HttpContext.GetAccount()!.Id
            );

        if (order == null)
        {
            return NotFound("Order not found");
        }

        if (order.Status != "To Pickup" && order.Status != "Preparing")
        {
            return BadRequest("Order unable to be marked as completed");
        }

        order.Status = "Completed";
        db.SaveChanges();

        // todo: notify customer

        return Ok();
    }
}
