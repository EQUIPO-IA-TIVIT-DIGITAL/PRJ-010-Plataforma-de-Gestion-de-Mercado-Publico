using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Licitaciones.Services;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Services;

/// <summary>018-buscador-inteligente-nl: LicitacionService.BuscarNaturalAsync orquesta la
/// interpretación de ConsultaSemanticaService antes de delegar a LicitacionHandler. Estos tests
/// verifican esa orquestación (fallback FR-005 y prioridad del estado explícito, US2) sin tocar
/// Postgres ni Vertex AI real.</summary>
public class LicitacionServiceTests
{
    private readonly Mock<LicitacionHandler> _handlerMock;
    private readonly Mock<ConsultaSemanticaService> _semanticaMock;
    private readonly LicitacionService _service;

    public LicitacionServiceTests()
    {
        var dbFactory = new DbConnectionFactory("Host=localhost;Database=unused");
        _handlerMock = new Mock<LicitacionHandler>(dbFactory);
        _semanticaMock = new Mock<ConsultaSemanticaService>(
            new HttpClient(), new ConfigurationBuilder().Build(),
            new MPM.Shared.Services.GoogleAdcTokenProvider(),
            NullLogger<ConsultaSemanticaService>.Instance);

        _service = new LicitacionService(
            NullLogger<LicitacionService>.Instance,
            new ConfigurationBuilder().Build(),
            _handlerMock.Object,
            null!, // SyncService no se usa en BuscarNaturalAsync
            null!, // ApiMpService no se usa en BuscarNaturalAsync
            _semanticaMock.Object);
    }

    private void SetupHandler(List<LicitacionNaturalSearchResult>? items = null, long totalCount = 0)
    {
        _handlerMock.Setup(h => h.BuscarNaturalAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<short?>(),
                It.IsAny<List<string>?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((items ?? new List<LicitacionNaturalSearchResult>(), totalCount));
    }

    [Fact]
    public async Task BuscarNaturalAsync_ReturnsValidationError_WhenQueryTooShort()
    {
        var (result, error) = await _service.BuscarNaturalAsync("a", 1, 20, null);

        result.Should().BeNull();
        error.Should().StartWith("VAL_001");
    }

    [Fact]
    public async Task BuscarNaturalAsync_FallsBackToLiteralQuery_WhenInterpretationIsNull()
    {
        SetupHandler();
        _semanticaMock.Setup(s => s.InterpretarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsultaSemanticaResult?)null);

        var (result, error) = await _service.BuscarNaturalAsync("ciberseguridad", 1, 20, null);

        error.Should().BeNull();
        result.Should().NotBeNull();
        _handlerMock.Verify(h => h.BuscarNaturalAsync(
            "ciberseguridad", 1, 20, null, null, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuscarNaturalAsync_FallsBackToLiteralQuery_WhenConfianzaIsBaja()
    {
        SetupHandler();
        _semanticaMock.Setup(s => s.InterpretarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultaSemanticaResult { Confianza = ConfianzaInterpretacion.Baja, TerminosExpandidos = ["ruido"] });

        var (result, error) = await _service.BuscarNaturalAsync("asdf qwerty", 1, 20, null);

        error.Should().BeNull();
        _handlerMock.Verify(h => h.BuscarNaturalAsync(
            "asdf qwerty", 1, 20, null, null, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuscarNaturalAsync_EnrichesQuery_WhenInterpretationIsConfident()
    {
        SetupHandler();
        var fechaDesdeInferida = new DateTime(2025, 1, 1);
        _semanticaMock.Setup(s => s.InterpretarAsync("ciberseguridad salud", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultaSemanticaResult
            {
                Confianza = ConfianzaInterpretacion.Alta,
                TerminosExpandidos = ["SOC", "seguridad de la información"],
                EstadoInferido = 8,
                MontoDesde = 10_000_000m,
                FechaDesde = fechaDesdeInferida,
            });

        await _service.BuscarNaturalAsync("ciberseguridad salud", 1, 20, estado: null);

        // 029-fix-hallazgos-code-review-competidores-alertas (FR-002): FechaDesde ya se
        // calculaba en ConsultaSemanticaResult pero nunca llegaba al handler -- este assert
        // falla si se vuelve a romper ese threading de parámetro.
        _handlerMock.Verify(h => h.BuscarNaturalAsync(
            "ciberseguridad salud", 1, 20,
            (short?)8,
            It.Is<List<string>>(t => t.Contains("SOC")),
            10_000_000m, null,
            fechaDesdeInferida, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuscarNaturalAsync_ExplicitEstado_TakesPriorityOverInferred()
    {
        SetupHandler();
        _semanticaMock.Setup(s => s.InterpretarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultaSemanticaResult
            {
                Confianza = ConfianzaInterpretacion.Alta,
                EstadoInferido = 8, // "adjudicadas", inferido de la consulta
            });

        // El usuario pasa estado=6 ("cerradas") explícito en el selector de UI -- debe ganar.
        await _service.BuscarNaturalAsync("telecomunicaciones", 1, 20, estado: 6);

        _handlerMock.Verify(h => h.BuscarNaturalAsync(
            "telecomunicaciones", 1, 20, (short?)6,
            It.IsAny<List<string>?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuscarNaturalAsync_NoAcotaFechaDesde_WhenInterpretationDoesNotInferOne()
    {
        // FR-002 (edge case de la spec): una consulta NL sin fecha explícita no debe requerir
        // que el usuario especifique una para obtener resultados -- FechaDesde debe llegar como
        // null al handler (que a su vez no debe acotar el rango), no con un valor por defecto.
        SetupHandler();
        _semanticaMock.Setup(s => s.InterpretarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultaSemanticaResult
            {
                Confianza = ConfianzaInterpretacion.Alta,
                TerminosExpandidos = ["nube"],
            });

        await _service.BuscarNaturalAsync("servicios cloud", 1, 20, estado: null);

        _handlerMock.Verify(h => h.BuscarNaturalAsync(
            "servicios cloud", 1, 20, null,
            It.IsAny<List<string>?>(), null, null,
            null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // spec 031 (US1): filtro por área de negocio en el listado principal
    [Fact]
    public async Task ListarAsync_PassesAreaAndSinClasificar_ToHandler()
    {
        _handlerMock.Setup(h => h.ListarAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<short?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<short?>(), It.IsAny<bool?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<LicitacionResumenDto>(), 0));

        await _service.ListarAsync(1, 20, null, null, null, null, null, null,
            "fecha_publicacion", "desc", area: 2, sinClasificar: null);

        _handlerMock.Verify(h => h.ListarAsync(
            1, 20, null, null, null, null, null, null,
            "fecha_publicacion", "desc", (short?)2, (bool?)null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // spec 031 (US2): estadísticas por estado — los 5 estados reales siempre aparecen
    [Fact]
    public async Task ContarPorEstadoAsync_ReturnsAllRealStates_EvenWithZeroCount()
    {
        var stats = new List<EstadoConteoDto>
        {
            new() { CodigoEstado = 5, NombreEstado = "Publicada", Cantidad = 4210 },
            new() { CodigoEstado = 6, NombreEstado = "Cerrada", Cantidad = 0 },
            new() { CodigoEstado = 7, NombreEstado = "Desierta", Cantidad = 0 },
            new() { CodigoEstado = 8, NombreEstado = "Adjudicada", Cantidad = 5990 },
            new() { CodigoEstado = 15, NombreEstado = "Revocada", Cantidad = 41 },
        };
        _handlerMock.Setup(h => h.ContarPorEstadoAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var result = await _service.ContarPorEstadoAsync(null, null);

        result.Should().HaveCount(5);
        result.Select(r => r.CodigoEstado).Should().BeEquivalentTo(new short[] { 5, 6, 7, 8, 15 });
    }
}
