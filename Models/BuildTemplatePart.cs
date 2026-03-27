using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class BuildTemplatePart
{
    public int Id { get; set; }

    [Required]
    public int BuildTemplateId { get; set; }

    [Required]
    public int PartId { get; set; }

    [Required, Range(1, 500)]
    public int Quantity { get; set; } = 1;

    public int StackLevel { get; set; } = 1;

    [MaxLength(500)]
    public string? PositionNotes { get; set; }

    // Navigation
    public virtual BuildTemplate BuildTemplate { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
}
