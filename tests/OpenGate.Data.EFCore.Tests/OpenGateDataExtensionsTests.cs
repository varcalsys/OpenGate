using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Data.EFCore;
using OpenGate.Data.EFCore.Entities;
using OpenGate.Data.EFCore.Extensions;

namespace OpenGate.Data.EFCore.Tests;

public sealed class OpenGateDataExtensionsTests
{
    // ── AddOpenGateData(services, optionsAction) ──────────────────────────────

    [Fact]
    public void AddOpenGateData_Simple_Registers_UserManager()
    {
        var services = new ServiceCollection();

        services.AddOpenGateData(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<UserManager<OpenGateUser>>());
    }

    // ── AddOpenGateData<TContext>(services) — pre-configured context ──────────

    [Fact]
    public void AddOpenGateData_PreConfigured_Registers_UserManager()
    {
        var services = new ServiceCollection();

        // Register the context independently so the pre-configured overload can use it.
        services.AddDbContext<OpenGateDbContext>(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddOpenGateData<OpenGateDbContext>();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<UserManager<OpenGateUser>>());
    }

    // ── AddOpenGateData<TContext, TUser, TRole>(services, configureIdentity) ──

    private sealed class CustomUser : IdentityUser { }
    private sealed class CustomRole : IdentityRole { }

    private sealed class CustomDbContext(DbContextOptions<CustomDbContext> options)
        : IdentityDbContext<CustomUser, CustomRole, string>(options);

    [Fact]
    public void AddOpenGateData_PreConfigured_Custom_Types_Registers_Managers()
    {
        var services = new ServiceCollection();

        services.AddDbContext<CustomDbContext>(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddOpenGateData<CustomDbContext, CustomUser, CustomRole>();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<UserManager<CustomUser>>());
        Assert.NotNull(provider.GetService<RoleManager<CustomRole>>());
    }

    [Fact]
    public void AddOpenGateData_PreConfigured_Custom_Types_Applies_IdentityOptions()
    {
        var services = new ServiceCollection();

        services.AddDbContext<CustomDbContext>(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddOpenGateData<CustomDbContext, CustomUser, CustomRole>(
            configureIdentity: opt => opt.Password.RequiredLength = 20);

        var provider = services.BuildServiceProvider();
        var identityOptions = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value;

        Assert.Equal(20, identityOptions.Password.RequiredLength);
    }

    // ── Identity defaults applied by all overloads ────────────────────────────

    [Fact]
    public void AddOpenGateData_Applies_Default_Password_Policy()
    {
        var services = new ServiceCollection();

        services.AddOpenGateData(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var identityOptions = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value;

        Assert.True(identityOptions.Password.RequireDigit);
        Assert.True(identityOptions.Password.RequireUppercase);
        Assert.True(identityOptions.Password.RequireNonAlphanumeric);
        Assert.Equal(12, identityOptions.Password.RequiredLength);
    }

    [Fact]
    public void AddOpenGateData_Applies_Default_Lockout_Policy()
    {
        var services = new ServiceCollection();

        services.AddOpenGateData(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var identityOptions = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value;

        Assert.Equal(5, identityOptions.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), identityOptions.Lockout.DefaultLockoutTimeSpan);
        Assert.True(identityOptions.Lockout.AllowedForNewUsers);
    }
}
