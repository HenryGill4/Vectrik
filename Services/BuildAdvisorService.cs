using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class BuildAdvisorService : IBuildAdvisorService
{
    private readonly TenantDbContext _db;
    private readonly IProgramSchedulingService _programScheduling;
    private readonly IShiftManagementService _shiftService;

    public BuildAdvisorService(
        TenantDbContext db,
        IProgramSchedulingService programScheduling,
        IShiftManagementService shiftService)
    {
        _db = db;
        _programScheduling = programScheduling;
        _shiftService = shiftService;
    }

    // ══════════════════════════════════════════════════════════
    // Aggregate Demand
    // ══════════════════════════════════════════════════════════

    public async Task<List<DemandSummary>> GetAggregateDemandAsync()
    {
        var today = DateTime.UtcNow.Date;

        // Load active WO lines with part + build config
        var lines = await _db.WorkOrderLines
            .Include(l => l.Part).ThenInclude(p => p!.AdditiveBuildConfig)
            .Include(l => l.Part).ThenInclude(p => p!.ManufacturingApproach)
            .Include(l => l.WorkOrder)
            .Where(l => l.WorkOrder.Status == WorkOrderStatus.Released
                || l.WorkOrder.Status == WorkOrderStatus.InProgress)
            .Where(l => l.Quantity > l.ProducedQuantity)
            .ToListAsync();

        // Count parts already in active programs (not yet produced)
        // Load to memory first — SQLite GroupBy + navigation filter can miscompute sums
        var activeProgramParts = await _db.ProgramParts
            .Where(pp => pp.MachineProgram.Status == ProgramStatus.Active
                && pp.MachineProgram.ScheduleStatus != ProgramScheduleStatus.Completed
                && pp.MachineProgram.ScheduleStatus != ProgramScheduleStatus.Cancelled)
            .Select(pp => new { pp.PartId, pp.Quantity, pp.StackLevel })
            .ToListAsync();
        var inProgramsMap = activeProgramParts
            .GroupBy(pp => pp.PartId)
            .ToDictionary(g => g.Key, g => g.Sum(pp => pp.Quantity * pp.StackLevel));

        // Count parts in active jobs (not completed/cancelled)
        var activeJobParts = await _db.Jobs
            .Where(j => j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled
                && j.Scope == JobScope.Part)
            .Select(j => new { j.PartId, j.Quantity })
            .ToListAsync();
        var inProductionMap = activeJobParts
            .GroupBy(j => j.PartId)
            .ToDictionary(g => g.Key, g => g.Sum(j => j.Quantity));

        // Group lines by part
        var grouped = lines.GroupBy(l => l.PartId);

        var summaries = new List<DemandSummary>();
        foreach (var group in grouped)
        {
            var part = group.First().Part;
            if (part == null) continue;

            var totalOrdered = group.Sum(l => l.Quantity);
            var totalProduced = group.Sum(l => l.ProducedQuantity);
            var inPrograms = inProgramsMap.GetValueOrDefault(part.Id, 0);
            var inProduction = inProductionMap.GetValueOrDefault(part.Id, 0);
            var netRemaining = Math.Max(0, totalOrdered - totalProduced - inPrograms);

            var earliestDue = group.Min(l => l.WorkOrder.DueDate);
            var highestPriority = group.Any(l => l.WorkOrder.Priority == JobPriority.Emergency) ? JobPriority.Emergency
                : group.Any(l => l.WorkOrder.Priority == JobPriority.Rush) ? JobPriority.Rush
                : group.Any(l => l.WorkOrder.Priority == JobPriority.High) ? JobPriority.High
                : JobPriority.Normal;

            var sourceLines = group.Select(l => new DemandSourceLine(
                l.Id, l.WorkOrder.OrderNumber, l.WorkOrder.CustomerName,
                l.Quantity, l.ProducedQuantity, l.WorkOrder.DueDate)).ToList();

            summaries.Add(new DemandSummary(
                part.Id, part.PartNumber ?? $"Part #{part.Id}",
                totalOrdered, totalProduced, inPrograms, inProduction, netRemaining,
                earliestDue, highestPriority,
                IsOverdue: earliestDue.Date < today,
                IsAdditive: part.ManufacturingApproach?.IsAdditive == true,
                BuildConfig: part.AdditiveBuildConfig,
                SourceLines: sourceLines));
        }

        return summaries
            .OrderByDescending(d => d.IsOverdue)
            .ThenBy(d => d.EarliestDueDate)
            .ThenByDescending(d => d.HighestPriority)
            .ToList();
    }

    // ══════════════════════════════════════════════════════════
    // Plate Composition Optimizer
    // ══════════════════════════════════════════════════════════

    public async Task<PlateComposition> OptimizePlateAsync(
        int machineId, DateTime slotStart, List<DemandSummary> demand,
        int maxPartTypes = 4, int? forcePrimaryPartId = null)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        // Read per-tenant max part types setting (overrides parameter default)
        var maxTypesSetting = await _db.SystemSettings
            .Where(s => s.Key == "scheduler.max_part_types_per_plate")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
        if (int.TryParse(maxTypesSetting, out var configuredMax))
            maxPartTypes = configuredMax;

        var shifts = await _shiftService.GetEffectiveShiftsForMachineAsync(machineId);
        var changeoverMinutes = machine.AutoChangeoverEnabled ? machine.ChangeoverMinutes : 0;

        // Filter to additive parts with build config and remaining demand
        var candidates = demand
            .Where(d => d.IsAdditive && d.BuildConfig != null && d.NetRemaining > 0)
            .ToList();

        // If a specific part is forced as primary, move it to the front
        if (forcePrimaryPartId.HasValue)
        {
            var forced = candidates.FirstOrDefault(c => c.PartId == forcePrimaryPartId.Value);
            if (forced != null)
            {
                candidates.Remove(forced);
                candidates.Insert(0, forced);
            }
        }

        if (!candidates.Any())
        {
            return new PlateComposition([], 1, 0, true, slotStart, true);
        }

        var allocations = new List<PlatePartAllocation>();
        var maxPrintHours = 0.0;

        // Pick primary part (highest urgency)
        var primary = candidates.First();
        var primaryConfig = primary.BuildConfig!;

        // Select stack level based on changeover alignment
        var bestLevel = SelectStackLevel(primaryConfig, slotStart, changeoverMinutes, shifts, primary.NetRemaining);
        var primaryDuration = primaryConfig.GetStackDuration(bestLevel) ?? primaryConfig.SingleStackDurationHours ?? 24;
        var primaryPositions = primaryConfig.GetPositionsPerBuild(bestLevel);

        // Cap positions to demand (but don't go below 1)
        var primaryNeeded = (int)Math.Ceiling((double)primary.NetRemaining / bestLevel);
        var actualPositions = Math.Min(primaryPositions, Math.Max(1, primaryNeeded));
        var surplus = (actualPositions * bestLevel) - primary.NetRemaining;

        allocations.Add(new PlatePartAllocation(
            primary.PartId, primary.PartNumber,
            actualPositions, bestLevel,
            actualPositions * bestLevel,
            primary.NetRemaining,
            Math.Max(0, surplus),
            primary.SourceLines.FirstOrDefault()?.WorkOrderLineId,
            primary.EarliestDueDate));

        maxPrintHours = primaryDuration;

        // Fill remaining plate capacity with other parts (up to maxPartTypes total)
        // Use fraction-based capacity: each part's fraction = positions / its full-plate max
        var usedFraction = primaryConfig.GetPlateFraction(actualPositions, bestLevel);
        if (usedFraction < 1.0 && candidates.Count > 1)
        {
            foreach (var fill in candidates.Skip(1).Take(maxPartTypes - 1))
            {
                if (usedFraction >= 1.0) break;

                var fillConfig = fill.BuildConfig;
                if (fillConfig == null) continue;

                var fillLevel = bestLevel; // Use same stack level for consistency
                var fillFullMax = fillConfig.GetPositionsPerBuild(fillLevel);
                if (fillFullMax <= 0) continue;

                // Scale fill positions by remaining plate fraction
                var remainingFraction = 1.0 - usedFraction;
                var capacityLimit = Math.Max(1, (int)Math.Floor(remainingFraction * fillFullMax));
                var fillNeeded = (int)Math.Ceiling((double)fill.NetRemaining / fillLevel);
                var fillPositions = Math.Min(capacityLimit, Math.Max(1, fillNeeded));

                var fillDuration = fillConfig.GetStackDuration(fillLevel) ?? fillConfig.SingleStackDurationHours ?? 24;
                maxPrintHours = Math.Max(maxPrintHours, fillDuration);

                var fillSurplus = (fillPositions * fillLevel) - fill.NetRemaining;

                allocations.Add(new PlatePartAllocation(
                    fill.PartId, fill.PartNumber,
                    fillPositions, fillLevel,
                    fillPositions * fillLevel,
                    fill.NetRemaining,
                    Math.Max(0, fillSurplus),
                    fill.SourceLines.FirstOrDefault()?.WorkOrderLineId,
                    fill.EarliestDueDate));

                usedFraction += fillConfig.GetPlateFraction(fillPositions, fillLevel);
            }
        }

        // Calculate changeover timing
        var buildEnd = slotStart.AddHours(maxPrintHours);
        var changeoverTime = buildEnd;
        var changeoverEnd = buildEnd.AddMinutes(changeoverMinutes);
        var operatorAvailable = changeoverMinutes > 0
            ? ShiftTimeHelper.IsWithinShiftWindow(changeoverTime, changeoverEnd, shifts)
            : true;

        return new PlateComposition(
            allocations, bestLevel, maxPrintHours,
            operatorAvailable, changeoverTime, operatorAvailable);
    }

    // ══════════════════════════════════════════════════════════
    // Next Build Recommendation
    // ══════════════════════════════════════════════════════════

    public async Task<BuildRecommendation> RecommendNextBuildAsync(int machineId, DateTime? startAfter = null)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var notBefore = startAfter ?? DateTime.UtcNow;

        // Find the next available slot
        var slot = await _programScheduling.FindEarliestSlotAsync(
            machineId, 1, notBefore); // 1h placeholder — real duration comes from plate composition

        // Get all demand
        var demand = await GetAggregateDemandAsync();

        // Optimize plate composition for this slot
        var plate = await OptimizePlateAsync(machineId, slot.PrintStart, demand);

        // Re-find slot with actual duration
        if (plate.EstimatedPrintHours > 0)
        {
            slot = await _programScheduling.FindEarliestSlotAsync(
                machineId, plate.EstimatedPrintHours, notBefore);
        }

        // Build rationale
        var warnings = new List<string>();
        var rationale = BuildRationale(plate, slot, machine, warnings);

        return new BuildRecommendation(
            machineId, machine.Name, slot, plate, rationale, warnings);
    }

    // ══════════════════════════════════════════════════════════
    // Bottleneck Detection
    // ══════════════════════════════════════════════════════════

    public async Task<BottleneckReport> AnalyzeBottlenecksAsync(DateTime horizonStart, DateTime horizonEnd)
    {
        var machines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .ToListAsync();

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

        // Calculate scheduled hours per machine
        var executions = await _db.StageExecutions
            .Include(e => e.ProductionStage)
            .Include(e => e.Machine)
            .Where(e => e.MachineId != null
                && e.ScheduledStartAt != null && e.ScheduledEndAt != null
                && e.ScheduledStartAt >= horizonStart && e.ScheduledEndAt <= horizonEnd
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Failed)
            .ToListAsync();

        var horizonDays = Math.Max(1, (horizonEnd - horizonStart).TotalDays);
        var deptUtil = new Dictionary<string, double>();
        var items = new List<BottleneckItem>();
        var recommendations = new List<string>();

        // Group by department
        var byDept = machines.GroupBy(m => m.Department ?? m.MachineType ?? "Other");
        foreach (var dept in byDept)
        {
            var deptName = dept.Key;
            var deptMachines = dept.ToList();
            var machineIds = deptMachines.Select(m => m.Id).ToHashSet();

            // Hours of work scheduled on this department
            var scheduledHours = executions
                .Where(e => machineIds.Contains(e.MachineId!.Value))
                .Sum(e => e.EstimatedHours ?? (e.ScheduledEndAt!.Value - e.ScheduledStartAt!.Value).TotalHours);

            // Capacity: machines * shift hours per day * days
            var shiftHoursPerDay = shifts.Any()
                ? shifts.Sum(s =>
                {
                    var duration = s.EndTime - s.StartTime;
                    if (duration < TimeSpan.Zero) duration += TimeSpan.FromHours(24);
                    return duration.TotalHours;
                })
                : 24.0;
            var capacityHours = deptMachines.Count * shiftHoursPerDay * horizonDays;

            var utilization = capacityHours > 0 ? scheduledHours / capacityHours * 100 : 0;
            deptUtil[deptName] = utilization;

            var severity = utilization > 90 ? "critical" : utilization > 75 ? "warning" : "ok";

            if (severity != "ok")
            {
                items.Add(new BottleneckItem(
                    deptName, null, utilization, scheduledHours, capacityHours, severity));

                if (severity == "critical")
                    recommendations.Add($"{deptName}: At {utilization:F0}% capacity. Consider adding machines or extending shifts.");
                else
                    recommendations.Add($"{deptName}: At {utilization:F0}% capacity. Monitor for delays.");
            }
        }

        // SLS → CNC throughput check
        var slsPartsScheduled = executions
            .Where(e => e.Machine?.IsAdditiveMachine == true)
            .Sum(e => e.BatchPartCount ?? 1);
        var cncDeptMachines = machines.Count(m => !m.IsAdditiveMachine
            && (m.MachineType?.Contains("CNC", StringComparison.OrdinalIgnoreCase) == true
                || m.MachineType?.Contains("Lathe", StringComparison.OrdinalIgnoreCase) == true));
        if (slsPartsScheduled > 0 && cncDeptMachines > 0)
        {
            var slsPartsPerDay = slsPartsScheduled / horizonDays;
            var cncCapacityPerDay = cncDeptMachines * 8; // rough: 8 parts/day per CNC
            if (slsPartsPerDay > cncCapacityPerDay * 1.1)
            {
                recommendations.Add($"CNC throughput may not keep up with SLS output ({slsPartsPerDay:F0} parts/day from SLS vs ~{cncCapacityPerDay:F0}/day CNC capacity).");
            }
        }

        return new BottleneckReport(items, deptUtil, recommendations);
    }

    // ══════════════════════════════════════════════════════════
    // Machine Availability Summary (Dispatch View)
    // ══════════════════════════════════════════════════════════

    public async Task<List<MachineAvailabilitySummary>> GetMachineAvailabilitySummaryAsync()
    {
        var machines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling && m.IsAdditiveMachine)
            .OrderBy(m => m.Priority).ThenBy(m => m.Name)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var summaries = new List<MachineAvailabilitySummary>();

        foreach (var machine in machines)
        {
            // Find currently printing build (if any)
            var currentBuild = await _db.MachinePrograms
                .Where(p => p.MachineId == machine.Id
                    && p.ProgramType == ProgramType.BuildPlate
                    && p.ScheduleStatus == ProgramScheduleStatus.Printing)
                .Select(p => new { p.Name, p.ScheduledDate, p.EstimatedPrintHours })
                .FirstOrDefaultAsync();

            string? currentBuildName = currentBuild?.Name;
            DateTime? currentBuildEnd = currentBuild?.ScheduledDate != null && currentBuild.EstimatedPrintHours.HasValue
                ? currentBuild.ScheduledDate.Value.AddHours(currentBuild.EstimatedPrintHours.Value)
                : null;

            // Count queued builds (Scheduled but not yet printing)
            var queuedCount = await _db.MachinePrograms
                .CountAsync(p => p.MachineId == machine.Id
                    && p.ProgramType == ProgramType.BuildPlate
                    && p.ScheduleStatus == ProgramScheduleStatus.Scheduled);

            // Find next available slot
            DateTime nextSlotStart;
            try
            {
                var slot = await _programScheduling.FindEarliestSlotAsync(machine.Id, 1, now);
                nextSlotStart = slot.PrintStart;
            }
            catch
            {
                nextSlotStart = now; // Fallback if slot finding fails
            }

            summaries.Add(new MachineAvailabilitySummary(
                machine.Id, machine.Name, machine.Status,
                nextSlotStart, currentBuildName, currentBuildEnd, queuedCount));
        }

        return summaries;
    }

    // ══════════════════════════════════════════════════════════
    // Completion Estimation
    // ══════════════════════════════════════════════════════════

    public async Task<DateTime?> EstimateCompletionDateAsync(int partId, int quantity)
    {
        if (quantity <= 0) return null;

        var buildConfig = await _db.PartAdditiveBuildConfigs
            .FirstOrDefaultAsync(c => c.PartId == partId);
        if (buildConfig == null) return null;

        var partsPerBuild = buildConfig.PlannedPartsPerBuildSingle > 0
            ? buildConfig.PlannedPartsPerBuildSingle : 1;
        var buildsNeeded = (int)Math.Ceiling((double)quantity / partsPerBuild);

        var hoursPerBuild = buildConfig.SingleStackDurationHours ?? 24.0;
        var totalPrintHours = buildsNeeded * hoursPerBuild;

        // Estimate downstream processing (depowder + EDM + CNC + finishing)
        // Use process stage estimates if available, otherwise default 48h
        var downstreamHours = 48.0;
        var part = await _db.Parts
            .Include(p => p.ManufacturingProcess)
                .ThenInclude(mp => mp!.Stages)
                    .ThenInclude(ps => ps.ProductionStage)
            .FirstOrDefaultAsync(p => p.Id == partId);

        if (part?.ManufacturingProcess?.Stages?.Any() == true)
        {
            // Sum estimated run time for non-print stages
            var nonPrintStages = part.ManufacturingProcess.Stages
                .Where(ps => ps.ProductionStage?.Department != "SLS"
                    && ps.ProductionStage?.Department != "Additive"
                    && !ps.DurationFromBuildConfig)
                .ToList();

            var stageMinutes = nonPrintStages.Sum(ps =>
                (ps.SetupTimeMinutes ?? 0) + (ps.RunTimeMinutes ?? 0));
            if (stageMinutes > 0)
                downstreamHours = (stageMinutes / 60.0) * quantity;
        }

        return DateTime.UtcNow.AddHours(totalPrintHours + downstreamHours);
    }

    // ══════════════════════════════════════════════════════════
    // Private Helpers
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Selects the best stack level for a part based on changeover alignment with operator shifts.
    /// </summary>
    private static int SelectStackLevel(
        PartAdditiveBuildConfig config, DateTime slotStart,
        double changeoverMinutes, List<OperatingShift> shifts, int netRemaining)
    {
        var levels = config.AvailableStackLevels;
        if (levels.Count <= 1) return 1;

        var bestLevel = 1;
        var bestScore = -1;

        foreach (var level in levels)
        {
            var duration = config.GetStackDuration(level);
            var partsPerBuild = config.GetPartsPerBuild(level);
            if (!duration.HasValue || !partsPerBuild.HasValue) continue;

            var score = 0;
            var buildEnd = slotStart.AddHours(duration.Value);
            var changeoverEnd = buildEnd.AddMinutes(changeoverMinutes);

            // Changeover alignment: +30 if operator is available (matches ProgramSchedulingService scoring)
            if (changeoverMinutes <= 0 || ShiftTimeHelper.IsWithinShiftWindow(buildEnd, changeoverEnd, shifts))
                score += 30;

            // Demand fit: +30 if parts per build <= remaining (no overproduction)
            if (partsPerBuild.Value <= netRemaining)
                score += 30;
            else
            {
                // Penalize overproduction proportionally
                var overPct = (double)(partsPerBuild.Value - netRemaining) / partsPerBuild.Value;
                score += (int)(30 * (1 - overPct));
            }

            // Efficiency: +20 for higher parts-per-hour
            var pph = partsPerBuild.Value / duration.Value;
            score += (int)(pph * 5); // Rough scaling

            if (score > bestScore)
            {
                bestScore = score;
                bestLevel = level;
            }
        }

        return bestLevel;
    }

    private static string BuildRationale(PlateComposition plate, ProgramScheduleSlot slot, Machine machine, List<string> warnings)
    {
        var parts = plate.Parts;
        if (!parts.Any()) return "No demand found.";

        var primary = parts[0];
        var rationale = $"Print {primary.TotalParts}x {primary.PartNumber}";
        if (parts.Count > 1)
            rationale += $" + {parts.Skip(1).Sum(p => p.TotalParts)} mixed parts ({parts.Count} types)";

        rationale += $" — {plate.EstimatedPrintHours:F1}h, {(plate.RecommendedStackLevel > 1 ? $"{plate.RecommendedStackLevel}x stack" : "single stack")}";

        if (!plate.OperatorAvailable && machine.AutoChangeoverEnabled)
        {
            warnings.Add($"Changeover at {plate.ChangeoverTime:MMM d HH:mm} — no operator available. Machine may be down until next shift.");
        }

        foreach (var part in parts.Where(p => p.Surplus > 0))
        {
            warnings.Add($"{part.PartNumber}: {part.Surplus} surplus parts beyond current demand (will go to stock).");
        }

        return rationale;
    }
}
