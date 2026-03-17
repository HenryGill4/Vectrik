using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class RfqRequest
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Phone { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    public int? Quantity { get; set; }

    [MaxLength(100)]
    public string? Material { get; set; }

    public DateTime? NeededByDate { get; set; }

    public string? AttachmentPaths { get; set; } // JSON array of file paths

    [MaxLength(20)]
    public string Status { get; set; } = "New"; // New, Reviewed, Quoted, Declined

    public int? ConvertedQuoteId { get; set; }

    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedDate { get; set; }

    // Navigation
    public virtual Quote? ConvertedQuote { get; set; }
}
