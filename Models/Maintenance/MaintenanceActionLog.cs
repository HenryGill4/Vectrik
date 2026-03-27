using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models.Maintenance;

public class MaintenanceActionLog
{
    public int Id { get; set; }

    public int? MaintenanceRuleId { get; set; }

    [Required, MaxLength(50)]
    public string MachineId { get; set; } = string.Empty;

    public int? MachineComponentId { get; set; }

    [Required, MaxLength(200)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string PerformedBy { get; set; } = string.Empty;

    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation
    public virtual MaintenanceRule? MaintenanceRule { get; set; }
}
