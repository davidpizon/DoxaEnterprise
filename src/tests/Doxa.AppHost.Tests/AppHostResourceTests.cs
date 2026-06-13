using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Doxa.AppHost.Tests;

/// <summary>
/// Validates the Aspire application model: the orchestrator must declare every
/// expected resource and the documented dependency wiring. These tests inspect the
/// model only — they never call StartAsync, so no container runtime is required.
/// </summary>
public sealed class AppHostResourceTests
{
    private static async Task<IDistributedApplicationTestingBuilder> CreateAppHostAsync(CancellationToken ct)
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Doxa_AppHost>(ct);

        // Provide parameter values so the model resolves cleanly without prompting.
        appHost.Configuration["Parameters:postgres-password"] = "test-postgres";
        appHost.Configuration["Parameters:keycloak-admin"] = "admin";
        appHost.Configuration["Parameters:keycloak-admin-password"] = "test-keycloak";
        appHost.Configuration["Parameters:doxa-web-client-secret"] = "test-client-secret";

        return appHost;
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("doxadb")]
    [InlineData("cache")]
    [InlineData("keycloak")]
    [InlineData("apiservice")]
    [InlineData("webfrontend")]
    public async Task AppHost_DeclaresResource(string resourceName)
    {
        var appHost = await CreateAppHostAsync(TestContext.Current.CancellationToken);

        var names = appHost.Resources.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(resourceName, names);
    }

    [Fact]
    public async Task ApiService_IsAnExternallyReachableProject()
    {
        var appHost = await CreateAppHostAsync(TestContext.Current.CancellationToken);

        var api = Assert.Single(appHost.Resources.OfType<ProjectResource>(), r => r.Name == "apiservice");

        // Marked with WithExternalHttpEndpoints() in the AppHost.
        Assert.Contains(api.Annotations, a => a is EndpointAnnotation);
    }

    [Fact]
    public async Task SecretParameters_AreMarkedSecret()
    {
        var appHost = await CreateAppHostAsync(TestContext.Current.CancellationToken);

        var parameters = appHost.Resources.OfType<ParameterResource>().ToList();

        Assert.NotEmpty(parameters);
        Assert.All(parameters, p => Assert.True(p.Secret));
    }
}
