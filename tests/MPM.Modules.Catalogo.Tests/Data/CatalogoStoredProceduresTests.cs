using MPM.Modules.Catalogo.Data;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Catalogo.Tests.Data;

public class CatalogoStoredProceduresTests
{
    [Fact]
    public void Estados_ContainsCorrectFunctionName()
    {
        CatalogoStoredProcedures.Estados.Should().Contain("usp_Catalogos_EstadosLicitacion");
    }

    [Fact]
    public void TiposLicitacion_ContainsCorrectFunctionName()
    {
        CatalogoStoredProcedures.TiposLicitacion.Should().Contain("usp_Catalogos_TiposLicitacion");
    }

    [Fact]
    public void Monedas_ContainsCorrectFunctionName()
    {
        CatalogoStoredProcedures.Monedas.Should().Contain("usp_Catalogos_Monedas");
    }

    [Fact]
    public void Estados_IsSelectStatement()
    {
        CatalogoStoredProcedures.Estados.Should().StartWith("SELECT");
    }

    [Fact]
    public void TiposLicitacion_IsSelectStatement()
    {
        CatalogoStoredProcedures.TiposLicitacion.Should().StartWith("SELECT");
    }

    [Fact]
    public void Monedas_IsSelectStatement()
    {
        CatalogoStoredProcedures.Monedas.Should().StartWith("SELECT");
    }
}