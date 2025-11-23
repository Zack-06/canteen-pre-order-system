global using Superchef.Models;
global using Superchef.Services;
// global using Superchef.Hubs;
global using X.PagedList.Extensions;
// using Superchef.BackgroundWorkers;
// using Superchef.Middlewares;
// using Stripe;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\Superchef.mdf;
");

// Add http context accessor
builder.Services.AddHttpContextAccessor();

// Add services
builder.Services.AddSingleton<ImageService>();

builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<SecurityService>();
builder.Services.AddScoped<VerificationService>();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseStatusCodePagesWithReExecute("/Error/{0}"); // hit error controller if error occurs

// // Add middlewares
// app.UseMiddleware<ExpiryCleanupMiddleware>();
// app.UseMiddleware<AuthSessionMiddleware>();

// // Add hubs
// app.MapHub<BookingHub>("/BookingHub");
// app.MapHub<FnbOrderHub>("/FnbOrderHub");
// app.MapHub<AccountHub>("/AccountHub");

app.MapDefaultControllerRoute();
app.Run();