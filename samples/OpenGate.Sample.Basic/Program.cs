using OpenGate.Sample.Basic;
using OpenGate.Admin.Api.Extensions;
using OpenGate.Server;
using OpenGate.Server.Extensions;
using OpenGate.Server.Options;
using Scalar.AspNetCore;
using System.Net;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ── OpenGate Identity Server ─────────────────────────────────────────────────
// Read connection string — validation deferred to first DB use so tests can replace the DbContext.
var connectionString = builder.Configuration.GetConnectionString("OpenGate") ?? string.Empty;

builder.Services
    .AddOpenGate(opt =>
    {
        opt.SecurityPreset = builder.Environment.IsDevelopment()
            ? OpenGateSecurityPreset.Development
            : OpenGateSecurityPreset.Production;

        if (builder.Configuration["OpenGate:IssuerUri"] is { Length: > 0 } issuer)
            opt.IssuerUri = new Uri(issuer);
    })
    .UseSqlServer(connectionString)
    .Build();

// ── Razor Pages — serves OpenGate.UI pages ───────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddOpenApi("v1");
builder.Services.AddOpenGateAdminApi();

// ── Seed demo data (users + OAuth clients) on startup ────────────────────────
builder.Services.AddHostedService<SeedDataService>();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Static files — includes _content/OpenGate.UI/css/opengate.css from the RCL
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
    app.MapScalarApiReference("/docs", options => options
        .WithTitle("OpenGate Admin API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json"));
}

app.MapRazorPages();
app.MapOpenGateAdminApi();

// Root page:
// - Anonymous users: redirect to Login
// - Authenticated users: show a tiny "you are logged in" landing page
app.MapGet("/", (HttpContext ctx) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true)
        return Results.Redirect("/Account/Login");

    var display = ctx.User.Identity?.Name
               ?? ctx.User.FindFirstValue(ClaimTypes.Email)
               ?? "(unknown)";

    var html = $"""
<!doctype html>
<html lang="en">
<head><meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1" />
<title>OpenGate Sample</title></head>
<body style="font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif; margin: 2rem;">
  <h1>OpenGate.Sample.Basic</h1>
  <p>Logged in as <strong>{WebUtility.HtmlEncode(display)}</strong>.</p>
  <ul>
    <li><a href="/.well-known/openid-configuration">OIDC discovery</a></li>
    <li><a href="/health">Health</a></li>
    <li><a href="/openapi/v1.json">OpenAPI</a></li>
    <li><a href="/docs">API Docs</a></li>
    {(ctx.User.IsInRole("Viewer") || ctx.User.IsInRole("Admin") || ctx.User.IsInRole("SuperAdmin") ? "<li><a href=\"/Admin\">Admin UI</a></li>" : string.Empty)}
    <li><a href="/connect/logout">Logout</a></li>
  </ul>
</body>
</html>
""";

    return Results.Content(html, "text/html");
});

// Simple health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
