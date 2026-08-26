namespace MPM.Shared.Models;

public class CmResumenCacheDto
{
    public short Anio { get; set; }
    public string Rut { get; set; } = string.Empty;
    public long AmountClp { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime ActualizadoAt { get; set; }
}
