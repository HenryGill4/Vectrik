using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class PartNote
{
    public int Id { get; set; }

    public int PartId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(50)]
    public string NoteType { get; set; } = "Engineering"; // Engineering, Quality, Manufacturing, General

    public bool IsPinned { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? LastModifiedDate { get; set; }

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }

    // Navigation
    public virtual Part Part { get; set; } = null!;
}
