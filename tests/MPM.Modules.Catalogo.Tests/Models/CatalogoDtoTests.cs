using MPM.Modules.Catalogo.Models;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Catalogo.Tests.Models;

public class CatalogoDtoTests
{
    [Fact]
    public void EstadoItemDto_DefaultValues()
    {
        var dto = new EstadoItemDto();
        dto.Codigo.Should().Be(0);
        dto.Nombre.Should().BeEmpty();
    }

    [Fact]
    public void EstadoItemDto_WithValues()
    {
        var dto = new EstadoItemDto { Codigo = 5, Nombre = "Adjudicada" };
        dto.Codigo.Should().Be(5);
        dto.Nombre.Should().Be("Adjudicada");
    }

    [Fact]
    public void TipoLicitacionItemDto_DefaultValues()
    {
        var dto = new TipoLicitacionItemDto();
        dto.Codigo.Should().Be(0);
        dto.Nombre.Should().BeEmpty();
        dto.Slug.Should().BeEmpty();
    }

    [Fact]
    public void TipoLicitacionItemDto_WithValues()
    {
        var dto = new TipoLicitacionItemDto { Codigo = 1, Nombre = "Licitación Pública", Slug = "publica" };
        dto.Codigo.Should().Be(1);
        dto.Nombre.Should().Be("Licitación Pública");
        dto.Slug.Should().Be("publica");
    }

    [Fact]
    public void MonedaItemDto_DefaultValues()
    {
        var dto = new MonedaItemDto();
        dto.Codigo.Should().Be(0);
        dto.Nombre.Should().BeEmpty();
        dto.Simbolo.Should().BeEmpty();
        dto.CodigoIso.Should().BeEmpty();
    }

    [Fact]
    public void MonedaItemDto_WithValues()
    {
        var dto = new MonedaItemDto { Codigo = 1, Nombre = "Peso Chileno", Simbolo = "$", CodigoIso = "CLP" };
        dto.Codigo.Should().Be(1);
        dto.Nombre.Should().Be("Peso Chileno");
        dto.Simbolo.Should().Be("$");
        dto.CodigoIso.Should().Be("CLP");
    }

    [Fact]
    public void CatalogosResponseDto_DefaultValues()
    {
        var dto = new CatalogosResponseDto();
        dto.EstadosLicitacion.Should().NotBeNull();
        dto.EstadosLicitacion.Should().BeEmpty();
        dto.TiposLicitacion.Should().NotBeNull();
        dto.TiposLicitacion.Should().BeEmpty();
        dto.Monedas.Should().NotBeNull();
        dto.Monedas.Should().BeEmpty();
    }

    [Fact]
    public void CatalogosResponseDto_WithValues()
    {
        var dto = new CatalogosResponseDto
        {
            EstadosLicitacion = [new EstadoItemDto { Codigo = 5, Nombre = "Adjudicada" }],
            TiposLicitacion = [new TipoLicitacionItemDto { Codigo = 1, Nombre = "LP", Slug = "publica" }],
            Monedas = [new MonedaItemDto { Codigo = 1, Simbolo = "$", CodigoIso = "CLP" }]
        };
        dto.EstadosLicitacion.Should().HaveCount(1);
        dto.TiposLicitacion.Should().HaveCount(1);
        dto.Monedas.Should().HaveCount(1);
        dto.EstadosLicitacion[0].Nombre.Should().Be("Adjudicada");
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "LE")]
    [InlineData(5, "Adjudicada")]
    public void EstadoItemDto_Theories(int codigo, string nombre)
    {
        var dto = new EstadoItemDto { Codigo = codigo, Nombre = nombre };
        dto.Codigo.Should().Be(codigo);
        dto.Nombre.Should().Be(nombre);
    }

    [Theory]
    [InlineData("CLP", "$")]
    [InlineData("USD", "US$")]
    [InlineData("EUR", "€")]
    public void MonedaItemDto_Simbolos(string iso, string simbolo)
    {
        var dto = new MonedaItemDto { CodigoIso = iso, Simbolo = simbolo };
        dto.CodigoIso.Should().Be(iso);
        dto.Simbolo.Should().Be(simbolo);
    }
}