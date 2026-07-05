using MPM.Modules.Licitaciones.Data;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Data;

public class LicitacionStoredProceduresTests
{
    [Fact]
    public void Listar_ShouldContainCorrectFunctionName()
    {
        LicitacionStoredProcedures.Listar.Should()
            .Contain("usp_Licitaciones_Listar");
    }

    [Fact]
    public void Obtener_ShouldContainCorrectFunctionName()
    {
        LicitacionStoredProcedures.Obtener.Should()
            .Contain("usp_Licitaciones_ObtenerPorCodigo");
    }

    [Fact]
    public void Buscar_ShouldContainCorrectFunctionName()
    {
        LicitacionStoredProcedures.Buscar.Should()
            .Contain("usp_Licitaciones_Buscar");
    }

    [Fact]
    public void Estados_ShouldContainCorrectFunctionName()
    {
        LicitacionStoredProcedures.Estados.Should()
            .Contain("usp_Catalogos_EstadosLicitacion");
    }

    [Fact]
    public void SyncIniciar_ShouldContainCorrectProcedureName()
    {
        LicitacionStoredProcedures.SyncIniciar.Should()
            .Contain("usp_SyncLog_Iniciar");
    }

    [Fact]
    public void SyncFinalizar_ShouldContainCorrectProcedureName()
    {
        LicitacionStoredProcedures.SyncFinalizar.Should()
            .Contain("usp_SyncLog_Finalizar");
    }

    [Fact]
    public void MergeLicitaciones_ShouldContainCorrectProcedureName()
    {
        LicitacionStoredProcedures.MergeLicitaciones.Should()
            .Contain("usp_SyncEngine_MergeLicitaciones");
    }
}