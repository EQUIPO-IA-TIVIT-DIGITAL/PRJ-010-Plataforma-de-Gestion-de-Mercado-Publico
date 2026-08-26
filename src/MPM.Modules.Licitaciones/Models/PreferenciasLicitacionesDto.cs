namespace MPM.Modules.Licitaciones.Models;

/// <summary>
/// Preferencia persistida de monto mínimo por defecto para el listado de licitaciones (Feature B, Track 1).
/// Spec: docs/api-first/preferencias-usuario.md — GET/PUT /api/v1/usuarios/me/preferencias-licitaciones
/// Patron: replica CensoPreferenciasDto (V143) en módulo Licitaciones.
/// </summary>
public class PreferenciasLicitacionesDto
{
    /// <summary>Umbral mínimo CLP. NULL = sin preferencia (no filtrar).</summary>
    public decimal? MontoMinimo { get; set; }
}

/// <summary>Body de PUT — montoMinimo nullable explícito (null borra la preferencia, PREF-R003).</summary>
public class PreferenciasLicitacionesUpdateDto
{
    public decimal? MontoMinimo { get; set; }
}
