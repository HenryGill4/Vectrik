using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Opcentrix_V3.Models;

public class QuoteRevision
{
    public int Id { get; set; }

    public int QuoteId { get; set; }

    public int RevisionNumber { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalEstimatedCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? QuotedPrice { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedLaborCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedMaterialCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedOverheadCost { get; set; }

    public decimal TargetMarginPct { get; set; }

    public string? LinesSnapshot { get; set; } // JSON snapshot of lines at this revision

    [MaxLength(500)]
    public string? ChangeNotes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Quote Quote { get; set; } = null!;
}
