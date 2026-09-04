namespace MPM.Modules.Licitaciones.Data;

public static class PreferenciasLicitacionesStoredProcedures
{
    public const string Obtener = "SELECT * FROM usp_PreferenciasUsuario_Obtener(@p_user_id)";
    public const string Upsert = "CALL usp_PreferenciasUsuario_Upsert(@p_user_id, @p_monto_minimo, @p_error_msg)";
}
