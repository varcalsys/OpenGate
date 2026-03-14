using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Admin.Api.Contracts;
using OpenGate.Data.EFCore;
using OpenGate.Migration;
using OpenGate.Server.Extensions;
using OpenGate.Server.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenGate.Server.Tests;

public sealed class MigrationCliTests
{
    [Fact]
    public void TryParse_Accepts_Migrate_Command_And_Defaults_To_DryRun()
    {
        var parsed = MigrationCli.TryParse(
            ["migrate", "--source", "opengate-json", "--provider", "sqlite", "--connection-string", "Data Source=test.db", "--input", "config.json"],
            out var command,
            out var message,
            out var showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Equal(string.Empty, message);
        Assert.NotNull(command);
        Assert.True(command!.DryRun);
        Assert.Equal("sqlite", command.Provider);
    }

    [Fact]
    public void TryParse_Accepts_OutputPlan_And_Normalizes_Path()
    {
        var parsed = MigrationCli.TryParse(
            ["migrate", "--source", "opengate-json", "--provider", "sqlite", "--connection-string", "Data Source=test.db", "--input", "config.json", "--output-plan", ".\\artifacts\\plan.json", "--apply"],
            out var command,
            out var message,
            out var showHelp);

        Assert.True(parsed);
        Assert.False(showHelp);
        Assert.Equal(string.Empty, message);
        Assert.NotNull(command);
        Assert.False(command!.DryRun);
        Assert.Equal(Path.GetFullPath(@".\artifacts\plan.json"), command.OutputPlanPath);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Friendly_Message_For_Unknown_Source()
    {
        var jsonPath = await WriteDocumentAsync(CreateDocument());

        try
        {
            var result = await MigrationCli.ExecuteAsync(
                new MigrationCommand("legacy", "sqlite", "Data Source=ignored.db", jsonPath, DryRun: true),
                CancellationToken.None);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("não reconhecido", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_Reports_Create_Plan_For_Duende_Document()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"opengate-cli-duende-dryrun-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var jsonPath = await WriteTextAsync(CreateDuendeJsonFixture());

        try
        {
            await InitializeSqliteDatabaseAsync(connectionString);

            var result = await MigrationCli.ExecuteAsync(
                new MigrationCommand("duende", "sqlite", connectionString, jsonPath, DryRun: true),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.OutputLines, line => line == "Source: duende");
            Assert.Contains(result.OutputLines, line => line == "Scopes: criar=2, atualizar=0");
            Assert.Contains(result.OutputLines, line => line == "Clients: criar=2, atualizar=0");
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Apply_Imports_Duende_Configuration_Into_Sqlite_Database()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"opengate-cli-duende-apply-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var jsonPath = await WriteTextAsync(CreateDuendeJsonFixture());

        try
        {
            var result = await MigrationCli.ExecuteAsync(
                new MigrationCommand("duende", "sqlite", connectionString, jsonPath, DryRun: false),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.OutputLines, line => line == "Source: duende");
            Assert.Contains(result.OutputLines, line => line == "Execução concluída com sucesso.");

            await using var provider = CreateSqliteProvider(connectionString);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
            var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

            var spaClient = await applicationManager.FindByClientIdAsync("duende-spa", CancellationToken.None);
            var machineClient = await applicationManager.FindByClientIdAsync("duende-machine", CancellationToken.None);
            var apiScope = await scopeManager.FindByNameAsync("orders-api", CancellationToken.None);
            var customIdentityScope = await scopeManager.FindByNameAsync("tenant", CancellationToken.None);

            Assert.NotNull(spaClient);
            Assert.NotNull(machineClient);
            Assert.NotNull(apiScope);
            Assert.NotNull(customIdentityScope);

            var spaDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(spaDescriptor, spaClient!, CancellationToken.None);
            var machineDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(machineDescriptor, machineClient!, CancellationToken.None);
            var apiScopeDescriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(apiScopeDescriptor, apiScope!, CancellationToken.None);

            Assert.Equal(ClientTypes.Public, spaDescriptor.ClientType);
            Assert.Equal(ConsentTypes.Explicit, spaDescriptor.ConsentType);
            Assert.Contains(Requirements.Features.ProofKeyForCodeExchange, spaDescriptor.Requirements);
            Assert.Contains(Permissions.Endpoints.EndSession, spaDescriptor.Permissions);
            Assert.Contains($"{Permissions.Prefixes.Scope}orders-api", spaDescriptor.Permissions);
            Assert.Contains($"{Permissions.Prefixes.Scope}tenant", spaDescriptor.Permissions);

            Assert.Equal(ClientTypes.Confidential, machineDescriptor.ClientType);
            Assert.Contains(Permissions.GrantTypes.ClientCredentials, machineDescriptor.Permissions);
            Assert.Contains($"{Permissions.Prefixes.Scope}orders-api", machineDescriptor.Permissions);

            Assert.Equal("Orders API", apiScopeDescriptor.DisplayName);
            Assert.Contains("orders_resource", apiScopeDescriptor.Resources);

            var auditLog = (await db.AuditLogs
                    .Where(log => log.EventType == "Migration.ConfigurationImported")
                    .ToListAsync())
                .OrderByDescending(log => log.OccurredAt)
                .FirstOrDefault();

            Assert.NotNull(auditLog);
            Assert.Contains("duende-spa", auditLog!.Details ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("duende-machine", auditLog.Details ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_Writes_OutputPlan_With_Warnings_And_Notes()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"opengate-cli-plan-dryrun-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var jsonPath = await WriteTextAsync(CreateDuendeJsonFixture());
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"opengate-cli-plan-{Guid.NewGuid():N}");
        var outputPlanPath = Path.Combine(outputDirectory, "migration-plan.json");

        try
        {
            await InitializeSqliteDatabaseAsync(connectionString);

            var result = await MigrationCli.ExecuteAsync(
                new MigrationCommand("duende", "sqlite", connectionString, jsonPath, DryRun: true, OutputPlanPath: outputPlanPath),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.OutputLines, line => line == $"Plano JSON: {outputPlanPath}");
            Assert.Contains(result.OutputLines, line => line.StartsWith("Warnings: ", StringComparison.Ordinal));
            Assert.True(File.Exists(outputPlanPath));

            var plan = JsonSerializer.Deserialize<MigrationPlanArtifact>(await File.ReadAllTextAsync(outputPlanPath));
            Assert.NotNull(plan);
            Assert.Equal("duende", plan!.Source);
            Assert.Equal("dry-run", plan.Mode);
            Assert.Equal(outputPlanPath, plan.OutputPlanPath);
            Assert.Contains("orders-api", plan.ScopeNames);
            Assert.Contains("duende-spa", plan.ClientIds);
            Assert.Contains("orders-api", plan.ScopeCreateNames);
            Assert.Contains("tenant", plan.ScopeCreateNames);
            Assert.Empty(plan.ScopeUpdateNames);
            Assert.Contains("duende-machine", plan.ClientCreateIds);
            Assert.Contains("duende-spa", plan.ClientCreateIds);
            Assert.Empty(plan.ClientUpdateIds);
            Assert.Contains(plan.Warnings, warning => warning.Contains("Built-in identity resources", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(plan.Warnings, warning => warning.Contains("primeiro clientSecret", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(plan.Notes, note => note.Contains("Converted from Duende IdentityServer", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(jsonPath);
            if (File.Exists(outputPlanPath)) File.Delete(outputPlanPath);
            if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_Writes_OutputPlan_With_Entity_Diff_For_Create_And_Update()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"opengate-cli-plan-diff-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var seedJsonPath = await WriteDocumentAsync(CreateDocument());
        var jsonPath = await WriteDocumentAsync(CreateDocumentWithMixedCreateAndUpdate());
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"opengate-cli-plan-mixed-{Guid.NewGuid():N}");
        var outputPlanPath = Path.Combine(outputDirectory, "migration-plan.json");

        try
        {
            var seedResult = await MigrationCli.ExecuteAsync(
                new MigrationCommand("opengate-json", "sqlite", connectionString, seedJsonPath, DryRun: false),
                CancellationToken.None);

            Assert.Equal(0, seedResult.ExitCode);

            var result = await MigrationCli.ExecuteAsync(
                new MigrationCommand("opengate-json", "sqlite", connectionString, jsonPath, DryRun: true, OutputPlanPath: outputPlanPath),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.OutputLines, line => line == "Scopes: criar=1, atualizar=1");
            Assert.Contains(result.OutputLines, line => line == "Clients: criar=1, atualizar=1");
            Assert.Contains(result.OutputLines, line => line == "Scopes a criar: billing-api");
            Assert.Contains(result.OutputLines, line => line == "Scopes a atualizar: orders-api");
            Assert.Contains(result.OutputLines, line => line == "Clients a criar: billing-cli");
            Assert.Contains(result.OutputLines, line => line == "Clients a atualizar: orders-cli");

            var plan = JsonSerializer.Deserialize<MigrationPlanArtifact>(await File.ReadAllTextAsync(outputPlanPath));
            Assert.NotNull(plan);
            Assert.Equal(["billing-api"], plan!.ScopeCreateNames);
            Assert.Equal(["orders-api"], plan.ScopeUpdateNames);
            Assert.Equal(["billing-cli"], plan.ClientCreateIds);
            Assert.Equal(["orders-cli"], plan.ClientUpdateIds);
        }
        finally
        {
            File.Delete(seedJsonPath);
            File.Delete(jsonPath);
            if (File.Exists(outputPlanPath)) File.Delete(outputPlanPath);
            if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_Reports_Create_Plan_For_IdentityServer4_Document()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"opengate-cli-is4-dryrun-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var jsonPath = await WriteTextAsync(CreateIdentityServer4JsonFixture());

        try
        {
            await InitializeSqliteDatabaseAsync(connectionString);

            var result = await MigrationCli.ExecuteAsync(
                new MigrationCommand("is4", "sqlite", connectionString, jsonPath, DryRun: true),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.OutputLines, line => line == "Source: is4");
            Assert.Contains(result.OutputLines, line => line == "Scopes: criar=2, atualizar=0");
            Assert.Contains(result.OutputLines, line => line == "Clients: criar=2, atualizar=0");
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Apply_Imports_IdentityServer4_Configuration_Into_Sqlite_Database()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"opengate-cli-is4-apply-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var jsonPath = await WriteTextAsync(CreateIdentityServer4JsonFixture());

        try
        {
            var result = await MigrationCli.ExecuteAsync(
                new MigrationCommand("is4", "sqlite", connectionString, jsonPath, DryRun: false),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.OutputLines, line => line == "Source: is4");
            Assert.Contains(result.OutputLines, line => line == "Execução concluída com sucesso.");

            await using var provider = CreateSqliteProvider(connectionString);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
            var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

            var spaClient = await applicationManager.FindByClientIdAsync("is4-spa", CancellationToken.None);
            var machineClient = await applicationManager.FindByClientIdAsync("is4-machine", CancellationToken.None);
            var apiScope = await scopeManager.FindByNameAsync("catalog-api", CancellationToken.None);
            var customIdentityScope = await scopeManager.FindByNameAsync("tenant", CancellationToken.None);

            Assert.NotNull(spaClient);
            Assert.NotNull(machineClient);
            Assert.NotNull(apiScope);
            Assert.NotNull(customIdentityScope);

            var spaDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(spaDescriptor, spaClient!, CancellationToken.None);
            var machineDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(machineDescriptor, machineClient!, CancellationToken.None);
            var apiScopeDescriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(apiScopeDescriptor, apiScope!, CancellationToken.None);

            Assert.Equal(ClientTypes.Public, spaDescriptor.ClientType);
            Assert.Contains(Requirements.Features.ProofKeyForCodeExchange, spaDescriptor.Requirements);
            Assert.Contains($"{Permissions.Prefixes.Scope}catalog-api", spaDescriptor.Permissions);
            Assert.Contains($"{Permissions.Prefixes.Scope}tenant", spaDescriptor.Permissions);

            Assert.Equal(ClientTypes.Confidential, machineDescriptor.ClientType);
            Assert.Contains(Permissions.GrantTypes.ClientCredentials, machineDescriptor.Permissions);
            Assert.Equal("Catalog API", apiScopeDescriptor.DisplayName);
            Assert.Contains("catalog_resource", apiScopeDescriptor.Resources);

            var auditLog = (await db.AuditLogs
                    .Where(log => log.EventType == "Migration.ConfigurationImported")
                    .ToListAsync())
                .OrderByDescending(log => log.OccurredAt)
                .FirstOrDefault();

            Assert.NotNull(auditLog);
            Assert.Contains("is4-spa", auditLog!.Details ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("is4-machine", auditLog.Details ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_Reports_Create_Plan_For_Empty_Target_Database()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"opengate-cli-dryrun-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var jsonPath = await WriteDocumentAsync(CreateDocument());

        try
        {
            await InitializeSqliteDatabaseAsync(connectionString);

            var result = await MigrationCli.ExecuteAsync(
                new MigrationCommand("opengate-json", "sqlite", connectionString, jsonPath, DryRun: true),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.OutputLines, line => line == "Modo: dry-run");
            Assert.Contains(result.OutputLines, line => line == "Scopes: criar=1, atualizar=0");
            Assert.Contains(result.OutputLines, line => line == "Clients: criar=1, atualizar=0");
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Apply_Imports_Configuration_Into_Sqlite_Database()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"opengate-cli-apply-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var jsonPath = await WriteDocumentAsync(CreateDocument());

        try
        {
            var result = await MigrationCli.ExecuteAsync(
                new MigrationCommand("opengate-json", "sqlite", connectionString, jsonPath, DryRun: false),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.OutputLines, line => line == "Execução concluída com sucesso.");

            await using var provider = CreateSqliteProvider(connectionString);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
            var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

            var application = await applicationManager.FindByClientIdAsync("orders-cli", CancellationToken.None);
            var importedScope = await scopeManager.FindByNameAsync("orders-api", CancellationToken.None);
            Assert.NotNull(application);
            Assert.NotNull(importedScope);

            var applicationDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(applicationDescriptor, application!, CancellationToken.None);
            var scopeDescriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(scopeDescriptor, importedScope!, CancellationToken.None);

            Assert.Equal("Orders CLI", applicationDescriptor.DisplayName);
            Assert.Contains("http://localhost/orders-cli/callback", applicationDescriptor.RedirectUris.Select(uri => uri.AbsoluteUri));
            Assert.Equal("Orders API", scopeDescriptor.DisplayName);
            Assert.Contains("orders_resource", scopeDescriptor.Resources);

            var auditLog = (await db.AuditLogs
                    .Where(log => log.EventType == "Migration.ConfigurationImported")
                    .ToListAsync())
                .OrderByDescending(log => log.OccurredAt)
                .FirstOrDefault();

            Assert.NotNull(auditLog);
            Assert.Contains("orders-cli", auditLog!.Details ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }

    private static AdminConfigurationDocument CreateDocument()
        => new()
        {
            Format = "opengate-admin-configuration",
            Version = 1,
            GeneratedAt = DateTimeOffset.UtcNow,
            Clients =
            [
                new AdminClientRequest
                {
                    ClientId = "orders-cli",
                    DisplayName = "Orders CLI",
                    ClientType = ClientTypes.Public,
                    ConsentType = ConsentTypes.Explicit,
                    RedirectUris = ["http://localhost/orders-cli/callback"],
                    Permissions =
                    [
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                        $"{Permissions.Prefixes.Scope}openid",
                        $"{Permissions.Prefixes.Scope}orders-api"
                    ],
                    Requirements = [Requirements.Features.ProofKeyForCodeExchange]
                }
            ],
            Scopes =
            [
                new AdminScopeRequest
                {
                    Name = "orders-api",
                    DisplayName = "Orders API",
                    Description = "Orders scope imported by CLI",
                    Resources = ["resource_server", "orders_resource"]
                }
            ]
        };

    private static AdminConfigurationDocument CreateDocumentWithMixedCreateAndUpdate()
        => new()
        {
            Format = "opengate-admin-configuration",
            Version = 1,
            GeneratedAt = DateTimeOffset.UtcNow,
            Notes = ["Mixed plan fixture"],
            Clients =
            [
                new AdminClientRequest
                {
                    ClientId = "orders-cli",
                    DisplayName = "Orders CLI Updated",
                    ClientType = ClientTypes.Public,
                    ConsentType = ConsentTypes.Explicit,
                    RedirectUris = ["http://localhost/orders-cli/callback"],
                    Permissions =
                    [
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                        $"{Permissions.Prefixes.Scope}openid",
                        $"{Permissions.Prefixes.Scope}orders-api"
                    ],
                    Requirements = [Requirements.Features.ProofKeyForCodeExchange]
                },
                new AdminClientRequest
                {
                    ClientId = "billing-cli",
                    DisplayName = "Billing CLI",
                    ClientType = ClientTypes.Public,
                    ConsentType = ConsentTypes.Explicit,
                    RedirectUris = ["http://localhost/billing-cli/callback"],
                    Permissions =
                    [
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                        $"{Permissions.Prefixes.Scope}openid",
                        $"{Permissions.Prefixes.Scope}billing-api"
                    ],
                    Requirements = [Requirements.Features.ProofKeyForCodeExchange]
                }
            ],
            Scopes =
            [
                new AdminScopeRequest
                {
                    Name = "orders-api",
                    DisplayName = "Orders API Updated",
                    Description = "Orders scope updated by CLI",
                    Resources = ["resource_server", "orders_resource"]
                },
                new AdminScopeRequest
                {
                    Name = "billing-api",
                    DisplayName = "Billing API",
                    Description = "Billing scope imported by CLI",
                    Resources = ["billing_resource"]
                }
            ]
        };

    private static async Task<string> WriteDocumentAsync(AdminConfigurationDocument document)
    {
        var path = Path.Combine(Path.GetTempPath(), $"opengate-cli-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(document));
        return path;
    }

    private static async Task<string> WriteTextAsync(string value)
    {
        var path = Path.Combine(Path.GetTempPath(), $"opengate-cli-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, value);
        return path;
    }

    private static string CreateDuendeJsonFixture()
        => """
        {
          "identityServer": {
            "clients": [
              {
                "clientId": "duende-spa",
                "clientName": "Duende SPA",
                "requireClientSecret": false,
                "requireConsent": true,
                "requirePkce": true,
                "allowedGrantTypes": ["authorization_code", "refresh_token"],
                "redirectUris": ["http://localhost:5003/signin-oidc"],
                "postLogoutRedirectUris": ["http://localhost:5003/signout-callback-oidc"],
                "allowedScopes": ["openid", "profile", "orders-api", "tenant"]
              },
              {
                "clientId": "duende-machine",
                "clientName": "Duende Machine",
                "requireClientSecret": true,
                "allowedGrantTypes": ["client_credentials"],
                "allowedScopes": ["orders-api"],
                "clientSecrets": [{ "value": "machine-secret" }, { "value": "machine-secret-2" }]
              }
            ],
            "apiScopes": [
              {
                "name": "orders-api",
                "displayName": "Orders API",
                "description": "Orders access"
              }
            ],
            "apiResources": [
              {
                "name": "orders_resource",
                "displayName": "Orders Resource",
                "scopes": ["orders-api"]
              }
            ],
            "identityResources": [
              {
                "name": "openid",
                "displayName": "OpenID"
              },
              {
                "name": "profile",
                "displayName": "Profile"
              },
              {
                "name": "tenant",
                "displayName": "Tenant",
                "description": "Current tenant"
              }
            ]
          }
        }
        """;

    private static string CreateIdentityServer4JsonFixture()
        => """
        {
          "clients": [
            {
              "clientId": "is4-spa",
              "clientName": "IS4 SPA",
              "requireClientSecret": false,
              "requireConsent": true,
              "requirePkce": true,
              "allowedGrantTypes": ["authorization_code", "refresh_token"],
              "redirectUris": ["http://localhost:6001/signin-oidc"],
              "postLogoutRedirectUris": ["http://localhost:6001/signout-callback-oidc"],
              "allowedScopes": ["openid", "profile", "catalog-api", "tenant"]
            },
            {
              "clientId": "is4-machine",
              "clientName": "IS4 Machine",
              "requireClientSecret": true,
              "allowedGrantTypes": ["client_credentials"],
              "allowedScopes": ["catalog-api"],
              "clientSecrets": [{ "value": "is4-machine-secret" }]
            }
          ],
          "apiScopes": [
            {
              "name": "catalog-api",
              "displayName": "Catalog API",
              "description": "Catalog access"
            }
          ],
          "apiResources": [
            {
              "name": "catalog_resource",
              "displayName": "Catalog Resource",
              "scopes": ["catalog-api"]
            }
          ],
          "identityResources": [
            {
              "name": "openid",
              "displayName": "OpenID"
            },
            {
              "name": "profile",
              "displayName": "Profile"
            },
            {
              "name": "tenant",
              "displayName": "Tenant",
              "description": "Current tenant"
            }
          ]
        }
        """;

    private static async Task InitializeSqliteDatabaseAsync(string connectionString)
    {
        await using var provider = CreateSqliteProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
        await db.Database.MigrateAsync();
    }

    private static ServiceProvider CreateSqliteProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenGate(options => options.SecurityPreset = OpenGateSecurityPreset.Development)
            .UseSqlite(connectionString)
            .Build();

        return services.BuildServiceProvider();
    }
}