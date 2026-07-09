using FluentAssertions;
using Xunit;

namespace MPM.Modules.Alertas.Tests.Data;

// QA BUG-015: la auto-vinculacion de Telegram guardaba el chat_id pero nunca marcaba
// es_account_manager_gobierno = TRUE, asi que usp_AlertasDestinatarios_ListarAccountManagers()
// jamas devolvia a esos usuarios y no recibian alertas -- sin ningun error visible. Guarda de
// regresion contra el SQL de V096 (source-level, igual patron que 022-qa-fixes-preproduccion,
// ya que este test project no monta una conexion Postgres real).
public class AlertasDestinatariosTelegramFixTests
{
    [Fact]
    public void V096_GuardarChatId_MarcaEsAccountManagerGobiernoEnInsertYUpdate()
    {
        var source = File.ReadAllText(FindMigration("V096__Fix_Alertas_Destinatarios_Telegram.sql")).Replace("\r\n", "\n");

        source.Should().Contain("usuario_id, telegram_chat_id, es_account_manager_gobierno",
            "el INSERT debe setear el flag explicitamente, no dejarlo en su DEFAULT FALSE");
        source.Should().Contain("VALUES (p_usuario_id, p_telegram_chat_id, TRUE)",
            "el INSERT debe marcar TRUE para un usuario que se auto-vincula por primera vez");
        source.Should().Contain("es_account_manager_gobierno = TRUE,",
            "el ON CONFLICT DO UPDATE tambien debe marcar TRUE (usuario que re-vincula un chat_id nuevo)");
    }

    [Fact]
    public void V096_IncluyeBackfillLimitadoAUsuariosConTelegramChatIdNoNulo()
    {
        var source = File.ReadAllText(FindMigration("V096__Fix_Alertas_Destinatarios_Telegram.sql")).Replace("\r\n", "\n");

        source.Should().Contain("UPDATE alertas_destinatarios",
            "debe existir un backfill para usuarios que ya se vincularon antes del fix");
        source.Should().Contain("WHERE telegram_chat_id IS NOT NULL",
            "el backfill NO debe tocar filas sin telegram_chat_id -- podrian ser un flag FALSE " +
            "intencional de una via administrativa distinta al autoservicio");
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
