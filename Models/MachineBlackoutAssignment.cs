namespace Vectrik.Models;

/// <summary>
/// Many-to-many join: assigns a shared BlackoutPeriod to a specific machine.
/// Only machines with this assignment AND a BlackoutPeriod scheduling rule enabled
/// will have builds blocked during the blackout window.
/// </summary>
public class MachineBlackoutAssignment
{
    public int MachineId { get; set; }
    public int BlackoutPeriodId { get; set; }

    public virtual Machine Machine { get; set; } = null!;
    public virtual BlackoutPeriod BlackoutPeriod { get; set; } = null!;
}
