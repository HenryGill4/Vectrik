namespace Vectrik.Models;

public class InspectionPlan
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<InspectionPlanCharacteristic> Characteristics { get; set; } = new List<InspectionPlanCharacteristic>();
}

public class InspectionPlanCharacteristic
{
    public int Id { get; set; }
    public int InspectionPlanId { get; set; }
    public InspectionPlan InspectionPlan { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? DrawingCallout { get; set; }
    public decimal NominalValue { get; set; }
    public decimal TolerancePlus { get; set; }
    public decimal ToleranceMinus { get; set; }
    public string? InstrumentType { get; set; }
    public bool IsKeyCharacteristic { get; set; }
    public int DisplayOrder { get; set; }
}
