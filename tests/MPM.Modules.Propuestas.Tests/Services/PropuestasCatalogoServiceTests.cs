using FluentAssertions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Models;
using MPM.Modules.Propuestas.Services;
using Xunit;

namespace MPM.Modules.Propuestas.Tests.Services;

public class PropuestasCatalogoServiceTests
{
    [Fact]
    public async Task CrearCertificacion_EquivalentName_NormalizesBeforePersisting()
    {
        var handler = new Mock<PropuestasHandler>(new DbConnectionFactory("Host=unused"));
        handler.Setup(h => h.CrearCertificacionAsync(
                It.Is<CertificacionCatalogoRequest>(r => r.Nombre == "ISO 27001"),
                "iso 27001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CertificacionCatalogoDto { Id = 7, Nombre = "ISO 27001", Activo = true });
        var service = new PropuestasCatalogoService(handler.Object);

        var result = await service.CrearCertificacionAsync(new CertificacionCatalogoRequest { Nombre = " ISO/IEC 27001 " });

        result.Id.Should().Be(7);
        handler.VerifyAll();
    }

    [Fact]
    public async Task ListarExperiencias_InvalidPageSize_RejectsBeforeDatabase()
    {
        var handler = new Mock<PropuestasHandler>(new DbConnectionFactory("Host=unused"));
        var service = new PropuestasCatalogoService(handler.Object);

        var act = () => service.ListarExperienciasAsync(null, true, 1, 101);

        await act.Should().ThrowAsync<PropuestasCatalogoService.PropuestasValidationException>()
            .WithMessage("size debe estar entre 1 y 100");
        handler.Verify(h => h.ListarExperienciasAsync(It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActualizarExperiencia_ValidRequest_DelegatesCrudToHandler()
    {
        var handler = new Mock<PropuestasHandler>(new DbConnectionFactory("Host=unused"));
        handler.Setup(h => h.ActualizarExperienciaAsync(12, It.IsAny<ExperienciaCatalogoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExperienciaCatalogoDto { Id = 12, Titulo = "Proyecto", Cliente = "Cliente", Activo = true });
        var service = new PropuestasCatalogoService(handler.Object);

        var result = await service.ActualizarExperienciaAsync(12, new ExperienciaCatalogoRequest { Titulo = "Proyecto", Cliente = "Cliente" });

        result.Id.Should().Be(12);
        handler.Verify(h => h.ActualizarExperienciaAsync(12, It.Is<ExperienciaCatalogoRequest>(r => r.Titulo == "Proyecto"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EliminarCapitulo_ValidId_UsesSoftDeleteHandlerOperation()
    {
        var handler = new Mock<PropuestasHandler>(new DbConnectionFactory("Host=unused"));
        handler.Setup(h => h.EliminarCapituloAsync(4, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new PropuestasCatalogoService(handler.Object);

        await service.EliminarCapituloAsync(4);

        handler.Verify(h => h.EliminarCapituloAsync(4, It.IsAny<CancellationToken>()), Times.Once);
    }
}
