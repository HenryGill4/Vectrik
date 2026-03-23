using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Models.Maintenance;

namespace Opcentrix_V3.Models;

public class Machine
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string MachineId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string MachineType { get; set; } = "SLS";

    [MaxLength(100)]
    public string? MachineModel { get; set; }

    [MaxLength(50)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(50)]
    public string? Department { get; set; }

    public MachineStatus Status { get; set; } = MachineStatus.Idle;

    public bool IsActive { get; set; } = true;
    public bool IsAvailableForScheduling { get; set; } = true;

    [Range(1, 10)]
    public int Priority { get; set; } = 5;

    [MaxLength(1000)]
    public string? SupportedMaterials { get; set; }

    [MaxLength(100)]
    public string? CurrentMaterial { get; set; }

    public double MaintenanceIntervalHours { get; set; } = 500;
    public double HoursSinceLastMaintenance { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public double TotalOperatingHours { get; set; }

    // SLS-specific
    public double BuildLengthMm { get; set; } = 250;
    public double BuildWidthMm { get; set; } = 250;
    public double BuildHeightMm { get; set; } = 300;
    public double MaxLaserPowerWatts { get; set; } = 400;

    // Build plate management
    public int BuildPlateCapacity { get; set; } = 1;
    public bool AutoChangeoverEnabled { get; set; }
    public double ChangeoverMinutes { get; set; } = 30;

    // Laser configuration (planning reference)
    public int? LaserCount { get; set; }

    // OPC UA
    [MaxLength(200)]
    public string? OpcUaEndpointUrl { get; set; }
    public bool OpcUaEnabled { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal HourlyRate { get; set; } = 150.00m;

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// Whether this machine is an additive/build-plate machine (SLS, DMLS, MJF, EBM, etc.).
    /// Persisted in DB so admins can set this for any machine type.
    /// </summary>
    public bool IsAdditiveMachine { get; set; }

    // Navigation
    public virtual ICollection<MachineComponent> Components { get; set; } = new List<MachineComponent>();
    public virtual ICollection<MachineProgramAssignment> ProgramAssignments { get; set; } = new List<MachineProgramAssignment>();
}
