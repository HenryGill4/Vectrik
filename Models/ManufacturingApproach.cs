using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

/// <summary>
/// Defines a manufacturing approach (process template) such as "SLS-Based" or "CNC Machining".
/// When selected on a Part, the <see cref="DefaultRoutingTemplate"/> is used to auto-scaffold
/// a <see cref="ManufacturingProcess"/> with <see cref="ProcessStage"/> entries.
/// </summary>
public class ManufacturingApproach
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(10)]
    public string? IconEmoji { get; set; }

    /// <summary>true → this is an additive/3D-printing approach (show stacking config on parts).</summary>
    public bool IsAdditive { get; set; }

    /// <summary>true → parts using this approach are scheduled via BuildPackages.</summary>
    public bool RequiresBuildPlate { get; set; }

    /// <summary>
    /// JSON array of routing template stages with processing levels.
    /// Enhanced format: [{"Slug":"sls-printing","Level":"Build",...}, ...]
    /// </summary>
    public string DefaultRoutingTemplate { get; set; } = "[]";

    public int DefaultBatchCapacity { get; set; } = 60;

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Parses the <see cref="DefaultRoutingTemplate"/> JSON into structured routing template stages.
    /// Handles both legacy format (["slug1","slug2"]) and enhanced format ([{Slug, Level, ...}]).
    /// </summary>
    [NotMapped]
    public List<RoutingTemplateStage> ParsedRoutingTemplate
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DefaultRoutingTemplate)) return [];
            try
            {
                var trimmed = DefaultRoutingTemplate.TrimStart();
                if (trimmed.StartsWith("[") && trimmed.Contains("{"))
                {
                    return JsonSerializer.Deserialize<List<RoutingTemplateStage>>(DefaultRoutingTemplate, RoutingTemplateStage.JsonOptions) ?? [];
                }
                // Legacy format: plain string array of slugs
                var slugs = JsonSerializer.Deserialize<List<string>>(DefaultRoutingTemplate) ?? [];
                return slugs.Select(s => new RoutingTemplateStage { Slug = s }).ToList();
            }
            catch { return []; }
        }
    }

    /// <summary>
    /// Serializes structured routing template stages back to JSON.
    /// </summary>
    public void SetRoutingTemplate(List<RoutingTemplateStage> stages)
    {
        DefaultRoutingTemplate = JsonSerializer.Serialize(stages, RoutingTemplateStage.JsonOptions);
    }
}

/// <summary>
/// A single stage entry within a <see cref="ManufacturingApproach.DefaultRoutingTemplate"/>.
/// Defines the stage slug, processing level, and scaffold hints for auto-creating a ManufacturingProcess.
/// </summary>
public class RoutingTemplateStage
{
    public string Slug { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProcessingLevel Level { get; set; } = ProcessingLevel.Part;

    public bool DurationFromBuildConfig { get; set; }
    public bool IsPlateReleaseTrigger { get; set; }
    public int? BatchCapacityOverride { get; set; }

    /// <summary>
    /// Machine DB Ids assigned to this stage. Supports multi-machine assignment.
    /// </summary>
    public List<int> MachineIds { get; set; } = [];

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
