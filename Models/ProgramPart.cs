using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

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

    // Navigation
    public virtual MachineProgram MachineProgram { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
    public virtual WorkOrderLine? WorkOrderLine { get; set; }
}
