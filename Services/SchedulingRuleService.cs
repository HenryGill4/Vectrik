using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class SchedulingRuleService : ISchedulingRuleService
{
    private readonly TenantDbContext _db;

    public SchedulingRuleService(TenantDbContext db)
    {
        _db = db;
    }

    // ══════════════════════════════════════════════════════════
    // Machine Scheduling Rules
    // ══════════════════════════════════════════════════════════

    public async Task<List<MachineSchedulingRule>> GetRulesForMachineAsync(int machineId)
    {
        return await _db.MachineSchedulingRules
            .Where(r => r.MachineId == machineId)
            .OrderBy(r => r.RuleType)
            .ThenBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<List<MachineSchedulingRule>> GetEnabledRulesForMachineAsync(int machineId)
    {
        return await _db.MachineSchedulingRules
            .Where(r => r.MachineId == machineId && r.IsEnabled)
            .ToListAsync();
    }

    public async Task<MachineSchedulingRule?> GetRuleAsync(int ruleId)
    {
        return await _db.MachineSchedulingRules.FindAsync(ruleId);
    }

    public async Task<MachineSchedulingRule> CreateRuleAsync(MachineSchedulingRule rule)
    {
        rule.CreatedDate = DateTime.UtcNow;
        rule.LastModifiedDate = DateTime.UtcNow;
        _db.MachineSchedulingRules.Add(rule);
        await _db.SaveChangesAsync();
        return rule;
    }

    public async Task<MachineSchedulingRule> UpdateRuleAsync(MachineSchedulingRule rule)
    {
        var existing = await _db.MachineSchedulingRules.FindAsync(rule.Id)
            ?? throw new InvalidOperationException($"Rule {rule.Id} not found.");

        existing.Name = rule.Name;
        existing.Description = rule.Description;
        existing.IsEnabled = rule.IsEnabled;
        existing.RuleType = rule.RuleType;
        existing.MaxConsecutiveBuilds = rule.MaxConsecutiveBuilds;
        existing.MinBreakHours = rule.MinBreakHours;
        existing.LastModifiedDate = DateTime.UtcNow;
        existing.LastModifiedBy = rule.LastModifiedBy;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteRuleAsync(int ruleId)
    {
        var rule = await _db.MachineSchedulingRules.FindAsync(ruleId);
        if (rule != null)
        {
            _db.MachineSchedulingRules.Remove(rule);
            await _db.SaveChangesAsync();
        }
    }

    public async Task ToggleRuleAsync(int ruleId, bool isEnabled)
    {
        var rule = await _db.MachineSchedulingRules.FindAsync(ruleId)
            ?? throw new InvalidOperationException($"Rule {ruleId} not found.");
        rule.IsEnabled = isEnabled;
        rule.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ══════════════════════════════════════════════════════════
    // Blackout Periods
    // ══════════════════════════════════════════════════════════

    public async Task<List<BlackoutPeriod>> GetAllBlackoutPeriodsAsync()
    {
        return await _db.BlackoutPeriods
            .Include(b => b.MachineAssignments)
            .OrderBy(b => b.StartDate)
            .ToListAsync();
    }

    public async Task<List<BlackoutPeriod>> GetActiveBlackoutsInRangeAsync(DateTime start, DateTime end)
    {
        var periods = await _db.BlackoutPeriods
            .Where(b => b.IsActive)
            .ToListAsync();

        // Filter in-memory for recurring annual blackouts (month/day comparison)
        return periods
            .Where(b => DoesBlackoutOverlap(b, start, end))
            .ToList();
    }

    public async Task<List<BlackoutPeriod>> GetMachineBlackoutsAsync(int machineId)
    {
        return await _db.MachineBlackoutAssignments
            .Where(a => a.MachineId == machineId)
            .Select(a => a.BlackoutPeriod)
            .OrderBy(b => b.StartDate)
            .ToListAsync();
    }

    public async Task<BlackoutPeriod> CreateBlackoutPeriodAsync(BlackoutPeriod period)
    {
        period.CreatedDate = DateTime.UtcNow;
        _db.BlackoutPeriods.Add(period);
        await _db.SaveChangesAsync();
        return period;
    }

    public async Task<BlackoutPeriod> UpdateBlackoutPeriodAsync(BlackoutPeriod period)
    {
        var existing = await _db.BlackoutPeriods.FindAsync(period.Id)
            ?? throw new InvalidOperationException($"BlackoutPeriod {period.Id} not found.");

        existing.Name = period.Name;
        existing.StartDate = period.StartDate;
        existing.EndDate = period.EndDate;
        existing.Reason = period.Reason;
        existing.IsRecurringAnnually = period.IsRecurringAnnually;
        existing.IsActive = period.IsActive;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteBlackoutPeriodAsync(int periodId)
    {
        var period = await _db.BlackoutPeriods
            .Include(b => b.MachineAssignments)
            .FirstOrDefaultAsync(b => b.Id == periodId);

        if (period != null)
        {
            _db.MachineBlackoutAssignments.RemoveRange(period.MachineAssignments);
            _db.BlackoutPeriods.Remove(period);
            await _db.SaveChangesAsync();
        }
    }

    // ══════════════════════════════════════════════════════════
    // Machine ↔ Blackout Assignments
    // ══════════════════════════════════════════════════════════

    public async Task AssignBlackoutToMachineAsync(int machineId, int blackoutPeriodId)
    {
        var exists = await _db.MachineBlackoutAssignments
            .AnyAsync(a => a.MachineId == machineId && a.BlackoutPeriodId == blackoutPeriodId);

        if (!exists)
        {
            _db.MachineBlackoutAssignments.Add(new MachineBlackoutAssignment
            {
                MachineId = machineId,
                BlackoutPeriodId = blackoutPeriodId
            });
            await _db.SaveChangesAsync();
        }
    }

    public async Task UnassignBlackoutFromMachineAsync(int machineId, int blackoutPeriodId)
    {
        var assignment = await _db.MachineBlackoutAssignments
            .FirstOrDefaultAsync(a => a.MachineId == machineId && a.BlackoutPeriodId == blackoutPeriodId);

        if (assignment != null)
        {
            _db.MachineBlackoutAssignments.Remove(assignment);
            await _db.SaveChangesAsync();
        }
    }

    public async Task SetMachineBlackoutsAsync(int machineId, IEnumerable<int> blackoutPeriodIds)
    {
        var existing = await _db.MachineBlackoutAssignments
            .Where(a => a.MachineId == machineId)
            .ToListAsync();

        _db.MachineBlackoutAssignments.RemoveRange(existing);

        foreach (var bpId in blackoutPeriodIds)
        {
            _db.MachineBlackoutAssignments.Add(new MachineBlackoutAssignment
            {
                MachineId = machineId,
                BlackoutPeriodId = bpId
            });
        }

        await _db.SaveChangesAsync();
    }

    // ══════════════════════════════════════════════════════════
    // Rule Validation (called by scheduling engine)
    // ══════════════════════════════════════════════════════════

    public async Task<SchedulingRuleValidationResult> ValidateSlotAsync(
        int machineId,
        DateTime buildStart,
        DateTime buildEnd,
        DateTime changeoverStart,
        DateTime changeoverEnd,
        bool operatorAvailableForChangeover,
        int consecutiveBuildCount)
    {
        var rules = await GetEnabledRulesForMachineAsync(machineId);
        var violations = new List<SchedulingRuleViolation>();

        foreach (var rule in rules)
        {
            switch (rule.RuleType)
            {
                case SchedulingRuleType.RequireOperatorForChangeover:
                    if (!operatorAvailableForChangeover)
                    {
                        violations.Add(new SchedulingRuleViolation(
                            rule.RuleType,
                            rule.Name,
                            $"Changeover window ({changeoverStart:MMM dd HH:mm} – {changeoverEnd:MMM dd HH:mm}) " +
                            "falls outside operator shift hours. No operator available to empty the cooldown chamber."));
                    }
                    break;

                case SchedulingRuleType.MaxConsecutiveBuilds:
                    if (rule.MaxConsecutiveBuilds.HasValue &&
                        consecutiveBuildCount >= rule.MaxConsecutiveBuilds.Value)
                    {
                        violations.Add(new SchedulingRuleViolation(
                            rule.RuleType,
                            rule.Name,
                            $"Machine has {consecutiveBuildCount} consecutive builds — " +
                            $"exceeds limit of {rule.MaxConsecutiveBuilds}. " +
                            $"A {rule.MinBreakHours ?? 2}hr break is required."));
                    }
                    break;

                case SchedulingRuleType.BlackoutPeriod:
                    var machineBlackouts = await GetMachineBlackoutsAsync(machineId);
                    foreach (var blackout in machineBlackouts.Where(b => b.IsActive))
                    {
                        if (DoesBlackoutOverlap(blackout, buildStart, changeoverEnd))
                        {
                            violations.Add(new SchedulingRuleViolation(
                                rule.RuleType,
                                rule.Name,
                                $"Build window overlaps blackout \"{blackout.Name}\" " +
                                $"({blackout.StartDate:MMM dd} – {blackout.EndDate:MMM dd})."));
                        }
                    }
                    break;
            }
        }

        return new SchedulingRuleValidationResult(
            violations.Count == 0,
            violations);
    }

    public async Task<MachineRuleSummary> GetRuleSummaryAsync(int machineId)
    {
        var rules = await GetEnabledRulesForMachineAsync(machineId);

        var requiresOperator = rules.Any(r => r.RuleType == SchedulingRuleType.RequireOperatorForChangeover);
        var maxConsecRule = rules.FirstOrDefault(r => r.RuleType == SchedulingRuleType.MaxConsecutiveBuilds);
        var hasBlackoutRule = rules.Any(r => r.RuleType == SchedulingRuleType.BlackoutPeriod);

        var activeBlackoutCount = 0;
        if (hasBlackoutRule)
        {
            var now = DateTime.UtcNow;
            var blackouts = await GetMachineBlackoutsAsync(machineId);
            activeBlackoutCount = blackouts.Count(b => b.IsActive && b.EndDate >= now);
        }

        return new MachineRuleSummary(
            machineId,
            requiresOperator,
            maxConsecRule?.MaxConsecutiveBuilds,
            maxConsecRule?.MinBreakHours,
            activeBlackoutCount,
            rules.Count);
    }

    // ══════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Determines if a blackout period overlaps with [start, end].
    /// Handles recurring annual blackouts by comparing month+day.
    /// </summary>
    private static bool DoesBlackoutOverlap(BlackoutPeriod blackout, DateTime start, DateTime end)
    {
        if (blackout.IsRecurringAnnually)
        {
            // Check if the month/day range overlaps for the years spanned by [start, end]
            for (var year = start.Year; year <= end.Year; year++)
            {
                try
                {
                    var recurStart = new DateTime(year, blackout.StartDate.Month, blackout.StartDate.Day,
                        blackout.StartDate.Hour, blackout.StartDate.Minute, 0);
                    var recurEnd = new DateTime(year, blackout.EndDate.Month, blackout.EndDate.Day,
                        blackout.EndDate.Hour, blackout.EndDate.Minute, 0);

                    // Handle year-crossing recurring blackouts (e.g., Dec 24 - Jan 2)
                    if (recurEnd < recurStart)
                        recurEnd = recurEnd.AddYears(1);

                    if (recurStart < end && recurEnd > start)
                        return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Feb 29 in non-leap year — skip this occurrence
                    continue;
                }
            }
            return false;
        }

        // Standard non-recurring: simple range overlap
        return blackout.StartDate < end && blackout.EndDate > start;
    }
}
