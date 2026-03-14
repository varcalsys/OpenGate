using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenGate.Data.EFCore;
using OpenGate.Sample.Basic;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenGate.Server.Tests.Integration;

/// <summary>
/// WebApplicationFactory for integration tests.
/// Replaces SQL Server with an isolated InMemory database per factory instance.
/// </summary>
public sealed class OpenGateWebFactory : WebApplicationFactory<Program>
{
    // Each factory instance gets its own isolated InMemory DB.
    private readonly string _dbName = $"OGTest_{Guid.NewGuid():N}";

    public OpenGateWebFactory()
    {
        // WebApplicationFactory executes Program.cs via HostFactoryResolver.
        // ConfigureWebHost / ConfigureAppConfiguration callbacks only run AFTER
        // WebApplication.CreateBuilder(args) has already read app configuration,
        // so they are too late to override what Program.cs reads.
        //
        // Environment variables ARE read inside CreateBuilder(args), so setting
        // them here (before StartServer() triggers CreateBuilder) is the only
        // reliable way to inject a placeholder connection string that prevents
        // UseSqlServer from throwing before ConfigureTestServices can run.
        //
        // Double-underscore (__) maps to (:) in .NET configuration hierarchy.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__OpenGate",
            "Server=(local);Database=OpenGateTest;Integrated Security=true;");
        Environment.SetEnvironmentVariable(
            "OpenGate__IssuerUri", "https://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Development" → IsDevelopment() = true → Program.cs applies the Development
        // preset (DisableTransportSecurityRequirement + ephemeral keys).
        // Using "Testing" would trigger the Production preset which requires HTTPS,
        // causing every OpenIddict endpoint to return 400 on the plain-HTTP test server.
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // ── Swap SQL Server DbContext → InMemory ───────────────────────────
            // EF Core 8+ registers the provider configuration as
            // IDbContextOptionsConfiguration<TContext> singletons (one per
            // AddDbContext call).  Removing only DbContextOptions<T> leaves the
            // SQL Server configuration delegate in the container; when InMemory is
            // then added, both providers coexist and EF Core throws.
            // We must remove ALL three artifact types from the original registration:
            //   1. IDbContextOptionsConfiguration<OpenGateDbContext>  ← the lambda
            //   2. DbContextOptions<OpenGateDbContext>                ← the built opts
            //   3. OpenGateDbContext                                  ← the context itself
            var optCfgType = typeof(IDbContextOptionsConfiguration<OpenGateDbContext>);
            var toRemove = services
                .Where(d => d.ServiceType == optCfgType
                         || d.ServiceType == typeof(DbContextOptions<OpenGateDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || d.ServiceType == typeof(OpenGateDbContext))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<OpenGateDbContext>(opt =>
                opt.UseInMemoryDatabase(_dbName)
                   .UseOpenIddict());

            // ── Replace SeedDataService (calls MigrateAsync, incompatible with InMemory)
            var seedDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                         && d.ImplementationType == typeof(SeedDataService))
                .ToList();
            foreach (var d in seedDescriptors) services.Remove(d);

            services.AddHostedService<IntegrationSeedService>();
        });
    }
}

/// <summary>
/// Minimal seed service for integration tests.
/// Implements <see cref="IHostedService"/> directly (NOT BackgroundService) so that
/// <c>StartAsync</c> is AWAITED by <c>IHost.StartAsync</c> before any request is served.
/// This guarantees OAuth clients are in the InMemory DB before the first test runs.
///
/// Uses <see cref="CancellationToken.None"/> for all DB operations — the token passed
/// to <c>StartAsync</c> is only meant for startup-abort scenarios and should NOT be
/// forwarded to seeding work that must complete before the server is ready.
/// </summary>
internal sealed class IntegrationSeedService(IServiceProvider services) : IHostedService
{
    internal const string DemoEmail           = "demo@opengate.test";
    internal const string DemoPassword        = "Demo@1234!abcd";
    internal const string MachineClientId     = "machine-demo";
    internal const string MachineClientSecret = "machine-demo-secret-change-in-prod";
    internal const string InteractiveClientId = "interactive-demo";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync();
        }
        catch (Exception ex)
        {
            // Log but do NOT rethrow — a seed failure should not abort host startup.
            // Tests that depend on seeded data will fail with clear assertion errors.
            Console.Error.WriteLine($"[IntegrationSeedService] Seed failed: {ex}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // EnsureCreated creates the schema without running migrations (InMemory-compatible).
        // CancellationToken.None — we must not cancel this even if host startup times out.
        var db = sp.GetRequiredService<OpenGateDbContext>();
        await db.Database.EnsureCreatedAsync(CancellationToken.None);

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<OpenGate.Data.EFCore.Entities.OpenGateUser>>();
        var appMgr   = sp.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeMgr = sp.GetRequiredService<IOpenIddictScopeManager>();

        await SeedDemoUserAsync(userManager);
        await SeedAdminRolesAsync(roleManager, userManager);
        await SeedAdminDataAsync(db, userManager);

        // ── Scopes ────────────────────────────────────────────────────────────
        // OpenIddict validates scope references when creating applications.
        // Register the scopes first so application creation doesn't fail.
        if (await scopeMgr.FindByNameAsync("api", CancellationToken.None) is null)
        {
            await scopeMgr.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name        = "api",
                DisplayName = "API access",
                Resources   = { "resource_server" }
            }, CancellationToken.None);
        }

        // ── Machine client — Client Credentials ───────────────────────────────
        if (await appMgr.FindByClientIdAsync(MachineClientId, CancellationToken.None) is null)
        {
            await appMgr.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId     = MachineClientId,
                ClientSecret = MachineClientSecret,
                ClientType   = ClientTypes.Confidential,
                ConsentType  = ConsentTypes.Implicit,
                Permissions  =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    $"{Permissions.Prefixes.Scope}api"
                }
            }, CancellationToken.None);
        }

        // ── Interactive client — Authorization Code + PKCE ────────────────────
        if (await appMgr.FindByClientIdAsync(InteractiveClientId, CancellationToken.None) is null)
        {
            await appMgr.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId    = InteractiveClientId,
                ClientType  = ClientTypes.Public,
                ConsentType = ConsentTypes.Explicit,
                RedirectUris = { new Uri("http://localhost/callback") },
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
                    $"{Permissions.Prefixes.Scope}openid"
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange }
            }, CancellationToken.None);
        }
    }

    private static async Task SeedDemoUserAsync(UserManager<OpenGate.Data.EFCore.Entities.OpenGateUser> userManager)
    {
        if (await userManager.FindByEmailAsync(DemoEmail) is not null)
            return;

        var user = new OpenGate.Data.EFCore.Entities.OpenGateUser
        {
            UserName = DemoEmail,
            Email = DemoEmail,
            EmailConfirmed = true,
            Profile = new OpenGate.Data.EFCore.Entities.UserProfile
            {
                FirstName = "Demo",
                LastName = "User",
                DisplayName = "Demo User"
            }
        };

        await userManager.CreateAsync(user, DemoPassword);
    }

    private static async Task SeedAdminRolesAsync(
        RoleManager<IdentityRole> roleManager,
        UserManager<OpenGate.Data.EFCore.Entities.OpenGateUser> userManager)
    {
        foreach (var roleName in new[] { "SuperAdmin", "Admin", "Viewer" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        var demoUser = await userManager.FindByEmailAsync(DemoEmail);
        if (demoUser is not null && !await userManager.IsInRoleAsync(demoUser, "SuperAdmin"))
        {
            await userManager.AddToRoleAsync(demoUser, "SuperAdmin");
        }
    }

    private static async Task SeedAdminDataAsync(
        OpenGateDbContext db,
        UserManager<OpenGate.Data.EFCore.Entities.OpenGateUser> userManager)
    {
        var demoUser = await userManager.FindByEmailAsync(DemoEmail);
        if (demoUser is null)
            return;

        if (!await db.UserSessions.AnyAsync(s => s.UserId == demoUser.Id, CancellationToken.None))
        {
            db.UserSessions.Add(new OpenGate.Data.EFCore.Entities.UserSession
            {
                Id = Guid.NewGuid(),
                UserId = demoUser.Id,
                ClientId = InteractiveClientId,
                IpAddress = "127.0.0.1",
                UserAgent = "IntegrationTests",
                DeviceInfo = "Integration Browser Session",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
            });
        }

        if (!await db.AuditLogs.AnyAsync(a => a.UserId == demoUser.Id && a.EventType == "Admin.Seed", CancellationToken.None))
        {
            db.AuditLogs.Add(new OpenGate.Data.EFCore.Entities.AuditLog
            {
                UserId = demoUser.Id,
                EventType = "Admin.Seed",
                ClientId = InteractiveClientId,
                Succeeded = true,
                Details = "{\"source\":\"integration-seed\"}",
                OccurredAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }
}

