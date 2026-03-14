using System.Net;

namespace OpenGate.Server.Tests.Integration;

public sealed class OpenApiDocsTests(OpenGateWebFactory factory)
    : IClassFixture<OpenGateWebFactory>
{
    [Fact]
    public async Task OpenApi_Document_Is_Exposed_For_Development()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/admin/api/users", body, StringComparison.Ordinal);
        Assert.Contains("/admin/api/clients", body, StringComparison.Ordinal);
        Assert.Contains("openapi", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scalar_Docs_Are_Exposed_For_Development()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/docs");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("OpenGate Admin API", body, StringComparison.OrdinalIgnoreCase);
    }
}