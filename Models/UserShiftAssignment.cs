using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

/// <summary>
/// Many-to-many: links an operator to the shifts they work.
/// </summary>
public class UserShiftAssignment
{
    public int UserId { get; set; }
    public int OperatingShiftId { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }

    [MaxLength(100)]
    public string? AssignedBy { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual OperatingShift OperatingShift { get; set; } = null!;
}
