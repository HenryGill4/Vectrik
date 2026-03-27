using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Vectrik.Models;

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

    public int? RequiredOperatorRoleId { get; set; }
    public virtual OperatorRole? RequiredOperatorRole { get; set; }

    public string CustomFieldsConfig { get; set; } = "[]";

    /// <summary>JSON-serialized StageUiConfig controlling operator shop floor UI.</summary>
    public string StageUiConfigJson { get; set; } = "{}";

    /// <summary>JSON-serialized StagePageLayout controlling widget layout on operator page.</summary>
    public string PageLayoutJson { get; set; } = "{}";

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

    public bool IsExternalOperation { get; set; }
    public double? DefaultTurnaroundDays { get; set; }
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

    public StageUiConfig GetUiConfig()
    {
        if (string.IsNullOrWhiteSpace(StageUiConfigJson) || StageUiConfigJson == "{}")
            return new StageUiConfig();
        return JsonSerializer.Deserialize<StageUiConfig>(StageUiConfigJson) ?? new StageUiConfig();
    }

    public void SetUiConfig(StageUiConfig config)
    {
        StageUiConfigJson = JsonSerializer.Serialize(config);
    }

    public StagePageLayout GetPageLayout()
    {
        if (string.IsNullOrWhiteSpace(PageLayoutJson) || PageLayoutJson == "{}")
            return StagePageLayout.Default;
        return JsonSerializer.Deserialize<StagePageLayout>(PageLayoutJson) ?? StagePageLayout.Default;
    }

    public void SetPageLayout(StagePageLayout layout)
    {
        PageLayoutJson = JsonSerializer.Serialize(layout);
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

    /// <summary>
    /// Returns assigned machine IDs as integers (Machine.Id), parsed from the
    /// comma-separated AssignedMachineIds field which stores Machine.Id int values.
    /// </summary>
    public List<int> GetAssignedMachineIntIds()
    {
        if (string.IsNullOrWhiteSpace(AssignedMachineIds))
            return new List<int>();
        var result = new List<int>();
        foreach (var entry in AssignedMachineIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(entry, out var id))
                result.Add(id);
        }
        return result;
    }

    /// <summary>
    /// Checks whether the given machine (by int PK) can execute this stage.
    /// Returns true if no machines are assigned (any machine is capable) or the machine is in the assigned list.
    /// </summary>
    public bool CanMachineExecuteStage(int machineId)
    {
        var assigned = GetAssignedMachineIntIds();
        return assigned.Count == 0 || assigned.Contains(machineId);
    }

    public decimal GetTotalEstimatedCost()
    {
        return (DefaultHourlyRate * (decimal)DefaultDurationHours) + DefaultMaterialCost;
    }
}
