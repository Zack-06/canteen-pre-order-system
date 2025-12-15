using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

namespace Superchef.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly DB db;
    private readonly IHubContext<OrderHub> orderHubContext;

    public OrderController(DB db, IHubContext<OrderHub> orderHubContext)
    {
        this.db = db;
        this.orderHubContext = orderHubContext;
    }

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

    public IActionResult Manage(ManageOrderVM vm)
    {
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
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "id", Text = "Search By Id" },
            new() { Value = "customer_name", Text = "Search By Customer Name" },
            new() { Value = "customer_id", Text = "Search By Customer Id" },
        ];
        vm.AvailableStatuses = ["Confirmed", "Preparing", "Completed", "Cancelled"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        ViewBag.StoreName = "abc";

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        // edit order details (vendor)
        return View();
    }
}
