using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class WorkInstructionMedia
{
    public int Id { get; set; }

    [Required]
    public int WorkInstructionStepId { get; set; }
    public virtual WorkInstructionStep Step { get; set; } = null!;

    public MediaType MediaType { get; set; }

    [Required, MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? AltText { get; set; }

    public int DisplayOrder { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
