using System.ComponentModel.DataAnnotations.Schema;

namespace Vectrik.Models;

/// <summary>
/// Per-machine (or global default) dispatch configuration.
/// Controls auto-dispatch behavior, scoring weights, and queue settings.
/// MachineId = null means global default; per-machine configs override global.
/// </summary>
public class DispatchConfiguration
{
    public int Id { get; set; }

    /// <summary>Null = global default config. Set = machine-specific override.</summary>
    public int? MachineId { get; set; }

    /// <summary>Optional: stage-specific config override.</summary>
    public int? ProductionStageId { get; set; }

    public bool AutoDispatchEnabled { get; set; }

    /// <summary>Max dispatches in a machine's queue before auto-dispatch stops adding.</summary>
    public int MaxQueueDepth { get; set; } = 3;

    /// <summary>How far ahead (hours) the auto-dispatch engine looks for candidates.</summary>
    public int LookAheadHours { get; set; } = 8;

    // ── Scoring Weights (must sum to ~1.0) ───────────────────

    [Column(TypeName = "decimal(4,2)")]
    public decimal ChangeoverPenaltyWeight { get; set; } = 0.30m;

    [Column(TypeName = "decimal(4,2)")]
    public decimal DueDateWeight { get; set; } = 0.40m;

    [Column(TypeName = "decimal(4,2)")]
    public decimal ThroughputWeight { get; set; } = 0.15m;

    /// <summary>
    /// Weight for scheduling rule compliance scoring. Penalizes dispatches that
    /// would place work in slots violating machine scheduling rules (e.g., no
    /// operator for changeover, blackout periods, consecutive build limits).
    /// </summary>
    [Column(TypeName = "decimal(4,2)")]
    public decimal SchedulingRuleWeight { get; set; } = 0.15m;

    // ── Behavior ─────────────────────────────────────────────

    /// <summary>Hours before maintenance to start routing short jobs.</summary>
    public int MaintenanceBufferHours { get; set; } = 4;

    /// <summary>Require QC verification after setup before marking complete.</summary>
    public bool RequiresVerification { get; set; } = true;

    /// <summary>Auto-assign best-proficiency operator when dispatching.</summary>
    public bool AutoAssignOperator { get; set; }

    /// <summary>Push SignalR notification to operators on dispatch.</summary>
    public bool NotifyOnDispatch { get; set; } = true;

    /// <summary>Hours within which same-program jobs are batched into one dispatch.</summary>
    public int BatchGroupingWindowHours { get; set; } = 4;

    /// <summary>Auto-generated dispatches require scheduler approval before entering queue.</summary>
    public bool RequiresSchedulerApproval { get; set; } = true;

    // ── Navigation ───────────────────────────────────────────

    public virtual Machine? Machine { get; set; }
    public virtual ProductionStage? ProductionStage { get; set; }
}
