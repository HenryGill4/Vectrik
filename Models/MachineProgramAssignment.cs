using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

/// <summary>
/// Join entity linking a <see cref="MachineProgram"/> to one or more <see cref="Machine"/>s.
/// Enables many-to-many: a single program can run on several machines,
/// and each machine can have multiple programs assigned to it.
/// </summary>
public class MachineProgramAssignment
{
    public int Id { get; set; }

    public int MachineProgramId { get; set; }

    public int MachineId { get; set; }

    /// <summary>
    /// Marks the preferred/primary machine for this program.
    /// Scheduling may prioritise this machine when multiple are available.
    /// </summary>
    public bool IsPreferred { get; set; }

    /// <summary>
    /// Setup-specific notes (e.g. "Requires fixture plate B", "Use offset #3").
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string AssignedBy { get; set; } = string.Empty;

    // ── Navigation ───────────────────────────────────────────
    public virtual MachineProgram MachineProgram { get; set; } = null!;
    public virtual Machine Machine { get; set; } = null!;
}
