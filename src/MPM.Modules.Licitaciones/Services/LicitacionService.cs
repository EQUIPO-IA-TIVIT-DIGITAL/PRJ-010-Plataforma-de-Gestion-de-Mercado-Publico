using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Licitaciones.Services;

public class LicitacionService(
    ILogger<LicitacionService> logger,
    IConfiguration config,
    LicitacionHandler licitacionHandler,
    SyncService syncService,
    ApiMpService apiMpService,
    ConsultaSemanticaService consultaSemanticaService)
{
    public async Task<(List<LicitacionResumenDto> items, int totalCount)> ListarAsync(
        int page, int pageSize, string? search, short? estado, string? tipo, string? organismo,
        DateTime? fechaDesde, DateTime? fechaHasta, string sortBy, string sortDir,
        short? area = null, bool? sinClasificar = null,
        CancellationToken ct = default)
    {
        return await licitacionHandler.ListarAsync(
            page, pageSize, search, estado, tipo, organismo,
            fechaDesde, fechaHasta, sortBy, sortDir, area, sinClasificar, ct);
    }

    // US2 (spec 031)
    public async Task<List<EstadoConteoDto>> ContarPorEstadoAsync(
        short? area, bool? sinClasificar, CancellationToken ct = default)
    {
        return await licitacionHandler.ContarPorEstadoAsync(area, sinClasificar, ct);
    }

    public async Task<LicitacionDetalleDto?> ObtenerPorCodigoAsync(string codigoExterno, CancellationToken ct = default)
    {
        var dto = await licitacionHandler.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (dto == null) return null;

        if (string.IsNullOrEmpty(dto.Descripcion) && dto.FechaPublicacion == null)
        {
            try
            {
                var ticket = config["MP_TICKET"] ?? "";
                var detalle = await apiMpService.GetDetalleAsync(codigoExterno, ticket, ct);
                if (detalle != null)
                {
                    dto = MapDetalleToDto(detalle, dto);
                    await licitacionHandler.ActualizarDetalleAsync(codigoExterno, dto, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo obtener detalle de {Codigo}", codigoExterno);
            }
        }

        return dto;
    }

    public async Task<List<LicitacionSearchResult>> BuscarAsync(string search, int limit, CancellationToken ct = default)
    {
        return await licitacionHandler.BuscarAsync(search, limit, ct);
    }

    public async Task<SyncStatusDto> ForzarSyncAsync(DateTime? desde = null, CancellationToken ct = default)
    {
        logger.LogInformation("Sincronizacion manual solicitada via API desde {Desde}", desde?.ToString("yyyy-MM-dd") ?? "ultimos 7 dias");
        return await syncService.ExecuteSyncAsync(desde, ct);
    }

    private static LicitacionDetalleDto MapDetalleToDto(ApiMpLicitacion api, LicitacionDetalleDto existing)
    {
        var fechas = api.Fechas;
        return new LicitacionDetalleDto
        {
            CodigoExterno = existing.CodigoExterno,
            Nombre = existing.Nombre,
            Tipo = api.Tipo ?? existing.Tipo,
            CodigoEstado = existing.CodigoEstado,
            EstadoNombre = existing.EstadoNombre,
            Organismo = api.Comprador?.NombreOrganismo ?? existing.Organismo,
            UnidadTecnica = api.Comprador?.NombreUnidad,
            Moneda = api.Moneda ?? existing.Moneda,
            MontoEstimado = api.MontoEstimado ?? existing.MontoEstimado,
            Descripcion = api.Descripcion,
            FechaPublicacion = DateTime.TryParse(fechas?.FechaPublicacion, out var fp) ? fp : existing.FechaPublicacion,
            FechaCierre = DateTime.TryParse(fechas?.FechaCierre, out var fc) ? fc : existing.FechaCierre,
            FechaAdjudicacion = DateTime.TryParse(fechas?.FechaAdjudicacion, out var fa) ? fa : existing.FechaAdjudicacion,
            FechaEstimadaAdjudicacion = DateTime.TryParse(fechas?.FechaEstimadaAdjudicacion, out var fea) ? fea : existing.FechaEstimadaAdjudicacion,
            // V138: ?idlicitacion= es la URL PUBLICA de la ficha (sin login) -- ver ApiMpService.
            Link = $"https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion={api.CodigoExterno}",
            Items = api.Items?.Select(i => new LicitacionItemDto
            {
                Codigo = i.Correlativo ?? i.CodigoProducto.GetHashCode(),
                Nombre = i.NombreProducto ?? "",
                Cantidad = (int?)(i.Cantidad),
                UnidadMedida = i.UnidadMedida,
                PrecioEstimado = i.Adjudicacion?.MontoUnitario,
                Categoria = i.Categoria?.Split(" / ").LastOrDefault(),
            }).ToList() ?? new(),
        };
    }

    public async Task<(PaginatedResult<LicitacionNaturalSearchResult>? Result, string? Error)> BuscarNaturalAsync(
        string query, int page, int pageSize, short? estado, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return (null, "VAL_001:La búsqueda debe tener al menos 2 caracteres");

        var queryTrim = query.Trim();

        // 018-buscador-inteligente-nl (FR-005): si la interpretación falla, no está disponible,
        // o tiene confianza baja, se usa la consulta tal cual -- comportamiento idéntico al
        // buscar-natural literal anterior a esta feature.
        List<string>? terminosExpandidos = null;
        short? estadoEfectivo = estado;
        decimal? montoDesde = null;
        decimal? montoHasta = null;
        DateTime? fechaDesde = null;
        DateTime? fechaHasta = null;

        var interpretacion = await consultaSemanticaService.InterpretarAsync(queryTrim, ct);
        if (interpretacion is { Confianza: Models.ConfianzaInterpretacion.Alta })
        {
            terminosExpandidos = interpretacion.TerminosExpandidos;
            montoDesde = interpretacion.MontoDesde;
            montoHasta = interpretacion.MontoHasta;
            // 029-fix-hallazgos-code-review-competidores-alertas (FR-002): FechaDesde ya se
            // calculaba acá, pero nunca se pasaba al handler -- BuscarNaturalAsync la
            // hardcodeaba a 2026-01-01, bloqueando cualquier búsqueda NL de un período anterior.
            fechaDesde = interpretacion.FechaDesde;
            fechaHasta = interpretacion.FechaHasta;
            // El estado explícito del usuario siempre tiene prioridad sobre el inferido (US2).
            estadoEfectivo = estado ?? interpretacion.EstadoInferido;
        }

        var (items, totalCount) = await licitacionHandler.BuscarNaturalAsync(
            queryTrim, page, pageSize, estadoEfectivo, terminosExpandidos, montoDesde, montoHasta, fechaDesde, fechaHasta, ct);

        return (new PaginatedResult<LicitacionNaturalSearchResult>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = (int)totalCount,
        }, null);
    }
}
