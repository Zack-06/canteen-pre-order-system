using System.Globalization;
using Stripe;

namespace Superchef.Services;

public class PaymentService
{
    private readonly DB db;

    public PaymentService(DB db)
    {
        this.db = db;
    }

    public void HandlePaymentIntentSucceeded(PaymentIntent paymentIntent)
    {
        // 1. Retrieve OrderId from PaymentIntent metadata
        if (!paymentIntent.Metadata.TryGetValue("OrderId", out var orderIdStr)) return;

        var order = db.Orders.FirstOrDefault(o => o.Id == orderIdStr);
        if (order == null) return;

        var method = "";
        var cardBrand = "";
        var cardLast4 = "";
        var fpxBank = "";

        var paymentMethodId = paymentIntent.PaymentMethodId;
        if (!string.IsNullOrEmpty(paymentMethodId))
        {
            var paymentMethod = new PaymentMethodService().Get(paymentMethodId);

            method = paymentMethod.Type;
            cardBrand = paymentMethod.Card?.Brand;
            cardLast4 = paymentMethod.Card?.Last4;
            fpxBank = paymentMethod.Fpx?.Bank;
        }

        // 2. Get payment method and details
        string methodStr = "";
        string? details = null;
        if (method == "fpx")
        {
            methodStr = "Bank Transfer";
            details = fpxBank != null ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fpxBank.Replace("_", " ")) : null;
        }
        else if (method == "card")
        {
            methodStr = "Card";
            details = cardBrand != null && cardLast4 != null ? $"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cardBrand)} •••• {cardLast4}" : null;
        }
        else if (method == "grabpay")
        {
            methodStr = "Grab Pay";
        }
        else
        {
            methodStr = "Unknown";
        }

        // 3. Create Payment record
        var payment = new Payment
        {
            OrderId = order.Id,
            StripePaymentIntentId = paymentIntent.Id,
            Amount = paymentIntent.AmountReceived / 100m, // convert cents to RM
            PaymentMethod = methodStr,
            Details = details,
        };
    }

    public void HandleChargeRefunded(Charge charge)
    {
        var payment = db.Payments.FirstOrDefault(p => p.StripePaymentIntentId == charge.PaymentIntentId);
        if (payment == null) return;

        payment.IsRefunded = true;
        db.SaveChanges();
    }

    public void HandlePayout(Order order)
    {
        if (order.Payment == null) return;

        // 1. Send money to vendor via Stripe Transfer
        var store = db.Stores.FirstOrDefault(v => v.Id == order.StoreId);
        if (store != null && !string.IsNullOrEmpty(store.StripeAccountId))
        {
            // 2. Calculate vendor payout (if you take a platform commission)
            decimal vendorAmount = order.Payment.Amount;
            decimal commissionPercent = 0.1m; // e.g., 1% platform fee
            decimal commissionAmount = Math.Round(order.Payment.Amount * commissionPercent, 2);
            vendorAmount -= commissionAmount;

            var transferService = new TransferService();
            var transferOptions = new TransferCreateOptions
            {
                Amount = (long)(vendorAmount * 100), // in cents
                Currency = "myr",
                Destination = store.StripeAccountId,
                TransferGroup = order.Payment.StripePaymentIntentId
            };

            var transfer = transferService.Create(transferOptions);

            // 3. Update transfer status
            order.Payment.IsPayoutFinished = true;
            db.SaveChanges();
        }
    }

    public void TriggerRefund(string paymentIntentId)
    {
        // 1. List charges for the PaymentIntent
        var chargeService = new ChargeService();
        var charges = chargeService.List(new()
        {
            PaymentIntent = paymentIntentId,
            Limit = 1
        });

        var charge = charges.Data.FirstOrDefault();
        if (charge == null)
        {
            throw new Exception("Charge not found");
        }

        // 2. Refund the charge
        var refundService = new RefundService();
        refundService.Create(new()
        {
            Charge = charge.Id,
        });

        // 3. Update payment status
        var payment = db.Payments.FirstOrDefault(p => p.StripePaymentIntentId == paymentIntentId);
        if (payment == null) return;

        payment.IsRefunded = true;
        db.SaveChanges();
    }
}