using Microsoft.Extensions.Logging;

namespace MPM.Modules.Licitaciones;

/// <summary>
/// OBS-001: EventIds estables para trazabilidad CM (1200-1300).
/// Usados en CmIngestaController y ChileCompraMservService.
/// </summary>
public static class CmEventIds
{
    public static readonly EventId SyncOk = new(1200, nameof(SyncOk));
    public static readonly EventId Sync429 = new(1201, nameof(Sync429));
    public static readonly EventId Sync5xx = new(1202, nameof(Sync5xx));
    public static readonly EventId YoYCalc = new(1300, nameof(YoYCalc));
}
