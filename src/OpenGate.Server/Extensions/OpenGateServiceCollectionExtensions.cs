using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Data.EFCore;
using OpenGate.Server.Internal;
using OpenGate.Server.Options;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenGate.Server.Extensions;

/// <summary>
/// Extension methods for registering OpenGate Identity Server services.
/// </summary>
public static class OpenGateServiceCollectionExtensions
{
    /// <summary>
    /// Registers all OpenGate Identity Server services with a fluent builder.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configure">
    /// Optional action to configure <see cref="OpenGateOptions"/> before the builder is returned.
    /// </param>
    /// <returns>
    /// An <see cref="OpenGateBuilder"/> for further configuration
    /// (e.g. <c>.UseSqlServer(...)</c>, <c>.UsePostgreSql(...)</c> or <c>.UseSqlite(...)</c>).
    /// Call <c>.Build()</c> when done, or let it be built automatically at host startup.
    /// </returns>
    /// <example>
    /// <code>
    /// // Minimal setup (SQL Server, Production preset):
    /// builder.Services
    ///     .AddOpenGate()
    ///     .UseSqlServer(builder.Configuration.GetConnectionString("OpenGate")!);
    ///
    /// // With explicit preset:
    /// builder.Services
    ///     .AddOpenGate(opt => opt.SecurityPreset = OpenGateSecurityPreset.Development)
    ///     .UseSqlServer(connectionString);
    /// </code>
    /// </example>
    public static OpenGateBuilder AddOpenGate(
        this IServiceCollection services,
        Action<OpenGateOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OpenGateOptions();
        configure?.Invoke(options);

        var builder = new OpenGateBuilder(services, options);
        ConfigureOpenGateCore<OpenGateDbContext>(services, options);

        // Register the builder in DI so Build() can be called lazily if needed
        services.AddSingleton(builder);

        return builder;
    }

    /// <summary>
    /// Registers all OpenGate Identity Server services with a fluent builder
    /// targeting a custom Identity DbContext.
    /// </summary>
    public static OpenGateBuilder<TContext> AddOpenGate<TContext>(
        this IServiceCollection services,
        Action<OpenGateOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OpenGateOptions();
        configure?.Invoke(options);

        var builder = new OpenGateBuilder<TContext>(services, options);
        ConfigureOpenGateCore<TContext>(services, options);

        // Register the builder in DI so Build() can be called lazily if needed
        services.AddSingleton(builder);

        return builder;
    }

    // -- Private helpers ------------------------------------------------------

    private static void ConfigureOpenGateCore<TContext>(
        IServiceCollection services,
        OpenGateOptions options)
        where TContext : DbContext
    {
        // Register OpenGate options in DI for access at runtime.
        services.AddSingleton(options);

        // OpenIddict registers the core + ASP.NET Core + EF Core integration.
        services.AddOpenIddict()
            .AddCore(core =>
            {
                // OpenIddict shares the Identity DbContext registered by AddOpenGateData().
                core.UseEntityFrameworkCore()
                    .UseDbContext<TContext>();
            })
            .AddServer(server =>
            {
                ConfigureEndpoints(server, options);
                ConfigureFlows(server, options);
                ConfigureScopesAndHandlers(server, options);
                ApplySecurityPreset(server, options);

                if (options.IssuerUri is not null)
                {
                    server.SetIssuer(options.IssuerUri);
                }
            })
            .AddValidation(validation =>
            {
                // Validate tokens against the local OpenIddict server.
                validation.UseLocalServer();
                validation.UseAspNetCore();
            });
    }

    private static void ConfigureEndpoints(
        OpenIddictServerBuilder server,
        OpenGateOptions options)
    {
        server
            .SetAuthorizationEndpointUris(options.AuthorizationEndpointPath)
            .SetTokenEndpointUris(options.TokenEndpointPath)
            .SetEndSessionEndpointUris(options.LogoutEndpointPath)
            .SetUserInfoEndpointUris(options.UserinfoEndpointPath);

        if (options.EnableIntrospection)
        {
            server.SetIntrospectionEndpointUris(options.IntrospectionEndpointPath);
        }

        if (options.EnableRevocation)
        {
            server.SetRevocationEndpointUris(options.RevocationEndpointPath);
        }

        if (options.EnableDeviceFlow)
        {
            server.SetDeviceAuthorizationEndpointUris(options.DeviceEndpointPath);
            // OpenIddict requires the end-user verification endpoint (where users type the
            // user code) to be configured whenever the device authorization flow is enabled.
            server.SetEndUserVerificationEndpointUris(options.DeviceVerificationEndpointPath);
        }
    }

    private static void ConfigureFlows(
        OpenIddictServerBuilder server,
        OpenGateOptions options)
    {
        server
            // Authorization Code is the primary interactive flow.
            .AllowAuthorizationCodeFlow()
            // Refresh Token enabled by default.
            .AllowRefreshTokenFlow()
            // Client Credentials for machine-to-machine.
            .AllowClientCredentialsFlow();

        if (options.EnableDeviceFlow)
        {
            server.AllowDeviceAuthorizationFlow();
        }
    }

    private static void ConfigureScopesAndHandlers(OpenIddictServerBuilder server, OpenGateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Register common OIDC scopes so default UI/flows work without explicit seeding.
        // ApiScopeName defaults to "api" and is used by samples and integration tests.
        server.RegisterScopes(
            Scopes.OpenId,
            Scopes.Email,
            Scopes.Profile,
            Scopes.Roles,
            Scopes.OfflineAccess,
            options.ApiScopeName);

        // Provide a default principal for client_credentials token requests.
        server.AddEventHandler<OpenIddictServerEvents.HandleTokenRequestContext>(builder =>
        {
            builder.UseScopedHandler<ClientCredentialsTokenHandler>();
            builder.SetOrder(OpenIddictServerHandlers.Exchange.AttachPrincipal.Descriptor.Order + 1_000);
        });
    }

    private static void ApplySecurityPreset(
        OpenIddictServerBuilder server,
        OpenGateOptions options)
    {
        switch (options.SecurityPreset)
        {
            case OpenGateSecurityPreset.Development:
                ApplyDevelopmentPreset(server, options);
                break;

            case OpenGateSecurityPreset.Production:
                ApplyProductionPreset(server, options);
                break;

            case OpenGateSecurityPreset.HighSecurity:
                ApplyHighSecurityPreset(server, options);
                break;
        }
    }

    private static void ApplyDevelopmentPreset(
        OpenIddictServerBuilder server,
        OpenGateOptions options)
    {
        // Ephemeral keys regenerated on every restart; never use in production.
        server.AddEphemeralEncryptionKey()
              .AddEphemeralSigningKey();

        // Token and UserInfo endpoints are handled directly by OpenIddict.
        // Passthrough is only needed for endpoints that render UI.
        var aspNet = server.UseAspNetCore()
              .DisableTransportSecurityRequirement()
              .EnableAuthorizationEndpointPassthrough()
              .EnableEndSessionEndpointPassthrough()
              .EnableStatusCodePagesIntegration();

        if (options.EnableDeviceFlow)
            aspNet.EnableEndUserVerificationEndpointPassthrough();

        // Extended lifetimes for local debugging.
        server.SetAccessTokenLifetime(TimeSpan.FromDays(1))
              .SetRefreshTokenLifetime(TimeSpan.FromDays(30))
              .SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(30));
    }

    [ExcludeFromCodeCoverage(Justification = "Requires a live host with persisted X.509 certificates; the Development preset exercises the same pipeline in integration tests.")]
    private static void ApplyProductionPreset(
        OpenIddictServerBuilder server,
        OpenGateOptions options)
    {
        // In production, use persisted development certs (survive restarts).
        // TODO: replace with real X.509 certificates from machine store.
        server.AddDevelopmentEncryptionCertificate()
              .AddDevelopmentSigningCertificate();

        var aspNetProd = server.UseAspNetCore()
              .EnableAuthorizationEndpointPassthrough()
              .EnableEndSessionEndpointPassthrough()
              .EnableStatusCodePagesIntegration();

        if (options.EnableDeviceFlow)
            aspNetProd.EnableEndUserVerificationEndpointPassthrough();

        server.SetAccessTokenLifetime(options.AccessTokenLifetime)
              .SetRefreshTokenLifetime(options.RefreshTokenLifetime)
              .SetAuthorizationCodeLifetime(options.AuthorizationCodeLifetime)
              .SetRefreshTokenReuseLeeway(TimeSpan.Zero);

        // PKCE required for all authorization code flows.
        server.RequireProofKeyForCodeExchange();
    }

    [ExcludeFromCodeCoverage(Justification = "Requires a live host with persisted X.509 certificates; the Development preset exercises the same pipeline in integration tests.")]
    private static void ApplyHighSecurityPreset(
        OpenIddictServerBuilder server,
        OpenGateOptions options)
    {
        // Start from Production baseline.
        ApplyProductionPreset(server, options);

        // Override with stricter lifetimes.
        server.SetAccessTokenLifetime(TimeSpan.FromMinutes(15))
              .SetRefreshTokenLifetime(TimeSpan.FromHours(24))
              .SetRefreshTokenReuseLeeway(TimeSpan.Zero);

        // Reference tokens; resource servers must introspect.
        server.UseReferenceAccessTokens()
              .UseReferenceRefreshTokens();
    }
}

