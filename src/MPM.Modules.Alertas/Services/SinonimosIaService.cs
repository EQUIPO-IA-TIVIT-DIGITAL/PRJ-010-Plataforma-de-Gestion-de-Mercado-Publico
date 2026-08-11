using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPM.Core.SystemConfig;
using MPM.Shared.Services;

namespace MPM.Modules.Alertas.Services;

/// <summary>
/// Expande una keyword a sinónimos/conceptos relacionados vía el proveedor de IA activo
/// (033-migracion-qwen-g4), calculado una sola vez al crear/editar la regla
/// (research.md §2 de 003-fase6-alertas-keywords), no en cada ciclo de matching.
/// Antes de esa spec llamaba directo a Gemini/Vertex AI con HTTP crudo duplicado; ahora usa
/// <see cref="LlmClientResolver"/> como el resto de módulos. El contrato de salida no cambia.
/// </summary>
public class SinonimosIaService(
    LlmClientResolver resolver,
    ILogger<SinonimosIaService> logger)
{
    public async Task<List<string>?> ExpandirAsync(string keyword, CancellationToken ct = default)
    {
        var prompt = $"Dado el término de búsqueda de licitaciones públicas '{keyword}', " +
            "devuelve entre 5 y 10 sinónimos o términos relacionados que un comprador público " +
            "podría usar en el nombre o descripción de una licitación. " +
            "Responde solo JSON con esta forma exacta: {\"sinonimos\": [\"...\", \"...\"]}";

        try
        {
            var request = new LlmRequest(
                Messages: [new LlmMessage("user", [new LlmTextPart(prompt)])],
                Temperature: 0.3,
                MaxOutputTokens: 1024,
                JsonResponse: true);

            var client = await resolver.GetClientAsync(ct);
            var result = await client.GenerarContenidoAsync(request, ct);

            var text = result.Text;
            if (string.IsNullOrWhiteSpace(text)) return null;

            // El modelo a veces envuelve el JSON en fences ```json``` pese a pedir JSON mode.
            text = text.Trim();
            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                var lastFence = text.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                    text = text[(firstNewline + 1)..lastFence].Trim();
            }

            var parsed = JsonSerializer.Deserialize<SinonimosResponse>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return parsed?.Sinonimos;
        }
        catch (Exception ex)
        {
            // Un fallo al expandir sinónimos no debe impedir crear la regla — se guarda con
            // sinonimos_ia=null y se puede reintentar editando la regla.
            logger.LogWarning(ex, "Fallo expandiendo sinónimos para '{Keyword}'", keyword);
            return null;
        }
    }

    private class SinonimosResponse
    {
        public List<string>? Sinonimos { get; set; }
    }
}
