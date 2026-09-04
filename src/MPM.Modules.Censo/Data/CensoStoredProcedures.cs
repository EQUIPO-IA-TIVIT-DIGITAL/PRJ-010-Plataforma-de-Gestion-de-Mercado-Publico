namespace MPM.Modules.Censo.Data;

public static class CensoStoredProcedures
{
    public const string CatalogoListar = "SELECT * FROM usp_CensoCatalogo_Listar()";
    public const string CatalogoLimpiar = "CALL usp_CensoCatalogo_Limpiar(@p_error_msg)";
    public const string CatalogoUpsert =
        "CALL usp_CensoCatalogo_Upsert(@p_grupo, @p_categoria, @p_type_name, @p_tecnologia, @p_error_msg)";

    public const string ExpansionObtener = "SELECT * FROM usp_CensoExpansion_Obtener(@p_concepto)";
    public const string ExpansionUpsert =
        "CALL usp_CensoExpansion_Upsert(@p_concepto, @p_tecnologias, @p_fuente, @p_error_msg)";

    public const string CachePersonasFresco = "SELECT * FROM usp_CensoCachePersonas_ObtenerFresco(@p_tecnologia, @p_pais)";
    public const string CachePersonasUpsert =
        "CALL usp_CensoCachePersonas_Upsert(@p_tecnologia, @p_pais, @p_personas, @p_error_msg)";

    public const string MatchGuardar = "CALL usp_CensoMatch_Guardar(@p_licitacion_id, @p_resultado_json, @p_error_msg)";
    public const string MatchObtener = "SELECT * FROM usp_CensoMatch_Obtener(@p_licitacion_id)";

    public const string PreferenciasObtener = "SELECT * FROM usp_CensoPreferencias_Obtener(@p_user_id)";
    public const string PreferenciasUpsert =
        "CALL usp_CensoPreferencias_Upsert(@p_user_id, @p_filtrar_pais, @p_pais, @p_error_msg)";
}
