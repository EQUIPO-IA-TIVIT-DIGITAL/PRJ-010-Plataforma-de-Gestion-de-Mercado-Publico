using FluentAssertions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Models;
using MPM.Modules.Propuestas.Services;
using Xunit;

namespace MPM.Modules.Propuestas.Tests.Services;

public class PropuestasRecomendacionServiceTests
{
    [Theory]
    [InlineData("ISO 27001", "ISO/IEC 27001", 1.0)]
    [InlineData("27001", "ISO 27001", 1.0)]
    public void Score_NormalizedOrSubstringMatch_UsesDeterministicScore(string requirement, string name, decimal expected)
        => PropuestasRecomendacionService.Score(requirement, name).Should().Be(expected);

    [Fact]
    public async Task RecomendarAsync_CertificationMatch_ReturnsCategoryWithoutPersisting()
    {
        var handler = new Mock<PropuestasHandler>(new DbConnectionFactory("Host=unused"));
        handler.Setup(h => h.ListarCertificacionesAsync(null, true, null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<CertificacionCatalogoDto>
            {
                Items = [new CertificacionCatalogoDto { Id = 9, Nombre = "ISO 27001", FileIdCensus = "file-1", Activo = true }],
                Page = 1, Size = 100, TotalRecords = 1, TotalPages = 1,
            });
        var service = new PropuestasRecomendacionService(handler.Object, null!, null!);

        var result = await service.RecomendarAsync(new RecomendacionRequest
        {
            Requisitos = new RequisitosRecomendacionDto { Certificaciones = ["27001"] },
        });

        result.Certificaciones.Should().ContainSingle();
        result.Certificaciones[0].Categoria.Should().Be("recomendado");
        result.Experiencias.Should().BeEmpty("Bundle B define el proveedor de experiencias; no se inventa otro en Bundle A");
        handler.Verify(h => h.ListarCertificacionesAsync(null, true, null, null, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }
}
