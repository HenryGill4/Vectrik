using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class BuildPackage
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(50)]
    public string MachineId { get; set; } = string.Empty;

    public BuildPackageStatus Status { get; set; } = BuildPackageStatus.Draft;

    [MaxLength(100)]
    public string? Material { get; set; }

    public int? ScheduledJobId { get; set; }

    public DateTime? ScheduledDate { get; set; }

    public double? EstimatedDurationHours { get; set; }

    public string? Notes { get; set; }

    // Build plate revision tracking
    public int? CurrentRevision { get; set; }

    // Slicer data
    public bool IsSlicerDataEntered { get; set; }

    // Scheduling lock
    public bool IsLocked { get; set; }

    // JSON for build-level params (layer thickness, laser power, etc.)
    public string? BuildParameters { get; set; }

    // Build lifecycle timestamps
    public DateTime? PrintStartedAt { get; set; }
    public DateTime? PrintCompletedAt { get; set; }
    public DateTime? PlateReleasedAt { get; set; }

    // Changeover chain: links to the build that was printing before this one
    public int? PredecessorBuildPackageId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual ICollection<BuildPackagePart> Parts { get; set; } = new List<BuildPackagePart>();
    public virtual ICollection<BuildPackageRevision> Revisions { get; set; } = new List<BuildPackageRevision>();
    public virtual BuildFileInfo? BuildFileInfo { get; set; }
    public virtual Job? ScheduledJob { get; set; }
    public virtual BuildPackage? PredecessorBuildPackage { get; set; }

    // Computed
    [NotMapped]
    public int TotalPartCount => Parts?.Sum(p => p.Quantity) ?? 0;

    [NotMapped]
    public int UniquePartCount => Parts?.Select(p => p.PartId).Distinct().Count() ?? 0;

    [NotMapped]
    public bool IsReadyToSchedule => Status == BuildPackageStatus.Ready && IsSlicerDataEntered && Parts?.Any() == true;
}

public class BuildPackagePart
{
    public int Id { get; set; }

    [Required]
    public int BuildPackageId { get; set; }

    [Required]
    public int PartId { get; set; }

    public int Quantity { get; set; } = 1;

    public int? WorkOrderLineId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int StackLevel { get; set; } = 1;           // 1=single, 2=double, 3=triple

    [MaxLength(500)]
    public string? SlicerNotes { get; set; }            // Position/orientation notes from slicer

    // Navigation
    public virtual BuildPackage BuildPackage { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
    public virtual WorkOrderLine? WorkOrderLine { get; set; }
}
