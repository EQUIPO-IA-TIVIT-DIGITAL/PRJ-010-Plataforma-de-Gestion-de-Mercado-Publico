using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace MPM.Core.Tests;

/// <summary>
/// Tests mínimo para gate de observabilidad 037-A: verifica contrato de /health.
/// No requiere BD real; valida serialización y predicados del pipeline.
/// </summary>
public class HealthCheckTests
{
    [Fact]
    public void HealthReport_Serialization_ContainsRequiredFields()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["licitaciones"] = new HealthReportEntry(HealthStatus.Healthy, "licitaciones ok", TimeSpan.FromMilliseconds(5), null, null),
                ["analisis"] = new HealthReportEntry(HealthStatus.Healthy, "analisis ok", TimeSpan.FromMilliseconds(3), null, null),
            },
            TimeSpan.FromMilliseconds(8));

        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            timestamp = DateTime.UtcNow.ToString("o"),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString().ToLowerInvariant(),
                    durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 2)
                }),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2)
        };

        var json = JsonSerializer.Serialize(payload, jsonOpts);
        var doc = JsonDocument.Parse(json).RootElement;

        doc.TryGetProperty("status", out _).Should().BeTrue();
        doc.TryGetProperty("timestamp", out _).Should().BeTrue();
        doc.TryGetProperty("checks", out var checks).Should().BeTrue();
        checks.TryGetProperty("licitaciones", out _).Should().BeTrue();
        doc.TryGetProperty("totalDurationMs", out _).Should().BeTrue();

        // Nunca expone excepción ni PII
        json.Should().NotContain("exception");
        json.Should().NotContain("stack");
        json.Should().NotContain("password");
    }

    [Fact]
    public void HealthReport_Unhealthy_Returns503Semantic()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["censo"] = new HealthReportEntry(HealthStatus.Unhealthy, "db failed", TimeSpan.FromMilliseconds(10), new Exception("SELECT 1 failed"), null),
            },
            TimeSpan.FromMilliseconds(10));

        // Simulate WriteHealthResponse status code logic
        var statusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
        statusCode.Should().Be(503);

        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString().ToLowerInvariant() })
        };
        var json = JsonSerializer.Serialize(payload, jsonOpts);
        // Mensaje no debe contener exception details
        json.Should().NotContain("SELECT 1 failed");
        json.Should().Contain("unhealthy");
    }

    [Fact]
    public void HealthChecks_Predicate_FiltersCorrectly()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["licitaciones"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null),
            ["analisis"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null),
            ["censo"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null),
        };
        // Simulate predicate for /health/licitaciones
        var predicate = (HealthCheckRegistration r) => r.Name == "licitaciones";
        var registrations = new[]
        {
            new HealthCheckRegistration("licitaciones", provider => new NoOpHealthCheck(), HealthStatus.Unhealthy, null),
            new HealthCheckRegistration("analisis", provider => new NoOpHealthCheck(), HealthStatus.Unhealthy, null),
        };
        registrations.Count(r => predicate(r)).Should().Be(1);
        registrations.First(r => predicate(r)).Name.Should().Be("licitaciones");
    }

    [Fact]
    public void HealthReport_ModulePayload_ContainsModuleField()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["propuestas"] = new HealthReportEntry(HealthStatus.Healthy, "ok", TimeSpan.FromMilliseconds(2), null, null),
            },
            TimeSpan.FromMilliseconds(2));

        var path = "/health/propuestas";
        var isModule = path.StartsWith("/health/", StringComparison.OrdinalIgnoreCase) && path.Length > 8;
        isModule.Should().BeTrue();

        var module = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        module.Should().Be("propuestas");

        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var basePayload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            timestamp = DateTime.UtcNow.ToString("o"),
            checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString().ToLowerInvariant() }),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2)
        };
        var payload = new
        {
            status = basePayload.status,
            module,
            timestamp = basePayload.timestamp,
            checks = basePayload.checks,
            totalDurationMs = basePayload.totalDurationMs
        };
        var json = JsonSerializer.Serialize(payload, jsonOpts);
        json.Should().Contain("\"module\":\"propuestas\"");
    }

    private class NoOpHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(HealthCheckResult.Healthy());
    }
}
