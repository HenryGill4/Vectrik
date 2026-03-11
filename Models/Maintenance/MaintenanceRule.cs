using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models.Maintenance;

public class MaintenanceRule
{
    public int Id { get; set; }

    [Required]
    public int MachineComponentId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public MaintenanceTriggerType TriggerType { get; set; } = MaintenanceTriggerType.HoursRun;

    public double ThresholdValue { get; set; }

    public MaintenanceSeverity Severity { get; set; } = MaintenanceSeverity.Warning;

    [Range(0, 100)]
    public int EarlyWarningPercent { get; set; } = 80;

    public double? EstimatedDurationHours { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Instructions { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual MachineComponent MachineComponent { get; set; } = null!;
}
