using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models.Platform;

public class TenantFeatureFlag
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string TenantCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FeatureKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public DateTime? EnabledAt { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
