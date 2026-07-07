namespace MPM.Modules.Alertas.Data;

public static class AlertasStoredProcedures
{
    // Reglas
    public const string Crear = "SELECT * FROM usp_Alertas_Crear(@p_usuario_id, @p_keyword, @p_monto_minimo, @p_monto_maximo, @p_tipos_licitacion, @p_organismos, @p_notificar_telegram)";
    public const string GuardarSinonimos = "SELECT usp_Alertas_GuardarSinonimos(@p_id, @p_sinonimos_ia)";
    public const string Editar = "SELECT * FROM usp_Alertas_Editar(@p_id, @p_usuario_id, @p_keyword, @p_monto_minimo, @p_monto_maximo, @p_tipos_licitacion, @p_organismos, @p_notificar_telegram)";
    public const string Listar = "SELECT * FROM usp_Alertas_Listar(@p_usuario_id)";
    public const string ListarActivas = "SELECT * FROM usp_Alertas_ListarActivas()";
    public const string Toggle = "SELECT * FROM usp_Alertas_Toggle(@p_id, @p_usuario_id)";
    public const string Eliminar = "SELECT * FROM usp_Alertas_Eliminar(@p_id, @p_usuario_id)";

    // Disparadas
    public const string ExisteParaLicitacion = "SELECT * FROM usp_AlertasDisparadas_ExisteParaLicitacion(@p_regla_id, @p_licitacion_id)";
    public const string RegistrarDisparo = "SELECT * FROM usp_AlertasDisparadas_Registrar(@p_regla_id, @p_licitacion_id, @p_termino_match, @p_resumen_enriquecido, @p_notificacion_inapp_id, @p_es_prueba)";
    public const string MarcarTelegram = "SELECT usp_AlertasDisparadas_MarcarTelegram(@p_id, @p_enviada, @p_error)";
    public const string Historial = "SELECT * FROM usp_AlertasDisparadas_Historial(@p_regla_id, @p_usuario_id, @p_page, @p_page_size)";

    // Destinatarios
    public const string ListarAccountManagers = "SELECT * FROM usp_AlertasDestinatarios_ListarAccountManagers()";
    public const string GuardarChatId = "SELECT usp_AlertasDestinatarios_GuardarChatId(@p_usuario_id, @p_telegram_chat_id)";

    // Vinculación Telegram vía deep link (token de un solo uso)
    public const string CrearLinkToken = "SELECT usp_TelegramLinkTokens_Crear(@p_usuario_id, @p_token, @p_ttl_minutos)";
    public const string ConsumirLinkToken = "SELECT * FROM usp_TelegramLinkTokens_Consumir(@p_token)";
}
