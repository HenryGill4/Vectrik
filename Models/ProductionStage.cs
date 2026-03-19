using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Opcentrix_V3.Models;

public class ProductionStage
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string StageSlug { get; set; } = string.Empty;

    public bool HasBuiltInPage { get; set; }
    public bool RequiresSerialNumber { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int DefaultSetupMinutes { get; set; } = 30;

    [Column(TypeName = "decimal(8,2)")]
    public decimal DefaultHourlyRate { get; set; } = 85.00m;

    public bool RequiresQualityCheck { get; set; } = true;
    public bool RequiresApproval { get; set; }
    public bool AllowSkip { get; set; }
    public bool IsOptional { get; set; }

    [MaxLength(50)]
    public string? RequiredRole { get; set; }

    public string CustomFieldsConfig { get; set; } = "[]";

    [MaxLength(500)]
    public string? AssignedMachineIds { get; set; }

    public bool RequiresMachineAssignment { get; set; }

    [MaxLength(50)]
    public string? DefaultMachineId { get; set; }

    [MaxLength(7)]
    public string StageColor { get; set; } = "#007bff";

    [MaxLength(50)]
    public string StageIcon { get; set; } = "fas fa-cogs";

    [MaxLength(100)]
    public string? Department { get; set; }

    public bool AllowParallelExecution { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal DefaultMaterialCost { get; set; }

    public double DefaultDurationHours { get; set; } = 1.0;
    public bool IsBatchStage { get; set; }
    public bool IsBuildLevelStage { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual ICollection<PartStageRequirement> PartStageRequirements { get; set; } = new List<PartStageRequirement>();
    public virtual ICollection<StageExecution> StageExecutions { get; set; } = new List<StageExecution>();

    // Helper methods
    public List<CustomFieldDefinition> GetCustomFields()
    {
        if (string.IsNullOrWhiteSpace(CustomFieldsConfig) || CustomFieldsConfig == "[]")
            return new List<CustomFieldDefinition>();
        return JsonSerializer.Deserialize<List<CustomFieldDefinition>>(CustomFieldsConfig) ?? new List<CustomFieldDefinition>();
    }

    public void SetCustomFields(List<CustomFieldDefinition> fields)
    {
        CustomFieldsConfig = JsonSerializer.Serialize(fields);
    }

    public List<string> GetAssignedMachineIds()
    {
        if (string.IsNullOrWhiteSpace(AssignedMachineIds))
            return new List<string>();
        return AssignedMachineIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public void SetAssignedMachineIds(List<string> machineIds)
    {
        AssignedMachineIds = string.Join(",", machineIds);
    }

    public bool CanMachineExecuteStage(string machineId)
    {
        var assigned = GetAssignedMachineIds();
        return assigned.Count == 0 || assigned.Contains(machineId);
    }

    public decimal GetTotalEstimatedCost()
    {
        return (DefaultHourlyRate * (decimal)DefaultDurationHours) + DefaultMaterialCost;
    }
}
