using System.Text.Json;

var authority = Environment.GetEnvironmentVariable("OPENGATE_AUTHORITY") ?? "https://localhost:7001";
var deviceEndpoint = $"{authority.TrimEnd('/')}/connect/device";
var tokenEndpoint = $"{authority.TrimEnd('/')}/connect/token";

using var http = new HttpClient();

var deviceResponse = await http.PostAsync(deviceEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["client_id"] = "interactive-demo",
    ["scope"] = "openid profile email"
}));

if (!deviceResponse.IsSuccessStatusCode)
{
    Console.WriteLine($"Device authorization failed: {(int)deviceResponse.StatusCode}");
    Console.WriteLine(await deviceResponse.Content.ReadAsStringAsync());
    return;
}

using var deviceJson = JsonDocument.Parse(await deviceResponse.Content.ReadAsStringAsync());
var deviceCode = deviceJson.RootElement.GetProperty("device_code").GetString();
var userCode = deviceJson.RootElement.GetProperty("user_code").GetString();
var verificationUri = deviceJson.RootElement.GetProperty("verification_uri").GetString();
var interval = deviceJson.RootElement.TryGetProperty("interval", out var intervalProp)
    ? intervalProp.GetInt32()
    : 5;

Console.WriteLine($"Acesse: {verificationUri}");
Console.WriteLine($"Digite o codigo: {userCode}");
Console.WriteLine("Aguardando confirmacao...");

while (true)
{
    await Task.Delay(TimeSpan.FromSeconds(interval));

    var pollResponse = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
        ["client_id"] = "interactive-demo",
        ["device_code"] = deviceCode ?? string.Empty
    }));

    var body = await pollResponse.Content.ReadAsStringAsync();

    if (pollResponse.IsSuccessStatusCode)
    {
        Console.WriteLine("Token emitido com sucesso:");
        Console.WriteLine(body);
        break;
    }

    if (body.Contains("authorization_pending", StringComparison.OrdinalIgnoreCase))
        continue;

    if (body.Contains("slow_down", StringComparison.OrdinalIgnoreCase))
    {
        interval += 5;
        continue;
    }

    Console.WriteLine($"Polling encerrado ({(int)pollResponse.StatusCode}):");
    Console.WriteLine(body);
    break;
}
