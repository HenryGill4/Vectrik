namespace Vectrik.Models;

/// <summary>
/// Per-operator permission overrides. Stored as JSON in User.PermissionsJson.
/// Admins configure these in Admin → Users for fine-grained operator control.
/// All defaults allow everything — admins opt-in to restrictions.
/// </summary>
public class OperatorPermissions
{
    /// <summary>Operator can pause work and log delay reasons.</summary>
    public bool CanPause { get; set; } = true;

    /// <summary>Operator can fail/reject work and create NCRs.</summary>
    public bool CanFail { get; set; } = true;

    /// <summary>Operator can complete work without all checklist items signed.</summary>
    public bool CanCompleteWithoutChecklist { get; set; } = false;

    /// <summary>Operator can view cost data (hourly rates, material costs, job costs).</summary>
    public bool CanViewCostData { get; set; } = false;

    /// <summary>Operator can transfer work to another operator.</summary>
    public bool CanTransfer { get; set; } = false;

    /// <summary>Operator can claim unassigned work from the available pool.</summary>
    public bool CanClaimWork { get; set; } = true;

    /// <summary>Operator can access all stages regardless of AssignedStageIds.</summary>
    public bool CanAccessAllStages { get; set; } = false;

    /// <summary>Maximum number of concurrent active jobs (0 = unlimited).</summary>
    public int MaxConcurrentJobs { get; set; } = 1;
}
