namespace Opcentrix_V3.Models;

/// <summary>
/// Captures the full diagnostic output from a scheduling operation for debugging.
/// Wire into the Scheduler UI to inspect exactly what happened during scheduling.
/// </summary>
public record ScheduleDiagnosticReport
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public string Operation { get; init; } = "";
    public int? MachineId { get; init; }
    public List<BuildSlotDiagnostic> BuildSlots { get; init; } = [];
    public List<JobScheduleDiagnostic> Jobs { get; init; } = [];
    public List<string> Warnings { get; init; } = [];

    /// <summary>Total stage executions created across all jobs in this operation.</summary>
    public int TotalExecutionsCreated => Jobs.Sum(j => j.Executions.Count);

    /// <summary>Executions that ended up with no machine assigned.</summary>
    public int UnassignedCount => Jobs.Sum(j => j.Executions.Count(e => !e.AssignedMachineId.HasValue));
}

/// <summary>
/// Diagnostic info for a build-level slot allocation.
/// </summary>
public record BuildSlotDiagnostic
{
    public int JobId { get; init; }
    public DateTime SlotStart { get; init; }
    public DateTime SlotEnd { get; init; }
    public int MachineId { get; init; }
    public string MachineName { get; init; } = "";
    public int ExistingBlocksOnMachine { get; init; }
    public double DurationHours { get; init; }
}

/// <summary>
/// Diagnostic info for a job's auto-scheduling pass.
/// </summary>
public record JobScheduleDiagnostic
{
    public int JobId { get; init; }
    public string JobNumber { get; init; } = "";
    public string Scope { get; init; } = "";
    public string PartNumber { get; init; } = "";
    public int ExecutionCount { get; init; }
    public List<ExecutionScheduleDiagnostic> Executions { get; init; } = [];
}

/// <summary>
/// Diagnostic info for a single stage execution's scheduling decision.
/// </summary>
public record ExecutionScheduleDiagnostic
{
    public int ExecutionId { get; init; }
    public string StageName { get; init; } = "";
    public int SortOrder { get; init; }
    public double TotalDurationHours { get; init; }

    // Machine resolution
    public string MachineResolutionPath { get; init; } = "";
    public List<string> CandidateMachines { get; init; } = [];
    public int? AssignedMachineId { get; init; }
    public string? AssignedMachineName { get; init; }

    // Slot finding
    public DateTime? ScheduledStart { get; init; }
    public DateTime? ScheduledEnd { get; init; }
    public DateTime CursorBefore { get; init; }
    public DateTime CursorAfter { get; init; }

    // Issues
    public bool HasMachine => AssignedMachineId.HasValue;
    public List<string> Issues { get; init; } = [];
}
