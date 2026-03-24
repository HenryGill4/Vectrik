using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

/// <summary>
/// Analyzes outstanding work order demand and generates build suggestions
/// by matching against certified templates and detecting mixed-build opportunities.
/// </summary>
public class BuildSuggestionService : IBuildSuggestionService
{
    private readonly TenantDbContext _db;
    private readonly IBuildTemplateService _templateService;

    public BuildSuggestionService(TenantDbContext db, IBuildTemplateService templateService)
    {
        _db = db;
        _templateService = templateService;
    }

    public async Task<BuildSuggestionResult> GetSuggestionsAsync()
    {
        var demand = await GetOutstandingDemandAsync();

        if (demand.Count == 0)
            return new BuildSuggestionResult([], []);

        var certifiedTemplates = await _templateService.GetAllAsync(BuildTemplateStatus.Certified);
        var templateSuggestions = GenerateTemplateSuggestions(demand, certifiedTemplates);
        var mixedSuggestions = GenerateMixedBuildSuggestions(demand, certifiedTemplates, templateSuggestions);

        return new BuildSuggestionResult(templateSuggestions, mixedSuggestions);
    }

    // ── Outstanding Demand ────────────────────────────────────

    private async Task<List<DemandLine>> GetOutstandingDemandAsync()
    {
        var workOrders = await _db.WorkOrders
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p!.ManufacturingApproach)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p!.MaterialEntity)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p!.AdditiveBuildConfig)
            .Include(w => w.Lines)
                .ThenInclude(l => l.ProgramParts)
                    .ThenInclude(pp => pp.MachineProgram)
            .Where(w => w.Status == WorkOrderStatus.Released || w.Status == WorkOrderStatus.InProgress)
            .ToListAsync();

        var result = new List<DemandLine>();

        foreach (var wo in workOrders)
        {
            foreach (var line in wo.Lines)
            {
                if (line.Part?.ManufacturingApproach?.RequiresBuildPlate != true)
                    continue;

                var inBuilds = GetInBuildQty(line);
                var outstanding = Math.Max(0, line.Quantity - line.ProducedQuantity - inBuilds);
                if (outstanding <= 0)
                    continue;

                result.Add(new DemandLine(
                    line.Id,
                    wo.Id,
                    wo.OrderNumber,
                    wo.DueDate,
                    wo.Priority,
                    line.PartId,
                    line.Part.PartNumber,
                    line.Part.MaterialId,
                    line.Part.MaterialEntity?.Name ?? line.Part.Material,
                    outstanding,
                    line.Part.AdditiveBuildConfig?.PlannedPartsPerBuildSingle ?? 1));
            }
        }

        return result;
    }

    private static int GetInBuildQty(WorkOrderLine line)
    {
        if (line.ProgramParts == null) return 0;
        return line.ProgramParts
            .Where(pp => pp.MachineProgram != null
                && pp.MachineProgram.ScheduleStatus != ProgramScheduleStatus.Cancelled
                && pp.MachineProgram.ScheduleStatus != ProgramScheduleStatus.Completed)
            .Sum(pp => pp.Quantity * pp.StackLevel);
    }

    // ── Template Suggestions (Single-Part) ────────────────────

    private static List<TemplateSuggestion> GenerateTemplateSuggestions(
        List<DemandLine> demand,
        List<BuildTemplate> certifiedTemplates)
    {
        var suggestions = new List<TemplateSuggestion>();

        // Group demand by part to avoid duplicate suggestions for the same part across WOs
        var demandByPart = demand
            .GroupBy(d => d.PartId)
            .Select(g => new
            {
                PartId = g.Key,
                PartNumber = g.First().PartNumber,
                TotalOutstanding = g.Sum(d => d.Outstanding),
                Lines = g.OrderBy(d => d.DueDate).ToList()
            })
            .ToList();

        foreach (var partDemand in demandByPart)
        {
            var matchingTemplates = certifiedTemplates
                .Where(t => t.IsCertified && t.Parts.Any(p => p.PartId == partDemand.PartId))
                .ToList();

            foreach (var template in matchingTemplates)
            {
                var templatePartQty = template.Parts
                    .Where(p => p.PartId == partDemand.PartId)
                    .Sum(p => p.Quantity);

                if (templatePartQty <= 0) continue;

                var runsNeeded = (int)Math.Ceiling((double)partDemand.TotalOutstanding / templatePartQty);
                var suggestedQty = runsNeeded * templatePartQty;

                var woRefs = new List<WorkOrderReference>();
                var remaining = partDemand.TotalOutstanding;
                foreach (var line in partDemand.Lines)
                {
                    var fulfilled = Math.Min(remaining, line.Outstanding);
                    woRefs.Add(new WorkOrderReference(
                        line.WorkOrderId,
                        line.OrderNumber,
                        line.WorkOrderLineId,
                        fulfilled,
                        line.DueDate));
                    remaining -= fulfilled;
                    if (remaining <= 0) break;
                }

                var rationale = runsNeeded == 1
                    ? $"1 build of {templatePartQty}pc fulfills {partDemand.TotalOutstanding}pc demand"
                    : $"{runsNeeded} builds of {templatePartQty}pc each to fulfill {partDemand.TotalOutstanding}pc demand";

                suggestions.Add(new TemplateSuggestion(
                    template.Id,
                    template.Name,
                    partDemand.PartId,
                    partDemand.PartNumber,
                    suggestedQty,
                    template.EstimatedDurationHours * runsNeeded,
                    template.UseCount,
                    woRefs,
                    rationale));
            }
        }

        // Sort: highest WO priority first, then earliest due date, then most used template
        return suggestions
            .OrderByDescending(s => s.FulfillsWorkOrders.Min(w => w.DueDate) < DateTime.UtcNow) // overdue first
            .ThenBy(s => s.FulfillsWorkOrders.Min(w => w.DueDate))
            .ThenByDescending(s => s.UseCount)
            .ToList();
    }

    // ── Mixed-Build Suggestions ───────────────────────────────

    private static List<MixedBuildSuggestion> GenerateMixedBuildSuggestions(
        List<DemandLine> demand,
        List<BuildTemplate> certifiedTemplates,
        List<TemplateSuggestion> templateSuggestions)
    {
        var suggestions = new List<MixedBuildSuggestion>();

        // Find parts with partial plate demand (outstanding < planned per build)
        var partialDemand = demand
            .GroupBy(d => d.PartId)
            .Select(g => new
            {
                PartId = g.Key,
                PartNumber = g.First().PartNumber,
                MaterialId = g.First().MaterialId,
                MaterialName = g.First().MaterialName,
                TotalOutstanding = g.Sum(d => d.Outstanding),
                PlannedPerBuild = g.First().PlannedPartsPerBuild,
                Lines = g.ToList()
            })
            .Where(p => p.TotalOutstanding < p.PlannedPerBuild)
            .ToList();

        // Exclude parts that already have a single-part template suggestion
        var coveredPartIds = templateSuggestions.Select(s => s.PartId).ToHashSet();
        partialDemand = partialDemand
            .Where(p => !coveredPartIds.Contains(p.PartId))
            .ToList();

        if (partialDemand.Count < 2)
            return suggestions;

        // Group by material (must match for mixing on same plate)
        var materialGroups = partialDemand
            .GroupBy(p => p.MaterialId)
            .Where(g => g.Count() >= 2)
            .ToList();

        foreach (var group in materialGroups)
        {
            var parts = group.OrderBy(p => p.Lines.Min(l => l.DueDate)).ToList();
            var materialName = parts.First().MaterialName;

            // Check if due dates align within 7 days
            var earliestDue = parts.Min(p => p.Lines.Min(l => l.DueDate));
            var latestDue = parts.Max(p => p.Lines.Min(l => l.DueDate));
            if ((latestDue - earliestDue).TotalDays > 7)
                continue;

            // Check for an existing certified multi-part template that matches this combination
            var partIds = parts.Select(p => p.PartId).ToHashSet();
            var matchingMultiTemplate = certifiedTemplates
                .Where(t => t.IsCertified && t.Parts.Count >= 2)
                .FirstOrDefault(t => partIds.SetEquals(t.Parts.Select(p => p.PartId).ToHashSet()));

            var mixedParts = parts.Select(p => new MixedBuildLine(
                p.PartId,
                p.PartNumber,
                p.TotalOutstanding,
                materialName)).ToList();

            var woRefs = parts.SelectMany(p => p.Lines.Select(l => new WorkOrderReference(
                l.WorkOrderId,
                l.OrderNumber,
                l.WorkOrderLineId,
                l.Outstanding,
                l.DueDate))).ToList();

            // Estimate duration from the max PlannedPerBuild part's typical build time
            var estimatedHours = matchingMultiTemplate?.EstimatedDurationHours
                ?? parts.Max(p => p.PlannedPerBuild) * 0.25; // rough fallback

            var partSummary = string.Join(" + ", parts.Select(p => $"{p.PartNumber} ({p.TotalOutstanding})"));
            var dueSummary = earliestDue == latestDue
                ? $"due {earliestDue:MM/dd}"
                : $"due {earliestDue:MM/dd}–{latestDue:MM/dd}";

            var rationale = $"Combine {partSummary} — {dueSummary}, {materialName}";

            suggestions.Add(new MixedBuildSuggestion(
                mixedParts,
                matchingMultiTemplate?.Name,
                matchingMultiTemplate?.Id,
                estimatedHours,
                woRefs,
                rationale));
        }

        return suggestions
            .OrderBy(s => s.FulfillsWorkOrders.Min(w => w.DueDate))
            .ToList();
    }

    // ── Internal Types ────────────────────────────────────────

    private record DemandLine(
        int WorkOrderLineId,
        int WorkOrderId,
        string OrderNumber,
        DateTime DueDate,
        JobPriority Priority,
        int PartId,
        string PartNumber,
        int? MaterialId,
        string MaterialName,
        int Outstanding,
        int PlannedPartsPerBuild);
}
