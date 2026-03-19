using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class NonConformanceReport
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string NcrNumber { get; set; } = string.Empty;

    public int? JobId { get; set; }
    public Job? Job { get; set; }

    public int? PartId { get; set; }
    public Part? Part { get; set; }

    public int? PartInstanceId { get; set; }
    public PartInstance? PartInstance { get; set; }

    public NcrType Type { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? QuantityAffected { get; set; }

    public NcrSeverity Severity { get; set; }
    public NcrDisposition Disposition { get; set; } = NcrDisposition.PendingReview;
    public NcrStatus Status { get; set; } = NcrStatus.Open;

    [MaxLength(100)]
    public string ReportedByUserId { get; set; } = string.Empty;

    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public int? CorrectiveActionId { get; set; }
    public CorrectiveAction? CorrectiveAction { get; set; }

    public string? CustomFieldValues { get; set; }
}
