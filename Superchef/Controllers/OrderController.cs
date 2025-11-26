using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace Superchef.Controllers;

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

    [HttpPost]
    public bool Create()
    {
        return true;
    }

    public IActionResult Customer()
    {
        // fill up name and phone number

        return View();
    }

    public IActionResult Slot()
    {
        // select pickup time slot
        return View();
    }

    public IActionResult Confirmation()
    {
        // show order confirmation, click "pay"
        // after that only set to "confirmed"

        // pending, confirmed, completed, cancelled
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

    public IActionResult Manage(int storeId)
    {
        // show all orders in a store (vendor)
        return View();
    }

    public IActionResult Edit(int id)
    {
        // edit order details (vendor)
        return View();
    }
}
