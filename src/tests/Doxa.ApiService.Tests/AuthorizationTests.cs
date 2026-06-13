using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Doxa.ApiService.Tests;

/// <summary>
/// Verifies the authorization contract of the protected endpoints:
/// anonymous → 401, authenticated → 200, and the admin policy → 403/200 by role.
/// </summary>
public sealed class AuthorizationTests(DoxaApiFactory factory) : IClassFixture<DoxaApiFactory>
{
    private readonly DoxaApiFactory _factory = factory;

    private HttpClient CreateClient(string? user = null, string? roles = null)
    {
        var client = _factory.CreateClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user);
        }
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        }
        return client;
    }

    [Fact]
    public async Task Profile_WithoutAuthentication_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/profile", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WhenAuthenticated_ReturnsUserName()
    {
        var client = CreateClient(user: "ada@doxa.test");

        var response = await client.GetAsync("/api/profile", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("ada@doxa.test", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task AdminMetrics_WithoutAuthentication_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/admin/metrics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminMetrics_WhenAuthenticatedWithoutAdminRole_Returns403()
    {
        var client = CreateClient(user: "ada@doxa.test", roles: "doxa-user");

        var response = await client.GetAsync("/api/admin/metrics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminMetrics_WhenAuthenticatedAsAdmin_ReturnsMetrics()
    {
        var client = CreateClient(user: "root@doxa.test", roles: "doxa-user,doxa-admin");

        var response = await client.GetAsync("/api/admin/metrics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(42, body.GetProperty("tenants").GetInt32());
    }
}
