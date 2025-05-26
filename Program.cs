using CredWise_Trail.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<BankLoanManagementDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme) // Specifies default authentication scheme
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";       // Path to your login action
        options.AccessDeniedPath = "/Account/AccessDenied"; // Optional: Path for access denied
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // How long the cookie is valid
        options.SlidingExpiration = true; // Renews cookie if half of expiration time has passed
        options.Cookie.HttpOnly = true; // Cookie is not accessible by client-side script
        options.Cookie.IsEssential = true; // Makes the cookie essential for GDPR compliance
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BankLoanManagementDbContext>();
    dbContext.Database.Migrate(); 
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Landing}/{id?}");

app.Run();
