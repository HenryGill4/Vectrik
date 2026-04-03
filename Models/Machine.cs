using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;
using Vectrik.Models.Maintenance;

namespace Vectrik.Models;

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

    /// <summary>
    /// Time (minutes) from operator arrival until machine can start a new build.
    /// Covers removing the cooled plate from the cooldown chamber, loading a fresh
    /// build plate, and any pre-print setup. Default 90 min (~1.5 hrs) for EOS M4.
    /// Only relevant when AutoChangeoverEnabled is true.
    /// </summary>
    public double OperatorUnloadMinutes { get; set; } = 90;

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

    /// <summary>
    /// Number of tool magazine slots on this machine (0 = unlimited/not applicable).
    /// </summary>
    public int ToolSlotCount { get; set; }

    // ── Dispatch System ──────────────────────────────────────

    /// <summary>
    /// FK to the program currently loaded/set up on this machine.
    /// Bridges scheduler intent vs floor reality for accurate changeover scoring.
    /// </summary>
    public int? CurrentProgramId { get; set; }

    /// <summary>Current setup state of the machine from a dispatch perspective.</summary>
    public MachineSetupState SetupState { get; set; } = MachineSetupState.Unknown;

    /// <summary>When the last setup change occurred on this machine.</summary>
    public DateTime? LastSetupChangeAt { get; set; }

    // Navigation
    public virtual MachineProgram? CurrentProgram { get; set; }
    public virtual ICollection<MachineComponent> Components { get; set; } = new List<MachineComponent>();
    public virtual ICollection<MachineProgramAssignment> ProgramAssignments { get; set; } = new List<MachineProgramAssignment>();
    public virtual ICollection<MachineShiftAssignment> ShiftAssignments { get; set; } = new List<MachineShiftAssignment>();
    public virtual ICollection<MachineSchedulingRule> SchedulingRules { get; set; } = new List<MachineSchedulingRule>();
    public virtual ICollection<MachineBlackoutAssignment> BlackoutAssignments { get; set; } = new List<MachineBlackoutAssignment>();
}
