namespace MPM.Modules.Analisis.Data;

public static class AnalisisStoredProcedures
{
    public const string WorkspacesCrear = "CALL usp_AnalisisWorkspaces_Crear(@p_licitacion_id, @p_nombre, @p_user_id, @p_id, @p_error_msg)";
    public const string WorkspacesListar = "SELECT * FROM usp_AnalisisWorkspaces_Listar(@p_page, @p_page_size, @p_search, @p_estado)";
    public const string WorkspacesObtener = "SELECT * FROM usp_AnalisisWorkspaces_Obtener(@p_id)";
    public const string WorkspacesActualizarEstado = "CALL usp_AnalisisWorkspaces_ActualizarEstado(@p_id, @p_estado, @p_error_msg)";
    public const string WorkspacesEliminar = "CALL usp_AnalisisWorkspaces_Eliminar(@p_id, @p_error_msg)";
    public const string DocumentosCrear = "CALL usp_AnalisisDocumentos_Crear(@p_workspace_id, @p_nombre_archivo, @p_mime_type, @p_tamanio_bytes, @p_ruta_storage, @p_id, @p_error_msg)";
    public const string DocumentosListar = "SELECT * FROM usp_AnalisisDocumentos_Listar(@p_workspace_id)";
    public const string DocumentosObtener = "SELECT * FROM usp_AnalisisDocumentos_Obtener(@p_id)";
    public const string ResultadosCrear = "CALL usp_AnalisisResultados_Crear(@p_workspace_id, @p_documento_id, @p_contenido_json, @p_modelo_usado, @p_tokens_entrada, @p_tokens_salida, @p_id, @p_error_msg)";
    public const string ResultadosObtenerPorWorkspace = "SELECT * FROM usp_AnalisisResultados_ObtenerPorWorkspace(@p_workspace_id)";
    public const string ChatObtenerOCrearConversacion = "CALL usp_AnalisisChat_ObtenerOCrearConversacion(@p_workspace_id, @p_conversacion_id, @p_error_msg)";
    public const string ChatEnviarMensaje = "CALL usp_AnalisisChat_EnviarMensaje(@p_conversacion_id, @p_rol, @p_contenido, @p_id, @p_error_msg)";
    public const string ChatObtenerHistorial = "SELECT * FROM usp_AnalisisChat_ObtenerHistorial(@p_conversacion_id, @p_limit)";
    public const string ResultadosObtenerCompletos = "SELECT * FROM usp_Analisis_ObtenerResultadosCompletos(@p_anio)";
}
