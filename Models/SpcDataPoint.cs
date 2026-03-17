namespace Opcentrix_V3.Models;

public class SpcDataPoint
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public string CharacteristicName { get; set; } = string.Empty;
    public decimal MeasuredValue { get; set; }
    public decimal NominalValue { get; set; }
    public decimal TolerancePlus { get; set; }
    public decimal ToleranceMinus { get; set; }
    public int? QcInspectionId { get; set; }
    public int? JobId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
