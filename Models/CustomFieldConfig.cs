using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class CustomFieldConfig
{
    public int Id { get; set; }

    /// <summary>
    /// Entity type this config applies to, e.g. "WorkOrder", "Quote", "Part", "Inventory".
    /// </summary>
    [Required, MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of CustomFieldDefinition objects describing each field.
    /// </summary>
    public string FieldDefinitionsJson { get; set; } = "[]";

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;
}

/// <summary>
/// Defines a single custom field. Serialized as JSON inside CustomFieldConfig.FieldDefinitionsJson.
/// </summary>
public class CustomFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// text, number, decimal, date, select, multiselect, checkbox, textarea
    /// </summary>
    public string FieldType { get; set; } = "text";

    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Comma-separated options for select/multiselect fields.
    /// </summary>
    public string? Options { get; set; }

    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public string? ValidationRegex { get; set; }

    /// <summary>
    /// Optional conditional visibility: show this field only when another field has a specific value.
    /// Format: "FieldName=Value"
    /// </summary>
    public string? VisibleWhen { get; set; }
}
