global using Superchef.Models;
global using Superchef.Services;
global using Superchef.Helpers;
global using Superchef.Hubs;
global using X.PagedList.Extensions;
using Superchef.BackgroundWorkers;
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

// Redirection
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        // If it's AJAX
        if (context.Request.Headers.XRequestedWith == "XMLHttpRequest") 
        {
            // Set the HTTP status code to 401 Unauthorized
            context.Response.StatusCode = 401;
            
            // Prevent the default redirect
            return Task.CompletedTask;
        }
        
        // For standard (non-AJAX) browser requests, proceed with the default redirect
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// Add http context accessor
builder.Services.AddHttpContextAccessor();

// Add cookie protection
builder.Services.AddDataProtection();

// Add session
builder.Services.AddSession();

// Add services
builder.Services.AddSingleton<ImageService>();

builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<SecurityService>();
builder.Services.AddScoped<VerificationService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<SystemOrderService>();
builder.Services.AddScoped<GenerateLinkService>();
builder.Services.AddScoped<CleanupService>();
builder.Services.AddScoped<GenerateSlotService>();

// Add background worker
builder.Services.AddHostedService<GenerateSlotBackgroundWorker>();
builder.Services.AddHostedService<ExpiryCleanupBackgroundWorker>();

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();

app.UseStatusCodePagesWithReExecute("/Error/{0}"); // hit error controller if error occurs

// Add middlewares
app.UseMiddleware<ExpiryCleanupMiddleware>();
app.UseMiddleware<AuthSessionMiddleware>();

// Add hubs
app.MapHub<VerificationHub>("/VerificationHub");
app.MapHub<AccountHub>("/AccountHub");
app.MapHub<OrderHub>("/OrderHub");

app.MapDefaultControllerRoute();
app.Run();

