global using Superchef.Models;
global using Superchef.Services;
global using Superchef.Hubs;
global using X.PagedList.Extensions;
// using Superchef.BackgroundWorkers;
using Superchef.Middlewares;
using Microsoft.AspNetCore.Mvc;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR(options =>
{
    // testing
    options.EnableDetailedErrors = true; // Shows full exception stack traces
});
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new ResponseCacheAttribute
    {
        NoStore = true
    });
});

builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\Superchef.mdf;
");

// Add authentication
builder.Services.AddAuthentication().AddCookie("Cookies", options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Error/403";
});

// Add http context accessor
builder.Services.AddHttpContextAccessor();

// Add cookie protection
builder.Services.AddDataProtection();

// Add services
builder.Services.AddSingleton<ImageService>();

builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<SecurityService>();
builder.Services.AddScoped<VerificationService>();
builder.Services.AddScoped<PaymentService>();

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseStatusCodePagesWithReExecute("/Error/{0}"); // hit error controller if error occurs

// Add middlewares
// app.UseMiddleware<ExpiryCleanupMiddleware>();
app.UseMiddleware<AuthSessionMiddleware>();

// Add hubs
app.MapHub<VerificationHub>("/VerificationHub");
// app.MapHub<BookingHub>("/BookingHub");
// app.MapHub<FnbOrderHub>("/FnbOrderHub");
// app.MapHub<AccountHub>("/AccountHub");

app.MapDefaultControllerRoute();
app.Run();