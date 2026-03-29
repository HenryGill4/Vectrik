using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

/// <summary>
/// First-class customer entity. Centralizes customer info used across
/// Quotes, Work Orders, and pricing rules. Existing string-based CustomerName
/// fields remain for backwards compat; this entity enables customer-level
/// pricing, contract management, and analytics.
/// </summary>
public class Customer
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Code { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Company { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    // ── Classification ───────────────────────────────────────

    public CustomerTier Tier { get; set; } = CustomerTier.Standard;

    /// <summary>Default discount % applied to new quotes for this customer (0-100).</summary>
    [Range(0, 100)]
    public decimal DefaultDiscountPct { get; set; }

    /// <summary>Default target margin % for this customer's quotes.</summary>
    [Range(0, 100)]
    public decimal? DefaultMarginPct { get; set; }

    /// <summary>Payment terms in days (Net 30, Net 60, etc.).</summary>
    public int PaymentTermDays { get; set; } = 30;

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    public bool IsDefenseCustomer { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── Audit ────────────────────────────────────────────────

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }

    // ── Navigation ───────────────────────────────────────────

    public virtual ICollection<CustomerPricingRule> PricingRules { get; set; } = new List<CustomerPricingRule>();
    public virtual ICollection<PricingContract> Contracts { get; set; } = new List<PricingContract>();
}

/// <summary>
/// Customer-specific pricing override for a part. Allows per-customer
/// negotiated prices, volume breaks, and discount overrides.
/// </summary>
public class CustomerPricingRule
{
    public int Id { get; set; }

    [Required]
    public int CustomerId { get; set; }

    [Required]
    public int PartId { get; set; }

    /// <summary>Negotiated sell price per unit for this customer+part combo.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? NegotiatedPricePerUnit { get; set; }

    /// <summary>Discount % off the standard PartPricing sell price (0-100).</summary>
    [Range(0, 100)]
    public decimal DiscountPct { get; set; }

    /// <summary>Minimum quantity for this pricing to apply.</summary>
    public int MinQuantity { get; set; } = 1;

    /// <summary>Maximum quantity for this pricing (null = unlimited).</summary>
    public int? MaxQuantity { get; set; }

    /// <summary>Priority for rule evaluation — lower number = higher priority.</summary>
    public int Priority { get; set; } = 100;

    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // ── Navigation ───────────────────────────────────────────

    public virtual Customer Customer { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
}

/// <summary>
/// A pricing contract with a customer — defines term-based pricing agreements
/// with volume commitments, blanket PO pricing, or annual rate cards.
/// </summary>
public class PricingContract
{
    public int Id { get; set; }

    [Required]
    public int CustomerId { get; set; }

    [Required, MaxLength(50)]
    public string ContractNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public ContractType Type { get; set; } = ContractType.Standard;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Blanket discount % that applies to all parts under this contract.</summary>
    [Range(0, 100)]
    public decimal BlanketDiscountPct { get; set; }

    /// <summary>Minimum annual volume commitment (dollars).</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal? MinAnnualCommitment { get; set; }

    /// <summary>Actual volume to date under this contract.</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal ActualVolume { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // ── Navigation ───────────────────────────────────────────

    public virtual Customer Customer { get; set; } = null!;
}
