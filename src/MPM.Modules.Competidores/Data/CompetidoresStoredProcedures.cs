namespace MPM.Modules.Competidores.Data;

public static class CompetidoresStoredProcedures
{
    public const string BuscarPorCompetidor = "SELECT * FROM usp_LicitacionesOfertas_BuscarPorCompetidor(@p_nombre)";
    public const string ContarPorCompetidorYRango = "SELECT usp_LicitacionesOfertas_ContarPorCompetidorYRango(@p_nombre, @p_fecha_desde, @p_fecha_hasta)";
    public const string ListarCompetidores = "SELECT * FROM usp_LicitacionesOfertas_ListarCompetidores()";

    public const string AnalisisBuscar = "SELECT * FROM usp_CompetidoresAnalisis_Buscar(@p_nombre_competidor, @p_fecha_desde, @p_fecha_hasta)";

    // ::jsonb explicito -- sin el cast, un p_contenido_json de tipo ambiguo puede romper la
    // resolucion de la sobrecarga de la funcion, mismo motivo que QA BUG-014 (ver
    // src/MPM.Modules.Mensajeria/Data/MensajeriaStoredProcedures.cs). usp_CompetidoresAnalisis_Guardar
    // es una FUNCTION (no PROCEDURE) -- se invoca con SELECT, no CALL.
    public const string AnalisisGuardar = "SELECT usp_CompetidoresAnalisis_Guardar(@p_nombre_competidor, @p_fecha_desde, @p_fecha_hasta, @p_contenido_json::jsonb, @p_cantidad_licitaciones, @p_usuario_id)";
}
