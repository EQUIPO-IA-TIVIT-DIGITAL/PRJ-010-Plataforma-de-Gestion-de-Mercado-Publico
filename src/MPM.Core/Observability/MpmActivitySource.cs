using System.Diagnostics;

namespace MPM.Core.Observability;

/// <summary>
/// ActivitySource central de MPM.Api para trazabilidad distribuida.
/// 037-A: solo definición vacía, sin OTel SDK aún (feature-flag). 037-B cableará el exporter OTLP.
/// </summary>
public static class MpmActivitySource
{
    public const string Name = "MPM.Api";
    public const string Version = "1.0.0";

    public static readonly ActivitySource Instance = new(Name, Version);
}
