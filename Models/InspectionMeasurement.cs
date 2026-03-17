namespace Opcentrix_V3.Models;

public class InspectionMeasurement
{
    public int Id { get; set; }
    public int QcInspectionId { get; set; }
    public QCInspection Inspection { get; set; } = null!;
    public string CharacteristicName { get; set; } = string.Empty;
    public string? DrawingCallout { get; set; }
    public decimal NominalValue { get; set; }
    public decimal TolerancePlus { get; set; }
    public decimal ToleranceMinus { get; set; }
    public decimal ActualValue { get; set; }
    public decimal Deviation { get; set; }
    public bool IsInSpec { get; set; }
    public string? InstrumentUsed { get; set; }
    public string? GageId { get; set; }
}
