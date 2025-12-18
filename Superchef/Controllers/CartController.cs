using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Controllers;

[Authorize(Roles = "Customer")]
public class CartController : Controller
{
    private readonly DB db;
    private readonly IHubContext<OrderHub> orderHubContext;

    public CartController(DB db, IHubContext<OrderHub> orderHubContext)
    {
        this.db = db;
        this.orderHubContext = orderHubContext;
    }

    // display stores
    public IActionResult Index()
    {

        var storeIds = db.Carts
            .Where(c => c.AccountId == HttpContext.GetAccount()!.Id)
            .Select(c => c.Variant.Item.StoreId)
            .Distinct()
            .ToList();

        var stores = db.Stores
            .Include(s => s.Items)
                .ThenInclude(i => i.Reviews)
            .Where(s =>
                storeIds.Contains(s.Id) &&
                !s.IsDeleted
            )
            .ToList();

        var itemsCount = new Dictionary<int, int>();
        foreach (var store in stores)
        {
            itemsCount.Add(
                store.Id,
                db.Carts.Count(c =>
                    c.AccountId == HttpContext.GetAccount()!.Id &&
                    c.Variant.Item.StoreId == store.Id
                )
            );
        }

        return View((stores, itemsCount));
    }

    public IActionResult Store(int id)
    {
        // display items for store with id

        var store = db.Stores
            .Include(s => s.Items)
                .ThenInclude(i => i.Reviews)
            .Include(s => s.Venue)
            .FirstOrDefault(s => s.Id == id && !s.IsDeleted);
        if (store == null)
        {
            return NotFound("Store not found");
        }

        var cartItems = db.Carts
            .Include(c => c.Variant)
            .Include(c => c.Variant.Item)
            .Where(c =>
                c.AccountId == HttpContext.GetAccount()!.Id &&
                c.Variant.Item.StoreId == store.Id &&
                c.Variant.IsActive
            )
            .OrderBy(c => c.Variant.Item.Id)
            .ToList();

        if (cartItems.Count == 0)
        {
            return RedirectToAction("Index");
        }

        var vm = new CartStoreVM
        {
            Id = id,
            Store = store,
            CartItems = cartItems
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult UpdateQuantity(int id, int quantity)
    {
        var cart = db.Carts
            .Include(c => c.Variant)
            .FirstOrDefault(c => 
                c.VariantId == id &&
                c.AccountId == HttpContext.GetAccount()!.Id
            );
        if (cart == null)
        {
            return NotFound("Invalid item");
        }

        cart.Quantity = quantity;
        db.SaveChanges();

        if (cart.Quantity < 1 || cart.Quantity > 10)
        {
            return BadRequest("Invalid value");
        }

        if (cart.Quantity > cart.Variant.Stock)
        {
            return BadRequest("Not enough stock");
        }

        return Ok();
    }

    [HttpPost]
    public IActionResult RemoveItem(int id)
    {
        var cart = db.Carts
            .Include(c => c.Variant)
            .FirstOrDefault(c => 
                c.VariantId == id &&
                c.AccountId == HttpContext.GetAccount()!.Id
            );
        if (cart == null)
        {
            return NotFound("Item not found");
        }

        db.Carts.Remove(cart);
        db.SaveChanges();

        return Ok();
    }

    public async Task<IActionResult> Checkout(CartStoreVM vm)
    {
        var store = db.Stores.FirstOrDefault(s => s.Id == vm.Id && !s.IsDeleted);
        if (store == null)
        {
            return NotFound("Store not found");
        }

        var cartItems = db.Carts
            .Include(c => c.Variant)
            .Where(c =>
                c.AccountId == HttpContext.GetAccount()!.Id &&
                c.Variant.Item.StoreId == store.Id &&
                c.Variant.IsActive &&
                vm.SelectedItems.Contains(c.VariantId)
            )
            .ToList();

        bool isValid = true;
        decimal totalPrice = 0;
        int totalItems = 0;
        foreach (var cItem in cartItems)
        {
            if (cItem.Quantity < 1 || cItem.Quantity > 10 || cItem.Quantity > cItem.Variant.Stock)
            {
                totalPrice = 0;
                totalItems = 0;
                isValid = false;
                break;
            }

            totalPrice += cItem.Variant.Price * cItem.Quantity;
            totalItems += cItem.Quantity;
        }

        if (Request.Method == "GET")
        {
            return PartialView("_SummaryContainer", new SummaryContainerDM
            {
                TotalPrice = totalPrice,
                TotalItems = totalItems,
                SubmitText = "Checkout"
            });
        }
        else if (vm.SelectedItems.Count == 0)
        {
            return BadRequest("Checkout failed! Please select at least one item");
        }
        else if (!isValid || totalPrice < 2 || totalItems < 1)
        {
            return BadRequest("Checkout failed! Please check your cart");
        }

        if (db.Orders.Any(o => o.Status == "Pending" && o.AccountId == HttpContext.GetAccount()!.Id))
        {
            TempData["Message"] = "Checkout failed! You have an existing pending order";
            Response.Headers.Append("X-Redirect-Url", Url.Action("History", "Account"));
            return Ok();
        }

        if (totalPrice > 1000)
        {
            return BadRequest("Checkout failed! Total price cannot exceed RM 1000.00");
        }

        var startOrderTime = DateTime.Now;
        var endOrderDate = startOrderTime.AddDays(2);

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
                s.StoreId == store.Id &&
                s.MaxOrders > s.Orders.Count &&
                s.StartTime >= startOrderTime.AddMinutes(8 + 30) &&
                s.StartTime <= endOrderDateTime
            )
            .ToList();
        if (availableSlots.Count == 0)
        {
            return BadRequest("No available slots for this store at the moment");
        }

        var acc = HttpContext.GetAccount();

        // Create Order
        var order = new Order
        {
            Name = acc!.Name,
            PhoneNumber = acc!.PhoneNumber ?? "",
            Status = "Pending",
            ExpiresAt = DateTime.Now.AddMinutes(7),
            AccountId = acc.Id,
            StoreId = store.Id,
            SlotId = availableSlots.First().Id
        };

        // Add Order Items
        foreach (var cItem in cartItems)
        {
            order.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                VariantId = cItem.VariantId,
                Quantity = cItem.Quantity,
                Price = cItem.Variant.Price
            });

            // Edit stock
            cItem.Variant.Stock -= cItem.Quantity;
        }

        // Remove cart items
        db.RemoveRange(cartItems);

        db.Orders.Add(order);
        db.SaveChanges();

        await orderHubContext.Clients.All.SendAsync("UpdateStock", store.Id);

        Response.Headers.Append("X-Redirect-Url", Url.Action("Customer", "Order", new { id = order.Id }));
        return Ok();
    }
}
