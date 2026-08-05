namespace MPM.Modules.Catalogo.Data;

public static class CatalogoStoredProcedures
{
    public const string Estados = "SELECT * FROM usp_Catalogos_EstadosLicitacion()";
    public const string TiposLicitacion = "SELECT * FROM usp_Catalogos_TiposLicitacion()";
    public const string Monedas = "SELECT * FROM usp_Catalogos_Monedas()";
    public const string AreasNegocio = "SELECT * FROM usp_Catalogos_AreasNegocio()";
}