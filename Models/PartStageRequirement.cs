using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Opcentrix_V3.Models;

public class PartStageRequirement
{
    public int Id { get; set; }

    [Required]
    public int PartId { get; set; }

    [Required]
    public int ProductionStageId { get; set; }

    [Range(1, 100)]
    public int ExecutionOrder { get; set; } = 1;

    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool AllowParallelExecution { get; set; }
    public bool IsBlocking { get; set; } = true;

    // Timing & Cost Overrides
    /// <summary>
    /// Estimated processing time in minutes for per-part stages.
    /// Build-level stages get their duration from the build configuration.
    /// </summary>
    public int? EstimatedMinutes { get; set; }
    public int? SetupTimeMinutes { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal? HourlyRateOverride { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MaterialCost { get; set; }

    // Machine Assignment
    [MaxLength(50)]
    public string? AssignedMachineId { get; set; }

    public bool RequiresSpecificMachine { get; set; }

    [MaxLength(200)]
    public string? PreferredMachineIds { get; set; }

    // Custom Field Values
    public string CustomFieldValues { get; set; } = "{}";

    // Process Config
    public string StageParameters { get; set; } = "{}";
    public string RequiredMaterials { get; set; } = "[]";

    [MaxLength(500)]
    public string RequiredTooling { get; set; } = string.Empty;

    public string QualityRequirements { get; set; } = "{}";

    // Notes
    public string SpecialInstructions { get; set; } = string.Empty;
    public string RequirementNotes { get; set; } = string.Empty;

    // Learning (EMA)
    public double? ActualAverageDurationHours { get; set; }
    public int ActualSampleCount { get; set; }
    public double? LastActualDurationHours { get; set; }

    [MaxLength(20)]
    public string EstimateSource { get; set; } = "Manual";

    public DateTime? EstimateLastUpdated { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Part Part { get; set; } = null!;
    public virtual ProductionStage ProductionStage { get; set; } = null!;

    // Helper methods
    /// <summary>
    /// Returns the effective estimated time in hours for scheduling/costing.
    /// Converts EstimatedMinutes to hours, or uses learned duration if available.
    /// Build-level stages should get duration from build config instead.
    /// </summary>
    public double GetEffectiveEstimatedHours()
    {
        if (ActualAverageDurationHours.HasValue && EstimateSource == "Auto")
            return ActualAverageDurationHours.Value;

        // Convert minutes to hours if set, otherwise use stage default
        if (EstimatedMinutes.HasValue)
            return EstimatedMinutes.Value / 60.0;

        return ProductionStage?.DefaultDurationHours ?? 1.0;
    }

    /// <summary>
    /// Returns the estimated minutes for display purposes.
    /// </summary>
    public int GetEffectiveEstimatedMinutes()
    {
        if (EstimatedMinutes.HasValue)
            return EstimatedMinutes.Value;

        // Convert stage default hours to minutes
        var defaultHours = ProductionStage?.DefaultDurationHours ?? 1.0;
        return (int)(defaultHours * 60);
    }

    public decimal GetEffectiveHourlyRate()
    {
        return HourlyRateOverride ?? ProductionStage?.DefaultHourlyRate ?? 85.00m;
    }

    public decimal CalculateTotalEstimatedCost()
    {
        return (GetEffectiveHourlyRate() * (decimal)GetEffectiveEstimatedHours()) + MaterialCost;
    }

    public Dictionary<string, object?> GetCustomFieldValues()
    {
        if (string.IsNullOrWhiteSpace(CustomFieldValues) || CustomFieldValues == "{}")
            return new Dictionary<string, object?>();
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(CustomFieldValues) ?? new Dictionary<string, object?>();
    }

    public void SetCustomFieldValues(Dictionary<string, object?> values)
    {
        CustomFieldValues = JsonSerializer.Serialize(values);
    }

    public T? GetCustomFieldValue<T>(string fieldName)
    {
        var values = GetCustomFieldValues();
        if (values.TryGetValue(fieldName, out var value) && value is JsonElement element)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return default;
    }

    public void SetCustomFieldValue(string fieldName, object? value)
    {
        var values = GetCustomFieldValues();
        values[fieldName] = value;
        SetCustomFieldValues(values);
    }

    public List<string> GetPreferredMachineIds()
    {
        if (string.IsNullOrWhiteSpace(PreferredMachineIds))
            return new List<string>();
        return PreferredMachineIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public void SetPreferredMachineIds(List<string> machineIds)
    {
        PreferredMachineIds = string.Join(",", machineIds);
    }

    public bool CanMachineExecute(string machineId)
    {
        if (RequiresSpecificMachine && !string.IsNullOrEmpty(AssignedMachineId))
            return AssignedMachineId == machineId;

        var preferred = GetPreferredMachineIds();
        return preferred.Count == 0 || preferred.Contains(machineId);
    }

    public string? GetBestMachineId()
    {
        if (RequiresSpecificMachine && !string.IsNullOrEmpty(AssignedMachineId))
            return AssignedMachineId;

        var preferred = GetPreferredMachineIds();
        return preferred.Count > 0 ? preferred[0] : null;
    }

    public List<int> GetDependencies()
    {
        // Returns stage IDs that must complete before this stage
        // Based on ExecutionOrder - all stages with lower order are dependencies
        return new List<int>();
    }
}
