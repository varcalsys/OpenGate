using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Data.EFCore;
using OpenGate.Data.EFCore.Entities;
using OpenGate.Data.EFCore.Extensions;
using OpenGate.Server.Options;
using OpenIddict.EntityFrameworkCore;

namespace OpenGate.Server;

/// <summary>
/// Fluent builder returned by <c>services.AddOpenGate()</c>.
/// Use it to configure the database provider and chain additional setup.
/// </summary>
public class OpenGateBuilder<TContext>
    where TContext : DbContext
{
    private readonly IServiceCollection _services;
    private readonly OpenGateOptions _options;

    internal OpenGateBuilder(IServiceCollection services, OpenGateOptions options)
    {
        _services = services;
        _options = options;
    }

    /// <summary>Exposes the underlying <see cref="IServiceCollection"/>.</summary>
    public IServiceCollection Services => _services;

    /// <summary>Exposes the resolved <see cref="OpenGateOptions"/>.</summary>
    public OpenGateOptions Options => _options;

    // ── Database providers ────────────────────────────────────────────────────

    /// <summary>
    /// Configures OpenGate to use SQL Server as the backing store.
    /// </summary>
    /// <param name="connectionString">SQL Server connection string.</param>
    public OpenGateBuilder<TContext> UseSqlServer(string connectionString, string schema = "opengate")
    {
        // Allow empty string — the SQL Server provider only validates the connection string
        // when a connection is actually attempted.  In tests the DbContext is replaced with
        // an InMemory provider before any connection is made, so an empty string is fine.
        ArgumentNullException.ThrowIfNull(connectionString);

        _options.ConfigureDatabase = builder =>
            builder.UseSqlServer(connectionString, sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
                   // Ensure OpenIddict EF Core entities are added to the model.
                   .UseOpenIddict();

        return this;
    }

    /// <summary>
    /// Configures OpenGate to use PostgreSQL as the backing store.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    public OpenGateBuilder<TContext> UsePostgreSql(string connectionString, string schema = "opengate")
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        _options.ConfigureDatabase = builder =>
            builder.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema)
                          .MigrationsAssembly("OpenGate.Data.EFCore.Migrations.PostgreSql"))
                   .UseOpenIddict();

        return this;
    }

    /// <summary>
    /// Configures OpenGate to use SQLite as the backing store.
    /// </summary>
    /// <param name="connectionString">SQLite connection string.</param>
    public OpenGateBuilder<TContext> UseSqlite(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        _options.ConfigureDatabase = builder =>
            builder.UseSqlite(connectionString, sqlite =>
                    sqlite.MigrationsAssembly("OpenGate.Data.EFCore.Migrations.Sqlite"))
                   .UseOpenIddict();

        return this;
    }

    /// <summary>
    /// Configures OpenGate to use an existing <see cref="DbContextOptionsBuilder"/> action.
    /// Use this when you need a provider not covered by the named overloads
    /// (e.g. Npgsql, SQLite).
    /// </summary>
    /// <param name="optionsAction">Action that configures the <see cref="DbContextOptionsBuilder"/>.</param>
    public OpenGateBuilder<TContext> UseDatabase(Action<DbContextOptionsBuilder> optionsAction)
    {
        ArgumentNullException.ThrowIfNull(optionsAction);
        _options.ConfigureDatabase = builder =>
        {
            optionsAction(builder);
            // Ensure OpenIddict EF Core entities are added to the model.
            builder.UseOpenIddict();
        };
        return this;
    }

    // ── Security ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Overrides the security preset for this instance.
    /// </summary>
    public OpenGateBuilder<TContext> WithPreset(OpenGateSecurityPreset preset)
    {
        _options.SecurityPreset = preset;
        return this;
    }

    /// <summary>
    /// Sets the issuer URI advertised in the OpenID Connect discovery document.
    /// </summary>
    public OpenGateBuilder<TContext> WithIssuer(Uri issuerUri)
    {
        ArgumentNullException.ThrowIfNull(issuerUri);
        _options.IssuerUri = issuerUri;
        return this;
    }

    /// <summary>
    /// Sets the issuer URI from a string.
    /// </summary>
    public OpenGateBuilder<TContext> WithIssuer(string issuerUri)
        => WithIssuer(new Uri(issuerUri, UriKind.Absolute));

    // ── Token lifetimes ───────────────────────────────────────────────────────

    /// <summary>
    /// Overrides the access token lifetime (default set by the active security preset).
    /// </summary>
    public OpenGateBuilder<TContext> WithAccessTokenLifetime(TimeSpan lifetime)
    {
        _options.AccessTokenLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Overrides the refresh token lifetime (default set by the active security preset).
    /// </summary>
    public OpenGateBuilder<TContext> WithRefreshTokenLifetime(TimeSpan lifetime)
    {
        _options.RefreshTokenLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Validates the configuration and registers the OpenGate data services into DI.
    /// Call this after configuring the database provider.
    /// </summary>
    public OpenGateBuilder<TContext> Build()
        => Build<OpenGateUser, IdentityRole>(configureIdentity: null);

    /// <summary>
    /// Validates the configuration and registers the OpenGate data services into DI.
    /// Call this after configuring the database provider.
    /// </summary>
    public OpenGateBuilder<TContext> Build(Action<IdentityOptions>? configureIdentity)
        => Build<OpenGateUser, IdentityRole>(configureIdentity);

    /// <summary>
    /// Validates the configuration and registers OpenGate data/Identity services
    /// using custom ASP.NET Core Identity user and role types.
    /// </summary>
    public OpenGateBuilder<TContext> Build<TUser, TRole>()
        where TUser : class
        where TRole : class
        => Build<TUser, TRole>(configureIdentity: null);

    /// <summary>
    /// Validates the configuration and registers OpenGate data/Identity services
    /// using custom ASP.NET Core Identity user and role types.
    /// </summary>
    public OpenGateBuilder<TContext> Build<TUser, TRole>(Action<IdentityOptions>? configureIdentity)
        where TUser : class
        where TRole : class
    {
        if (_options.ConfigureDatabase is null)
        {
            throw new InvalidOperationException(
                "A database provider must be configured. " +
                "Call builder.UseSqlServer(...), builder.UsePostgreSql(...), builder.UseSqlite(...) " +
                "or builder.UseDatabase(...) before building.");
        }

        // AddSignInManager() must be called here (in OpenGate.Server which has the
        // full ASP.NET Core FrameworkReference) because OpenGate.Data.EFCore only
        // references Microsoft.AspNetCore.Identity.EntityFrameworkCore which does NOT
        // include SignInManager or the Identity cookie schemes.
        //
        // AddSignInManager() — registers SignInManager<TUser> in DI.
        // AddAuthentication + AddIdentityCookies — add the cookie auth handlers that
        // SignInManager requires to persist login sessions.  The Identity application
        // scheme is set as the default challenge so that OpenIddict redirects
        // unauthenticated users to the Login Razor Page.
        _services.AddOpenGateData<TContext, TUser, TRole>(_options.ConfigureDatabase, configureIdentity)
                 .AddSignInManager();

        _services
            .AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                o.DefaultChallengeScheme    = IdentityConstants.ApplicationScheme;
                o.DefaultSignInScheme       = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        return this;
    }
}

/// <summary>
/// Default OpenGate builder bound to <see cref="OpenGateDbContext"/>.
/// </summary>
public sealed class OpenGateBuilder : OpenGateBuilder<OpenGateDbContext>
{
    internal OpenGateBuilder(IServiceCollection services, OpenGateOptions options)
        : base(services, options)
    {
    }
}
