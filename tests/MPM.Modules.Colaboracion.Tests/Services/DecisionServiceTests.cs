using FluentAssertions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Colaboracion.Data;
using MPM.Modules.Colaboracion.Models;
using MPM.Modules.Colaboracion.Services;
using Xunit;

namespace MPM.Modules.Colaboracion.Tests.Services;

/// <summary>
/// 036-flujo-comercial-ofertas (Fase 2, DEC-R001..R006): contrato de DecisionService —
/// validación de decisión go/no_go (DEC_002 / VAL_001), motivo obligatorio en NO GO y snapshot
/// de la recomendación IA del último análisis completado. No toca Postgres: mockea DecisionHandler.
/// </summary>
public class DecisionServiceTests
{
    private readonly Mock<DecisionHandler> _handlerMock;
    private readonly DecisionService _service;

    public DecisionServiceTests()
    {
        var dbFactory = new DbConnectionFactory("Host=localhost;Database=unused");
        _handlerMock = new Mock<DecisionHandler>(dbFactory);
        _service = new DecisionService(_handlerMock.Object);
    }

    private static DecisionRequest Request(string decision, string? motivo = null)
        => new() { Decision = decision, Motivo = motivo };

    [Fact]
    public async Task RegistrarAsync_NoGoSinMotivo_LanzaDec002()
    {
        var act = async () => await _service.RegistrarAsync(10, "729-134-LE26", "gerente@tivit.cl", Request("no_go"));

        var ex = await act.Should().ThrowAsync<DecisionService.DecisionValidationException>();
        ex.Which.ErrorCode.Should().Be("DEC_002");
        _handlerMock.Verify(h => h.RegistrarAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "sin motivo no se puede registrar un NO GO");
    }

    [Fact]
    public async Task RegistrarAsync_NoGoMotivoCorto_LanzaDec002()
    {
        var act = async () => await _service.RegistrarAsync(10, "729-134-LE26", "gerente@tivit.cl", Request("no_go", "corto"));

        var ex = await act.Should().ThrowAsync<DecisionService.DecisionValidationException>();
        ex.Which.ErrorCode.Should().Be("DEC_002", "el motivo en NO GO debe tener mínimo 10 caracteres (DEC-R002)");
    }

    [Fact]
    public async Task RegistrarAsync_GoConMotivo_RegistraConSnapshotDeLaRecomendacionIA()
    {
        _handlerMock.Setup(h => h.RecomendacionAnalisisAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(("go", 0.85m));

        var dto = await _service.RegistrarAsync(10, "729-134-LE26", "gerente@tivit.cl",
            Request("GO", "Alta probabilidad de adjudicación con margen positivo"));

        _handlerMock.Verify(h => h.RegistrarAsync(
            10, "go", "Alta probabilidad de adjudicación con margen positivo", "go", 0.85m, "gerente@tivit.cl",
            It.IsAny<CancellationToken>()), Times.Once,
            "el snapshot IA del último análisis completado se copia al decidir (DEC-R005)");
        dto.Decision.Should().Be("go", "DEC-R001 normaliza la decisión a minúsculas");
        dto.RecomendacionIa.Should().Be("go");
        dto.ScoreConfianza.Should().Be(0.85m);
        dto.CodigoExterno.Should().Be("729-134-LE26");
        dto.DecididoPor.Should().Be("gerente@tivit.cl");
    }

    [Fact]
    public async Task RegistrarAsync_DecisionInvalida_LanzaDec002()
    {
        var act = async () => await _service.RegistrarAsync(10, "729-134-LE26", "gerente@tivit.cl",
            Request("talvez", "motivo suficientemente largo"));

        var ex = await act.Should().ThrowAsync<DecisionService.DecisionValidationException>();
        ex.Which.ErrorCode.Should().Be("DEC_002", "solo se aceptan 'go' o 'no_go'");
        _handlerMock.Verify(h => h.RegistrarAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarAsync_DecisionVacia_LanzaVal001()
    {
        var act = async () => await _service.RegistrarAsync(10, "729-134-LE26", "gerente@tivit.cl", Request(""));

        var ex = await act.Should().ThrowAsync<DecisionService.DecisionValidationException>();
        ex.Which.ErrorCode.Should().Be("VAL_001", "el campo decision es obligatorio");
    }

    [Fact]
    public async Task RegistrarAsync_GoSinAnalisisCompletado_SnapshotNull()
    {
        _handlerMock.Setup(h => h.RecomendacionAnalisisAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ((string?)null, (decimal?)null));

        var dto = await _service.RegistrarAsync(10, "729-134-LE26", "gerente@tivit.cl",
            Request("go", "Operación viable dentro del plan comercial"));

        dto.RecomendacionIa.Should().BeNull("sin análisis completado la decisión es 100 % humana (DEC-R005)");
        dto.ScoreConfianza.Should().BeNull();
        _handlerMock.Verify(h => h.RegistrarAsync(10, "go", "Operación viable dentro del plan comercial", null, null, "gerente@tivit.cl", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObtenerAsync_NotificadosNull_MantieneCompatibilidadConFilasAntiguas()
    {
        _handlerMock.Setup(h => h.ObtenerAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecisionHandler.DecisionRow
            {
                Id = 7, LicitacionId = 10, Decision = "go", Notificados = null, NotificadoAt = null,
            });

        var result = await _service.ObtenerAsync(10);

        result.DecisionId.Should().Be(7);
        result.Notificados.Should().BeNull();
        result.NotificadoAt.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerAsync_NotificadosJsonValido_DeserializaDestinatariosYFecha()
    {
        var notifiedAt = new DateTime(2026, 8, 16, 18, 0, 0, DateTimeKind.Utc);
        _handlerMock.Setup(h => h.ObtenerAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecisionHandler.DecisionRow
            {
                Id = 7, LicitacionId = 10, Decision = "no_go",
                Notificados = "[\"persona-a@ejemplo.test\",\"persona-b@ejemplo.test\"]",
                NotificadoAt = notifiedAt,
            });

        var result = await _service.ObtenerAsync(10);

        result.Notificados.Should().Equal("persona-a@ejemplo.test", "persona-b@ejemplo.test");
        result.NotificadoAt.Should().Be(notifiedAt);
    }
}
