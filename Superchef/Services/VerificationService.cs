using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Services;

public class VerificationService
{
    private readonly DB db;
    private readonly IConfiguration cf;
    private readonly EmailService es;

    public VerificationService(DB db, IConfiguration cf, EmailService es)
    {
        this.db = db;
        this.cf = cf;
        this.es = es;
    }

    public Verification CreateVerification(string action, string baseUrl, int accountId, int? deviceId = null)
    {
        if (deviceId != null)
        {
            var existingRequest = db.Verifications.FirstOrDefault(u => u.DeviceId == deviceId);
            if (existingRequest != null)
            {
                return existingRequest;
            }
        }

        // Generate token
        var token = GeneratorHelper.RandomString(50);
        while (db.Verifications.Any(u => u.Token == token))
        {
            token = GeneratorHelper.RandomString(50);
        }

        // Generate OTP
        var otp = GeneratorHelper.RandomString(6, "0123456789");

        // Add new verification
        Verification verification = new()
        {
            Token = token,
            OTP = otp,
            Action = action,
            ExpiresAt = DateTime.Now.AddMinutes(5),
            DeviceId = deviceId,
            AccountId = accountId
        };
        db.Verifications.Add(verification);
        db.SaveChanges();

        db.Entry(verification).Reference(v => v.Account).Load();

        // Send email
        var link = $"{baseUrl}/Auth/Verify?Token={verification.Token}&otp={verification.OTP}";
        es.SendVerificationEmail(verification, link);

        return verification;
    }

    public Verification? GetVerificationRequest(string? token, string action)
    {
        return db.Verifications.Include(v => v.Account).FirstOrDefault(u => u.Token == token && u.Action == action && u.ExpiresAt > DateTime.Now);
    }

    async public Task<bool> VerifyRecaptcha(string recaptchaToken)
    {
        var secretKey = cf["RecaptchaSettings:SecretKey"]!;
        var verificationUrl = cf["RecaptchaSettings:VerificationUrl"]!;

        try
        {
            using var client = new HttpClient();

            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("secret", secretKey),
                new KeyValuePair<string, string>("response", recaptchaToken)
            ]);

            var response = await client.PostAsync(verificationUrl, content);

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool success = root.GetProperty("success").GetBoolean();

            return success;
        }
        catch
        {
            return false;
        }
    }
}