using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class PartRevisionHistory
{
    public int Id { get; set; }

    public int PartId { get; set; }

    [Required, MaxLength(20)]
    public string Revision { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PreviousRevision { get; set; }

    [MaxLength(1000)]
    public string? ChangeDescription { get; set; }

    [MaxLength(200)]
    public string? RawMaterialSpec { get; set; }

    [MaxLength(50)]
    public string? DrawingNumber { get; set; }

    public string? RoutingSnapshot { get; set; } // JSON snapshot of stage requirements at this revision

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Part Part { get; set; } = null!;
}
