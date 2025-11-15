global using Superchef.Models;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\Superchef.mdf;
");

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