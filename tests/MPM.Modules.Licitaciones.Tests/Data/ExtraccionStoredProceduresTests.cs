using MPM.Modules.Licitaciones.Data;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Data;

public class ExtraccionStoredProceduresTests
{
    [Fact]
    public void Registrar_ShouldContainCorrectFunctionName()
    {
        ExtraccionStoredProcedures.Registrar.Should().Contain("usp_ExtraccionLog_Registrar");
    }

    [Fact]
    public void ResumenPeriodo_ShouldContainCorrectFunctionName()
    {
        ExtraccionStoredProcedures.ResumenPeriodo.Should().Contain("usp_ExtraccionLog_ResumenPeriodo");
    }

    [Fact]
    public void ExistePorLicitacion_ShouldContainCorrectFunctionName()
    {
        ExtraccionStoredProcedures.ExistePorLicitacion.Should().Contain("usp_Adjuntos_ExistePorLicitacion");
    }

    [Fact]
    public void RegistrarAdjuntoDirecto_ShouldContainCorrectFunctionName()
    {
        ExtraccionStoredProcedures.RegistrarAdjuntoDirecto.Should().Contain("usp_Adjuntos_RegistrarDirecto");
    }
}
