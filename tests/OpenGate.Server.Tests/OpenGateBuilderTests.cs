using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Server;
using OpenGate.Server.Extensions;
using OpenGate.Server.Options;

namespace OpenGate.Server.Tests;

/// <summary>
/// Unit tests for <see cref="OpenGateBuilder"/> and
/// <see cref="OpenGateServiceCollectionExtensions.AddOpenGate"/>.
/// </summary>
public sealed class OpenGateBuilderTests
{
    // ── AddOpenGate registration ──────────────────────────────────────────────

    [Fact]
    public void AddOpenGate_Returns_OpenGateBuilder()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate();

        Assert.NotNull(builder);
        Assert.IsType<OpenGateBuilder>(builder);
    }

    [Fact]
    public void OpenGateOptions_Defaults_ApiScopeName_To_Api()
    {
        var options = new OpenGateOptions();

        Assert.Equal("api", options.ApiScopeName);
    }

    [Fact]
    public void AddOpenGate_Registers_OpenGateOptions_In_DI()
    {
        var services = new ServiceCollection();
        services.AddOpenGate(opt => opt.SecurityPreset = OpenGateSecurityPreset.Development);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<OpenGateOptions>();

        Assert.NotNull(options);
        Assert.Equal(OpenGateSecurityPreset.Development, options.SecurityPreset);
    }

    [Fact]
    public void AddOpenGate_Applies_Configure_Action_To_Options()
    {
        var services = new ServiceCollection();
        var issuer = new Uri("https://auth.example.com");

        services.AddOpenGate(opt =>
        {
            opt.SecurityPreset = OpenGateSecurityPreset.HighSecurity;
            opt.IssuerUri = issuer;
            opt.AccessTokenLifetime = TimeSpan.FromMinutes(15);
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<OpenGateOptions>();

        Assert.Equal(OpenGateSecurityPreset.HighSecurity, options.SecurityPreset);
        Assert.Equal(issuer, options.IssuerUri);
        Assert.Equal(TimeSpan.FromMinutes(15), options.AccessTokenLifetime);
    }

    // ── Fluent builder ────────────────────────────────────────────────────────

    [Fact]
    public void Builder_WithPreset_Changes_Preset_On_Options()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate()
            .WithPreset(OpenGateSecurityPreset.Development);

        Assert.Equal(OpenGateSecurityPreset.Development, builder.Options.SecurityPreset);
    }

    [Fact]
    public void Builder_WithIssuer_String_Sets_IssuerUri()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate()
            .WithIssuer("https://identity.mycompany.com");

        Assert.Equal(new Uri("https://identity.mycompany.com"), builder.Options.IssuerUri);
    }

    [Fact]
    public void Builder_WithIssuer_Uri_Sets_IssuerUri()
    {
        var uri = new Uri("https://identity.mycompany.com");
        var services = new ServiceCollection();
        var builder = services.AddOpenGate()
            .WithIssuer(uri);

        Assert.Equal(uri, builder.Options.IssuerUri);
    }

    [Fact]
    public void Builder_WithAccessTokenLifetime_Overrides_Default()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate()
            .WithAccessTokenLifetime(TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(30), builder.Options.AccessTokenLifetime);
    }

    [Fact]
    public void Builder_WithRefreshTokenLifetime_Overrides_Default()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate()
            .WithRefreshTokenLifetime(TimeSpan.FromDays(7));

        Assert.Equal(TimeSpan.FromDays(7), builder.Options.RefreshTokenLifetime);
    }

    [Fact]
    public void Builder_UseSqlServer_Sets_ConfigureDatabase()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate()
            .UseSqlServer("Server=localhost;Database=OpenGate;Trusted_Connection=True;");

        Assert.NotNull(builder.Options.ConfigureDatabase);
    }

    [Fact]
    public void Builder_UsePostgreSql_Sets_ConfigureDatabase()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate()
            .UsePostgreSql("Host=localhost;Database=opengate;Username=postgres;Password=postgres");

        Assert.NotNull(builder.Options.ConfigureDatabase);
    }

    [Fact]
    public void Builder_UseSqlite_Sets_ConfigureDatabase()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate()
            .UseSqlite("Data Source=opengate.db");

        Assert.NotNull(builder.Options.ConfigureDatabase);
    }

    [Fact]
    public void Builder_UseDatabase_Sets_ConfigureDatabase()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate()
            .UseDatabase(opt => opt.UseInMemoryDatabase("test"));

        Assert.NotNull(builder.Options.ConfigureDatabase);
    }

    [Fact]
    public void Builder_Build_Throws_When_No_Database_Configured()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Builder_Services_Returns_Service_Collection()
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenGate();

        Assert.Same(services, builder.Services);
    }
}
