using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Analisis.Data;
using MPM.Modules.Analisis.Services;
using Npgsql;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Services;

/// <summary>
/// Cubre QA BUG-002: el análisis de un documento se disparaba con Task.Run fire-and-forget y
/// se perdía si la instancia moría antes de terminar. Corre contra el Postgres real de
/// docker-compose (localhost:5433). IAnalisisBackgroundService se mockea deliberadamente: si se
/// llamara al real, EnqueueAnalisis dispararía una llamada real a Gemini/Vertex AI (factura
/// directo) — este test solo verifica QUÉ workspaces se reclaman, no el pipeline de análisis.
/// </summary>
public class AnalisisRecoveryWorkerTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    private readonly List<long> _workspaceIdsACleanup = new();
    private AnalisisHandler _handler = null!;
    private long _licitacionId;

    static AnalisisRecoveryWorkerTests()
    {
        // Program.cs setea esto globalmente al arrancar la app real; estos tests instancian
        // AnalisisHandler directo (sin bootear el host completo), así que hay que replicarlo
        // o Dapper no mapea columnas snake_case (nombre_archivo, ruta_storage) a las
        // propiedades PascalCase de los DTOs.
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public async Task InitializeAsync()
    {
        _handler = new AnalisisHandler(new DbConnectionFactory(TestConnectionString));

        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<long?>("SELECT id FROM licitaciones LIMIT 1");
        _licitacionId = id ?? throw new InvalidOperationException("La base de test no tiene ninguna licitación — no se puede crear un workspace de prueba.");
    }

    public async Task DisposeAsync()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        foreach (var id in _workspaceIdsACleanup)
        {
            await conn.ExecuteAsync("DELETE FROM analisis_resultados WHERE workspace_id = @id", new { id });
            await conn.ExecuteAsync("DELETE FROM analisis_documentos WHERE workspace_id = @id", new { id });
            await conn.ExecuteAsync("DELETE FROM analisis_workspaces WHERE id = @id", new { id });
        }
    }

    private async Task<(long WorkspaceId, long DocumentoId)> CrearWorkspaceHuerfanoAsync(TimeSpan antiguedad, bool conResultado)
    {
        var (workspaceId, error) = await _handler.CrearWorkspaceAsync(_licitacionId, $"Test recovery {Guid.NewGuid()}", "test-user");
        error.Should().BeNull();
        _workspaceIdsACleanup.Add(workspaceId);

        var (documentoId, docError) = await _handler.CrearDocumentoAsync(
            workspaceId, "test.pdf", "application/pdf", 1024, $"local/test-{workspaceId}.pdf");
        docError.Should().BeNull();

        await _handler.ActualizarEstadoAsync(workspaceId, "analizando");

        if (conResultado)
        {
            await _handler.CrearResultadoAsync(workspaceId, documentoId, "{}", "test-model", 0, 0);
        }

        // Retrasa artificialmente updated_at para simular inactividad — ActualizarEstadoAsync
        // siempre lo deja en CURRENT_TIMESTAMP.
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            "UPDATE analisis_workspaces SET updated_at = @updatedAt WHERE id = @id",
            new { id = workspaceId, updatedAt = DateTime.UtcNow - antiguedad });

        return (workspaceId, documentoId);
    }

    private ServiceProvider BuildScope(out Mock<IAnalisisBackgroundService> backgroundServiceMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DbConnectionFactory(TestConnectionString));
        services.AddScoped<AnalisisHandler>();
        var mock = new Mock<IAnalisisBackgroundService>();
        services.AddSingleton(mock.Object);
        backgroundServiceMock = mock;
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RecuperarHuerfanosAsync_WorkspaceAnalizandoSinResultadoYVencido_ReencolaElAnalisis()
    {
        var (workspaceId, documentoId) = await CrearWorkspaceHuerfanoAsync(TimeSpan.FromMinutes(10), conResultado: false);

        using var provider = BuildScope(out var mock);
        var worker = new AnalisisRecoveryWorker(
            new FakeScopeFactory(provider), new ConfigurationBuilder().Build(), NullLogger<AnalisisRecoveryWorker>.Instance);

        await worker.RecuperarHuerfanosAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        mock.Verify(b => b.EnqueueAnalisis(
            workspaceId, documentoId,
            It.Is<List<(long Id, string Nombre, string RutaStorage)>>(docs =>
                docs.Count == 1 && docs[0].Id == documentoId && docs[0].Nombre == "test.pdf" && docs[0].RutaStorage == $"local/test-{workspaceId}.pdf")),
            Times.Once);
    }

    [Fact]
    public async Task RecuperarHuerfanosAsync_WorkspaceAnalizandoReciente_NoLoReencola()
    {
        var (workspaceId, _) = await CrearWorkspaceHuerfanoAsync(TimeSpan.FromMinutes(1), conResultado: false);

        using var provider = BuildScope(out var mock);
        var worker = new AnalisisRecoveryWorker(
            new FakeScopeFactory(provider), new ConfigurationBuilder().Build(), NullLogger<AnalisisRecoveryWorker>.Instance);

        await worker.RecuperarHuerfanosAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        mock.Verify(b => b.EnqueueAnalisis(workspaceId, It.IsAny<long>(), It.IsAny<List<(long Id, string Nombre, string RutaStorage)>>()), Times.Never);
    }

    [Fact]
    public async Task RecuperarHuerfanosAsync_WorkspaceConResultadoExistente_NoLoReencola()
    {
        // El análisis en realidad terminó pero el estado no se actualizó a "completado" — un bug
        // distinto, fuera de alcance; el worker no debe reprocesar (evita gasto duplicado de
        // Gemini) ni pisar el resultado ya guardado.
        var (workspaceId, _) = await CrearWorkspaceHuerfanoAsync(TimeSpan.FromMinutes(10), conResultado: true);

        using var provider = BuildScope(out var mock);
        var worker = new AnalisisRecoveryWorker(
            new FakeScopeFactory(provider), new ConfigurationBuilder().Build(), NullLogger<AnalisisRecoveryWorker>.Instance);

        await worker.RecuperarHuerfanosAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        mock.Verify(b => b.EnqueueAnalisis(workspaceId, It.IsAny<long>(), It.IsAny<List<(long Id, string Nombre, string RutaStorage)>>()), Times.Never);
    }

    /// <summary>Envuelve un ServiceProvider de test como IServiceScopeFactory sin crear scopes reales anidados.</summary>
    private sealed class FakeScopeFactory(ServiceProvider provider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeScope(provider);

        private sealed class FakeScope(ServiceProvider provider) : IServiceScope
        {
            public IServiceProvider ServiceProvider => provider;
            public void Dispose() { }
        }
    }
}
