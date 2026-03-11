using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models.Maintenance;

public class MaintenanceWorkOrder
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string MachineId { get; set; } = string.Empty;

    public int? MachineComponentId { get; set; }
    public int? MaintenanceRuleId { get; set; }

    public MaintenanceWorkOrderType Type { get; set; } = MaintenanceWorkOrderType.Preventive;
    public MaintenanceWorkOrderStatus Status { get; set; } = MaintenanceWorkOrderStatus.Open;
    public MaintenanceWorkOrderPriority Priority { get; set; } = MaintenanceWorkOrderPriority.Normal;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int? AssignedTechnicianUserId { get; set; }

    public DateTime? ScheduledDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public double? EstimatedHours { get; set; }
    public double? ActualHours { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? EstimatedCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? ActualCost { get; set; }

    public bool RequiresShutdown { get; set; }

    public string? PartsUsed { get; set; }
    public string? WorkPerformed { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Machine Machine { get; set; } = null!;
    public virtual MachineComponent? MachineComponent { get; set; }
    public virtual MaintenanceRule? MaintenanceRule { get; set; }
    public virtual User? AssignedTechnician { get; set; }
}
