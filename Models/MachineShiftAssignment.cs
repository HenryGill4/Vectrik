namespace Opcentrix_V3.Models;

/// <summary>
/// Many-to-many: links a machine to the shifts it operates during.
/// If a machine has no shift assignments, the scheduler falls back to all active shifts.
/// </summary>
public class MachineShiftAssignment
{
    public int MachineId { get; set; }
    public int OperatingShiftId { get; set; }

    public virtual Machine Machine { get; set; } = null!;
    public virtual OperatingShift OperatingShift { get; set; } = null!;
}
