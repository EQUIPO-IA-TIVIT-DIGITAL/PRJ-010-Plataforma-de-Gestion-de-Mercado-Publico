namespace MPM.Modules.Propuestas.Data;

public static class PropuestasStoredProcedures
{
    public const string ExperienciasObtener = "SELECT * FROM usp_CatalogoExperiencias_Obtener(@p_id)";
    public const string ExperienciasListar = "SELECT * FROM usp_CatalogoExperiencias_Listar(@p_q, @p_activo, @p_offset, @p_limit)";
    public const string ExperienciasInsertar = "CALL usp_CatalogoExperiencias_Insertar(@p_titulo, @p_cliente, @p_descripcion, @p_fecha_inicio, @p_fecha_fin, @p_monto_usd, @p_pais, @p_id, @p_error_msg)";
    public const string ExperienciasActualizar = "CALL usp_CatalogoExperiencias_Actualizar(@p_id, @p_titulo, @p_cliente, @p_descripcion, @p_fecha_inicio, @p_fecha_fin, @p_monto_usd, @p_pais, @p_activo, @p_error_msg)";
    public const string ExperienciasEliminar = "CALL usp_CatalogoExperiencias_Eliminar(@p_id, @p_error_msg)";

    public const string CertificacionesListar = "SELECT * FROM usp_CatalogoCertificaciones_Listar(@p_q, @p_activo, @p_con_archivo, @p_tipo, @p_offset, @p_limit)";
    public const string CertificacionesObtener = "SELECT * FROM usp_CatalogoCertificaciones_Obtener(@p_id)";
    public const string CertificacionesInsertar = "CALL usp_CatalogoCertificaciones_Insertar(@p_nombre, @p_nombre_normalizado, @p_file_id_census, @p_institucion, @p_vigencia, @p_id, @p_error_msg)";
    public const string CertificacionesActualizar = "CALL usp_CatalogoCertificaciones_Actualizar(@p_id, @p_nombre, @p_nombre_normalizado, @p_file_id_census, @p_institucion, @p_vigencia, @p_activo, @p_error_msg)";
    public const string CertificacionesEliminar = "CALL usp_CatalogoCertificaciones_Eliminar(@p_id, @p_error_msg)";
    public const string CertificacionesSincronizar = "SELECT * FROM usp_CatalogoCertificaciones_SincronizarCensus(@p_items)";

    public const string CapitulosListar = "SELECT * FROM usp_CatalogoCapitulos_Listar(@p_q, @p_activo, @p_offset, @p_limit)";
    public const string CapitulosObtener = "SELECT * FROM usp_CatalogoCapitulos_Obtener(@p_id)";
    public const string CapitulosInsertar = "CALL usp_CatalogoCapitulos_Insertar(@p_titulo, @p_contenido_markdown, @p_orden, @p_id, @p_error_msg)";
    public const string CapitulosActualizar = "CALL usp_CatalogoCapitulos_Actualizar(@p_id, @p_titulo, @p_contenido_markdown, @p_orden, @p_activo, @p_error_msg)";
    public const string CapitulosEliminar = "CALL usp_CatalogoCapitulos_Eliminar(@p_id, @p_error_msg)";

    public const string DecisionObtener = "SELECT * FROM usp_LicitacionesDecision_Obtener(@p_licitacion_id)";
    public const string DecisionActualizarNotificados = "CALL usp_LicitacionesDecision_ActualizarNotificados(@p_id, @p_notificados_json, @p_error_msg)";
    public const string PropuestaGenerar = "CALL usp_Propuestas_Generar(@p_licitacion_id, @p_capitulos_json, @p_certificaciones_json, @p_experiencias_json, @p_ruta_archivo, @p_generado_por, @p_version, @p_id, @p_error_msg)";
    public const string PropuestasListar = "SELECT * FROM usp_Propuestas_Listar(@p_licitacion_id, @p_estado, @p_offset, @p_limit)";
    public const string PropuestaObtener = "SELECT * FROM usp_Propuestas_Obtener(@p_id)";
    public const string PropuestaEstadoActualizar = "CALL usp_Propuestas_ActualizarEstado(@p_id, @p_estado, @p_error_msg)";
}
