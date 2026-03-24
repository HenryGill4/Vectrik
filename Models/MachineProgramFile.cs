using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

/// <summary>
/// A file attachment for a MachineProgram. Stores G-code, NC files, setup sheets,
/// and other documents on the local file system.
/// </summary>
public class MachineProgramFile
{
    public int Id { get; set; }

    [Required]
    public int MachineProgramId { get; set; }

    [Required, MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// File extension/type (e.g., "NC", "TAP", "MPF", "DXF", "PDF", "STEP", "Other").
    /// </summary>
    [MaxLength(20)]
    public string FileType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    /// <summary>
    /// SHA-256 hash for integrity verification and deduplication.
    /// </summary>
    [MaxLength(64)]
    public string? FileHash { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is the primary program file (vs supporting docs like setup sheets).
    /// </summary>
    public bool IsPrimary { get; set; }

    [Required, MaxLength(100)]
    public string UploadedBy { get; set; } = string.Empty;

    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual MachineProgram MachineProgram { get; set; } = null!;
}
