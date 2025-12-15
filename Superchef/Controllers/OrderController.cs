using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Superchef.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly DB db;
    private readonly PaymentService paySrv;
    private readonly IConfiguration cf;

    public OrderController(DB db, PaymentService paySrv, IConfiguration cf)
    {
        this.db = db;
        this.paySrv = paySrv;
        this.cf = cf;
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
            .Include(s => s.Slot)
            .FirstOrDefault(o => o.Id == vm.Id &&
            o.AccountId == HttpContext.GetAccount()!.Id &&
            o.Status == "Pending"
        );
        if (order == null)
        {
            return NotFound();
        }

        vm.AvailableDates = [
            DateOnly.FromDateTime(DateTime.Now),
            DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            DateOnly.FromDateTime(DateTime.Now.AddDays(2))
        ];

        if (vm.Date == null || !vm.AvailableDates.Contains(vm.Date.Value))
        {
            vm.Date = vm.AvailableDates.First();
        }

        vm.AvailableSlots = [];
        foreach (var avSlot in db.SlotTemplates.Where(s => s.DayOfWeek == (int)vm.Date.Value.DayOfWeek))
        {
            var slot = new DateTime(vm.Date.Value.Year, vm.Date.Value.Month, vm.Date.Value.Day, avSlot.StartTime.Hour, avSlot.StartTime.Minute, 0);
            if (slot > DateTime.Now)
            {
                vm.AvailableSlots.Add(TimeOnly.FromDateTime(slot));
            }
        }

        vm.EnabledSlots = db.Slots
            .Where(s => 
                s.StoreId == order.StoreId && 
                DateOnly.FromDateTime(s.StartTime) == vm.Date
            )
            .Select(s => TimeOnly.FromDateTime(s.StartTime))
            .ToList();

        if (Request.IsAjax())
        {
            return PartialView("_Slot", vm);
        }

        if (order.ExpiresAt != null)
        {
            ViewBag.ExpiredTimestamp = ViewBag.ExpiredTimestamp = new DateTimeOffset(order.ExpiresAt.Value).ToUnixTimeMilliseconds();
        }

        // select pickup time slot
        return View(vm);
    }

    public IActionResult Confirmation()
    {
        // show order confirmation, click "pay"
        // after that only set to "confirmed"

        // pending, confirmed, preparing, completed, cancelled
        return View();
    }

    [HttpPost]
    public IActionResult Confirmation(string orderId)
    {
        var baseUrl = Request.GetBaseUrl();

        var order = db.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null) return BadRequest("Order not found");

        var options = new SessionCreateOptions
        {
            SuccessUrl = baseUrl + "/Order/Success",
            CancelUrl = baseUrl + "/Order/Failed",
            LineItems = [],
            Mode = "payment",
            PaymentMethodTypes = ["card", "fpx", "grabpay"],
            CustomerEmail = HttpContext.GetAccount()!.Email,
            Metadata = new Dictionary<string, string> { { "OrderId", order.Id.ToString() } }
        };

        // Fill line items
        foreach (var item in order.OrderItems)
        {
            options.LineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    UnitAmount = (long)(item.Price / item.Quantity * 100),
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

    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        string endpointSecret = cf["Stripe:EndpointSecret"] ?? "";
        try
        {
            var stripeEvent = EventUtility.ParseEvent(json);
            var signatureHeader = Request.Headers["Stripe-Signature"];

            stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, endpointSecret);

            // Handle the event
            if (stripeEvent.Type == EventTypes.PaymentIntentSucceeded)
            {
                if (stripeEvent.Data.Object is not PaymentIntent paymentIntent)
                {
                    return BadRequest("PaymentIntent not found");
                }
                paySrv.HandlePaymentIntentSucceeded(paymentIntent);
            }
            else if (stripeEvent.Type == EventTypes.ChargeRefunded)
            {
                if (stripeEvent.Data.Object is not Charge charge)
                {
                    return BadRequest("Charge not found");
                }
                paySrv.HandleChargeRefunded(charge);
            }
            else
            {
                Console.WriteLine("Unhandled event type: {0}", stripeEvent.Type);
            }

            return Ok();
        }
        catch (StripeException e)
        {
            return BadRequest(e);
        }
    }

    public IActionResult Info()
    {
        // show order info
        return View();
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
