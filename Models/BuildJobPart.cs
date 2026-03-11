using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class BuildJobPart
{
    public int Id { get; set; }

    [Required]
    public int BuildJobId { get; set; }

    [Required]
    public int PartId { get; set; }

    [MaxLength(50)]
    public string? PartNumber { get; set; }

    public int Quantity { get; set; } = 1;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public virtual BuildJob BuildJob { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
}
