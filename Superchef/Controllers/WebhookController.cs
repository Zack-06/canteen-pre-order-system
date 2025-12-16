using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Superchef.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly DB db;
    private readonly PaymentService paySrv;
    private readonly IConfiguration cf;

    public WebhookController(DB db, PaymentService paySrv, IConfiguration cf)
    {
        this.db = db;
        this.paySrv = paySrv;
        this.cf = cf;
    }

    [HttpPost]
    public async Task<IActionResult> Index()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        string endpointSecret = cf["Stripe:WebhookSecret"] ?? "";
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
                    db.AuditLogs.Add(new()
                    {
                        AccountId = 1,
                        Entity = "error",
                        Action = $"Stripe Webhook Error: Expected PaymentIntent object missing for event type '{EventTypes.PaymentIntentSucceeded}'. Event ID: {stripeEvent.Id}",
                    });
                    db.SaveChanges();

                    return BadRequest("PaymentIntent not found");
                }
                
                string? error = paySrv.HandlePaymentIntentSucceeded(paymentIntent);
                if (error != null) return BadRequest(error);
            }
            else if (stripeEvent.Type == EventTypes.ChargeRefunded)
            {
                if (stripeEvent.Data.Object is not Charge charge)
                {
                    db.AuditLogs.Add(new()
                    {
                        AccountId = 1,
                        Entity = "error",
                        Action = $"Stripe Webhook Error: Expected Charge object missing for event type '{EventTypes.ChargeRefunded}'. Event ID: {stripeEvent.Id}",
                    });
                    db.SaveChanges();
                    
                    return BadRequest("Charge not found");
                }
                paySrv.HandleChargeRefunded(charge);
            }

            return Ok();
        }
        catch (StripeException e)
        {
            return BadRequest(e);
        }
    }
}