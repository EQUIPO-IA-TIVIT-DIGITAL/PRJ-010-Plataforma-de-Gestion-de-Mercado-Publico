namespace MPM.Modules.Colaboracion.Data;

public static class LicitacionesInteresStoredProcedures
{
    public const string Marcar = "SELECT * FROM usp_LicitacionesInteres_Marcar(@p_licitacion_id, @p_marcado_por)";
    public const string ObtenerPorLicitacion = "SELECT * FROM usp_LicitacionesInteres_ObtenerPorLicitacion(@p_licitacion_id)";
    public const string VincularWorkspace = "SELECT usp_LicitacionesInteres_VincularWorkspace(@p_licitacion_id, @p_workspace_id)";
    public const string VincularConversacion = "SELECT usp_LicitacionesInteres_VincularConversacion(@p_licitacion_id, @p_conversacion_id)";
    public const string Listar = "SELECT * FROM usp_LicitacionesInteres_Listar()";
}
