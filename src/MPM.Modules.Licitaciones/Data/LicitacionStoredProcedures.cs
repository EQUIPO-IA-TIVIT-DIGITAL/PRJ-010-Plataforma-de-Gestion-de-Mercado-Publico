namespace MPM.Modules.Licitaciones.Data;

public static class LicitacionStoredProcedures
{
    // Functions
    public const string Listar = "SELECT * FROM usp_Licitaciones_Listar(@p_page, @p_page_size, @p_search, @p_estado, @p_tipo, @p_organismo, @p_fecha_desde, @p_fecha_hasta, @p_sort_by, @p_sort_dir)";
    public const string Obtener = "SELECT * FROM usp_Licitaciones_ObtenerPorCodigo(@p_codigo_externo)";
    public const string Buscar = "SELECT * FROM usp_Licitaciones_Buscar(@p_q, @p_limit)";
    public const string BuscarNatural = "SELECT * FROM usp_Licitaciones_BuscarNatural(@p_query, @p_page, @p_page_size, @p_estado, @p_fecha_desde, @p_terminos_expandidos, @p_monto_desde, @p_monto_hasta, @p_fecha_hasta)";
    public const string BuscarNaturalCount = "SELECT * FROM usp_Licitaciones_BuscarNatural_Count(@p_query, @p_estado, @p_fecha_desde, @p_terminos_expandidos, @p_monto_desde, @p_monto_hasta, @p_fecha_hasta)";
    public const string Estados = "SELECT * FROM usp_Catalogos_EstadosLicitacion()";

    // Procedures
    public const string SyncIniciar = "CALL usp_SyncLog_Iniciar(@p_tipo, @p_sync_id, @p_error_msg)";
    public const string SyncFinalizar = "CALL usp_SyncLog_Finalizar(@p_sync_id, @p_creados, @p_actualizados, @p_eliminados, @p_errores, @p_detalle_errores, @p_error_msg)";
    public const string MergeLicitaciones = "CALL usp_SyncEngine_MergeLicitaciones(@p_datos, @p_creados, @p_actualizados, @p_error_msg)";

    // Seguimiento
    public const string SeguirToggle = "SELECT * FROM usp_Licitaciones_SeguirToggle(@p_usuario_id, @p_codigo)";
    public const string EsSeguida = "SELECT * FROM usp_Licitaciones_EsSeguida(@p_usuario_id, @p_codigo)";
    public const string ObtenerParaMonitor = "SELECT * FROM usp_Licitaciones_ObtenerParaMonitor(@p_estados)";
    public const string AclaracionUpsert = "SELECT * FROM usp_Licitaciones_Aclaracion_Upsert(@p_codigo, @p_codigo_aclaracion, @p_pregunta, @p_respuesta, @p_fecha_publicacion, @p_fecha_respuesta)";
    public const string AclaracionMarcarNotificada = "SELECT usp_Licitaciones_Aclaracion_MarcarNotificada(@p_id)";
    public const string ObtenerSeguidas = "SELECT * FROM usp_Licitaciones_ObtenerSeguidas(@p_usuario_id)";

    // Soporte para el motor de matching de Alertas (003-fase6-alertas-keywords)
    public const string ListarParaMatching = "SELECT * FROM usp_Licitaciones_ListarParaMatching(@p_fecha_desde)";
}
