using CacheViewVaryByDimensions.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

const string CookieName = "testCookie";
const string AbBucketCookieName = "abBucket";
const string TestHeaderName = "X-Cache-Test";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddLocalization();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie();
builder.Services.AddAuthorization();
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
var supportedCultures = new[] { new CultureInfo("en-US"), new CultureInfo("fr-FR") };
var localizationOptions = new RequestLocalizationOptions()
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("fr-FR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
app.Use(async (context, next) =>
{
    if (context.Request.Query.TryGetValue("AcceptLanguage", out var acceptLanguage))
    {
        context.Request.Headers.AcceptLanguage = acceptLanguage.ToString();
    }

    await next(context);
});
app.UseRequestLocalization(localizationOptions);


app.MapPost("/login", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var region = form["region"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(region))
    {
        region = "default";
    }
    var username = form["username"];
    var password = form["password"];

    // Validate user from DB
    if (username == "admin" && password == "admin123" || username == "user" && password == "user123")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username!)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    return Results.LocalRedirect(ResolveReturnUrl(returnUrl, region));
});

app.MapPost("/logout", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var region = form["region"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(region))
    {
        region = "default";
    }
    await context.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);

    return Results.LocalRedirect(ResolveReturnUrl(returnUrl, region));
});

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Query.TryGetValue("HeaderValue", out var headerValue))
    {
        if (string.IsNullOrWhiteSpace(headerValue.ToString()))
        {
            context.Request.Headers.Remove(TestHeaderName);
        }
        else
        {
            context.Request.Headers[TestHeaderName] = headerValue.ToString();
        }
    }

    await next(context);
});
app.MapStaticAssets();
app.MapGet("/culture/set",
(
    string culture,
    string? region,
    string? returnUrl,
    HttpContext context
) =>
{
    context.Response.Cookies.Append(
        "LastCulture",
        culture,
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });

    context.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(
            new RequestCulture(culture)));

    return Results.LocalRedirect(ResolveReturnUrl(returnUrl, region));
});

app.MapGet("/culture/accept-language",
(
    string language,
    string? region,
    string? returnUrl,
    HttpContext context
) =>
{
    var normalizedLanguage = supportedCultures
        .FirstOrDefault(culture => culture.Name.Equals(language, StringComparison.OrdinalIgnoreCase))
        ?.Name ?? localizationOptions.DefaultRequestCulture.Culture.Name;

    context.Response.Cookies.Delete(
        CookieRequestCultureProvider.DefaultCookieName,
        new CookieOptions { Path = "/" });

    var redirectUrl = QueryHelpers.AddQueryString(
        ResolveReturnUrl(returnUrl, region),
        "AcceptLanguage",
        normalizedLanguage);

    return Results.LocalRedirect(redirectUrl);
});

app.MapPost("/cookie/save", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var region = form["region"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(region))
    {
        region = "default";
    }

    const string cookieName = "testCookie";

    var operation = context.Request.Cookies.ContainsKey(cookieName)
        ? "Updated"
        : "Created";

    var cookieValue =
        $"{operation}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}";

    context.Response.Cookies.Append(
        cookieName,
        cookieValue,
        BuildCookieOptions());

    return Results.LocalRedirect(ResolveReturnUrl(returnUrl, region));
})
.DisableAntiforgery();

app.MapPost("/cookie/delete", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var region = form["region"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(region))
    {
        region = "default";
    }

    context.Response.Cookies.Delete(
        CookieName,
        new CookieOptions
        {
            Path = "/"
        });

    return Results.LocalRedirect(ResolveReturnUrl(returnUrl, region));
})
.DisableAntiforgery();

app.MapPost("/bucket/set", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var region = form["region"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    var bucket = form["bucket"].ToString();

    if (string.IsNullOrWhiteSpace(region))
    {
        region = "default";
    }

    var normalizedBucket = bucket.Equals("B", StringComparison.OrdinalIgnoreCase)
        ? "B"
        : "A";

    context.Response.Cookies.Append(
        AbBucketCookieName,
        normalizedBucket,
        BuildCookieOptions());

    return Results.LocalRedirect(ResolveReturnUrl(returnUrl, region));
})
.DisableAntiforgery();

app.MapRazorComponents<App>();

app.Run();

static CookieOptions BuildCookieOptions() =>
    new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = "/",
        MaxAge = TimeSpan.FromMinutes(30)
    };

static string ResolveReturnUrl(string? returnUrl, string? region)
{
    if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/'))
    {
        return returnUrl;
    }

    var safeRegion = string.IsNullOrWhiteSpace(region)
        ? "default"
        : Uri.EscapeDataString(region);

    return $"/{safeRegion}/demo";
}