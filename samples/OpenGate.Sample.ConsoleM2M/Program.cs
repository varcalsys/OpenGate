using System.Net.Http.Headers;
using System.Text.Json;

var authority = Environment.GetEnvironmentVariable("OPENGATE_AUTHORITY") ?? "https://localhost:7001";
var tokenEndpoint = $"{authority.TrimEnd('/')}/connect/token";
var apiEndpoint = Environment.GetEnvironmentVariable("PROTECTED_API") ?? "http://localhost:5090/api/me";

using var http = new HttpClient();

var tokenResponse = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["grant_type"] = "client_credentials",
    ["client_id"] = "machine-demo",
    ["client_secret"] = "machine-demo-secret-change-in-prod",
    ["scope"] = "api"
}));

if (!tokenResponse.IsSuccessStatusCode)
{
    Console.WriteLine($"Token request failed: {(int)tokenResponse.StatusCode}");
    Console.WriteLine(await tokenResponse.Content.ReadAsStringAsync());
    return;
}

using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();

if (string.IsNullOrWhiteSpace(accessToken))
{
    Console.WriteLine("Token endpoint did not return access_token.");
    return;
}

http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
var apiResponse = await http.GetAsync(apiEndpoint);

Console.WriteLine($"API status: {(int)apiResponse.StatusCode}");
Console.WriteLine(await apiResponse.Content.ReadAsStringAsync());
