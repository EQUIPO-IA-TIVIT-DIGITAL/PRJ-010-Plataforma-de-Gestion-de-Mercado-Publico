using System.Data;
using System.Text.Json;
using Dapper;
using MPM.Core.Data;
using MPM.Modules.Censo.Models;

namespace MPM.Modules.Censo.Data;

public class CensoHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    // ── Catálogo ──────────────────────────────────────────────────────────────
    public virtual async Task<List<CensoCatalogoItemDto>> CatalogoListarAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<CensoCatalogoItemDto>(CensoStoredProcedures.CatalogoListar, commandType: CommandType.Text);
        return rows.ToList();
    }

    public virtual async Task CatalogoLimpiarAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(CensoStoredProcedures.CatalogoLimpiar, new { p_error_msg = "" }, commandType: CommandType.Text);
    }

    public virtual async Task CatalogoUpsertAsync(CensoCatalogoItemDto item, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(CensoStoredProcedures.CatalogoUpsert,
            new { p_grupo = item.Grupo, p_categoria = item.Categoria, p_type_name = item.TypeName, p_tecnologia = item.Tecnologia, p_error_msg = "" },
            commandType: CommandType.Text);
    }

    // ── Expansiones ───────────────────────────────────────────────────────────
    public virtual async Task<List<string>?> ExpansionObtenerAsync(string concepto, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<ExpansionRow>(CensoStoredProcedures.ExpansionObtener,
            new { p_concepto = concepto }, commandType: CommandType.Text);
        var row = rows.FirstOrDefault();
        if (row?.Tecnologias == null) return null;
        return JsonSerializer.Deserialize<List<string>>(row.Tecnologias);
    }

    public virtual async Task ExpansionUpsertAsync(string concepto, List<string> tecnologias, string fuente, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(CensoStoredProcedures.ExpansionUpsert,
            new
            {
                p_concepto = concepto,
                p_tecnologias = JsonSerializer.Serialize(tecnologias),
                p_fuente = fuente,
                p_error_msg = "",
            },
            commandType: CommandType.Text);
    }

    // ── Cache de personas ─────────────────────────────────────────────────────
    public virtual async Task<List<JsonElement>?> CachePersonasFrescoAsync(string tecnologia, string pais, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<CachePersonasRow>(CensoStoredProcedures.CachePersonasFresco,
            new { p_tecnologia = tecnologia, p_pais = pais }, commandType: CommandType.Text);
        var row = rows.FirstOrDefault();
        if (row?.Personas == null) return null;
        return JsonSerializer.Deserialize<List<JsonElement>>(row.Personas);
    }

    public virtual async Task CachePersonasUpsertAsync(string tecnologia, string pais, List<JsonElement> personas, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(CensoStoredProcedures.CachePersonasUpsert,
            new { p_tecnologia = tecnologia, p_pais = pais, p_personas = JsonSerializer.Serialize(personas), p_error_msg = "" },
            commandType: CommandType.Text);
    }

    // ── Requisitos desde el análisis comercial (V142) ───────────────────────────
    /// <summary>
    /// Lee el último análisis comercial completado de la licitación y extrae las
    /// certificaciones requeridas (resultado_json.requisitos_tecnicos.certificaciones_requeridas).
    /// Las tecnologías del análisis quedan fuera del match (la spec las define vacías).
    /// </summary>
    public virtual async Task<AnalisisRequisitosResult> AnalisisRequisitosAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<AnalisisRequisitosRow>(
            "SELECT estado, resultado_json FROM analisis_licitacion_comercial " +
            "WHERE licitacion_id = @p_licitacion_id ORDER BY id DESC LIMIT 1",
            new { p_licitacion_id = licitacionId });

        if (row == null || row.Estado != "completado" || string.IsNullOrWhiteSpace(row.ResultadoJson))
            return new AnalisisRequisitosResult(false, new List<string>());

        var certificaciones = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(row.ResultadoJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("requisitos_tecnicos", out var rt) &&
                rt.TryGetProperty("certificaciones_requeridas", out var certs) &&
                certs.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in certs.EnumerateArray())
                {
                    if (c.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(c.GetString()))
                        certificaciones.Add(c.GetString()!);
                }
            }
        }
        catch (JsonException)
        {
            // Resultado corrupto → sin requisitos extraíbles (CEN_001).
        }

        return new AnalisisRequisitosResult(true, certificaciones);
    }

    public record AnalisisRequisitosResult(bool TieneAnalisisCompletado, List<string> Certificaciones);

    private class AnalisisRequisitosRow
    {
        public string Estado { get; set; } = "";
        public string? ResultadoJson { get; set; }
    }

    // ── Match ─────────────────────────────────────────────────────────────────
    public virtual async Task MatchGuardarAsync(long licitacionId, string resultadoJson, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(CensoStoredProcedures.MatchGuardar,
            new { p_licitacion_id = licitacionId, p_resultado_json = resultadoJson, p_error_msg = "" },
            commandType: CommandType.Text);
    }

    public virtual async Task<string?> MatchObtenerAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<MatchRow>(CensoStoredProcedures.MatchObtener,
            new { p_licitacion_id = licitacionId }, commandType: CommandType.Text);
        return rows.FirstOrDefault()?.ResultadoJson;
    }

    // ── Preferencias ──────────────────────────────────────────────────────────
    public virtual async Task<CensoPreferenciasDto?> PreferenciasObtenerAsync(string userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<PreferenciasRow>(CensoStoredProcedures.PreferenciasObtener,
            new { p_user_id = userId }, commandType: CommandType.Text);
        var row = rows.FirstOrDefault();
        return row == null ? null : new CensoPreferenciasDto { FiltrarPais = row.FiltrarPais, Pais = row.Pais };
    }

    public virtual async Task PreferenciasUpsertAsync(string userId, bool filtrarPais, string pais, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(CensoStoredProcedures.PreferenciasUpsert,
            new { p_user_id = userId, p_filtrar_pais = filtrarPais, p_pais = pais, p_error_msg = "" },
            commandType: CommandType.Text);
    }

    private class ExpansionRow
    {
        public string? Tecnologias { get; set; }
    }

    private class CachePersonasRow
    {
        public string? Personas { get; set; }
    }

    private class MatchRow
    {
        public string? ResultadoJson { get; set; }
    }

    private class PreferenciasRow
    {
        public bool FiltrarPais { get; set; }
        public string Pais { get; set; } = "Chile";
    }
}
