namespace MPM.Modules.Mensajeria.Data;

public static class MensajeriaStoredProcedures
{
    // ::jsonb explicito -- sin el cast, Npgsql manda p_participante_ids como texto/unknown y
    // Postgres no encuentra ninguna sobrecarga de usp_Conversaciones_Crear que matchee (QA BUG-014).
    public const string CrearConversacion = "CALL usp_Conversaciones_Crear(@p_tipo, @p_asunto, @p_licitacion_id, @p_participante_ids::jsonb, @p_creador_id, @p_id, @p_error_msg)";
    public const string ListarConversaciones = "SELECT * FROM usp_Conversaciones_Listar(@p_user_id, @p_page, @p_page_size, @p_search, @p_sort_by, @p_sort_dir)";
    public const string ObtenerConversacion = "SELECT * FROM usp_Conversaciones_Obtener(@p_id, @p_user_id)";
    public const string ActualizarConversacion = "CALL usp_Conversaciones_Actualizar(@p_id, @p_asunto, @p_user_id, @p_error_msg)";
    public const string AbandonarConversacion = "CALL usp_Conversaciones_Abandonar(@p_id, @p_user_id, @p_error_msg)";
    
    public const string AgregarParticipante = "CALL usp_ConversacionParticipantes_Agregar(@p_conversacion_id, @p_user_id, @p_rol, @p_solicitante_id, @p_error_msg)";
    public const string QuitarParticipante = "CALL usp_ConversacionParticipantes_Quitar(@p_conversacion_id, @p_user_id, @p_solicitante_id, @p_error_msg)";
    
    public const string EnviarMensaje = "CALL usp_Mensajes_Enviar(@p_conversacion_id, @p_user_id, @p_tipo, @p_contenido, @p_reply_to_id, @p_id, @p_error_msg)";
    public const string ListarMensajes = "SELECT * FROM usp_Mensajes_Listar(@p_conversacion_id, @p_user_id, @p_page, @p_page_size, @p_before)";
    public const string EditarMensaje = "CALL usp_Mensajes_Editar(@p_id, @p_user_id, @p_contenido, @p_error_msg)";
    public const string EliminarMensaje = "CALL usp_Mensajes_Eliminar(@p_id, @p_user_id, @p_error_msg)";
    public const string MarcarLeido = "CALL usp_Mensajes_MarcarLeido(@p_mensaje_id, @p_user_id)";
    
    public const string CrearAdjunto = "CALL usp_MensajeAdjuntos_Crear(@p_mensaje_id, @p_nombre_archivo, @p_mime_type, @p_tamanio_bytes, @p_ruta_storage, @p_id, @p_error_msg)";
    public const string ObtenerAdjunto = "SELECT * FROM usp_MensajeAdjuntos_Obtener(@p_id, @p_conversacion_id, @p_user_id)";
    
    public const string ActualizarPresencia = "CALL usp_Presencia_Actualizar(@p_user_id, @p_estado, @p_conversacion_id)";

    // ::jsonb explicito -- mismo bug que CrearConversacion arriba (QA BUG-014): sin el cast,
    // Npgsql manda p_user_ids como texto/unknown y Postgres no encuentra ninguna sobrecarga
    // de usp_Presencia_Obtener(jsonb) que matchee (42883). Este quedó afuera del fix original.
    public const string ObtenerPresencia = "SELECT * FROM usp_Presencia_Obtener(@p_user_ids::jsonb)";
}
