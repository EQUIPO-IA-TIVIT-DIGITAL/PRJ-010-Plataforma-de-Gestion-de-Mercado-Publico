namespace MPM.Core.SystemConfig;

/// <summary>
/// Proveedor de IA activo resuelto (033-migracion-qwen-g4).
/// </summary>
/// <param name="Provider">"gemini" | "openai".</param>
/// <param name="Endpoint">Base URL del proveedor (solo openai; null para gemini).</param>
/// <param name="Model">Id del modelo activo (se persiste en analisis.modelo_usado).</param>
/// <param name="ResolvedFrom">Origen de la resolución: "database" | "environment" | (default gemini).</param>
/// <param name="UpdatedByUsername">Usuario del último cambio (solo si ResolvedFrom=database).</param>
/// <param name="UpdatedAt">Fecha del último cambio (solo si ResolvedFrom=database).</param>
public sealed record AiProviderSettings(
    string Provider,
    string? Endpoint,
    string Model,
    string ResolvedFrom,
    string? UpdatedByUsername = null,
    DateTime? UpdatedAt = null);
