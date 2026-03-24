namespace Opcentrix_V3.Models;

public class PartSeparationResult
{
    public int MachineProgramId { get; set; }
    public List<SeparatedPart> OkParts { get; set; } = new();
    public List<DamagedPart> DamagedParts { get; set; } = new();
}

public class SeparatedPart
{
    public int PartId { get; set; }
    public int Quantity { get; set; }
    public string? SerialNumber { get; set; }
    public int? WorkOrderLineId { get; set; }
}

public class DamagedPart
{
    public int PartId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = "";
    public int? WorkOrderLineId { get; set; }
}
