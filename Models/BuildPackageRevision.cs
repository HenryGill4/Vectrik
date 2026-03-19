using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class BuildPackageRevision
{
    public int Id { get; set; }

    public int BuildPackageId { get; set; }
    public virtual BuildPackage BuildPackage { get; set; } = null!;

    public int RevisionNumber { get; set; }

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string ChangedBy { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ChangeNotes { get; set; }

    public string PartsSnapshotJson { get; set; } = "[]";

    public string? ParametersSnapshotJson { get; set; }
}
