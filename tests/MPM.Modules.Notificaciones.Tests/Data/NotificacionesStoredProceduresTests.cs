using MPM.Modules.Notificaciones.Data;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Notificaciones.Tests.Data;

public class NotificacionesStoredProceduresTests
{
    [Fact]
    public void Eliminar_ContainsCorrectFunctionName()
    {
        NotificacionesStoredProcedures.Eliminar.Should().Contain("usp_Notificaciones_Eliminar");
    }

    [Fact]
    public void Eliminar_FiltersByUsuario()
    {
        NotificacionesStoredProcedures.Eliminar.Should().Contain("@p_usuario_id");
    }

    [Fact]
    public void Eliminar_ReceivesId()
    {
        NotificacionesStoredProcedures.Eliminar.Should().Contain("@p_id");
    }

    [Fact]
    public void EliminarTodas_ContainsCorrectFunctionName()
    {
        NotificacionesStoredProcedures.EliminarTodas.Should().Contain("usp_Notificaciones_EliminarTodas");
    }

    [Fact]
    public void EliminarTodas_FiltersByUsuario()
    {
        NotificacionesStoredProcedures.EliminarTodas.Should().Contain("@p_usuario_id");
    }

    [Fact]
    public void AllProcedures_AreSelectStatements()
    {
        NotificacionesStoredProcedures.Eliminar.Should().StartWith("SELECT");
        NotificacionesStoredProcedures.EliminarTodas.Should().StartWith("SELECT");
    }
}
