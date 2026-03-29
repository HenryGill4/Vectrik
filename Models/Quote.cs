using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

public class Quote
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string QuoteNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CustomerEmail { get; set; }

    [MaxLength(50)]
    public string? CustomerPhone { get; set; }

    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationDate { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalEstimatedCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? QuotedPrice { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Markup { get; set; }

    // Cost Breakdown
    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedLaborCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedMaterialCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedOverheadCost { get; set; }

    [Range(0, 100)]
    public decimal TargetMarginPct { get; set; } = 25;

    // Revision tracking
    public int RevisionNumber { get; set; } = 1;

    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }

    public DateTime? LastModifiedDate { get; set; }

    // DLMS / Customization
    public string? CustomFieldValues { get; set; }

    [MaxLength(50)]
    public string? ContractNumber { get; set; }

    public bool IsDefenseContract { get; set; }

    public string? Notes { get; set; }

    // ── Win/Loss Tracking ────────────────────────────────────

    public QuoteLossReason LossReason { get; set; } = QuoteLossReason.None;

    [MaxLength(500)]
    public string? LossNotes { get; set; }

    /// <summary>Competitor price if known (for price sensitivity analysis).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? CompetitorPrice { get; set; }

    /// <summary>Days from quote sent to customer decision (accepted or rejected).</summary>
    public int? DecisionDays { get; set; }

    public int? ConvertedWorkOrderId { get; set; }

    // Navigation
    public virtual ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
    public virtual ICollection<QuoteRevision> Revisions { get; set; } = new List<QuoteRevision>();
    public virtual WorkOrder? ConvertedWorkOrder { get; set; }
}

public class QuoteLine
{
    public int Id { get; set; }

    [Required]
    public int QuoteId { get; set; }

    [Required]
    public int PartId { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedCostPerPart { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal QuotedPricePerPart { get; set; }

    // Cost breakdown per line
    public double LaborMinutes { get; set; }

    public double SetupMinutes { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MaterialCostEach { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal OutsideProcessCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal OverheadCostEach { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal SetupCostEach { get; set; }

    /// <summary>Stack level used for cost estimate (1=single, 2=double, 3=triple). Null for non-additive parts.</summary>
    public int? StackLevel { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public virtual Quote Quote { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
}
