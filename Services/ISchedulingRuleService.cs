using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

/// <summary>
/// Manages scheduling constraint rules per machine and shared blackout periods.
/// Rules are enforced as hard blocks in ProgramSchedulingService.FindEarliestSlotAsync.
/// </summary>
public interface ISchedulingRuleService
{
    // ── Machine Scheduling Rules ─────────────────────────────

    /// <summary>Get all scheduling rules for a machine (enabled and disabled).</summary>
    Task<List<MachineSchedulingRule>> GetRulesForMachineAsync(int machineId);

    /// <summary>Get only enabled rules for a machine (used by scheduling engine).</summary>
    Task<List<MachineSchedulingRule>> GetEnabledRulesForMachineAsync(int machineId);

    /// <summary>Get a single rule by ID.</summary>
    Task<MachineSchedulingRule?> GetRuleAsync(int ruleId);

    /// <summary>Create a new scheduling rule on a machine.</summary>
    Task<MachineSchedulingRule> CreateRuleAsync(MachineSchedulingRule rule);

    /// <summary>Update an existing scheduling rule.</summary>
    Task<MachineSchedulingRule> UpdateRuleAsync(MachineSchedulingRule rule);

    /// <summary>Delete a scheduling rule.</summary>
    Task DeleteRuleAsync(int ruleId);

    /// <summary>Toggle a rule's enabled state.</summary>
    Task ToggleRuleAsync(int ruleId, bool isEnabled);

    // ── Blackout Periods (shared company-wide) ───────────────

    /// <summary>Get all blackout periods.</summary>
    Task<List<BlackoutPeriod>> GetAllBlackoutPeriodsAsync();

    /// <summary>Get active blackout periods that overlap a given date range.</summary>
    Task<List<BlackoutPeriod>> GetActiveBlackoutsInRangeAsync(DateTime start, DateTime end);

    /// <summary>Get blackout periods assigned to a specific machine.</summary>
    Task<List<BlackoutPeriod>> GetMachineBlackoutsAsync(int machineId);

    /// <summary>Create a new shared blackout period.</summary>
    Task<BlackoutPeriod> CreateBlackoutPeriodAsync(BlackoutPeriod period);

    /// <summary>Update a blackout period.</summary>
    Task<BlackoutPeriod> UpdateBlackoutPeriodAsync(BlackoutPeriod period);

    /// <summary>Delete a blackout period (and all machine assignments).</summary>
    Task DeleteBlackoutPeriodAsync(int periodId);

    // ── Machine ↔ Blackout Assignments ───────────────────────

    /// <summary>Assign a blackout period to a machine.</summary>
    Task AssignBlackoutToMachineAsync(int machineId, int blackoutPeriodId);

    /// <summary>Remove a blackout assignment from a machine.</summary>
    Task UnassignBlackoutFromMachineAsync(int machineId, int blackoutPeriodId);

    /// <summary>Set the full list of blackout assignments for a machine (replaces existing).</summary>
    Task SetMachineBlackoutsAsync(int machineId, IEnumerable<int> blackoutPeriodIds);

    // ── Rule Validation (used by scheduling engine) ──────────

    /// <summary>
    /// Validate a proposed schedule slot against all enabled rules for a machine.
    /// Returns a list of blocking reasons. Empty list = slot is valid.
    /// </summary>
    Task<SchedulingRuleValidationResult> ValidateSlotAsync(
        int machineId,
        DateTime buildStart,
        DateTime buildEnd,
        DateTime changeoverStart,
        DateTime changeoverEnd,
        bool operatorAvailableForChangeover,
        int consecutiveBuildCount);

    /// <summary>
    /// Get a summary of all active rules for a machine (for UI display).
    /// </summary>
    Task<MachineRuleSummary> GetRuleSummaryAsync(int machineId);
}

/// <summary>
/// Result of validating a slot against scheduling rules.
/// </summary>
public record SchedulingRuleValidationResult(
    bool IsValid,
    List<SchedulingRuleViolation> Violations);

/// <summary>
/// A single rule violation describing why a slot was blocked.
/// </summary>
public record SchedulingRuleViolation(
    SchedulingRuleType RuleType,
    string RuleName,
    string Reason);

/// <summary>
/// Summary of active scheduling rules for a machine (used in UI badges/indicators).
/// </summary>
public record MachineRuleSummary(
    int MachineId,
    bool RequiresOperatorForChangeover,
    int? MaxConsecutiveBuilds,
    double? MinBreakHours,
    int ActiveBlackoutCount,
    int TotalEnabledRules);
