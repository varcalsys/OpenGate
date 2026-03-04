using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

var authority = builder.Configuration["Auth:Authority"] ?? "https://localhost:7001";
var audience = builder.Configuration["Auth:Audience"] ?? "resource_server";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/api/me", (HttpContext context) =>
{
    var subject = context.User.FindFirst("sub")?.Value ?? "(unknown)";
    var scope = context.User.FindFirst("scope")?.Value ?? "(none)";

    return Results.Ok(new
    {
        subject,
        scope,
        timestamp = DateTimeOffset.UtcNow
    });
}).RequireAuthorization();

app.Run();
