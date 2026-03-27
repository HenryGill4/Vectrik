using System.ComponentModel.DataAnnotations;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

public class QCInspection
{
    public int Id { get; set; }

    public int? JobId { get; set; }
    public int? PartId { get; set; }
    public int? PartInstanceId { get; set; }

    public int InspectorUserId { get; set; }

    public int? InspectionPlanId { get; set; }
    public bool IsFair { get; set; }
    public InspectionResult OverallResult { get; set; } = InspectionResult.Pending;
    public int? NonConformanceReportId { get; set; }

    public DateTime InspectionDate { get; set; } = DateTime.UtcNow;

    public bool OverallPass { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    [MaxLength(200)]
    public string? CorrectiveActionText { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Job? Job { get; set; }
    public virtual Part? Part { get; set; }
    public virtual PartInstance? PartInstance { get; set; }
    public virtual User Inspector { get; set; } = null!;
    public virtual InspectionPlan? InspectionPlan { get; set; }
    public virtual ICollection<QCChecklistItem> ChecklistItems { get; set; } = new List<QCChecklistItem>();
    public virtual ICollection<InspectionMeasurement> Measurements { get; set; } = new List<InspectionMeasurement>();
}

public class QCChecklistItem
{
    public int Id { get; set; }

    [Required]
    public int QCInspectionId { get; set; }

    [Required, MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool Passed { get; set; }

    [MaxLength(100)]
    public string? MeasuredValue { get; set; }

    [MaxLength(100)]
    public string? ExpectedValue { get; set; }

    [MaxLength(100)]
    public string? Tolerance { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public virtual QCInspection QCInspection { get; set; } = null!;
}
