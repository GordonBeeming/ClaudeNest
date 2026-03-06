using System.Net;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class InstallScriptControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GetBashScript_ReturnsScript()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/install.sh");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetPowerShellScript_ReturnsScript()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/install.ps1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetBashScript_NoAuthRequired()
    {
        // No auth header set
        var client = factory.CreateAuthenticatedClient(TestUsers.Anonymous);

        var response = await client.GetAsync("/install.sh");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBashScript_ContainsBackendUrl()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/install.sh");
        var content = await response.Content.ReadAsStringAsync();

        // The script should have the backend URL injected (replacing %%BACKEND_URL%%)
        Assert.DoesNotContain("%%BACKEND_URL%%", content);
    }
}
