using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using CinePlex.Data;
using CinePlex.Models;
using System.Globalization;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireDigit = true;
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o => o.Cookie.IsEssential = true);
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

builder.Services.AddHostedService<CinePlex.Workers.ReservationExpiryWorker>();
builder.Services.AddScoped<CinePlex.Services.IEmailService, CinePlex.Services.EmailService>();
builder.Services.AddScoped<CinePlex.Services.ISearchService, CinePlex.Services.SearchService>();
builder.Services.AddScoped<CinePlex.Services.IFileUploadService, CinePlex.Services.FileUploadService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<CinePlex.Hubs.SeatHub>("/hubs/seats");

app.MapControllerRoute(
    name: "admin",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}",
    constraints: new { area = "Admin" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "User" });

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();
