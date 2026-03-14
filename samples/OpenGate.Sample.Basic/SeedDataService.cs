using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGate.Admin.Api.Security;
using OpenGate.Data.EFCore;
using OpenGate.Data.EFCore.Entities;
using OpenGate.Server.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenGate.Sample.Basic;

/// <summary>
/// Runs on startup to apply EF Core migrations and seed demo OAuth clients + user.
/// Safe to run multiple times — skips already-existing records.
/// </summary>
public sealed partial class SeedDataService(
    IServiceProvider services,
    ILogger<SeedDataService> logger,
    OpenGateOptions openGateOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // 1. Apply pending migrations
        var db = sp.GetRequiredService<OpenGateDbContext>();
        await db.Database.MigrateAsync(stoppingToken);
        Log.MigrationsApplied(logger);

        // 2. Demo user
        await SeedDemoUserAsync(sp, stoppingToken);

        // 3. Admin roles + assignment
        await SeedAdminRolesAsync(sp, stoppingToken);

        // 4. OAuth clients
        await SeedClientsAsync(sp, stoppingToken);

        // 5. Admin sample data
        await SeedAdminSampleDataAsync(sp, stoppingToken);
    }

    // ── Demo user ─────────────────────────────────────────────────────────────

    private async Task SeedDemoUserAsync(IServiceProvider sp, CancellationToken ct)
    {
        var userManager = sp.GetRequiredService<UserManager<OpenGateUser>>();
        const string email    = "demo@opengate.test";
        // Must satisfy the Identity password policy configured in OpenGate.Data.EFCore.
        // (At the time of writing, RequiredLength is 12.)
        const string password = "Demo@1234!abcd";

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var user = new OpenGateUser
        {
            UserName       = email,
            Email          = email,
            EmailConfirmed = true,
            Profile        = new UserProfile { FirstName = "Demo", LastName = "User" }
        };

        var result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
            Log.DemoUserCreated(logger, email);
        else
            foreach (var e in result.Errors)
                Log.DemoUserError(logger, e.Description);
    }

    private async Task SeedAdminRolesAsync(IServiceProvider sp, CancellationToken ct)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<OpenGateUser>>();

        foreach (var roleName in OpenGateAdminRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                        Log.DemoUserError(logger, error.Description);
                }
            }
        }

        var demoUser = await userManager.FindByEmailAsync("demo@opengate.test");
        if (demoUser is null)
            return;

        if (!await userManager.IsInRoleAsync(demoUser, OpenGateAdminRoles.SuperAdmin))
        {
            var roleResult = await userManager.AddToRoleAsync(demoUser, OpenGateAdminRoles.SuperAdmin);
            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                    Log.DemoUserError(logger, error.Description);
            }
        }
    }

    // ── OAuth clients ─────────────────────────────────────────────────────────

    private async Task SeedClientsAsync(IServiceProvider sp, CancellationToken ct)
    {
        var mgr = sp.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeMgr = sp.GetRequiredService<IOpenIddictScopeManager>();

        // ── Scopes ────────────────────────────────────────────────────────────
        // OpenIddict validates scope references when creating applications.
        // Register the API scope first so machine client creation doesn't fail.
        var apiScope = openGateOptions.ApiScopeName;
        if (await scopeMgr.FindByNameAsync(apiScope, ct) is null)
        {
            await scopeMgr.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name        = apiScope,
                DisplayName = "API access",
                Resources   = { "resource_server" }
            }, ct);
        }

        // ── Interactive (Authorization Code + PKCE) ───────────────────────────
        if (await mgr.FindByClientIdAsync("interactive-demo", ct) is null)
        {
            await mgr.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId    = "interactive-demo",
                DisplayName = "Interactive Demo (Authorization Code)",
                ClientType  = ClientTypes.Public,
                ConsentType = ConsentTypes.Explicit,
                RedirectUris =
                {
                    new Uri("https://oauth.pstmn.io/v1/callback"),  // Postman
                    new Uri("http://localhost:5001/callback")
                },
                PostLogoutRedirectUris = { new Uri("http://localhost:5001/") },
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.EndSession,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles,
                    $"{Permissions.Prefixes.Scope}openid"
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange }
            }, ct);

            Log.ClientCreated(logger, "interactive-demo");
        }

        // ── Machine (Client Credentials) ──────────────────────────────────────
        if (await mgr.FindByClientIdAsync("machine-demo", ct) is null)
        {
            await mgr.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId     = "machine-demo",
                ClientSecret = "machine-demo-secret-change-in-prod",
                DisplayName  = "Machine Demo (Client Credentials)",
                ClientType   = ClientTypes.Confidential,
                ConsentType  = ConsentTypes.Implicit,
                Permissions  =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    $"{Permissions.Prefixes.Scope}{apiScope}"
                }
            }, ct);

            Log.ClientCreated(logger, "machine-demo");
        }
    }

    private static async Task SeedAdminSampleDataAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<OpenGateDbContext>();
        var demoUser = await db.Users.SingleOrDefaultAsync(u => u.Email == "demo@opengate.test", ct);
        if (demoUser is null)
            return;

        if (!await db.UserSessions.AnyAsync(s => s.UserId == demoUser.Id, ct))
        {
            db.UserSessions.Add(new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = demoUser.Id,
                ClientId = "interactive-demo",
                IpAddress = "127.0.0.1",
                UserAgent = "OpenGate.Sample.Basic/Seed",
                DeviceInfo = "Seeded Browser Session",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8)
            });
        }

        if (!await db.AuditLogs.AnyAsync(a => a.UserId == demoUser.Id && a.EventType == "Admin.Seed", ct))
        {
            db.AuditLogs.Add(new AuditLog
            {
                UserId = demoUser.Id,
                EventType = "Admin.Seed",
                ClientId = "interactive-demo",
                IpAddress = "127.0.0.1",
                UserAgent = "OpenGate.Sample.Basic/Seed",
                Succeeded = true,
                Details = "{\"source\":\"sample-seed\"}",
                OccurredAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Structured logging ────────────────────────────────────────────────────

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "EF Core migrations applied.")]
        public static partial void MigrationsApplied(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Demo user created: {Email}")]
        public static partial void DemoUserCreated(ILogger logger, string email);

        [LoggerMessage(Level = LogLevel.Error, Message = "Demo user creation failed: {Error}")]
        public static partial void DemoUserError(ILogger logger, string error);

        [LoggerMessage(Level = LogLevel.Information, Message = "OAuth client seeded: {ClientId}")]
        public static partial void ClientCreated(ILogger logger, string clientId);
    }
}

