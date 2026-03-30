using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class CapacityPlanningService : ICapacityPlanningService
{
    private readonly TenantDbContext _db;
    private readonly IStageService _stageService;
    private readonly IBuildAdvisorService _buildAdvisor;
    private readonly IMachineProgramService _programService;
    private readonly ISetupDispatchService _dispatchService;

    public CapacityPlanningService(
        TenantDbContext db,
        IStageService stageService,
        IBuildAdvisorService buildAdvisor,
        IMachineProgramService programService,
        ISetupDispatchService dispatchService)
    {
        _db = db;
        _stageService = stageService;
        _buildAdvisor = buildAdvisor;
        _programService = programService;
        _dispatchService = dispatchService;
    }

    public async Task<List<MachineCapacityCard>> GetMachineCapacityCardsAsync(DateTime from, DateTime to)
    {
        var machines = await _db.Machines
            .Include(m => m.CurrentProgram)
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .ToListAsync();

        var capacities = await _stageService.GetMachineCapacityAsync(from, to);
        var capacityMap = capacities.ToDictionary(c => c.MachineId);

        // Get pending execution counts and next due per machine
        var pendingExecutions = await _db.StageExecutions
            .Include(se => se.Job).ThenInclude(j => j!.Part)
            .Include(se => se.Job).ThenInclude(j => j!.WorkOrderLine).ThenInclude(wol => wol!.WorkOrder)
            .Where(se => se.Status == StageExecutionStatus.NotStarted
                && se.MachineId.HasValue)
            .ToListAsync();

        var cards = new List<MachineCapacityCard>();
        foreach (var machine in machines.OrderBy(m => m.Name))
        {
            capacityMap.TryGetValue(machine.Id, out var cap);
            var machineExecs = pendingExecutions.Where(se => se.MachineId == machine.Id).ToList();

            // Find earliest due part
            var nextDue = machineExecs
                .Where(se => se.Job?.WorkOrderLine?.WorkOrder?.DueDate != null)
                .OrderBy(se => se.Job!.WorkOrderLine!.WorkOrder!.DueDate)
                .FirstOrDefault();

            cards.Add(new MachineCapacityCard
            {
                MachineId = machine.Id,
                MachineName = machine.Name,
                MachineType = machine.MachineType,
                Department = machine.Department,
                Status = machine.Status,
                CurrentProgramName = machine.CurrentProgram?.Name,
                UtilizationPct = cap?.UtilizationPct ?? 0,
                LoadedHours = cap?.LoadedHours ?? 0,
                AvailableHours = cap?.AvailableHours ?? 0,
                QueueCount = machineExecs.Count,
                NextDuePart = nextDue?.Job?.Part?.PartNumber,
                NextDueDate = nextDue?.Job?.WorkOrderLine?.WorkOrder?.DueDate,
                IsAdditive = machine.IsAdditiveMachine,
                ToolSlotCount = machine.ToolSlotCount,
                LoadedToolCount = machine.CurrentProgramId.HasValue
                    ? _db.Set<ProgramToolingItem>().Count(t => t.MachineProgramId == machine.CurrentProgramId.Value && t.IsActive)
                    : 0
            });
        }

        return cards;
    }

    public async Task<List<DemandGapItem>> GetDemandGapsAsync()
    {
        var demand = await _buildAdvisor.GetAggregateDemandAsync();
        var gaps = new List<DemandGapItem>();

        foreach (var item in demand.Where(d => d.NetRemaining > 0))
        {
            // Count capable machines (machines with active programs for this part)
            var programs = await _programService.GetProgramsForPartAsync(item.PartId);
            var capableMachineIds = new HashSet<int>();
            foreach (var prog in programs.Where(p => p.Status == ProgramStatus.Active))
            {
                var assignments = await _programService.GetMachineAssignmentsAsync(prog.Id);
                foreach (var a in assignments) capableMachineIds.Add(a.MachineId);
                if (prog.MachineId.HasValue) capableMachineIds.Add(prog.MachineId.Value);
            }

            gaps.Add(new DemandGapItem
            {
                PartId = item.PartId,
                PartNumber = item.PartNumber,
                PartName = item.PartNumber,
                DemandQty = item.TotalOrdered,
                ScheduledQty = item.TotalProduced + item.InPrograms + item.InProduction,
                GapQty = item.NetRemaining,
                CapableMachineCount = capableMachineIds.Count,
                EarliestDueDate = item.EarliestDueDate,
                IsOverdue = item.IsOverdue,
                HighestPriority = item.HighestPriority
            });
        }

        return gaps.OrderByDescending(g => g.IsOverdue)
            .ThenBy(g => g.EarliestDueDate)
            .ThenByDescending(g => g.HighestPriority)
            .ToList();
    }

    public async Task<List<MachineAssignmentSuggestion>> SuggestAssignmentsAsync(List<int> partIds)
    {
        var suggestions = new List<MachineAssignmentSuggestion>();

        var machines = await _db.Machines
            .Include(m => m.CurrentProgram)
            .Where(m => m.IsActive && m.IsAvailableForScheduling && !m.IsAdditiveMachine)
            .ToListAsync();

        var demand = await _buildAdvisor.GetAggregateDemandAsync();

        foreach (var partId in partIds)
        {
            var partDemand = demand.FirstOrDefault(d => d.PartId == partId);
            if (partDemand == null || partDemand.NetRemaining <= 0) continue;

            var part = await _db.Parts.FindAsync(partId);
            if (part == null) continue;

            // Find all active programs for this part
            var programs = await _programService.GetActiveProgramsAsync(partId: partId);
            if (programs.Count == 0) continue;

            foreach (var program in programs)
            {
                // Find machines that can run this program
                var assignments = await _programService.GetMachineAssignmentsAsync(program.Id);
                var machineIds = assignments.Select(a => a.MachineId).ToList();
                if (program.MachineId.HasValue && !machineIds.Contains(program.MachineId.Value))
                    machineIds.Add(program.MachineId.Value);

                foreach (var machineId in machineIds)
                {
                    var machine = machines.FirstOrDefault(m => m.Id == machineId);
                    if (machine == null) continue;

                    var duration = await _programService.GetDurationFromProgramAsync(program.Id, partDemand.NetRemaining);
                    var setupMin = duration?.SetupMinutes ?? program.SetupTimeMinutes ?? 0;
                    var runMin = duration?.RunMinutes ?? ((program.RunTimeMinutes ?? 0) * partDemand.NetRemaining);

                    // Score: tool-aware changeover analysis
                    var toolChangeCount = 0;
                    var changeoverScore = 30; // default: different program
                    var reasons = new List<string>();

                    if (machine.CurrentProgramId == program.Id)
                    {
                        changeoverScore = 100;
                        reasons.Add("tools already loaded");
                    }
                    else if (machine.CurrentProgramId.HasValue)
                    {
                        // Compare tooling lists to find how many tools differ
                        var currentTools = await _db.Set<ProgramToolingItem>()
                            .Where(t => t.MachineProgramId == machine.CurrentProgramId.Value && t.IsActive)
                            .ToListAsync();
                        var targetTools = await _db.Set<ProgramToolingItem>()
                            .Where(t => t.MachineProgramId == program.Id && t.IsActive)
                            .ToListAsync();

                        if (currentTools.Count > 0 && targetTools.Count > 0)
                        {
                            // Count tools at same position with different names (actual changes needed)
                            var currentByPos = currentTools.ToDictionary(t => t.ToolPosition, t => t.Name);
                            foreach (var target in targetTools)
                            {
                                if (!currentByPos.TryGetValue(target.ToolPosition, out var currentName)
                                    || !currentName.Equals(target.Name, StringComparison.OrdinalIgnoreCase))
                                    toolChangeCount++;
                            }

                            if (toolChangeCount == 0) { changeoverScore = 90; reasons.Add("all tools match"); }
                            else if (toolChangeCount <= 2) { changeoverScore = 60; reasons.Add($"{toolChangeCount} tool change(s)"); }
                            else { changeoverScore = 30; reasons.Add($"{toolChangeCount} tool changes"); }
                        }
                        else
                        {
                            reasons.Add("changeover required");
                        }
                    }
                    else
                    {
                        reasons.Add("no program loaded");
                    }

                    var isPreferred = assignments.Any(a => a.MachineId == machineId && a.IsPreferred);
                    if (isPreferred) reasons.Add("preferred machine");

                    var score = (int)(changeoverScore * 0.35 + 50 * 0.45 + (isPreferred ? 80 : 40) * 0.20);

                    suggestions.Add(new MachineAssignmentSuggestion
                    {
                        PartId = partId,
                        PartNumber = part.PartNumber,
                        MachineId = machineId,
                        MachineName = machine.Name,
                        MachineProgramId = program.Id,
                        ProgramName = program.Name,
                        EstimatedSetupMinutes = setupMin,
                        EstimatedRunMinutes = runMin,
                        Quantity = partDemand.NetRemaining,
                        Score = score,
                        ScoreReason = string.Join(", ", reasons),
                        IsRecommended = false,
                        ToolChangeCount = toolChangeCount
                    });
                }
            }
        }

        // Mark the best suggestion per part as recommended
        foreach (var partId in partIds)
        {
            var best = suggestions
                .Where(s => s.PartId == partId)
                .OrderByDescending(s => s.Score)
                .FirstOrDefault();
            if (best != null) best.IsRecommended = true;
        }

        return suggestions.OrderByDescending(s => s.IsRecommended).ThenByDescending(s => s.Score).ToList();
    }

    public async Task<List<StageExecution>> ExecuteAssignmentsAsync(
        List<MachineAssignmentSuggestion> assignments, int userId)
    {
        var created = new List<StageExecution>();

        foreach (var assignment in assignments.Where(a => a.IsRecommended))
        {
            // Find or create a job for this part
            var existingJob = await _db.Jobs
                .Where(j => j.PartId == assignment.PartId
                    && j.Status != JobStatus.Completed
                    && j.Status != JobStatus.Cancelled)
                .FirstOrDefaultAsync();

            int jobId;
            if (existingJob != null)
            {
                jobId = existingJob.Id;
            }
            else
            {
                var part = await _db.Parts.FindAsync(assignment.PartId);
                var job = new Job
                {
                    PartId = assignment.PartId,
                    MachineId = assignment.MachineId,
                    Quantity = assignment.Quantity,
                    Status = JobStatus.Scheduled,
                    Priority = JobPriority.Normal,
                    JobNumber = $"JOB-CAP-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    CreatedBy = userId.ToString()
                };
                _db.Jobs.Add(job);
                await _db.SaveChangesAsync();
                jobId = job.Id;
            }

            // Create stage execution
            var execution = new StageExecution
            {
                JobId = jobId,
                MachineId = assignment.MachineId,
                MachineProgramId = assignment.MachineProgramId,
                Status = StageExecutionStatus.NotStarted,
                EstimatedHours = (assignment.EstimatedSetupMinutes + assignment.EstimatedRunMinutes) / 60.0,
                SetupHours = assignment.EstimatedSetupMinutes / 60.0,
                ScheduledStartAt = DateTime.UtcNow,
                CreatedBy = userId.ToString()
            };
            _db.StageExecutions.Add(execution);
            await _db.SaveChangesAsync();

            // Create setup dispatch
            await _dispatchService.CreateManualDispatchAsync(
                machineId: assignment.MachineId,
                type: DispatchType.Setup,
                machineProgramId: assignment.MachineProgramId,
                stageExecutionId: execution.Id,
                jobId: jobId,
                partId: assignment.PartId,
                requestedByUserId: userId,
                estimatedSetupMinutes: assignment.EstimatedSetupMinutes,
                notes: $"Auto-assigned from capacity planner: {assignment.PartNumber} x{assignment.Quantity}");

            created.Add(execution);
        }

        return created;
    }
}
