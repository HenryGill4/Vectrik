using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class DocumentTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Entity type this template renders: Quote, WorkOrder, PackingList, CoC, FAIR, BOL.
    /// </summary>
    [Required, MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Handlebars-style HTML template body with merge fields like {{CompanyName}}.
    /// </summary>
    public string TemplateHtml { get; set; } = string.Empty;

    /// <summary>
    /// Optional HTML for the document header (repeated on each page).
    /// </summary>
    public string? HeaderHtml { get; set; }

    /// <summary>
    /// Optional HTML for the document footer (repeated on each page).
    /// </summary>
    public string? FooterHtml { get; set; }

    /// <summary>
    /// Optional CSS overrides for this template.
    /// </summary>
    public string? CssOverrides { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;
}
