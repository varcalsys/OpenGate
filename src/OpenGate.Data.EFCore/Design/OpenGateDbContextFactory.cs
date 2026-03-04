using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenIddict.EntityFrameworkCore;

namespace OpenGate.Data.EFCore.Design;

/// <summary>
/// Used by <c>dotnet ef</c> CLI at design time to create
/// an <see cref="OpenGateDbContext"/> without a running host.
/// Not included in runtime or published packages.
/// Provider can be selected with <c>--provider=sqlserver|postgresql|sqlite</c>
/// or environment variable <c>OPENGATE_EF_PROVIDER</c>.
/// </summary>
internal sealed class OpenGateDbContextFactory : IDesignTimeDbContextFactory<OpenGateDbContext>
{
    public OpenGateDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OpenGateDbContext>();
        var provider = ResolveProvider(args);

        ConfigureProvider(optionsBuilder, provider);

        // Ensure OpenIddict EF Core entities are included in the model when generating migrations.
        optionsBuilder.UseOpenIddict();

        return new OpenGateDbContext(optionsBuilder.Options);
    }

    private static string ResolveProvider(string[] args)
    {
        var fromArgs = args.FirstOrDefault(a => a.StartsWith("--provider=", StringComparison.OrdinalIgnoreCase));
        if (fromArgs is not null)
            return fromArgs["--provider=".Length..].Trim().ToLowerInvariant();

        var fromEnv = Environment.GetEnvironmentVariable("OPENGATE_EF_PROVIDER");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim().ToLowerInvariant();

        return "sqlserver";
    }

    private static void ConfigureProvider(DbContextOptionsBuilder optionsBuilder, string provider)
    {
        switch (provider)
        {
            case "postgres":
            case "postgresql":
            case "npgsql":
            {
                var connection = Environment.GetEnvironmentVariable("OPENGATE_EF_CONNECTION")
                    ?? "Host=localhost;Port=5432;Database=OpenGate_Design;Username=postgres;Password=postgres";

                optionsBuilder.UseNpgsql(connection, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "opengate"));
                break;
            }

            case "sqlite":
            {
                var connection = Environment.GetEnvironmentVariable("OPENGATE_EF_CONNECTION")
                    ?? "Data Source=OpenGate.Design.db";

                optionsBuilder.UseSqlite(connection);
                break;
            }

            default:
            {
                // Design-time only: placeholder connection string.
                // Actual connection strings are provided via appsettings / environment variables at runtime.
                var connection = Environment.GetEnvironmentVariable("OPENGATE_EF_CONNECTION")
                    ?? "Server=(localdb)\\mssqllocaldb;Database=OpenGate_Design;Trusted_Connection=True;";

                optionsBuilder.UseSqlServer(connection, sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "opengate"));
                break;
            }
        }
    }
}
