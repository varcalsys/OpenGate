using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Data.EFCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenGate.Server.Tests.Integration;

public sealed class AdminClientScopeApiTests(OpenGateWebFactory factory)
    : IClassFixture<OpenGateWebFactory>
{
    private static readonly string[] OrdersScopeResources = ["resource_server", "orders_api"];
    private static readonly string[] UpdatedOrdersScopeResources = ["resource_server", "orders_api_v2"];
    private static readonly string[] OrdersClientRedirectUris = ["http://localhost/orders/callback"];
    private static readonly string[] UpdatedOrdersClientRedirectUris = ["http://localhost/orders/callback-v2"];
    private static readonly string[] OrdersClientPostLogoutRedirectUris = ["http://localhost/orders/signout-callback"];
    private static readonly string[] OrdersClientPermissions =
    [
        Permissions.Endpoints.Authorization,
        Permissions.Endpoints.Token,
        Permissions.Endpoints.EndSession,
        Permissions.GrantTypes.AuthorizationCode,
        Permissions.GrantTypes.RefreshToken,
        Permissions.ResponseTypes.Code,
        Permissions.Scopes.Email,
        Permissions.Scopes.Profile,
        $"{Permissions.Prefixes.Scope}openid",
        $"{Permissions.Prefixes.Scope}orders-api"
    ];
    private static readonly string[] OrdersClientRequirements = [Requirements.Features.ProofKeyForCodeExchange];

    private readonly HttpClient _client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task AdminApi_Clients_Returns_Seeded_Applications()
    {
        await LoginAsAdminAsync();

        var response = await _client.GetAsync("/admin/api/clients");
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(json);

        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, item => item.GetProperty("clientId").GetString() == IntegrationSeedService.InteractiveClientId);
        Assert.Contains(items, item => item.GetProperty("clientId").GetString() == IntegrationSeedService.MachineClientId);
    }

    [Fact]
    public async Task AdminApi_Can_Create_Update_And_Delete_Clients_And_Scopes()
    {
        await LoginAsAdminAsync();

        var createScopeResponse = await _client.PostAsJsonAsync("/admin/api/scopes", new
        {
            name = "orders-api",
            displayName = "Orders API",
            description = "Orders scope",
            resources = OrdersScopeResources
        });

        Assert.Equal(HttpStatusCode.Created, createScopeResponse.StatusCode);

        var createClientResponse = await _client.PostAsJsonAsync("/admin/api/clients", new
        {
            clientId = "orders-admin",
            displayName = "Orders Admin",
            clientType = ClientTypes.Public,
            consentType = ConsentTypes.Explicit,
            redirectUris = OrdersClientRedirectUris,
            postLogoutRedirectUris = OrdersClientPostLogoutRedirectUris,
            permissions = OrdersClientPermissions,
            requirements = OrdersClientRequirements
        });

        Assert.Equal(HttpStatusCode.Created, createClientResponse.StatusCode);

        var updateClientResponse = await _client.PutAsJsonAsync("/admin/api/clients/orders-admin", new
        {
            displayName = "Orders Admin v2",
            redirectUris = UpdatedOrdersClientRedirectUris,
            permissions = OrdersClientPermissions,
            requirements = OrdersClientRequirements
        });

        var clientJson = await updateClientResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(HttpStatusCode.OK, updateClientResponse.StatusCode);
        Assert.NotNull(clientJson);
        Assert.Equal("Orders Admin v2", clientJson.RootElement.GetProperty("displayName").GetString());
        Assert.Contains(
            clientJson.RootElement.GetProperty("redirectUris").EnumerateArray().Select(item => item.GetString()),
            value => value == "http://localhost/orders/callback-v2");

        var updateScopeResponse = await _client.PutAsJsonAsync("/admin/api/scopes/orders-api", new
        {
            displayName = "Orders API v2",
            description = "Orders scope updated",
            resources = UpdatedOrdersScopeResources
        });

        var scopeJson = await updateScopeResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(HttpStatusCode.OK, updateScopeResponse.StatusCode);
        Assert.NotNull(scopeJson);
        Assert.Equal("Orders API v2", scopeJson.RootElement.GetProperty("displayName").GetString());

        var deleteClientResponse = await _client.DeleteAsync("/admin/api/clients/orders-admin");
        Assert.True(deleteClientResponse.IsSuccessStatusCode, await deleteClientResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, deleteClientResponse.StatusCode);

        var getDeletedClientResponse = await _client.GetAsync("/admin/api/clients/orders-admin");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedClientResponse.StatusCode);

        var deleteScopeResponse = await _client.DeleteAsync("/admin/api/scopes/orders-api");
        Assert.True(deleteScopeResponse.IsSuccessStatusCode, await deleteScopeResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, deleteScopeResponse.StatusCode);

        var getDeletedScopeResponse = await _client.GetAsync("/admin/api/scopes/orders-api");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedScopeResponse.StatusCode);
    }

    [Fact]
    public async Task AdminApi_Can_Export_And_Import_Configuration_Document()
    {
        await LoginAsAdminAsync();

        var scopeName = "inventory-api";
        var clientId = "inventory-admin";
        var scopeResources = new[] { "resource_server", "inventory_api" };
        var clientRedirectUris = new[] { "http://localhost/inventory/callback" };
        var clientPermissions = new[]
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.ResponseTypes.Code,
            $"{Permissions.Prefixes.Scope}openid",
            $"{Permissions.Prefixes.Scope}{scopeName}"
        };
        var clientRequirements = new[] { Requirements.Features.ProofKeyForCodeExchange };

        var createScopeResponse = await _client.PostAsJsonAsync("/admin/api/scopes", new
        {
            name = scopeName,
            displayName = "Inventory API",
            description = "Inventory scope",
            resources = scopeResources
        });
        Assert.Equal(HttpStatusCode.Created, createScopeResponse.StatusCode);

        var createClientResponse = await _client.PostAsJsonAsync("/admin/api/clients", new
        {
            clientId,
            displayName = "Inventory Admin",
            clientType = ClientTypes.Public,
            consentType = ConsentTypes.Explicit,
            redirectUris = clientRedirectUris,
            permissions = clientPermissions,
            requirements = clientRequirements
        });
        Assert.Equal(HttpStatusCode.Created, createClientResponse.StatusCode);

        var exportResponse = await _client.GetAsync("/admin/api/configuration/export");
        var exportJson = await exportResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.NotNull(exportJson);
        Assert.Equal(1, exportJson.RootElement.GetProperty("version").GetInt32());
        Assert.Contains(
            exportJson.RootElement.GetProperty("notes").EnumerateArray().Select(item => item.GetString()),
            note => string.Equals(note, "Client secrets are never exported.", StringComparison.Ordinal));

        var exportedScope = exportJson.RootElement.GetProperty("scopes")
            .EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == scopeName);
        var exportedClient = exportJson.RootElement.GetProperty("clients")
            .EnumerateArray()
            .Single(item => item.GetProperty("clientId").GetString() == clientId);

        Assert.Equal("Inventory API", exportedScope.GetProperty("displayName").GetString());
        Assert.Equal("Inventory Admin", exportedClient.GetProperty("displayName").GetString());

        var deleteClientResponse = await _client.DeleteAsync($"/admin/api/clients/{clientId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteClientResponse.StatusCode);

        var deleteScopeResponse = await _client.DeleteAsync($"/admin/api/scopes/{scopeName}");
        Assert.Equal(HttpStatusCode.NoContent, deleteScopeResponse.StatusCode);

        var importResponse = await _client.PostAsJsonAsync("/admin/api/configuration/import", new
        {
            format = exportJson.RootElement.GetProperty("format").GetString(),
            version = exportJson.RootElement.GetProperty("version").GetInt32(),
            clients = new[]
            {
                new
                {
                    clientId,
                    displayName = exportedClient.GetProperty("displayName").GetString(),
                    clientType = exportedClient.GetProperty("clientType").GetString(),
                    consentType = exportedClient.GetProperty("consentType").GetString(),
                    redirectUris = exportedClient.GetProperty("redirectUris").EnumerateArray().Select(item => item.GetString()).ToArray(),
                    postLogoutRedirectUris = Array.Empty<string>(),
                    permissions = exportedClient.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray(),
                    requirements = exportedClient.GetProperty("requirements").EnumerateArray().Select(item => item.GetString()).ToArray()
                }
            },
            scopes = new[]
            {
                new
                {
                    name = exportedScope.GetProperty("name").GetString(),
                    displayName = exportedScope.GetProperty("displayName").GetString(),
                    description = exportedScope.GetProperty("description").GetString(),
                    resources = exportedScope.GetProperty("resources").EnumerateArray().Select(item => item.GetString()).ToArray()
                }
            }
        });

        var importJson = await importResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.NotNull(importJson);
        Assert.Equal(1, importJson.RootElement.GetProperty("clientCreatedCount").GetInt32());
        Assert.Equal(0, importJson.RootElement.GetProperty("clientUpdatedCount").GetInt32());
        Assert.Equal(1, importJson.RootElement.GetProperty("scopeCreatedCount").GetInt32());
        Assert.Equal(0, importJson.RootElement.GetProperty("scopeUpdatedCount").GetInt32());

        var getImportedClientResponse = await _client.GetAsync($"/admin/api/clients/{clientId}");
        var getImportedScopeResponse = await _client.GetAsync($"/admin/api/scopes/{scopeName}");
        Assert.Equal(HttpStatusCode.OK, getImportedClientResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getImportedScopeResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
        var auditLogs = await db.AuditLogs
            .Where(log => log.EventType == "Admin.ConfigurationExported" || log.EventType == "Admin.ConfigurationImported")
            .OrderBy(log => log.OccurredAt)
            .ToListAsync();

        Assert.Contains(auditLogs, log => log.EventType == "Admin.ConfigurationExported");
        Assert.Contains(auditLogs, log => log.EventType == "Admin.ConfigurationImported");
    }

    private async Task LoginAsAdminAsync()
    {
        var loginPage = await _client.GetAsync("/Account/Login");
        var html = await loginPage.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);

        var antiforgeryToken = ExtractInputValue(html, "__RequestVerificationToken");
        var returnUrl = ExtractInputValue(html, "ReturnUrl");

        var response = await _client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = IntegrationSeedService.DemoEmail,
            ["Input.Password"] = IntegrationSeedService.DemoPassword,
            ["Input.RememberMe"] = "false",
            ["ReturnUrl"] = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl,
            ["__RequestVerificationToken"] = antiforgeryToken
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string ExtractInputValue(string html, string inputName)
    {
        var inputMatch = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(inputName)}\"[^>]*value=\"([^\"]*)\"[^>]*>",
            RegexOptions.IgnoreCase);

        Assert.True(inputMatch.Success, $"Could not find input '{inputName}' in login form.");
        return WebUtility.HtmlDecode(inputMatch.Groups[1].Value);
    }
}