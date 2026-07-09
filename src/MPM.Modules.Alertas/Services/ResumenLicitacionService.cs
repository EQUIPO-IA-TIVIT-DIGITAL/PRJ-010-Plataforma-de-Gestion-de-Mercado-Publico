using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Core.Data;

namespace MPM.Modules.Alertas.Services;

/// <summary>
/// 024-inteligencia-competencia-alertas / US2: arma el resumen "bajo demanda" para el botón
/// "Me interesa" de Telegram, sin invocar Gemini (FR-008). Nota de implementación (corrige
/// research.md R6): NO se referencia ApiMpService de MPM.Modules.Licitaciones directamente
/// porque ese módulo ya referencia a MPM.Modules.Alertas (para sus alertas operativas del
/// scraper) -- una referencia en el otro sentido crearía una dependencia circular de proyectos.
/// En su lugar, este servicio llama al mismo endpoint público de Mercado Público
/// (api.mercadopublico.cl) de forma independiente, igual URL/formato que ApiMpService.GetDetalleAsync.
/// </summary>
public class ResumenLicitacionService(HttpClient httpClient, IConfiguration config, DbConnectionFactory dbFactory, ILogger<ResumenLicitacionService> logger)
{
    /// <summary>Resuelve el codigo_externo desde el id local (barato, ya sincronizado) antes de pedir el detalle completo a la API pública.</summary>
    public async Task<string?> ObtenerResumenPorIdAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        var codigoExterno = await conn.QuerySingleOrDefaultAsync<string?>(
            "SELECT codigo_externo FROM licitaciones WHERE id = @id AND deleted_at IS NULL",
            new { id = licitacionId }, commandType: CommandType.Text);

        if (string.IsNullOrWhiteSpace(codigoExterno))
        {
            logger.LogWarning("No se encontró la licitación {Id} para armar el resumen de 'Me interesa'", licitacionId);
            return null;
        }

        return await ObtenerResumenAsync(codigoExterno, ct);
    }

    public async Task<string?> ObtenerResumenAsync(string codigoExterno, CancellationToken ct = default)
    {
        var ticket = config["MP_TICKET"];
        if (string.IsNullOrWhiteSpace(ticket))
        {
            logger.LogWarning("MP_TICKET no configurado -- no se puede armar el resumen de {Codigo}", codigoExterno);
            return null;
        }

        try
        {
            var url = $"https://api.mercadopublico.cl/servicios/v1/publico/licitaciones.json?ticket={ticket}&codigo={codigoExterno}";
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("API MP respondió {Status} al pedir detalle de {Codigo}", response.StatusCode, codigoExterno);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Listado", out var listado) || listado.GetArrayLength() == 0)
                return null;

            var l = listado[0];
            string? Get(string prop) => l.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

            var nombre = Get("Nombre") ?? codigoExterno;
            var descripcion = Get("Descripcion");
            var organismo = l.TryGetProperty("Comprador", out var comprador) && comprador.ValueKind == JsonValueKind.Object
                ? Get2(comprador, "NombreOrganismo") : null;
            var monto = Get("MontoEstimado");
            var fechaCierre = l.TryGetProperty("Fechas", out var fechas) && fechas.ValueKind == JsonValueKind.Object
                ? Get2(fechas, "FechaCierre") : null;

            var lineas = new List<string> { $"📋 *{TelegramNotificationService.EscaparMarkdownV2(nombre)}*" };
            if (!string.IsNullOrWhiteSpace(organismo)) lineas.Add($"Organismo: {TelegramNotificationService.EscaparMarkdownV2(organismo)}");
            if (!string.IsNullOrWhiteSpace(monto)) lineas.Add($"Monto estimado: {TelegramNotificationService.EscaparMarkdownV2(monto)}");
            if (!string.IsNullOrWhiteSpace(fechaCierre)) lineas.Add($"Cierre de ofertas: {TelegramNotificationService.EscaparMarkdownV2(fechaCierre)}");
            if (!string.IsNullOrWhiteSpace(descripcion)) lineas.Add($"\n{TelegramNotificationService.EscaparMarkdownV2(descripcion)}");

            return string.Join("\n", lineas);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error armando resumen de {Codigo}", codigoExterno);
            return null;
        }
    }

    private static string? Get2(JsonElement obj, string prop) =>
        obj.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;
}
