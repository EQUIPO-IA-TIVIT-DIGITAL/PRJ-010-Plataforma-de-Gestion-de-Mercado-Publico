using System.Data;
using System.Text.Json;
using Dapper;
using MPM.Core.Data;
using MPM.Modules.Propuestas.Models;

namespace MPM.Modules.Propuestas.Data;

public class PropuestasHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public class PropuestasDataException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    public virtual async Task<CatalogoPage<ExperienciaCatalogoDto>> ListarExperienciasAsync(string? q, bool? activo, int page, int size, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = (await conn.QueryAsync<ExperienciaRow>(PropuestasStoredProcedures.ExperienciasListar,
            new { p_q = q, p_activo = activo, p_offset = (page - 1) * size, p_limit = size }, commandType: CommandType.Text)).ToList();
        return new CatalogoPage<ExperienciaCatalogoDto> { Items = rows.Select(ToDto).ToList(), Page = page, Size = size, TotalRecords = rows.FirstOrDefault()?.TotalCount ?? 0, TotalPages = Pages(rows.FirstOrDefault()?.TotalCount ?? 0, size) };
    }

    public virtual async Task<ExperienciaCatalogoDto> CrearExperienciaAsync(ExperienciaCatalogoRequest request, CancellationToken ct = default)
    {
        var result = await ExecuteMutationAsync<MutationResult>(PropuestasStoredProcedures.ExperienciasInsertar, new
        {
            p_titulo = request.Titulo, p_cliente = request.Cliente, p_descripcion = request.Descripcion,
            p_fecha_inicio = request.FechaInicio, p_fecha_fin = request.FechaFin, p_monto_usd = request.MontoUsd,
            p_pais = request.Pais, p_id = 0L, p_error_msg = ""
        });
        return await ObtenerExperienciaAsync(result.Id, ct) ?? throw new PropuestasDataException("SYS_001", "No se pudo leer la experiencia creada");
    }

    public virtual async Task<ExperienciaCatalogoDto> ActualizarExperienciaAsync(long id, ExperienciaCatalogoRequest request, CancellationToken ct = default)
    {
        await ExecuteMutationAsync<MutationResult>(PropuestasStoredProcedures.ExperienciasActualizar, new
        {
            p_id = id, p_titulo = request.Titulo, p_cliente = request.Cliente, p_descripcion = request.Descripcion,
            p_fecha_inicio = request.FechaInicio, p_fecha_fin = request.FechaFin, p_monto_usd = request.MontoUsd,
            p_pais = request.Pais, p_activo = request.Activo ?? true, p_error_msg = ""
        });
        return await ObtenerExperienciaAsync(id, ct) ?? throw new PropuestasDataException("PRO_001", "Experiencia no encontrada");
    }

    public virtual async Task EliminarExperienciaAsync(long id, CancellationToken ct = default)
        => await ExecuteMutationAsync<MutationResult>(PropuestasStoredProcedures.ExperienciasEliminar, new { p_id = id, p_error_msg = "" });

    public virtual async Task<CatalogoPage<CertificacionCatalogoDto>> ListarCertificacionesAsync(string? q, bool? activo, bool? conArchivo, string? tipo, int page, int size, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = (await conn.QueryAsync<CertificacionRow>(PropuestasStoredProcedures.CertificacionesListar,
            new { p_q = q, p_activo = activo, p_con_archivo = conArchivo, p_tipo = tipo, p_offset = (page - 1) * size, p_limit = size }, commandType: CommandType.Text)).ToList();
        return new CatalogoPage<CertificacionCatalogoDto> { Items = rows.Select(ToDto).ToList(), Page = page, Size = size, TotalRecords = rows.FirstOrDefault()?.TotalCount ?? 0, TotalPages = Pages(rows.FirstOrDefault()?.TotalCount ?? 0, size) };
    }

    public virtual async Task<CertificacionCatalogoDto> CrearCertificacionAsync(CertificacionCatalogoRequest request, string nombreNormalizado, CancellationToken ct = default)
    {
        var result = await ExecuteMutationAsync<MutationResult>(PropuestasStoredProcedures.CertificacionesInsertar, new
        {
            p_nombre = request.Nombre, p_nombre_normalizado = nombreNormalizado, p_file_id_census = request.FileIdCensus,
            p_institucion = request.Institucion, p_vigencia = request.Vigencia, p_id = 0L, p_error_msg = ""
        });
        return await ObtenerCertificacionAsync(result.Id, ct) ?? throw new PropuestasDataException("SYS_001", "No se pudo leer la certificación creada");
    }

    public virtual async Task<CertificacionCatalogoDto> ActualizarCertificacionAsync(long id, CertificacionCatalogoRequest request, string nombreNormalizado, CancellationToken ct = default)
    {
        await ExecuteMutationAsync<MutationResult>(PropuestasStoredProcedures.CertificacionesActualizar, new
        {
            p_id = id, p_nombre = request.Nombre, p_nombre_normalizado = nombreNormalizado,
            p_file_id_census = request.FileIdCensus, p_institucion = request.Institucion, p_vigencia = request.Vigencia,
            p_activo = request.Activo ?? true, p_error_msg = ""
        });
        return await ObtenerCertificacionAsync(id, ct) ?? throw new PropuestasDataException("PRO_001", "Certificación no encontrada");
    }

    public virtual async Task EliminarCertificacionAsync(long id, CancellationToken ct = default)
        => await ExecuteMutationAsync<MutationResult>(PropuestasStoredProcedures.CertificacionesEliminar, new { p_id = id, p_error_msg = "" });

    public virtual async Task<CensusSyncMutationResult> SincronizarCertificacionesAsync(IReadOnlyCollection<CertificationSyncItem> items, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var json = JsonSerializer.Serialize(items.Select(i => new
        {
            nombre = i.Nombre,
            nombreNormalizado = i.NombreNormalizado,
            fileIdCensus = i.FileIdCensus,
            institucion = i.Institucion,
            vigencia = i.Vigencia,
        }));
        return await conn.QuerySingleAsync<CensusSyncMutationResult>(PropuestasStoredProcedures.CertificacionesSincronizar,
            new { p_items = json }, commandType: CommandType.Text);
    }

    public virtual async Task<CatalogoPage<CapituloCatalogoDto>> ListarCapitulosAsync(string? q, bool? activo, int page, int size, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = (await conn.QueryAsync<CapituloRow>(PropuestasStoredProcedures.CapitulosListar,
            new { p_q = q, p_activo = activo, p_offset = (page - 1) * size, p_limit = size }, commandType: CommandType.Text)).ToList();
        return new CatalogoPage<CapituloCatalogoDto> { Items = rows.Select(ToDto).ToList(), Page = page, Size = size, TotalRecords = rows.FirstOrDefault()?.TotalCount ?? 0, TotalPages = Pages(rows.FirstOrDefault()?.TotalCount ?? 0, size) };
    }

    public virtual async Task<CapituloCatalogoDto> CrearCapituloAsync(CapituloCatalogoRequest request, CancellationToken ct = default)
    {
        var result = await ExecuteMutationAsync<MutationResult>(PropuestasStoredProcedures.CapitulosInsertar, new { p_titulo = request.Titulo, p_contenido_markdown = request.ContenidoMarkdown, p_orden = request.Orden, p_id = 0L, p_error_msg = "" });
        return await ObtenerCapituloAsync(result.Id, ct) ?? throw new PropuestasDataException("SYS_001", "No se pudo leer el capítulo creado");
    }

    public virtual async Task<CapituloCatalogoDto> ActualizarCapituloAsync(long id, CapituloCatalogoRequest request, CancellationToken ct = default)
    {
        await ExecuteMutationAsync<MutationResult>(PropuestasStoredProcedures.CapitulosActualizar, new { p_id = id, p_titulo = request.Titulo, p_contenido_markdown = request.ContenidoMarkdown, p_orden = request.Orden, p_activo = request.Activo ?? true, p_error_msg = "" });
        return await ObtenerCapituloAsync(id, ct) ?? throw new PropuestasDataException("PRO_001", "Capítulo no encontrado");
    }

    public virtual async Task EliminarCapituloAsync(long id, CancellationToken ct = default)
        => await ExecuteMutationAsync<MutationResult>(PropuestasStoredProcedures.CapitulosEliminar, new { p_id = id, p_error_msg = "" });

    public virtual Task<CatalogoPage<CapituloCatalogoDto>> ListarCapitulosActivosAsync(CancellationToken ct = default)
        => ListarCapitulosAsync(null, true, 1, 1000, ct);

    public virtual Task<CatalogoPage<CertificacionCatalogoDto>> ListarCertificacionesActivasAsync(CancellationToken ct = default)
        => ListarCertificacionesAsync(null, true, null, null, 1, 1000, ct);

    public virtual Task<CatalogoPage<ExperienciaCatalogoDto>> ListarExperienciasActivasAsync(CancellationToken ct = default)
        => ListarExperienciasAsync(null, true, 1, 1000, ct);

    public virtual async Task<DecisionProposalRow?> ObtenerDecisionAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QuerySingleOrDefaultAsync<DecisionProposalRow>(
            PropuestasStoredProcedures.DecisionObtener,
            new { p_licitacion_id = licitacionId }, commandType: CommandType.Text);
    }

    public virtual async Task ActualizarDecisionNotificadosAsync(
        long decisionId, string notificadosJson, CancellationToken ct = default)
    {
        var result = await ExecuteMutationAsync<MutationResult>(
            PropuestasStoredProcedures.DecisionActualizarNotificados,
            new { p_id = decisionId, p_notificados_json = notificadosJson, p_error_msg = "" });
        ThrowIfError(result.ErrorMessage);
    }

    public virtual async Task<ProposalMutationResult> GenerarPropuestaAsync(
        long licitacionId, string capitulosJson, string certificacionesJson, string experienciasJson,
        string rutaArchivo, string generadoPor, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QuerySingleAsync<ProposalMutationResult>(
            PropuestasStoredProcedures.PropuestaGenerar,
            new
            {
                p_licitacion_id = licitacionId,
                p_capitulos_json = capitulosJson,
                p_certificaciones_json = certificacionesJson,
                p_experiencias_json = experienciasJson,
                p_ruta_archivo = rutaArchivo,
                p_generado_por = generadoPor,
                p_version = 0,
                p_id = 0L,
                p_error_msg = "",
            }, commandType: CommandType.Text);
        ThrowIfError(result.ErrorMessage);
        return result;
    }

    public virtual async Task<CatalogoPage<PropuestaHistorialDto>> ListarPropuestasAsync(
        long licitacionId, string? estado, int page, int size, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = (await conn.QueryAsync<PropuestaRow>(
            PropuestasStoredProcedures.PropuestasListar,
            new { p_licitacion_id = licitacionId, p_estado = estado, p_offset = (page - 1) * size, p_limit = size },
            commandType: CommandType.Text)).ToList();
        var total = rows.FirstOrDefault()?.TotalCount ?? 0;
        return new CatalogoPage<PropuestaHistorialDto>
        {
            Items = rows.Select(ToHistorialDto).ToList(),
            Page = page,
            Size = size,
            TotalRecords = total,
            TotalPages = Pages(total, size),
        };
    }

    public virtual async Task<PropuestaRow?> ObtenerPropuestaAsync(long propuestaId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QuerySingleOrDefaultAsync<PropuestaRow>(
            PropuestasStoredProcedures.PropuestaObtener,
            new { p_id = propuestaId }, commandType: CommandType.Text);
    }

    public virtual async Task ActualizarEstadoPropuestaAsync(long propuestaId, string estado, CancellationToken ct = default)
    {
        var result = await ExecuteMutationAsync<MutationResult>(
            PropuestasStoredProcedures.PropuestaEstadoActualizar,
            new { p_id = propuestaId, p_estado = estado, p_error_msg = "" });
        ThrowIfError(result.ErrorMessage);
    }

    private async Task<ExperienciaCatalogoDto?> ObtenerExperienciaAsync(long id, CancellationToken ct)
    {
        await using var conn = _dbFactory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<ExperienciaRow>(PropuestasStoredProcedures.ExperienciasObtener, new { p_id = id }, commandType: CommandType.Text);
        return row == null ? null : ToDto(row);
    }

    public virtual async Task<CertificacionCatalogoDto?> ObtenerCertificacionAsync(long id, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<CertificacionRow>(PropuestasStoredProcedures.CertificacionesObtener, new { p_id = id }, commandType: CommandType.Text);
        return row == null ? null : ToDto(row);
    }

    private async Task<CapituloCatalogoDto?> ObtenerCapituloAsync(long id, CancellationToken ct)
    {
        await using var conn = _dbFactory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<CapituloRow>(PropuestasStoredProcedures.CapitulosObtener, new { p_id = id }, commandType: CommandType.Text);
        return row == null ? null : ToDto(row);
    }

    private async Task<T> ExecuteMutationAsync<T>(string sql, object parameters)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QuerySingleAsync<T>(sql, parameters, commandType: CommandType.Text);
        if (result is MutationResult mutation) ThrowIfError(mutation.ErrorMessage);
        return result;
    }

    private static void ThrowIfError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return;
        var split = error.Split(':', 2);
        throw new PropuestasDataException(split[0], split.Length == 2 ? split[1] : error);
    }

    private static int Pages(long total, int size) => total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
    private static ExperienciaCatalogoDto ToDto(ExperienciaRow row) => new()
    {
        Id = row.Id,
        Titulo = row.Titulo,
        Cliente = row.Cliente,
        Descripcion = row.Descripcion,
        FechaInicio = row.FechaInicio.HasValue ? DateOnly.FromDateTime(row.FechaInicio.Value) : null,
        FechaFin = row.FechaFin.HasValue ? DateOnly.FromDateTime(row.FechaFin.Value) : null,
        MontoUsd = row.MontoUsd,
        Pais = row.Pais,
        Activo = row.Activo,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt
    };
    private static CertificacionCatalogoDto ToDto(CertificacionRow row) => new() { Id = row.Id, Nombre = row.Nombre, FileIdCensus = row.FileIdCensus, Institucion = row.Institucion, Vigencia = row.Vigencia, Titular = row.Titular, Tipo = row.Tipo, Activo = row.Activo, CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt };
    private static CapituloCatalogoDto ToDto(CapituloRow row) => new() { Id = row.Id, Titulo = row.Titulo, ContenidoMarkdown = row.ContenidoMarkdown, Orden = row.Orden, Activo = row.Activo, CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt };
    private static PropuestaHistorialDto ToHistorialDto(PropuestaRow row) => new()
    {
        PropuestaId = row.Id,
        Version = row.Version,
        Estado = row.Estado,
        Capitulos = CountJsonItems(row.CapitulosSeleccionados),
        Certificaciones = CountJsonItems(row.CertificacionesIds),
        Experiencias = CountJsonItems(row.ExperienciasIds),
        GeneradoPor = row.GeneradoPor,
        GeneradoAt = row.GeneradoAt,
    };

    private static int CountJsonItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
        }
        catch (JsonException) { return 0; }
    }

    private sealed class ExperienciaRow { public long Id { get; set; } public string Titulo { get; set; } = ""; public string Cliente { get; set; } = ""; public string? Descripcion { get; set; } public DateTime? FechaInicio { get; set; } public DateTime? FechaFin { get; set; } public decimal? MontoUsd { get; set; } public string? Pais { get; set; } public bool Activo { get; set; } public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } public long TotalCount { get; set; } }
    private sealed class CertificacionRow { public long Id { get; set; } public string Nombre { get; set; } = ""; public string NombreNormalizado { get; set; } = ""; public string? FileIdCensus { get; set; } public string? Institucion { get; set; } public string? Vigencia { get; set; } public string? Titular { get; set; } public string Tipo { get; set; } = "corporativa"; public bool Activo { get; set; } public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } public long TotalCount { get; set; } }
    private sealed class CapituloRow { public long Id { get; set; } public string Titulo { get; set; } = ""; public string? ContenidoMarkdown { get; set; } public int Orden { get; set; } public bool Activo { get; set; } public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } public long TotalCount { get; set; } }
    private sealed class MutationResult { public long Id { get; set; } public string? ErrorMessage { get; set; } }
}

public sealed record CertificationSyncItem(string Nombre, string NombreNormalizado, string? FileIdCensus, string? Institucion, string? Vigencia);
public sealed class CensusSyncMutationResult { public int Insertadas { get; set; } public int Actualizadas { get; set; } public int SinArchivo { get; set; } }

public sealed class ProposalMutationResult
{
    public long Id { get; set; }
    public int Version { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class DecisionProposalRow
{
    public long Id { get; set; }
    public long LicitacionId { get; set; }
    public string? Decision { get; set; }
    public string? Motivo { get; set; }
    public string? Notificados { get; set; }
    public DateTime? NotificadoAt { get; set; }
}

public sealed class PropuestaRow
{
    public long Id { get; set; }
    public long LicitacionId { get; set; }
    public int Version { get; set; }
    public string? CapitulosSeleccionados { get; set; }
    public string? CertificacionesIds { get; set; }
    public string? ExperienciasIds { get; set; }
    public string? RutaArchivo { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? GeneradoPor { get; set; }
    public DateTime? GeneradoAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long TotalCount { get; set; }
}
