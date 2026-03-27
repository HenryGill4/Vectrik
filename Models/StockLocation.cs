using System.ComponentModel.DataAnnotations;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

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
