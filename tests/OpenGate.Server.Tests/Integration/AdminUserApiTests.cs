using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenGate.Server.Tests.Integration;

public sealed class AdminUserApiTests(OpenGateWebFactory factory)
    : IClassFixture<OpenGateWebFactory>
{
    private const string CreatedUserEmail = "operator@opengate.test";
    private const string CreatedUserPassword = "Operator@1234!";
    private const string AuthorizationUrl = "/connect/authorize" +
        "?response_type=code" +
        "&client_id=interactive-demo" +
        "&redirect_uri=http%3A%2F%2Flocalhost%2Fcallback" +
        "&scope=openid%20email" +
        "&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM" +
        "&code_challenge_method=S256";

    private static readonly string[] ViewerRole = ["Viewer"];
    private static readonly string[] AdminRole = ["Admin"];

    private readonly HttpClient _adminClient = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task AdminApi_Can_Create_Update_Assign_And_Delete_User()
    {
        await LoginAsync(_adminClient, IntegrationSeedService.DemoEmail, IntegrationSeedService.DemoPassword);

        var rolesResponse = await _adminClient.GetAsync("/admin/api/roles");
        var rolesJson = await rolesResponse.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        Assert.NotNull(rolesJson);
        Assert.Contains(
            rolesJson.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()),
            role => role == "SuperAdmin");

        var createResponse = await _adminClient.PostAsJsonAsync("/admin/api/users", new
        {
            email = CreatedUserEmail,
            userName = "operator",
            password = CreatedUserPassword,
            emailConfirmed = true,
            firstName = "Open",
            lastName = "Gate",
            displayName = "Open Gate Operator",
            locale = "en-US",
            timeZone = "UTC",
            roles = ViewerRole
        });

        var createdJson = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createdJson);

        var userId = createdJson.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userId));
        Assert.Equal("operator", createdJson.RootElement.GetProperty("userName").GetString());
        Assert.Contains(
            createdJson.RootElement.GetProperty("roles").EnumerateArray().Select(item => item.GetString()),
            role => role == "Viewer");

        var updateResponse = await _adminClient.PutAsJsonAsync($"/admin/api/users/{userId}", new
        {
            displayName = "Open Gate Operator v2",
            locale = "pt-BR",
            timeZone = "America/Sao_Paulo",
            emailConfirmed = true,
            roles = AdminRole
        });

        var updatedJson = await updateResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updatedJson);
        Assert.Equal("Open Gate Operator v2", updatedJson.RootElement.GetProperty("profile").GetProperty("displayName").GetString());
        Assert.Contains(
            updatedJson.RootElement.GetProperty("roles").EnumerateArray().Select(item => item.GetString()),
            role => role == "Admin");

        var rolesUpdateResponse = await _adminClient.PutAsJsonAsync($"/admin/api/users/{userId}/roles", new
        {
            roles = ViewerRole
        });

        var rolesUpdatedJson = await rolesUpdateResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(HttpStatusCode.OK, rolesUpdateResponse.StatusCode);
        Assert.NotNull(rolesUpdatedJson);
        Assert.Contains(
            rolesUpdatedJson.RootElement.GetProperty("roles").EnumerateArray().Select(item => item.GetString()),
            role => role == "Viewer");

        var deleteResponse = await _adminClient.DeleteAsync($"/admin/api/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getDeletedResponse = await _adminClient.GetAsync($"/admin/api/users/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
    }

    [Fact]
    public async Task Inactive_User_Cannot_Login_Or_Authorize()
    {
        await LoginAsync(_adminClient, IntegrationSeedService.DemoEmail, IntegrationSeedService.DemoPassword);

        var createResponse = await _adminClient.PostAsJsonAsync("/admin/api/users", new
        {
            email = "inactive@opengate.test",
            userName = "inactive-user",
            password = CreatedUserPassword,
            emailConfirmed = true,
            roles = ViewerRole
        });

        var createdJson = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createdJson);

        var userId = createdJson.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userId));

        var userClient = factory.CreateClient(new() { AllowAutoRedirect = false });
        var successfulLoginResponse = await LoginAsync(userClient, "inactive@opengate.test", CreatedUserPassword);
        Assert.Equal(HttpStatusCode.Redirect, successfulLoginResponse.StatusCode);

        var deactivateResponse = await _adminClient.PutAsJsonAsync($"/admin/api/users/{userId}", new
        {
            isActive = false
        });

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var blockedLoginClient = factory.CreateClient(new() { AllowAutoRedirect = false });
        var blockedLoginResponse = await LoginAsync(blockedLoginClient, "inactive@opengate.test", CreatedUserPassword);
        var blockedLoginHtml = await blockedLoginResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, blockedLoginResponse.StatusCode);
        Assert.Contains("E-mail ou senha incorretos", blockedLoginHtml, StringComparison.OrdinalIgnoreCase);

        var authorizeResponse = await userClient.GetAsync(AuthorizationUrl);
        Assert.Equal(HttpStatusCode.Redirect, authorizeResponse.StatusCode);
        Assert.Contains("Login", authorizeResponse.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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