using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Data.EFCore;
using OpenGate.Data.EFCore.Entities;
using OpenIddict.Abstractions;

namespace OpenGate.Server.Tests.Integration;

public sealed class AdminUiTests(OpenGateWebFactory factory)
    : IClassFixture<OpenGateWebFactory>
{
    private readonly HttpClient _adminClient = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task AdminUi_Unauthenticated_Redirects_To_Login()
    {
        var response = await _adminClient.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminUi_Authenticated_NonAdmin_Sees_AccessDenied_Page()
    {
        const string email = "regular.user@opengate.test";
        const string password = "Regular@1234!";

        await CreateRegularUserAsync(email, password);

        var client = factory.CreateClient();
        var loginResponse = await LoginAsync(client, email, password);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var accessDeniedResponse = await client.GetAsync("/Admin");
        var html = await accessDeniedResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, accessDeniedResponse.StatusCode);
        Assert.Contains("Você não tem permissão para acessar esta área", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminUi_Admin_Can_View_Dashboard_And_Primary_Pages()
    {
        await LoginAsync(_adminClient, IntegrationSeedService.DemoEmail, IntegrationSeedService.DemoPassword);

        var dashboardResponse = await _adminClient.GetAsync("/Admin");
        var usersResponse = await _adminClient.GetAsync("/Admin/Users");
        var clientsResponse = await _adminClient.GetAsync("/Admin/Clients");
        var scopesResponse = await _adminClient.GetAsync("/Admin/Scopes");
        var sessionsResponse = await _adminClient.GetAsync("/Admin/Sessions");
        var auditLogsResponse = await _adminClient.GetAsync("/Admin/AuditLogs");

        var dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();
        var usersHtml = await usersResponse.Content.ReadAsStringAsync();
        var clientsHtml = await clientsResponse.Content.ReadAsStringAsync();
        var scopesHtml = await scopesResponse.Content.ReadAsStringAsync();
        var sessionsHtml = await sessionsResponse.Content.ReadAsStringAsync();
        var auditLogsHtml = await auditLogsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, clientsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, scopesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, auditLogsResponse.StatusCode);

        Assert.Contains("OpenGate Admin", dashboardHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Visão geral", dashboardHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(IntegrationSeedService.DemoEmail, usersHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(IntegrationSeedService.InteractiveClientId, clientsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(IntegrationSeedService.MachineClientId, clientsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("api", scopesHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(IntegrationSeedService.DemoEmail, sessionsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Admin.Seed", auditLogsHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminUi_Admin_Can_Create_Edit_And_Toggle_User_Management_Flows()
    {
        await LoginAsync(_adminClient, IntegrationSeedService.DemoEmail, IntegrationSeedService.DemoPassword);

        var createToken = await GetAntiforgeryTokenAsync(_adminClient, "/Admin/Users/Create");
        var createResponse = await _adminClient.PostAsync("/Admin/Users/Create", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("Input.Email", "managed.user@opengate.test"),
            new("Input.UserName", "managed-user"),
            new("Input.Password", "Managed@1234!"),
            new("Input.FirstName", "Managed"),
            new("Input.LastName", "User"),
            new("Input.DisplayName", "Managed UI User"),
            new("Input.Locale", "pt-BR"),
            new("Input.TimeZone", "America/Sao_Paulo"),
            new("Input.IsActive", "true"),
            new("Input.EmailConfirmed", "true"),
            new("Input.SelectedRoles", "Viewer"),
            new("__RequestVerificationToken", createToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal("/Admin/Users", createResponse.Headers.Location?.ToString());

        await using var createScope = factory.Services.CreateAsyncScope();
        var createDb = createScope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
        var createdUser = await createDb.Users.Include(user => user.Profile).SingleAsync(user => user.Email == "managed.user@opengate.test");

        Assert.True(createdUser.EmailConfirmed);
        Assert.True(createdUser.IsActive);
        Assert.Equal("Managed UI User", createdUser.Profile?.DisplayName);

        createDb.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = createdUser.Id,
            ClientId = IntegrationSeedService.InteractiveClientId,
            DeviceInfo = "AdminUi Test Browser",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
        });
        await createDb.SaveChangesAsync();

        var editToken = await GetAntiforgeryTokenAsync(_adminClient, $"/Admin/Users/Edit/{createdUser.Id}");
        var editResponse = await _adminClient.PostAsync($"/Admin/Users/Edit/{createdUser.Id}", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("Input.Email", "managed.user@opengate.test"),
            new("Input.UserName", "managed-user-updated"),
            new("Input.FirstName", "Managed"),
            new("Input.LastName", "Operator"),
            new("Input.DisplayName", "Managed UI User Updated"),
            new("Input.Locale", "en-US"),
            new("Input.TimeZone", "UTC"),
            new("Input.EmailConfirmed", "true"),
            new("Input.SelectedRoles", "Admin"),
            new("__RequestVerificationToken", editToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        Assert.Equal("/Admin/Users", editResponse.Headers.Location?.ToString());

        await using var editScope = factory.Services.CreateAsyncScope();
        var editDb = editScope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
        var userManager = editScope.ServiceProvider.GetRequiredService<UserManager<OpenGateUser>>();
        var updatedUser = await editDb.Users.Include(user => user.Profile).SingleAsync(user => user.Id == createdUser.Id);
        var updatedRoles = await userManager.GetRolesAsync(updatedUser);

        Assert.Equal("managed-user-updated", updatedUser.UserName);
        Assert.Equal("Managed UI User Updated", updatedUser.Profile?.DisplayName);
        Assert.Contains("Admin", updatedRoles);

        var usersToken = await GetAntiforgeryTokenAsync(_adminClient, "/Admin/Users");
        var deactivateResponse = await _adminClient.PostAsync("/Admin/Users?handler=ToggleActive", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("Search", "managed.user@opengate.test"),
            new("id", createdUser.Id),
            new("isActive", "false"),
            new("__RequestVerificationToken", usersToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, deactivateResponse.StatusCode);
        Assert.Contains("/Admin/Users?Search=managed.user", deactivateResponse.Headers.Location?.ToString() ?? string.Empty, StringComparison.Ordinal);

        await using var deactivateScope = factory.Services.CreateAsyncScope();
        var deactivateDb = deactivateScope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
        var deactivatedUser = await deactivateDb.Users.SingleAsync(user => user.Id == createdUser.Id);
        var revokedSessions = await deactivateDb.UserSessions.Where(session => session.UserId == createdUser.Id).ToListAsync();
        var auditLogs = await deactivateDb.AuditLogs.Where(log => log.EventType.StartsWith("Admin.User", StringComparison.Ordinal)).ToListAsync();

        Assert.False(deactivatedUser.IsActive);
        Assert.All(revokedSessions, session => Assert.NotNull(session.RevokedAt));
        Assert.Contains(auditLogs, log => log.EventType == "Admin.UserCreated");
        Assert.Contains(auditLogs, log => log.EventType == "Admin.UserUpdated");
    }

    [Fact]
    public async Task AdminUi_Admin_Can_Create_Edit_And_Delete_Clients_And_Scopes()
    {
        await LoginAsync(_adminClient, IntegrationSeedService.DemoEmail, IntegrationSeedService.DemoPassword);

        var createScopeToken = await GetAntiforgeryTokenAsync(_adminClient, "/Admin/Scopes/Create");
        var createScopeResponse = await _adminClient.PostAsync("/Admin/Scopes/Create", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("Input.Name", "admin-ui-scope"),
            new("Input.DisplayName", "Admin UI Scope"),
            new("Input.Description", "Scope criado pelo Admin UI"),
            new("Input.Resources", "resource-admin-ui"),
            new("__RequestVerificationToken", createScopeToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, createScopeResponse.StatusCode);
        Assert.Equal("/Admin/Scopes", createScopeResponse.Headers.Location?.ToString());

        await using var scopeCreateScope = factory.Services.CreateAsyncScope();
        var scopeManager = scopeCreateScope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var createdScope = await scopeManager.FindByNameAsync("admin-ui-scope");

        Assert.NotNull(createdScope);

        var createClientToken = await GetAntiforgeryTokenAsync(_adminClient, "/Admin/Clients/Create");
        var createClientResponse = await _adminClient.PostAsync("/Admin/Clients/Create", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("Input.ClientId", "admin-ui-client"),
            new("Input.DisplayName", "Admin UI Client"),
            new("Input.ClientType", "public"),
            new("Input.ConsentType", "explicit"),
            new("Input.RedirectUris", "https://localhost:7788/signin-callback"),
            new("Input.PostLogoutRedirectUris", "https://localhost:7788/signout-callback"),
            new("Input.Permissions", "ept:authorization\n ept:token\n gt:authorization_code\n rst:code\n scp:openid\n scp:profile\n scp:admin-ui-scope"),
            new("Input.Requirements", "ft:pkce"),
            new("__RequestVerificationToken", createClientToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, createClientResponse.StatusCode);
        Assert.Equal("/Admin/Clients", createClientResponse.Headers.Location?.ToString());

        await using var clientCreateScope = factory.Services.CreateAsyncScope();
        var applicationManager = clientCreateScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var createdApplication = await applicationManager.FindByClientIdAsync("admin-ui-client");

        Assert.NotNull(createdApplication);

        var editScopeToken = await GetAntiforgeryTokenAsync(_adminClient, "/Admin/Scopes/Edit/admin-ui-scope");
        var editScopeResponse = await _adminClient.PostAsync("/Admin/Scopes/Edit/admin-ui-scope", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("Input.Name", "admin-ui-scope"),
            new("Input.DisplayName", "Admin UI Scope Updated"),
            new("Input.Description", "Scope atualizado pelo Admin UI"),
            new("Input.Resources", "resource-admin-ui-updated"),
            new("__RequestVerificationToken", editScopeToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, editScopeResponse.StatusCode);
        Assert.Equal("/Admin/Scopes", editScopeResponse.Headers.Location?.ToString());

        var editClientToken = await GetAntiforgeryTokenAsync(_adminClient, "/Admin/Clients/Edit/admin-ui-client");
        var editClientResponse = await _adminClient.PostAsync("/Admin/Clients/Edit/admin-ui-client", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("Input.ClientId", "admin-ui-client"),
            new("Input.DisplayName", "Admin UI Client Updated"),
            new("Input.ClientType", "public"),
            new("Input.ConsentType", "implicit"),
            new("Input.RedirectUris", "https://localhost:7799/callback"),
            new("Input.PostLogoutRedirectUris", "https://localhost:7799/logout"),
            new("Input.Permissions", "ept:authorization\n ept:token\n gt:authorization_code\n rst:code\n scp:openid\n scp:admin-ui-scope"),
            new("Input.Requirements", string.Empty),
            new("__RequestVerificationToken", editClientToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, editClientResponse.StatusCode);
        Assert.Equal("/Admin/Clients", editClientResponse.Headers.Location?.ToString());

        await using var editValidationScope = factory.Services.CreateAsyncScope();
        var validationScopeManager = editValidationScope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var validationAppManager = editValidationScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var updatedScope = await validationScopeManager.FindByNameAsync("admin-ui-scope");
        var updatedApplication = await validationAppManager.FindByClientIdAsync("admin-ui-client");

        Assert.NotNull(updatedScope);
        Assert.NotNull(updatedApplication);

        var scopeDescriptor = new OpenIddictScopeDescriptor();
        await validationScopeManager.PopulateAsync(scopeDescriptor, updatedScope!);
        var appDescriptor = new OpenIddictApplicationDescriptor();
        await validationAppManager.PopulateAsync(appDescriptor, updatedApplication!);

        Assert.Equal("Admin UI Scope Updated", scopeDescriptor.DisplayName);
        Assert.Contains("resource-admin-ui-updated", scopeDescriptor.Resources);
        Assert.Equal("Admin UI Client Updated", appDescriptor.DisplayName);
        Assert.Equal("implicit", appDescriptor.ConsentType);
        Assert.Contains(appDescriptor.RedirectUris, uri => uri.AbsoluteUri == "https://localhost:7799/callback");

        var deleteClientToken = await GetAntiforgeryTokenAsync(_adminClient, "/Admin/Clients");
        var deleteClientResponse = await _adminClient.PostAsync("/Admin/Clients?handler=Delete", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("clientId", "admin-ui-client"),
            new("__RequestVerificationToken", deleteClientToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, deleteClientResponse.StatusCode);

        var deleteScopeToken = await GetAntiforgeryTokenAsync(_adminClient, "/Admin/Scopes");
        var deleteScopeResponse = await _adminClient.PostAsync("/Admin/Scopes?handler=Delete", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("name", "admin-ui-scope"),
            new("__RequestVerificationToken", deleteScopeToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, deleteScopeResponse.StatusCode);

        await using var deleteValidationScope = factory.Services.CreateAsyncScope();
        var deleteScopeManager = deleteValidationScope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var deleteAppManager = deleteValidationScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var deleteDb = deleteValidationScope.ServiceProvider.GetRequiredService<OpenGateDbContext>();

        Assert.Null(await deleteAppManager.FindByClientIdAsync("admin-ui-client"));
        Assert.Null(await deleteScopeManager.FindByNameAsync("admin-ui-scope"));

        var auditLogs = await deleteDb.AuditLogs
            .Where(log => log.EventType.StartsWith("Admin.Client", StringComparison.Ordinal)
                       || log.EventType.StartsWith("Admin.Scope", StringComparison.Ordinal))
            .ToListAsync();

        Assert.Contains(auditLogs, log => log.EventType == "Admin.ClientCreated");
        Assert.Contains(auditLogs, log => log.EventType == "Admin.ClientUpdated");
        Assert.Contains(auditLogs, log => log.EventType == "Admin.ClientDeleted");
        Assert.Contains(auditLogs, log => log.EventType == "Admin.ScopeCreated");
        Assert.Contains(auditLogs, log => log.EventType == "Admin.ScopeUpdated");
        Assert.Contains(auditLogs, log => log.EventType == "Admin.ScopeDeleted");
    }

    [Fact]
    public async Task AdminUi_Admin_Can_Revoke_Session_From_Sessions_Page()
    {
        await LoginAsync(_adminClient, IntegrationSeedService.DemoEmail, IntegrationSeedService.DemoPassword);

        var sessionClientId = $"admin-ui-session-{Guid.NewGuid():N}";
        Guid sessionId;

        await using (var arrangeScope = factory.Services.CreateAsyncScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
            var adminUser = await db.Users.SingleAsync(user => user.Email == IntegrationSeedService.DemoEmail);

            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                ClientId = sessionClientId,
                DeviceInfo = "Admin UI Revoke Test Device",
                IpAddress = "127.0.0.10",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };

            db.UserSessions.Add(session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
        }

        var sessionsUrl = $"/Admin/Sessions?Search={sessionClientId}&ActiveOnly=true";
        var revokeToken = await GetAntiforgeryTokenAsync(_adminClient, sessionsUrl);
        var revokeResponse = await _adminClient.PostAsync("/Admin/Sessions?handler=Revoke", new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("Search", sessionClientId),
            new("ActiveOnly", "true"),
            new("id", sessionId.ToString()),
            new("__RequestVerificationToken", revokeToken)
        }));

        Assert.Equal(HttpStatusCode.Redirect, revokeResponse.StatusCode);
        Assert.Contains("/Admin/Sessions?Search=admin-ui-session", revokeResponse.Headers.Location?.ToString() ?? string.Empty, StringComparison.Ordinal);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<OpenGateDbContext>();
        var revokedSession = await assertDb.UserSessions.SingleAsync(session => session.Id == sessionId);
        var auditLog = await assertDb.AuditLogs
            .Where(log => log.EventType == "Admin.SessionRevoked")
            .OrderByDescending(log => log.OccurredAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(revokedSession.RevokedAt);
        Assert.NotNull(auditLog);
        Assert.Equal(sessionClientId, auditLog!.ClientId);
        Assert.Contains(sessionId.ToString(), auditLog.Details ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        var loginPage = await client.GetAsync("/Account/Login");
        var html = await loginPage.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);

        var antiforgeryToken = ExtractInputValue(html, "__RequestVerificationToken");
        var returnUrl = ExtractInputValue(html, "ReturnUrl");

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "false",
            ["ReturnUrl"] = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl,
            ["__RequestVerificationToken"] = antiforgeryToken
        }));
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractInputValue(html, "__RequestVerificationToken");
    }

    private async Task CreateRegularUserAsync(string email, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<OpenGateUser>>();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new OpenGateUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            Profile = new UserProfile
            {
                FirstName = "Regular",
                LastName = "User",
                DisplayName = "Regular User"
            }
        };

        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
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