using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class StockLocation
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public LocationType LocationType { get; set; }

    [MaxLength(50)]
    public string? ParentLocationCode { get; set; }

    public bool IsActive { get; set; } = true;
}
