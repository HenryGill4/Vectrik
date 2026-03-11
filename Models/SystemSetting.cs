using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class SystemSetting
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;
}
