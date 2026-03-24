using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

/// <summary>
/// Revision history for MachineProgram changes.
/// Tracks part list snapshots and change notes over time.
/// </summary>
public class ProgramRevision
{
    public int Id { get; set; }

    public int MachineProgramId { get; set; }
    public virtual MachineProgram MachineProgram { get; set; } = null!;

    public int RevisionNumber { get; set; }

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string ChangedBy { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ChangeNotes { get; set; }

    /// <summary>
    /// JSON snapshot of ProgramPart entries at this revision.
    /// </summary>
    public string PartsSnapshotJson { get; set; } = "[]";

    /// <summary>
    /// JSON snapshot of program parameters at this revision.
    /// </summary>
    public string? ParametersSnapshotJson { get; set; }
}
