using System.Net;
using System.Security.Claims;
using OpenGate.Server.Extensions;
using OpenGate.Server.Options;
using OpenGateServer;

var builder = WebApplication.CreateBuilder(args);

// 1 OpenGate Identity Server 1
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

// Razor Pages 1 serves OpenGate.UI pages
builder.Services.AddRazorPages();

//#if (seed)
builder.Services.AddHostedService<SeedDataService>();
//#endif

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Root page:
// - Anonymous users: redirect to Login
// - Authenticated users: show a tiny landing page
app.MapGet("/", (HttpContext ctx) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true)
        return Results.Redirect("/Account/Login");

    var display = ctx.User.Identity?.Name
               ?? ctx.User.FindFirstValue(ClaimTypes.Email)
               ?? "(unknown)";

    var html = $"""
<!doctype html>
<html lang=\"en\">
<head><meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />
<title>OpenGate Server</title></head>
<body style=\"font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif; margin: 2rem;\">
  <h1>OpenGate Identity Server</h1>
  <p>Logged in as <strong>{WebUtility.HtmlEncode(display)}</strong>.</p>
  <ul>
    <li><a href=\"/.well-known/openid-configuration\">OIDC discovery</a></li>
    <li><a href=\"/health\">Health</a></li>
    <li><a href=\"/connect/logout\">Logout</a></li>
  </ul>
</body>
</html>
""";

    return Results.Content(html, "text/html");
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));

app.Run();

// Required for WebApplicationFactory<Program> in integration tests (if you add them)
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
