using FluentAssertions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Services;
using Xunit;

namespace MPM.Modules.Propuestas.Tests.Services;

public sealed class DecisionAvisoServiceTests
{
    private readonly Mock<PropuestasHandler> _handler = new(new DbConnectionFactory("Host=unused"));
    private readonly Mock<IProposalLicitacionLookup> _lookup = new();
    private readonly Mock<IDecisionAvisoNotifier> _notifier = new();

    private DecisionAvisoService Create() => new(_handler.Object, _lookup.Object, _notifier.Object);

    private void GivenDecision(long id = 7, string decision = "go")
    {
        _lookup.Setup(x => x.ObtenerPorCodigoAsync("1425525-3-LE26", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 42, CodigoExterno = "1425525-3-LE26" });
        _handler.Setup(x => x.ObtenerDecisionAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecisionProposalRow { Id = id, LicitacionId = 42, Decision = decision });
        _notifier.Setup(x => x.CrearAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
    }

    [Fact]
    public async Task AvisarAsync_DestinatariosValidos_CreaAvisosYPersisteLaListaSeleccionada()
    {
        GivenDecision();

        var result = await Create().AvisarAsync(
            "1425525-3-LE26", 7,
            ["  persona-a@ejemplo.test ", "persona-b@ejemplo.test"]);

        result.Notificados.Should().Equal("persona-a@ejemplo.test", "persona-b@ejemplo.test");
        result.Enviados.Should().Be(2);
        _notifier.Verify(x => x.CrearAsync(
            It.IsAny<string>(), "1425525-3-LE26", "go", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _handler.Verify(x => x.ActualizarDecisionNotificadosAsync(
            7, It.Is<string>(json => json.Contains("persona-a@ejemplo.test") && json.Contains("persona-b@ejemplo.test")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AvisarAsync_ListaVacia_LanzaPro007YSinNotificar()
    {
        var act = () => Create().AvisarAsync("1425525-3-LE26", 7, []);

        var exception = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        exception.Which.Code.Should().Be("PRO_007");
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AvisarAsync_EmailInvalido_LanzaPro007()
    {
        var act = () => Create().AvisarAsync("1425525-3-LE26", 7, ["no-es-un-email"]);

        var exception = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        exception.Which.Code.Should().Be("PRO_007");
    }

    [Fact]
    public async Task AvisarAsync_MasDeCincuentaDestinatarios_LanzaPro007()
    {
        var recipients = Enumerable.Range(1, 51).Select(i => $"persona-{i}@ejemplo.test").ToList();

        var act = () => Create().AvisarAsync("1425525-3-LE26", 7, recipients);

        var exception = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        exception.Which.Code.Should().Be("PRO_007");
    }

    [Fact]
    public async Task AvisarAsync_DecisionInexistente_LanzaPro011()
    {
        _lookup.Setup(x => x.ObtenerPorCodigoAsync("1425525-3-LE26", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 42, CodigoExterno = "1425525-3-LE26" });
        _handler.Setup(x => x.ObtenerDecisionAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DecisionProposalRow?)null);

        var act = () => Create().AvisarAsync("1425525-3-LE26", 7, ["persona@ejemplo.test"]);

        var exception = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        exception.Which.Code.Should().Be("PRO_011");
        _handler.Verify(x => x.ActualizarDecisionNotificadosAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AvisarAsync_DecisionAjena_LanzaPro012YSinPersistir()
    {
        GivenDecision(id: 8);

        var act = () => Create().AvisarAsync("1425525-3-LE26", 7, ["persona@ejemplo.test"]);

        var exception = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        exception.Which.Code.Should().Be("PRO_012");
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AvisarAsync_FalloParcial_NoPersisteNiRespondeExito()
    {
        GivenDecision();
        _notifier.Setup(x => x.CrearAsync(
                "persona-b@ejemplo.test", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("SYS_001: fallo controlado");

        var act = () => Create().AvisarAsync(
            "1425525-3-LE26", 7,
            ["persona-a@ejemplo.test", "persona-b@ejemplo.test"]);

        var exception = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        exception.Which.Code.Should().Be("PRO_012");
        exception.Which.Message.Should().Contain("decisión se conservó");
        _handler.Verify(x => x.ActualizarDecisionNotificadosAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
