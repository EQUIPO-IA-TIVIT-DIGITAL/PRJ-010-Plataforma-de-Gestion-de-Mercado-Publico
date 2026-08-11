namespace MPM.Shared.Services;

/// <summary>
/// Contrato neutral de acceso al proveedor de modelos de IA (033-migracion-qwen-g4).
/// Los servicios de dominio inyectan <see cref="Core.SystemConfig.LlmClientResolver"/> (MPM.Core)
/// y resuelven el cliente activo por request; nunca dependen de una implementación concreta
/// ni del proveedor (Gemini/Vertex vs. OpenAI-compatible/Qwen).
/// </summary>
public interface ILlmClient
{
    /// <summary>Id del modelo que esta instancia va a usar (se persiste en modelo_usado).</summary>
    string ModelName { get; }

    /// <summary>
    /// Genera contenido a partir de un request neutral. El proveedor traduce el request a su
    /// formato nativo, llama a su endpoint, y devuelve la respuesta parseada.
    /// </summary>
    /// <exception cref="LlmRespuestaBloqueadaException">Cuando el proveedor responde sin contenido
    /// generado (equivalente a <see cref="GeminiRespuestaBloqueadaException"/> para Vertex).</exception>
    Task<LlmResult> GenerarContenidoAsync(LlmRequest request, CancellationToken ct = default);
}

/// <summary>
/// Cliente que recibe endpoint/modelo resueltos por request (033-migracion-qwen-g4).
/// Lo implementan los clientes cuyo endpoint/modelo pueden venir de la configuración
/// persistida (tabla system_ai_provider / switch del super admin), no solo de env vars.
/// <see cref="Core.SystemConfig.LlmClientResolver"/> llama <c>ApplySettings</c> tras resolver
/// el cliente activo.
/// </summary>
public interface IConfigurableLlmClient
{
    /// <param name="endpoint">Base URL del proveedor (null si no aplica, ej. Vertex AI).</param>
    /// <param name="model">Id del modelo activo (se persiste en modelo_usado).</param>
    void ApplySettings(string? endpoint, string model);
}
