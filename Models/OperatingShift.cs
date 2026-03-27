using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class OperatingShift
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    [Required, MaxLength(50)]
    public string DaysOfWeek { get; set; } = "Mon,Tue,Wed,Thu,Fri";

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string? Color { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<MachineShiftAssignment> MachineAssignments { get; set; } = new List<MachineShiftAssignment>();
    public virtual ICollection<UserShiftAssignment> UserAssignments { get; set; } = new List<UserShiftAssignment>();
}
