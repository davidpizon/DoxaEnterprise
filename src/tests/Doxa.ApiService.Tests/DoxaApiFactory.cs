using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Doxa.ApiService.Tests;

/// <summary>
/// Boots the API in-memory for integration tests.
///
/// The real app expects Aspire-injected connection strings and a Keycloak authority.
/// We supply throwaway connection strings (no endpoint under test touches the database
/// or cache, so nothing ever connects) and swap the JWT bearer scheme for a test scheme
/// so authorization can be exercised without a live identity provider.
/// </summary>
public sealed class DoxaApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Satisfy AddNpgsqlDbContext("doxadb") and AddRedisDistributedCache("cache").
        // These are parsed at registration but only connect on first use, which never
        // happens for the endpoints exercised here.
        builder.UseSetting("ConnectionStrings:doxadb", "Host=localhost;Database=doxa_test;Username=test;Password=test");
        builder.UseSetting("ConnectionStrings:cache", "localhost:6379");

        builder.ConfigureTestServices(services =>
        {
            // Make the test scheme the default so UseAuthentication/RequireAuthorization
            // resolve against it instead of the Keycloak JWT bearer handler.
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
