using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vectrik.Models;

/// <summary>
/// Standalone cost study for quoting hypothetical or in-development parts.
/// Independent of production — a study does not require Parts or ProductionStages to exist.
/// </summary>
public class CostStudy
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string StudyNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CustomerName { get; set; }

    [MaxLength(100)]
    public string? ProjectName { get; set; }

    /// <summary>Draft, Final, Archived.</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>Default margin applied to cost to derive a suggested sell price (0–100%).</summary>
    [Range(0, 500)]
    public double TargetMarginPercent { get; set; } = 30.0;

    /// <summary>Contingency / risk buffer added to total cost before margin (0–100%).</summary>
    [Range(0, 100)]
    public double ContingencyPercent { get; set; } = 5.0;

    /// <summary>Optional G&amp;A / administrative overhead applied to the study total (0–100%).</summary>
    [Range(0, 100)]
    public double AdminOverheadPercent { get; set; } = 0.0;

    /// <summary>Default vendor/outside-operation markup applied on external stage costs (0–200%).</summary>
    [Range(0, 200)]
    public double DefaultVendorMarkupPercent { get; set; } = 15.0;

    /// <summary>Optional payment-terms discount applied to the suggested sell price (0–20%).</summary>
    [Range(0, 20)]
    public double PaymentTermsDiscountPercent { get; set; } = 0.0;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    public virtual ICollection<CostStudyPart> Parts { get; set; } = new List<CostStudyPart>();
}
