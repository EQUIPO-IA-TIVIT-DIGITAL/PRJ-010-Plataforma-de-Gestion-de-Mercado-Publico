using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Core.SystemConfig;
using MPM.Shared.Models;

namespace MPM.Api.Controllers;

/// <summary>
/// Administración del proveedor de IA activo — el "switch" del super admin entre gcloud y qwen
/// (033-migracion-qwen-g4, US4). Solo rol SuperAdmin. El cambio persiste en la tabla
/// system_ai_provider (auditado) e invalida la cache del SystemConfigService, por lo que
/// aplica al análisis siguiente sin reiniciar nada.
/// </summary>
[ApiController]
[Route("api/system/ai-provider")]
[Authorize(Roles = "SuperAdmin")]
public class SystemConfigController(
    SystemConfigService configService,
    ILogger<SystemConfigController> logger) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    /// <summary>Estado actual del proveedor de IA: activo, modelo, origen y último cambio.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AiProviderInfo>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<AiProviderInfo>>> Obtener(CancellationToken ct = default)
    {
        var settings = await configService.ObtenerActivoAsync(ct);
        var info = new AiProviderInfo(
            settings.Provider,
            settings.Model,
            settings.Endpoint,
            settings.ResolvedFrom,
            settings.ResolvedFrom == "database" ? settings.UpdatedByUsername : null,
            settings.ResolvedFrom == "database" ? settings.UpdatedAt : null);
        return Ok(ApiResponse<AiProviderInfo>.Ok(info));
    }

    /// <summary>Cambia el proveedor de IA activo (UPSERT atómico; el último cambio gana).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<AiProviderInfo>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<AiProviderInfo>>> Actualizar(
        [FromBody] ActualizarAiProviderRequest request,
        CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null || string.IsNullOrEmpty(tenant.UserId))
            return Unauthorized(ApiResponse<object>.Fail("No autorizado"));
        if (!long.TryParse(tenant.UserId, out var userId))
            return BadRequest(ApiResponse<object>.Fail("Identificador de usuario inválido"));

        if (!request.Provider.Equals("gemini", StringComparison.OrdinalIgnoreCase) &&
            !request.Provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Fail("INVALID_PROVIDER: el proveedor debe ser 'gemini' o 'openai'"));

        if (request.Provider.Equals("openai", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(request.Endpoint) ||
             !Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpointUri) ||
             (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps)))
            return BadRequest(ApiResponse<object>.Fail("INVALID_ENDPOINT: se requiere una URL http/https válida para el proveedor openai"));

        if (string.IsNullOrWhiteSpace(request.Model))
            return BadRequest(ApiResponse<object>.Fail("INVALID_MODEL: el modelo no puede estar vacío"));

        var settings = await configService.ActualizarAsync(
            request.Provider.ToLowerInvariant(),
            request.Provider.Equals("openai", StringComparison.OrdinalIgnoreCase) ? request.Endpoint!.Trim() : null,
            request.Model.Trim(),
            userId,
            tenant.Username,
            ct);

        logger.LogInformation("Proveedor de IA cambiado a {Provider}/{Model} por {Username}",
            settings.Provider, settings.Model, tenant.Username);

        var info = new AiProviderInfo(
            settings.Provider, settings.Model, settings.Endpoint, settings.ResolvedFrom,
            settings.UpdatedByUsername, settings.UpdatedAt);
        return Ok(ApiResponse<AiProviderInfo>.Ok(info));
    }
}

/// <summary>Estado del proveedor de IA para la UI del super admin.</summary>
public sealed record AiProviderInfo(
    string Provider,
    string Model,
    string? Endpoint,
    string ResolvedFrom,
    string? UpdatedByUsername,
    DateTime? UpdatedAt);

public sealed record ActualizarAiProviderRequest(
    string Provider,
    string? Endpoint,
    string? Model);
