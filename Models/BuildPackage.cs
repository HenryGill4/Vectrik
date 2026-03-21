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

    /// <summary>
    /// FK to Machine.Id — the SLS machine assigned to this build.
    /// Null for Draft builds that haven't been assigned yet.
    /// </summary>
    public int? MachineId { get; set; }

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

    /// <summary>
    /// Estimated duration in hours for depowdering stage (build-level).
    /// Used when creating stage executions on plate release.
    /// </summary>
    public double? DepowderingHours { get; set; }

    /// <summary>
    /// Estimated duration in hours for heat treatment stage (build-level).
    /// </summary>
    public double? HeatTreatmentHours { get; set; }

    /// <summary>
    /// Estimated duration in hours for wire EDM stage (build-level).
    /// </summary>
    public double? WireEdmHours { get; set; }

    // Changeover chain: links to the build that was printing before this one
    public int? PredecessorBuildPackageId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Machine? Machine { get; set; }
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
