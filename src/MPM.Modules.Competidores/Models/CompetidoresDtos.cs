namespace MPM.Modules.Competidores.Models;

public record OfertaDto(
    long LicitacionId,
    string CodigoExterno,
    string NombreLicitacion,
    string? Organismo,
    DateTime? FechaCierre,
    string? RutProveedor,
    string NombreProveedor,
    decimal? MontoOferta,
    string? EstadoOferta);

public record GuardarOfertaRequest(
    string? RutProveedor,
    string NombreProveedor,
    decimal? MontoOferta,
    string? EstadoOferta);

public record GuardarOfertasRequest(long LicitacionId, List<GuardarOfertaRequest> Ofertas);

public record AnalizarCompetidorRequest(
    string NombreCompetidor,
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    bool Confirmar);

public record AnalisisCompetidorResponse(
    bool Cacheado,
    int CantidadLicitaciones,
    object? Contenido,
    bool RequiereConfirmacion);

internal record OfertaRow(
    long p_licitacion_id,
    string p_codigo_externo,
    string p_nombre_licitacion,
    string? p_organismo,
    DateTime? p_fecha_cierre,
    string? p_rut_proveedor,
    string p_nombre_proveedor,
    decimal? p_monto_oferta,
    string? p_estado_oferta);

internal record AnalisisCacheadoRow(
    long p_id,
    string p_contenido_json,
    int p_cantidad_licitaciones,
    DateTime p_created_at);

// US4 (spec 031): actividad total de mercado de un competidor
public record ActividadMercadoRequest(
    short? Area,
    DateOnly FechaDesde,
    DateOnly FechaHasta);

public record ActividadMercadoResponse(
    string Estado, // "generando" | "listo" | "error"
    string NombreCompetidor,
    int? CantidadLicitaciones,
    decimal? MontoTotalAdjudicado,
    object? Licitaciones);

public record ActividadMercadoCacheRow(
    string Estado,
    int? CantidadLicitaciones,
    decimal? MontoTotalAdjudicado,
    string? ContenidoJson,
    DateTime? GeneradoAt);
