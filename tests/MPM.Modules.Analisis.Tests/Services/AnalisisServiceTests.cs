using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Analisis.Data;
using MPM.Modules.Analisis.Services;
using MPM.Shared.Services;
using Npgsql;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Services;

/// <summary>
/// Cubre 029-fix-hallazgos-code-review-competidores-alertas FR-011/US7 (QA BUG-005): "Analizar
/// todo" solo procesaba <c>docList.First()</c> -- un único documento, ignorando el resto del
/// workspace sin avisar. Corre contra el Postgres real de docker-compose (localhost:5433),
/// mismo patrón que <see cref="AnalisisRecoveryWorkerTests"/>. IAnalisisBackgroundService se
/// mockea deliberadamente: llamar al real dispararía una llamada real a Gemini/Vertex AI.
/// </summary>
public class AnalisisServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    private readonly List<long> _workspaceIdsACleanup = new();
    private AnalisisHandler _handler = null!;
    private long _licitacionId;

    static AnalisisServiceTests()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public async Task InitializeAsync()
    {
        _handler = new AnalisisHandler(new DbConnectionFactory(TestConnectionString));

        // Fixture autocontenido: inserta su propia licitación (codigo único) en vez de
        // depender de datos preexistentes — funciona en BD fresca (CI) y en BD viva.
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        _licitacionId = await conn.ExecuteScalarAsync<long>(
            "INSERT INTO licitaciones (codigo_externo, nombre, codigo_estado, tipo) VALUES (@codigo, @nombre, 1, @tipo) RETURNING id",
            new { codigo = $"TEST-{Guid.NewGuid():N}", nombre = "Licitación fixture tests service", tipo = "LE" });
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
        await conn.ExecuteAsync("DELETE FROM licitaciones WHERE id = @id", new { id = _licitacionId });
    }

    private AnalisisService BuildService(Mock<IAnalisisBackgroundService> backgroundMock)
    {
        // GeminiService real resolvería un cliente LLM (llamada externa); se mockea solo el
        // nombre del modelo. Requiere GetModelNameAsync virtual (revertir ese virtual rompe estos tests).
        var geminiMock = new Mock<GeminiService>(null!, NullLogger<GeminiService>.Instance);
        geminiMock.Setup(m => m.GetModelNameAsync(It.IsAny<CancellationToken>())).ReturnsAsync("gemini-2.5-pro");
        return new(_handler, geminiMock.Object, Mock.Of<IStorageService>(), backgroundMock.Object,
            NullLogger<AnalisisService>.Instance);
    }

    [Fact]
    public async Task AnalizarAsync_SinDocumentoId_EncolaTodosLosDocumentosDelWorkspace()
    {
        var (workspaceId, error) = await _handler.CrearWorkspaceAsync(_licitacionId, $"Test US7 {Guid.NewGuid()}", "test-user");
        error.Should().BeNull();
        _workspaceIdsACleanup.Add(workspaceId);

        var (doc1Id, err1) = await _handler.CrearDocumentoAsync(workspaceId, "acta-evaluacion.pdf", "application/pdf", 1024, $"local/{workspaceId}-1.pdf");
        err1.Should().BeNull();
        var (doc2Id, err2) = await _handler.CrearDocumentoAsync(workspaceId, "resolucion-adjudicacion.pdf", "application/pdf", 2048, $"local/{workspaceId}-2.pdf");
        err2.Should().BeNull();
        var (doc3Id, err3) = await _handler.CrearDocumentoAsync(workspaceId, "resolucion-revocatoria.pdf", "application/pdf", 512, $"local/{workspaceId}-3.pdf");
        err3.Should().BeNull();

        var backgroundMock = new Mock<IAnalisisBackgroundService>();
        var service = BuildService(backgroundMock);

        var (resultado, analizarError) = await service.AnalizarAsync(workspaceId, documentoId: null);

        analizarError.Should().BeNull();
        resultado.Should().NotBeNull();
        resultado!.Estado.Should().Be("analizando");

        backgroundMock.Verify(b => b.EnqueueAnalisis(
            workspaceId,
            It.IsAny<long>(),
            It.Is<List<(long Id, string Nombre, string RutaStorage)>>(docs =>
                docs.Count == 3 &&
                docs.Any(d => d.Id == doc1Id) &&
                docs.Any(d => d.Id == doc2Id) &&
                docs.Any(d => d.Id == doc3Id))),
            Times.Once);
    }

    [Fact]
    public async Task AnalizarAsync_ConDocumentoIdExplicito_EncolaSoloEseDocumento()
    {
        // No debe regresionar el caso "analizar un documento específico" (documentoId provisto).
        var (workspaceId, error) = await _handler.CrearWorkspaceAsync(_licitacionId, $"Test US7 {Guid.NewGuid()}", "test-user");
        error.Should().BeNull();
        _workspaceIdsACleanup.Add(workspaceId);

        var (doc1Id, err1) = await _handler.CrearDocumentoAsync(workspaceId, "doc1.pdf", "application/pdf", 1024, $"local/{workspaceId}-1.pdf");
        err1.Should().BeNull();
        var (doc2Id, err2) = await _handler.CrearDocumentoAsync(workspaceId, "doc2.pdf", "application/pdf", 1024, $"local/{workspaceId}-2.pdf");
        err2.Should().BeNull();

        var backgroundMock = new Mock<IAnalisisBackgroundService>();
        var service = BuildService(backgroundMock);

        var (_, analizarError) = await service.AnalizarAsync(workspaceId, documentoId: doc2Id);

        analizarError.Should().BeNull();
        backgroundMock.Verify(b => b.EnqueueAnalisis(
            workspaceId, doc2Id,
            It.Is<List<(long Id, string Nombre, string RutaStorage)>>(docs => docs.Count == 1 && docs[0].Id == doc2Id)),
            Times.Once);
    }

    [Fact]
    public async Task AnalizarAsync_WorkspaceSinDocumentos_RetornaError()
    {
        var (workspaceId, error) = await _handler.CrearWorkspaceAsync(_licitacionId, $"Test US7 {Guid.NewGuid()}", "test-user");
        error.Should().BeNull();
        _workspaceIdsACleanup.Add(workspaceId);

        var backgroundMock = new Mock<IAnalisisBackgroundService>();
        var service = BuildService(backgroundMock);

        var (resultado, analizarError) = await service.AnalizarAsync(workspaceId, documentoId: null);

        resultado.Should().BeNull();
        analizarError.Should().StartWith("ANA_003");
        backgroundMock.Verify(b => b.EnqueueAnalisis(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<List<(long Id, string Nombre, string RutaStorage)>>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDashboardEjecutivoAsync_AniosDisponibles_UsaFechaRealDeLaLicitacion_NoCreadoEn()
    {
        // 029-fix-hallazgos-code-review-competidores-alertas (FR-018/US14, QA BUG-011): el
        // análisis se crea "hoy" (CreadoEn = año actual) pero la licitación que describe el
        // contenido_json fue adjudicada en 2025 -- AniosDisponibles debe listar 2025, no el año
        // de creación del registro. También verifica el filtro (usp_Analisis_ObtenerResultadosCompletos
        // V112): filtrar por anio=2025 debe devolver esta licitación aunque created_at sea de otro año.
        var (workspaceId, error) = await _handler.CrearWorkspaceAsync(_licitacionId, $"Test US14 {Guid.NewGuid()}", "test-user");
        error.Should().BeNull();
        _workspaceIdsACleanup.Add(workspaceId);

        var (docId, errDoc) = await _handler.CrearDocumentoAsync(workspaceId, "resolucion-adjudicacion.pdf", "application/pdf", 1024, $"local/{workspaceId}-1.pdf");
        errDoc.Should().BeNull();

        const string contenidoJson = """
        {
          "licitacion": { "fechas": { "publicacion": "2025-03-01", "adjudicacion": "2025-06-15" } },
          "analisis_tivit": { "es_ganador": true, "resultado": "Adjudicado" },
          "adjudicacion": { "adjudicatario": { "nombre": "TIVIT", "rut": "76.000.000-0" } }
        }
        """;
        var (resultadoId, errRes) = await _handler.CrearResultadoAsync(workspaceId, docId, contenidoJson, "gemini-2.5-pro", 100, 200);
        errRes.Should().BeNull();
        resultadoId.Should().BeGreaterThan(0);

        await using (var conn = new NpgsqlConnection(TestConnectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("UPDATE analisis_workspaces SET estado = 'completado' WHERE id = @id", new { id = workspaceId });
        }

        var service = BuildService(new Mock<IAnalisisBackgroundService>());

        // No se afirma NotContain(año actual) en la lista global: la base real puede tener otros
        // workspaces "completado" preexistentes de hoy (con o sin fecha real parseable) -- lo que
        // importa es que ESTE registro, creado hoy pero con licitacion.fechas.adjudicacion=2025,
        // aporte 2025 al conjunto (verificado abajo vía el filtro anio=2025).
        var (dashboardSinFiltro, errSinFiltro) = await service.GetDashboardEjecutivoAsync(anio: null);
        errSinFiltro.Should().BeNull();
        dashboardSinFiltro!.AniosDisponibles.Should().Contain(2025);

        var (dashboardFiltro2025, errFiltro) = await service.GetDashboardEjecutivoAsync(anio: 2025);
        errFiltro.Should().BeNull();
        dashboardFiltro2025!.Licitaciones.Should().Contain(l => l.WorkspaceId == workspaceId,
            "el filtro por año (SP usp_Analisis_ObtenerResultadosCompletos) debe usar la fecha real de la licitación, no created_at");
    }
}
