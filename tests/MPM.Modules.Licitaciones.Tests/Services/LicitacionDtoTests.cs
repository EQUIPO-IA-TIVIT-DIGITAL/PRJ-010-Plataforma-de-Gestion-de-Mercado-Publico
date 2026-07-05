using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Licitaciones.Services;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Services;

public class LicitacionResumenDtoTests
{
    [Fact]
    public void LicitacionResumenDto_ShouldInitializeWithDefaults()
    {
        var dto = new LicitacionResumenDto();
        dto.CodigoExterno.Should().BeEmpty();
        dto.Nombre.Should().BeEmpty();
        dto.Tipo.Should().BeEmpty();
        dto.Organismo.Should().BeEmpty();
        dto.Moneda.Should().BeEmpty();
        dto.ItemsCount.Should().Be(0);
        dto.Estado.Should().NotBeNull();
    }

    [Fact]
    public void LicitacionResumenDto_Estado_ShouldMapCorrectly()
    {
        var dto = new LicitacionResumenDto
        {
            CodigoEstado = 5,
            EstadoNombre = "Adjudicada"
        };

        dto.Estado.Codigo.Should().Be(5);
        dto.Estado.Nombre.Should().Be("Adjudicada");
    }

    [Fact]
    public void LicitacionDetalleDto_ShouldInheritFromResumen()
    {
        var dto = new LicitacionDetalleDto();
        dto.CodigoExterno.Should().BeEmpty();
        dto.Items.Should().NotBeNull();
        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public void LicitacionItemDto_ShouldInitializeWithDefaults()
    {
        var dto = new LicitacionItemDto();
        dto.Nombre.Should().BeEmpty();
        dto.Codigo.Should().Be(0);
    }

    [Fact]
    public void LicitacionFilter_ShouldHaveCorrectDefaults()
    {
        var filter = new LicitacionFilter();
        filter.Page.Should().Be(1);
        filter.PageSize.Should().Be(20);
        filter.SortBy.Should().Be("fecha_publicacion");
        filter.SortDir.Should().Be("desc");
        filter.Search.Should().BeNull();
        filter.Estado.Should().BeNull();
        filter.Tipo.Should().BeNull();
    }

    [Fact]
    public void PaginatedResult_ShouldCalculateTotalPages()
    {
        var result = new PaginatedResult<LicitacionResumenDto>
        {
            Items = new List<LicitacionResumenDto>(),
            Page = 1,
            PageSize = 20,
            TotalRecords = 45
        };

        result.TotalPages.Should().Be(3); // 45 / 20 = ceil(2.25) = 3
    }

    [Fact]
    public void PaginatedResult_ShouldHandleZeroRecords()
    {
        var result = new PaginatedResult<LicitacionResumenDto>
        {
            Items = new List<LicitacionResumenDto>(),
            Page = 1,
            PageSize = 20,
            TotalRecords = 0
        };

        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public void SyncStatusDto_ShouldInitializeWithDefaults()
    {
        var dto = new SyncStatusDto();
        dto.Status.Should().BeEmpty();
        dto.SyncId.Should().Be(0);
    }

    [Fact]
    public void LicitacionSearchResult_ShouldInitializeWithDefaults()
    {
        var result = new LicitacionSearchResult();
        result.CodigoExterno.Should().BeEmpty();
        result.Nombre.Should().BeEmpty();
        result.Tipo.Should().BeEmpty();
        result.Organismo.Should().BeEmpty();
    }
}