using OpenGate.Sample.PostgreSql;
using OpenGate.Server;
using OpenGate.Server.Extensions;
using OpenGate.Server.Options;
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
    .UseConfiguredDatabase(builder.Configuration, connectionString)
    .Build();

// ── Razor Pages — serves OpenGate.UI pages ───────────────────────────────────
builder.Services.AddRazorPages();

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

app.MapRazorPages();

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
  <h1>OpenGate.Sample.PostgreSql</h1>
  <p>Logged in as <strong>{WebUtility.HtmlEncode(display)}</strong>.</p>
  <ul>
    <li><a href="/.well-known/openid-configuration">OIDC discovery</a></li>
    <li><a href="/health">Health</a></li>
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

internal static class OpenGateBuilderDatabaseExtensions
{
    public static OpenGateBuilder UseConfiguredDatabase(
        this OpenGateBuilder builder,
        IConfiguration configuration,
        string connectionString)
    {
        var provider = configuration["OpenGate:DatabaseProvider"]?.Trim().ToLowerInvariant();

        return provider switch
        {
            "postgres" or "postgresql" or "npgsql" => builder.UsePostgreSql(connectionString),
            "sqlite" => builder.UseSqlite(connectionString),
            _ => builder.UseSqlServer(connectionString)
        };
    }
}

