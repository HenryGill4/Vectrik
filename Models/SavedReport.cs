using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class SavedReport
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ReportType { get; set; } = string.Empty;

    public string FilterJson { get; set; } = "{}";

    [Required, MaxLength(100)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public bool IsShared { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
