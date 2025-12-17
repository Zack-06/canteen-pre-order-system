using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace Superchef.Services;

public class PaymentService
{
    private readonly DB db;

    public PaymentService(DB db)
    {
        this.db = db;
    }

    public string? HandlePaymentIntentSucceeded(PaymentIntent paymentIntent)
    {
        // Retrieve OrderId from PaymentIntent metadata
        if (!paymentIntent.Metadata.TryGetValue("OrderId", out var orderIdStr))
        {
            db.AuditLogs.Add(new AuditLog
            {
                AccountId = 1,
                Entity = "error",
                Action = $"Stripe Webhook Error: Missing OrderId metadata for PaymentIntent with ID {paymentIntent.Id}",
            });
            db.SaveChanges();

            return "OrderId metadata missing";
        }

        var order = db.Orders
            .Include(o => o.Payment)
            .FirstOrDefault(o => o.Id == orderIdStr);
        if (order == null || order.Payment != null)
        {
            db.AuditLogs.Add(new AuditLog
            {
                AccountId = 1,
                Entity = "error",
                Action = $"Stripe Webhook Error: Order with ID {orderIdStr} not found",
            });
            db.SaveChanges();

            return "Order not found";
        }

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

        // Get payment method and details
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

        // Create Payment record
        var payment = new Payment
        {
            OrderId = order.Id,
            StripePaymentIntentId = paymentIntent.Id,
            Amount = paymentIntent.AmountReceived / 100m, // convert cents to RM
            PaymentMethod = methodStr,
            Details = details,
        };
        db.Payments.Add(payment);

        // Update order status
        order.Status = "Confirmed";
        order.ExpiresAt = null;
        db.SaveChanges();

        return null;
    }

    public string? HandleChargeRefunded(Charge charge)
    {
        var payment = db.Payments.FirstOrDefault(p => p.StripePaymentIntentId == charge.PaymentIntentId);
        if (payment == null)
        {
            db.AuditLogs.Add(new AuditLog
            {
                AccountId = 1,
                Entity = "error",
                Action = $"Refund failed! Payment not found for Charge {charge.Id}",
            });
            db.SaveChanges();

            return "Payment not found";
        }

        payment.IsRefunded = true;
        db.SaveChanges();

        return null;
    }

    public void TriggerPayout(Order order)
    {
        order = db.Orders
            .Include(o => o.Payment)
            .FirstOrDefault(o => o.Id == order.Id)!;

        if (order.Payment == null) return;
        if (order.Payment.IsPayoutFinished) return;

        // Send money to vendor via Stripe Transfer
        var store = db.Stores.FirstOrDefault(v => v.Id == order.StoreId);
        if (store != null && !string.IsNullOrEmpty(store.StripeAccountId))
        {
            var transferService = new TransferService();
            var transferOptions = new TransferCreateOptions
            {
                Amount = (long)(order.Payment.Amount * 100), // in cents
                Currency = "myr",
                Destination = store.StripeAccountId,
                TransferGroup = order.Payment.StripePaymentIntentId
            };

            var transfer = transferService.Create(transferOptions);

            // Update transfer status
            order.Payment.PayoutTransferId = transfer.Id;
            order.Payment.IsPayoutFinished = true;
            db.SaveChanges();
        }
    }

    public void TriggerRefund(string paymentIntentId)
    {
        // List charges for the PaymentIntent
        var chargeService = new ChargeService();
        var charges = chargeService.List(new()
        {
            PaymentIntent = paymentIntentId,
            Limit = 1
        });

        var charge = charges.Data.FirstOrDefault();
        if (charge == null)
        {
            db.AuditLogs.Add(new AuditLog
            {
                AccountId = 1,
                Entity = "error",
                Action = $"Refund failed! Charge not found for PaymentIntent {paymentIntentId}",
            });
            db.SaveChanges();

            return;
        }

        // Refund the charge
        var refundService = new RefundService();
        try
        {
            var refund = refundService.Create(new() { Charge = charge.Id });
        }
        catch (StripeException ex)
        {
            db.AuditLogs.Add(new AuditLog
            {
                AccountId = 1,
                Entity = "error",
                Action = $"Refund failed for PaymentIntent {paymentIntentId}: {ex.Message}"
            });
            db.SaveChanges();
        }
    }
}