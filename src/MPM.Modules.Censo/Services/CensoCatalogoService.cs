using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPM.Modules.Censo.Data;
using MPM.Modules.Censo.Models;

namespace MPM.Modules.Censo.Services;

/// <summary>
/// Catálogo local de types/tecnologías refrescable desde <c>census/knowledge</c>
/// (D7.7/D7.8): tabla <c>censo_catalogo</c> (grupo→categoría→type→tecnología, ~210 types /
/// ~939 tecnologías). Refresco manual (POST /catalogo/refrescar) + lazy automático si el
/// catálogo está vacío (spec CEN, GET /censo/catalogo).
/// </summary>
public class CensoCatalogoService(
    CensoHandler handler,
    CensusClient censusClient,
    ILogger<CensoCatalogoService> logger)
{
    // Guard in-process: dos refrescos simultáneos (lazy + manual) no limpian/insertan en paralelo.
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    /// <summary>Trae census/knowledge, limpia la tabla y reinserta grupo→categoría→type→tecnología.</summary>
    public virtual async Task<CensoRefrescoResultDto> RefrescarAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var json = await censusClient.GetCatalogoKnowledgeAsync(ct);
        var items = ParseKnowledge(json);

        await handler.CatalogoLimpiarAsync(ct);
        foreach (var item in items)
            await handler.CatalogoUpsertAsync(item, ct);

        sw.Stop();
        logger.LogInformation("Catálogo Census refrescado: {Tecnologias} tecnologías en {DurationMs} ms",
            items.Count, sw.ElapsedMilliseconds);

        return new CensoRefrescoResultDto
        {
            Grupos = items.Select(i => i.Grupo).Distinct().Count(),
            Categorias = items.Select(i => $"{i.Grupo}|{i.Categoria}").Distinct().Count(),
            Types = items.Select(i => i.TypeName).Distinct().Count(),
            Tecnologias = items.Count,
            DurationMs = sw.ElapsedMilliseconds,
        };
    }

    /// <summary>
    /// Listado del catálogo con filtros aplicados en servicio (volumen pequeño, sin SQL
    /// dinámico — spec CEN). Si el catálogo está vacío → refresco lazy automático.
    /// </summary>
    public virtual async Task<CensoCatalogoListadoDto> ListarAsync(
        string? q, string? grupo, string? categoria, CancellationToken ct = default)
    {
        var items = await handler.CatalogoListarAsync(ct);
        if (items.Count == 0)
        {
            await RefreshLock.WaitAsync(ct);
            try
            {
                // Double-check: otro request pudo refrescar mientras esperábamos.
                items = await handler.CatalogoListarAsync(ct);
                if (items.Count == 0)
                {
                    await RefrescarAsync(ct);
                    items = await handler.CatalogoListarAsync(ct);
                }
            }
            finally
            {
                RefreshLock.Release();
            }
        }

        IEnumerable<CensoCatalogoItemDto> filtrados = items;
        if (!string.IsNullOrWhiteSpace(q) && q.Trim().Length >= 2)
        {
            var term = q.Trim();
            filtrados = filtrados.Where(i =>
                i.TypeName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.Tecnologia.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(grupo))
            filtrados = filtrados.Where(i => i.Grupo.Equals(grupo, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(categoria))
            filtrados = filtrados.Where(i => i.Categoria.Equals(categoria, StringComparison.OrdinalIgnoreCase));

        var lista = filtrados
            .OrderBy(i => i.TypeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Tecnologia, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CensoCatalogoListadoDto
        {
            Items = lista,
            Resumen = new CensoCatalogoResumenDto
            {
                Types = items.Select(i => i.TypeName).Distinct().Count(),
                Tecnologias = items.Count,
                ActualizadoAt = DateTime.UtcNow,
            },
        };
    }

    /// <summary>Parsea la respuesta de census/knowledge (KnowledgeGroup → categories → types → knowledge).</summary>
    internal static List<CensoCatalogoItemDto> ParseKnowledge(string json)
    {
        var items = new List<CensoCatalogoItemDto>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return items;

        foreach (var grupo in doc.RootElement.EnumerateArray())
        {
            var grupoNombre = GetStr(grupo, "name") ?? "(sin grupo)";
            if (!grupo.TryGetProperty("categories", out var cats) || cats.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var cat in cats.EnumerateArray())
            {
                var catNombre = GetStr(cat, "name") ?? "(sin categoría)";
                if (!cat.TryGetProperty("types", out var types) || types.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var type in types.EnumerateArray())
                {
                    var typeNombre = GetStr(type, "name") ?? "(sin type)";
                    if (!type.TryGetProperty("knowledge", out var kn) || kn.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var item in kn.EnumerateArray())
                    {
                        var tecnologia = GetStr(item, "name");
                        if (string.IsNullOrWhiteSpace(tecnologia)) continue;
                        items.Add(new CensoCatalogoItemDto
                        {
                            Grupo = grupoNombre,
                            Categoria = catNombre,
                            TypeName = typeNombre,
                            Tecnologia = tecnologia,
                        });
                    }
                }
            }
        }

        return items;
    }

    private static string? GetStr(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
