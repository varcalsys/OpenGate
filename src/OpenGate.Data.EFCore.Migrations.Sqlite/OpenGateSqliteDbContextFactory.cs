using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenGate.Data.EFCore;
using OpenIddict.EntityFrameworkCore;

namespace OpenGate.Data.EFCore.Migrations.Sqlite;

internal sealed class OpenGateSqliteDbContextFactory : IDesignTimeDbContextFactory<OpenGateDbContext>
{
    public OpenGateDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OpenGateDbContext>();

        var connection = Environment.GetEnvironmentVariable("OPENGATE_EF_CONNECTION")
            ?? "Data Source=OpenGate.Design.db";

        optionsBuilder.UseSqlite(connection, sqlite =>
        {
            sqlite.MigrationsAssembly(typeof(OpenGateSqliteDbContextFactory).Assembly.FullName);
        });

        optionsBuilder.UseOpenIddict();

        return new OpenGateDbContext(optionsBuilder.Options);
    }
}
