using MPM.Modules.Analisis.Data;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Data;

public class AnalisisStoredProceduresTests
{
    [Theory]
    [InlineData("WorkspacesCrear", "usp_AnalisisWorkspaces_Crear", "CALL")]
    [InlineData("WorkspacesListar", "usp_AnalisisWorkspaces_Listar", "SELECT")]
    [InlineData("WorkspacesObtener", "usp_AnalisisWorkspaces_Obtener", "SELECT")]
    [InlineData("WorkspacesActualizarEstado", "usp_AnalisisWorkspaces_ActualizarEstado", "CALL")]
    [InlineData("WorkspacesEliminar", "usp_AnalisisWorkspaces_Eliminar", "CALL")]
    [InlineData("DocumentosCrear", "usp_AnalisisDocumentos_Crear", "CALL")]
    [InlineData("DocumentosListar", "usp_AnalisisDocumentos_Listar", "SELECT")]
    [InlineData("DocumentosObtener", "usp_AnalisisDocumentos_Obtener", "SELECT")]
    [InlineData("ResultadosCrear", "usp_AnalisisResultados_Crear", "CALL")]
    [InlineData("ResultadosObtenerPorWorkspace", "usp_AnalisisResultados_ObtenerPorWorkspace", "SELECT")]
    [InlineData("ChatObtenerOCrearConversacion", "usp_AnalisisChat_ObtenerOCrearConversacion", "CALL")]
    [InlineData("ChatEnviarMensaje", "usp_AnalisisChat_EnviarMensaje", "CALL")]
    [InlineData("ChatObtenerHistorial", "usp_AnalisisChat_ObtenerHistorial", "SELECT")]
    public void ProcedureConstant_ShouldHaveExpectedNameAndVerb(string fieldName, string expectedSpName, string expectedVerb)
    {
        var field = typeof(AnalisisStoredProcedures).GetField(fieldName);
        field.Should().NotBeNull($"constant {fieldName} must exist");
        var value = field!.GetRawConstantValue() as string;
        value.Should().NotBeNullOrWhiteSpace();
        value.Should().StartWith(expectedVerb);
        value.Should().Contain(expectedSpName);
        value.Should().EndWith(")");
    }

    [Fact]
    public void ProcedureConstants_AllHaveMatchingParentheses()
    {
        var fields = typeof(AnalisisStoredProcedures)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        foreach (var f in fields)
        {
            if (f.IsLiteral && f.FieldType == typeof(string))
            {
                var value = f.GetRawConstantValue() as string;
                value!.Count(c => c == '(').Should().Be(value!.Count(c => c == ')'),
                    $"constant {f.Name} must have balanced parentheses");
            }
        }
    }

    [Fact]
    public void WorkspacesCrear_ShouldUseCallSyntax()
    {
        AnalisisStoredProcedures.WorkspacesCrear.Should().StartWith("CALL ",
            "usp_AnalisisWorkspaces_Crear is a PROCEDURE (not FUNCTION) and must be invoked with CALL");
    }
}
