using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

/// <summary>
/// Join entity linking a <see cref="MachineProgram"/> to one or more <see cref="Part"/>s.
/// Represents a part assigned to a program with quantity, stack level, and position metadata.
/// Used by BuildPlate programs for plate nesting and by standard programs for additional parts.
/// </summary>
public class ProgramPart
{
    public int Id { get; set; }

    [Required]
    public int MachineProgramId { get; set; }

    [Required]
    public int PartId { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>1 = single, 2 = double-stack, 3 = triple-stack.</summary>
    public int StackLevel { get; set; } = 1;

    /// <summary>Position/orientation notes from slicer layout.</summary>
    [MaxLength(500)]
    public string? PositionNotes { get; set; }

    /// <summary>Optional link to a work order line that this plate entry fulfils.</summary>
    public int? WorkOrderLineId { get; set; }

    /// <summary>FK to the CertifiedLayout this entry was created from. Null for legacy/free-form programs.</summary>
    public int? CertifiedLayoutId { get; set; }

    /// <summary>
    /// Comma-separated slot indices this layout occupies on the plate (e.g. "0", "0,1", "2,3").
    /// Quadrant = 1 slot, Half = 2 adjacent slots. Null for legacy programs.
    /// </summary>
    [MaxLength(20)]
    public string? PlateSlots { get; set; }

    // Navigation
    public virtual MachineProgram MachineProgram { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
    public virtual WorkOrderLine? WorkOrderLine { get; set; }
    public virtual CertifiedLayout? CertifiedLayout { get; set; }
}
