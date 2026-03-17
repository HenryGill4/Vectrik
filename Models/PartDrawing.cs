using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class PartDrawing
{
    public int Id { get; set; }

    public int PartId { get; set; }

    [Required, MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(20)]
    public string FileType { get; set; } = string.Empty; // PDF, DXF, STEP, PNG, JPG

    public long FileSizeBytes { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string? Revision { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string UploadedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Part Part { get; set; } = null!;
}
