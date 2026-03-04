using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenGate.Data.EFCore;
using OpenIddict.EntityFrameworkCore;

namespace OpenGate.Data.EFCore.Migrations.PostgreSql;

internal sealed class OpenGatePostgreSqlDbContextFactory : IDesignTimeDbContextFactory<OpenGateDbContext>
{
    public OpenGateDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OpenGateDbContext>();

        var connection = Environment.GetEnvironmentVariable("OPENGATE_EF_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=OpenGate_Design;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connection, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(OpenGatePostgreSqlDbContextFactory).Assembly.FullName);
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "opengate");
        });

        optionsBuilder.UseOpenIddict();

        return new OpenGateDbContext(optionsBuilder.Options);
    }
}
