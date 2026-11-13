using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using LiMS.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "LiMS.Web.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Index";
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<LibraryApiClient>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(2);
});
builder.Services.AddHttpClient("LiMS.Api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["LiMSApi:BaseUrl"]!);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var session = context.Session;
    var sessionExpired = false;
    var expiresAtValue = session.GetString("TokenExpiresAt");
    if (DateTimeOffset.TryParse(expiresAtValue, out var expiresAt)
        && expiresAt <= DateTimeOffset.UtcNow)
    {
        session.Clear();
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        sessionExpired = true;
    }

    if (!sessionExpired
        && context.User.Identity?.IsAuthenticated == true
        && string.IsNullOrEmpty(session.GetString("AccessToken")))
    {
        session.SetString("StudentCode", context.User.FindFirstValue("LiMS:StudentCode") ?? "");
        session.SetString("FullName", context.User.FindFirstValue("LiMS:FullName") ?? "");
        session.SetString("Role", context.User.FindFirstValue(ClaimTypes.Role) ?? "");
        session.SetString("AccessToken", context.User.FindFirstValue("LiMS:AccessToken") ?? "");
        session.SetString("TokenExpiresAt", context.User.FindFirstValue("LiMS:TokenExpiresAt") ?? "");
    }

    await next();
});
app.UseAuthorization();

app.MapRazorPages();

app.Run();
