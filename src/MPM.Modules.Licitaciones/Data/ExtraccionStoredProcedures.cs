namespace MPM.Modules.Licitaciones.Data;

public static class ExtraccionStoredProcedures
{
    public const string Registrar =
        "SELECT * FROM usp_ExtraccionLog_Registrar(@p_licitacion_id, @p_metodo, @p_estado, @p_documentos_obtenidos, @p_acta_obtenida, @p_es_fallback, @p_error, @p_duracion_ms)";

    public const string ResumenPeriodo =
        "SELECT * FROM usp_ExtraccionLog_ResumenPeriodo(@p_desde, @p_hasta)";

    public const string ExistePorLicitacion =
        "SELECT * FROM usp_Adjuntos_ExistePorLicitacion(@p_licitacion_id)";

    public const string RegistrarAdjuntoDirecto =
        "SELECT * FROM usp_Adjuntos_RegistrarDirecto(@p_licitacion_id, @p_tipo, @p_nombre_archivo, @p_ruta_storage, @p_tamanio_bytes, @p_mime_type, @p_es_acta)";
}
