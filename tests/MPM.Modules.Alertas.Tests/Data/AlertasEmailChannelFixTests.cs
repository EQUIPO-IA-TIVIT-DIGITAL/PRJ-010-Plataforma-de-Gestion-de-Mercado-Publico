using FluentAssertions;
using Xunit;

namespace MPM.Modules.Alertas.Tests.Data;

// 024-inteligencia-competencia-alertas / US3: canal de alertas por correo, adicional a
// Telegram. Guarda de regresion source-level contra el SQL de V099 (mismo patron que
// AlertasDestinatariosTelegramFixTests, ya que este test project no monta una conexion
// Postgres real).
public class AlertasEmailChannelFixTests
{
    [Fact]
    public void V099_AgregaColumnaEmailAlertas()
    {
        var source = File.ReadAllText(FindMigration("V099__Add_Email_Alertas_Destinatarios.sql")).Replace("\r\n", "\n");

        source.Should().Contain("ADD COLUMN IF NOT EXISTS email_alertas",
            "debe agregar la columna de forma idempotente (IF NOT EXISTS)");
    }

    [Fact]
    public void V099_GuardarEmail_MarcaEsAccountManagerGobiernoEnInsertYUpdate()
    {
        var source = File.ReadAllText(FindMigration("V099__Add_Email_Alertas_Destinatarios.sql")).Replace("\r\n", "\n");

        source.Should().Contain("usp_AlertasDestinatarios_GuardarEmail",
            "debe existir el procedimiento de auto-servicio para configurar el correo de alertas");
        source.Should().Contain("es_account_manager_gobierno = TRUE",
            "el ON CONFLICT DO UPDATE debe marcar TRUE igual que el flujo de Telegram (V096), " +
            "para que usp_AlertasDestinatarios_ListarAccountManagers() devuelva a este usuario");
    }

    [Fact]
    public void V099_ListarAccountManagers_DevuelveTresColumnas()
    {
        var source = File.ReadAllText(FindMigration("V099__Add_Email_Alertas_Destinatarios.sql")).Replace("\r\n", "\n");

        source.Should().Contain("RETURNS TABLE(p_usuario_id VARCHAR(100), p_telegram_chat_id VARCHAR(50), p_email_alertas VARCHAR(200))",
            "el listado de account managers debe incluir el correo, no solo el chat_id de Telegram");
    }

    private static string FindMigration(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MPM.sln")))
            dir = dir.Parent;

        if (dir == null) throw new FileNotFoundException("No se encontró MPM.sln subiendo desde el directorio de test.");

        return Path.Combine(dir.FullName, "src", "MPM.Api", "Database", "Scripts", fileName);
    }
}
