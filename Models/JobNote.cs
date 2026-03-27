using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class JobNote
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }

    [Required]
    public string NoteText { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Job Job { get; set; } = null!;
}
