using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models.Platform;

public class Tenant
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(7)]
    public string? PrimaryColor { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? SubscriptionTier { get; set; }

    [MaxLength(200)]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    public string? ContactPhone { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
