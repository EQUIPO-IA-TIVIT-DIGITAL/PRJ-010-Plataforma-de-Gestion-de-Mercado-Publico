namespace MPM.Modules.Notificaciones.Data;

public static class NotificacionesStoredProcedures
{
    public const string Crear = "SELECT * FROM usp_Notificaciones_Crear(@p_usuario_id, @p_tipo, @p_titulo, @p_mensaje, @p_metadata)";
    public const string Listar = "SELECT * FROM usp_Notificaciones_Listar(@p_usuario_id, @p_page, @p_page_size, @p_solo_no_leidas)";
    public const string ContarNoLeidas = "SELECT * FROM usp_Notificaciones_ContarNoLeidas(@p_usuario_id)";
    public const string MarcarLeida = "SELECT * FROM usp_Notificaciones_MarcarLeida(@p_id, @p_usuario_id)";
    public const string MarcarTodasLeidas = "SELECT * FROM usp_Notificaciones_MarcarTodasLeidas(@p_usuario_id)";
    public const string Eliminar = "SELECT * FROM usp_Notificaciones_Eliminar(@p_id, @p_usuario_id)";
    public const string EliminarTodas = "SELECT * FROM usp_Notificaciones_EliminarTodas(@p_usuario_id)";
}
