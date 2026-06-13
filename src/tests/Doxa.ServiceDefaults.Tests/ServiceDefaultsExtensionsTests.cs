using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Doxa.ServiceDefaults.Tests;

/// <summary>
/// Unit tests for the shared <c>AddServiceDefaults</c> wiring: health checks,
/// service discovery, and OpenTelemetry registration.
/// </summary>
public sealed class ServiceDefaultsExtensionsTests
{
    private static IHost BuildHostWithDefaults()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        return builder.Build();
    }

    [Fact]
    public void AddServiceDefaults_AllowsHostToBuild()
    {
        using var host = BuildHostWithDefaults();

        Assert.NotNull(host.Services);
    }

    [Fact]
    public void AddServiceDefaults_RegistersHealthCheckService()
    {
        using var host = BuildHostWithDefaults();

        var healthChecks = host.Services.GetService<HealthCheckService>();

        Assert.NotNull(healthChecks);
    }

    [Fact]
    public async Task SelfLivenessCheck_IsRegisteredAndHealthy()
    {
        using var host = BuildHostWithDefaults();
        var healthChecks = host.Services.GetRequiredService<HealthCheckService>();

        // Mirrors the predicate used by the "/alive" endpoint in MapDefaultEndpoints.
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("live"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Contains("self", report.Entries.Keys);
    }

    [Fact]
    public void ConfigureOpenTelemetry_RegistersTracerAndMeterProviders()
    {
        using var host = BuildHostWithDefaults();

        Assert.NotNull(host.Services.GetService<TracerProvider>());
        Assert.NotNull(host.Services.GetService<MeterProvider>());
    }
}
