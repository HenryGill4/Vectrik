using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Opcentrix_V3.Models;

public class Material
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = "Metal Powder";

    public double? Density { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal CostPerKg { get; set; }

    [MaxLength(200)]
    public string? Supplier { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? CompatibleMaterials { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;
}
