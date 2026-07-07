using MPM.Modules.Alertas.Data;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Alertas.Tests.Data;

public class AlertasStoredProceduresTests
{
    [Fact]
    public void Crear_ShouldContainCorrectFunctionName()
    {
        AlertasStoredProcedures.Crear.Should().Contain("usp_Alertas_Crear");
    }

    [Fact]
    public void Listar_ShouldContainCorrectFunctionName()
    {
        AlertasStoredProcedures.Listar.Should().Contain("usp_Alertas_Listar");
    }

    [Fact]
    public void ListarActivas_ShouldContainCorrectFunctionName()
    {
        AlertasStoredProcedures.ListarActivas.Should().Contain("usp_Alertas_ListarActivas");
    }

    [Fact]
    public void Toggle_ShouldContainCorrectFunctionName()
    {
        AlertasStoredProcedures.Toggle.Should().Contain("usp_Alertas_Toggle");
    }

    [Fact]
    public void RegistrarDisparo_ShouldContainCorrectFunctionName()
    {
        AlertasStoredProcedures.RegistrarDisparo.Should().Contain("usp_AlertasDisparadas_Registrar");
    }

    [Fact]
    public void ExisteParaLicitacion_ShouldContainCorrectFunctionName()
    {
        AlertasStoredProcedures.ExisteParaLicitacion.Should().Contain("usp_AlertasDisparadas_ExisteParaLicitacion");
    }
}
