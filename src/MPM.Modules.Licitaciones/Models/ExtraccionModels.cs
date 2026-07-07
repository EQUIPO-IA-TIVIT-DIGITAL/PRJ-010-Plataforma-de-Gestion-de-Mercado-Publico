using System.Net;

namespace MPM.Modules.Licitaciones.Models;

/// <summary>Referencia mínima a una licitación para el pipeline de extracción de documentos.</summary>
public record LicitacionRef(long LicitacionId, string CodigoExterno);

/// <summary>Sesión autenticada contra el portal de Mercado Público, cacheada por <see cref="MpSessionProvider"/>.</summary>
public class MpSession
{
    public required CookieContainer Cookies { get; init; }
    public required DateTime ObtenidaEn { get; init; }
}

/// <summary>Una fila de la tabla de adjuntos del portal (grid <c>#DWNL_grdId</c>).</summary>
public record AdjuntoFila(
    string Nombre,
    string Tipo,
    string Descripcion,
    string Tamanio,
    string Fecha,
    string BotonNombrePostback,
    bool EsActa);

/// <summary>
/// Campos ocultos de la página WebForms necesarios para reproducir el postback de descarga.
/// Se capturan TODOS los <c>input[type=hidden]</c> de la página (no solo los tres "clásicos")
/// porque algunas configuraciones de ASP.NET fragmentan el ViewState en varios campos.
/// </summary>
public record WebFormsState(
    string ViewState,
    string ViewStateGenerator,
    string EventValidation,
    IReadOnlyDictionary<string, string> TodosLosCamposOcultos);

public record AdjuntosListado(IReadOnlyList<AdjuntoFila> Filas, WebFormsState State);

public record ResultadoExtraccion(
    string Metodo,            // "directo" | "navegador"
    string Estado,            // "exito" | "fallo" | "sin_adjuntos"
    int DocumentosObtenidos,
    bool ActaObtenida,
    string? Error,
    long DuracionMs,
    bool EsFallback);
