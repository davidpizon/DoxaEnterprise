using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Doxa.ApiService.Tests;

/// <summary>Tests for the anonymous status endpoint and the liveness probe.</summary>
public sealed class StatusEndpointTests(DoxaApiFactory factory) : IClassFixture<DoxaApiFactory>
{
    private readonly DoxaApiFactory _factory = factory;

    [Fact]
    public async Task GetStatus_IsAnonymous_AndReportsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/status", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("doxa-api", body.GetProperty("service").GetString());
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Alive_LivenessProbe_ReturnsOk()
    {
        // "/alive" filters to the "self" check only (no DB/Redis), so it is healthy
        // even though no backing services are running in tests.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
