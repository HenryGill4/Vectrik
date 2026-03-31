using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vectrik.Data;
using Vectrik.Hubs;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services.Platform;

namespace Vectrik.Services;

public class PlateLayoutDispatchService : IPlateLayoutDispatchService
{
    private readonly TenantDbContext _db;
    private readonly ISetupDispatchService _dispatchService;
    private readonly IBuildAdvisorService _buildAdvisor;
    private readonly IDispatchNotifier _notifier;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PlateLayoutDispatchService> _logger;

    public PlateLayoutDispatchService(
        TenantDbContext db,
        ISetupDispatchService dispatchService,
        IBuildAdvisorService buildAdvisor,
        IDispatchNotifier notifier,
        ITenantContext tenantContext,
        ILogger<PlateLayoutDispatchService> logger)
    {
        _db = db;
        _dispatchService = dispatchService;
        _buildAdvisor = buildAdvisor;
        _notifier = notifier;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<SetupDispatch>> DetectAndCreatePlateLayoutDispatchesAsync()
    {
        var created = new List<SetupDispatch>();
        var demand = await _buildAdvisor.GetAggregateDemandAsync();

        // Filter to additive parts with unmet demand
        var unmetDemand = demand.Where(d => d.IsAdditive && d.NetRemaining > 0).ToList();
        if (unmetDemand.Count == 0) return created;

        // Check for existing active PlateLayout dispatches to avoid duplicates
        var existingLayouts = await _dispatchService.GetActiveDispatchesByTypeAsync(DispatchType.PlateLayout);

        // Check if Ready/Scheduled programs already cover remaining demand
        var readyPrograms = await _db.MachinePrograms
            .Include(mp => mp.ProgramParts)
            .Where(mp => mp.ProgramType == ProgramType.BuildPlate
                && mp.ScheduleStatus != ProgramScheduleStatus.Completed
                && mp.ScheduleStatus != ProgramScheduleStatus.Cancelled)
            .ToListAsync();

        // Get SLS engineer role for targeting
        var engineerRole = await _db.OperatorRoles
            .FirstOrDefaultAsync(r => r.IsActive && (r.Slug == "sls-engineer" || r.Name.Contains("SLS")));

        // Get SLS machines for dispatch creation
        var slsMachines = await _db.Machines
            .Where(m => m.IsActive && m.IsAdditiveMachine)
            .ToListAsync();

        if (slsMachines.Count == 0) return created;
        var primaryMachine = slsMachines.First();

        foreach (var demandItem in unmetDemand)
        {
            // Skip if there's already an active PlateLayout dispatch mentioning this part
            var alreadyDispatched = existingLayouts.Any(d =>
                d.PartId == demandItem.PartId
                || (d.DemandSummaryJson?.Contains(demandItem.PartNumber) == true));
            if (alreadyDispatched) continue;

            // Skip if existing programs already cover this demand
            var coveredByPrograms = readyPrograms
                .SelectMany(p => p.ProgramParts)
                .Where(pp => pp.PartId == demandItem.PartId)
                .Sum(pp => pp.Quantity * (pp.StackLevel > 0 ? pp.StackLevel : 1));
            if (coveredByPrograms >= demandItem.NetRemaining) continue;

            // Calculate priority based on urgency
            var priority = CalculatePlateLayoutPriority(demandItem);

            var demandJson = JsonSerializer.Serialize(new
            {
                demandItem.PartId,
                demandItem.PartNumber,
                demandItem.NetRemaining,
                demandItem.EarliestDueDate,
                demandItem.IsOverdue,
                priority = demandItem.HighestPriority.ToString(),
                coveredByPrograms
            });

            var dispatch = await _dispatchService.CreateManualDispatchAsync(
                machineId: primaryMachine.Id,
                type: DispatchType.PlateLayout,
                partId: demandItem.PartId,
                notes: $"Plate layout needed: {demandItem.PartNumber} x{demandItem.NetRemaining - coveredByPrograms} remaining. Due: {demandItem.EarliestDueDate:d}");

            // Set role targeting and demand summary
            var entity = await _db.SetupDispatches.FindAsync(dispatch.Id);
            if (entity != null)
            {
                entity.TargetRoleId = engineerRole?.Id;
                entity.DemandSummaryJson = demandJson;
                await _db.SaveChangesAsync();
            }

            await _dispatchService.UpdateDispatchPriorityAsync(dispatch.Id, priority,
                $"PlateLayout: {demandItem.PartNumber} — {demandItem.NetRemaining} parts needed, due {demandItem.EarliestDueDate:d}");

            created.Add(dispatch);
        }

        return created;
    }

    public async Task<bool> TryAutoCompleteForProgramAsync(int machineProgramId)
    {
        var program = await _db.MachinePrograms
            .Include(mp => mp.ProgramParts)
            .FirstOrDefaultAsync(mp => mp.Id == machineProgramId);

        if (program == null || program.ScheduleStatus != ProgramScheduleStatus.Ready)
            return false;

        var partIds = program.ProgramParts.Select(pp => pp.PartId).Distinct().ToList();
        if (partIds.Count == 0) return false;

        // Find active PlateLayout dispatches for these parts
        var activeLayouts = await _dispatchService.GetActiveDispatchesByTypeAsync(DispatchType.PlateLayout);
        var matchingLayouts = activeLayouts
            .Where(d => d.PartId.HasValue && partIds.Contains(d.PartId.Value))
            .ToList();

        // Cache demand once instead of per-dispatch
        var demand = await _buildAdvisor.GetAggregateDemandAsync();

        var anyCompleted = false;
        foreach (var layout in matchingLayouts)
        {
            var partDemand = demand.FirstOrDefault(d => d.PartId == layout.PartId);
            if (partDemand == null || partDemand.NetRemaining <= 0)
            {
                // Start and complete the dispatch
                try
                {
                    var started = await _dispatchService.StartDispatchAsync(layout.Id);
                    await _dispatchService.CompleteDispatchAsync(started.Id);
                    anyCompleted = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Auto-completing layout dispatch {DispatchId} failed; may already be in terminal state", layout.Id);
                }
            }
        }

        return anyCompleted;
    }

    public async Task<List<SetupDispatch>> GetEngineerDispatchesAsync(int? roleId = null)
    {
        if (roleId.HasValue)
            return await _dispatchService.GetDispatchesByRoleAsync(roleId.Value);

        return await _dispatchService.GetActiveDispatchesByTypeAsync(DispatchType.PlateLayout);
    }

    private static int CalculatePlateLayoutPriority(DemandSummary demand)
    {
        if (demand.IsOverdue) return 95;

        var hoursUntilDue = (demand.EarliestDueDate - DateTime.UtcNow).TotalHours;
        var basePriority = hoursUntilDue switch
        {
            < 0 => 95,    // Overdue
            < 24 => 85,   // Due within a day
            < 48 => 70,   // Due within 2 days
            < 120 => 55,  // Due within 5 days
            _ => 40        // More than 5 days out
        };

        // Boost for higher WO priority
        var priorityMultiplier = demand.HighestPriority switch
        {
            JobPriority.Emergency => 1.3,
            JobPriority.Rush => 1.15,
            JobPriority.High => 1.05,
            _ => 1.0
        };

        return (int)Math.Clamp(basePriority * priorityMultiplier, 1, 100);
    }
}
