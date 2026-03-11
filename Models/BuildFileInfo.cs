using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class BuildFileInfo
{
    public int Id { get; set; }

    [Required]
    public int BuildPackageId { get; set; }

    [MaxLength(200)]
    public string? FileName { get; set; }

    public int? LayerCount { get; set; }
    public decimal? BuildHeightMm { get; set; }
    public decimal? EstimatedPrintTimeHours { get; set; }
    public decimal? EstimatedPowderKg { get; set; }

    public string? PartPositionsJson { get; set; }

    [MaxLength(100)]
    public string? SlicerSoftware { get; set; }

    [MaxLength(50)]
    public string? SlicerVersion { get; set; }

    public DateTime ImportedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string ImportedBy { get; set; } = string.Empty;

    // Navigation
    public virtual BuildPackage BuildPackage { get; set; } = null!;
}
