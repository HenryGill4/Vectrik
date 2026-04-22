using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;

namespace Vectrik.Services;

public class CostStudyService : ICostStudyService
{
    private readonly TenantDbContext _db;

    public CostStudyService(TenantDbContext db) => _db = db;

    // ── List / CRUD ──────────────────────────────────────────

    public async Task<List<CostStudy>> GetAllAsync(string? status = null, string? search = null)
    {
        var q = _db.CostStudies.Include(s => s.Parts).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(s => s.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x =>
                x.Name.Contains(s) ||
                (x.CustomerName != null && x.CustomerName.Contains(s)) ||
                (x.ProjectName != null && x.ProjectName.Contains(s)) ||
                x.StudyNumber.Contains(s));
        }
        return await q.OrderByDescending(s => s.LastModifiedDate).ToListAsync();
    }

    public async Task<CostStudy?> GetByIdAsync(int id)
    {
        return await _db.CostStudies
            .Include(s => s.Parts.OrderBy(p => p.DisplayOrder))
                .ThenInclude(p => p.Stages.OrderBy(st => st.DisplayOrder))
            .Include(s => s.Parts)
                .ThenInclude(p => p.Material)
            .Include(s => s.Parts)
                .ThenInclude(p => p.Part)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<CostStudy> CreateAsync(CostStudy study, string createdBy)
    {
        study.CreatedDate = DateTime.UtcNow;
        study.LastModifiedDate = DateTime.UtcNow;
        study.CreatedBy = createdBy;
        study.LastModifiedBy = createdBy;
        if (string.IsNullOrWhiteSpace(study.StudyNumber))
            study.StudyNumber = await NextStudyNumberAsync();
        _db.CostStudies.Add(study);
        await _db.SaveChangesAsync();
        return study;
    }

    public async Task UpdateAsync(CostStudy study, string modifiedBy)
    {
        study.LastModifiedDate = DateTime.UtcNow;
        study.LastModifiedBy = modifiedBy;
        _db.CostStudies.Update(study);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var study = await _db.CostStudies.FindAsync(id);
        if (study is null) return;
        _db.CostStudies.Remove(study);
        await _db.SaveChangesAsync();
    }

    public async Task<CostStudy> DuplicateAsync(int sourceId, string newName, string createdBy)
    {
        var source = await GetByIdAsync(sourceId)
            ?? throw new InvalidOperationException("Study not found.");
        var copy = new CostStudy
        {
            Name = newName,
            CustomerName = source.CustomerName,
            ProjectName = source.ProjectName,
            Status = "Draft",
            Notes = source.Notes,
            TargetMarginPercent = source.TargetMarginPercent,
            ContingencyPercent = source.ContingencyPercent,
            AdminOverheadPercent = source.AdminOverheadPercent,
            DefaultVendorMarkupPercent = source.DefaultVendorMarkupPercent,
            PaymentTermsDiscountPercent = source.PaymentTermsDiscountPercent,
        };
        foreach (var p in source.Parts)
        {
            var pc = new CostStudyPart
            {
                PartId = p.PartId, PartNumber = p.PartNumber, Name = p.Name, Description = p.Description,
                OrderQuantity = p.OrderQuantity, MaterialId = p.MaterialId, MaterialName = p.MaterialName,
                MaterialCostPerKg = p.MaterialCostPerKg, WeightPerPartKg = p.WeightPerPartKg,
                MaterialScrapPercent = p.MaterialScrapPercent, IsAdditive = p.IsAdditive,
                PartsPerPlate = p.PartsPerPlate, PlateBuildHours = p.PlateBuildHours,
                StackLevel = p.StackLevel, MachineHourlyRate = p.MachineHourlyRate,
                ConsumablesPerPlate = p.ConsumablesPerPlate,
                EngineeringNreCost = p.EngineeringNreCost, ToolingNreCost = p.ToolingNreCost,
                FirstArticleAndCertCost = p.FirstArticleAndCertCost,
                AmortizeNreAcrossOrder = p.AmortizeNreAcrossOrder,
                PackagingCostPerPart = p.PackagingCostPerPart,
                PackagingCostPerOrder = p.PackagingCostPerOrder,
                FreightCostPerOrder = p.FreightCostPerOrder,
                FreightMarkupPercent = p.FreightMarkupPercent,
                SalesPriceOverridePerPart = p.SalesPriceOverridePerPart,
                Notes = p.Notes,
                DisplayOrder = p.DisplayOrder,
            };
            foreach (var st in p.Stages)
            {
                pc.Stages.Add(new CostStudyStage
                {
                    ProductionStageId = st.ProductionStageId,
                    DisplayOrder = st.DisplayOrder,
                    StageName = st.StageName, Category = st.Category,
                    SetupMinutes = st.SetupMinutes, MinutesPerPart = st.MinutesPerPart,
                    BatchMinutes = st.BatchMinutes, BatchSize = st.BatchSize,
                    HourlyRate = st.HourlyRate, OperatorCount = st.OperatorCount,
                    OverheadPercent = st.OverheadPercent,
                    MaterialCostPerPart = st.MaterialCostPerPart,
                    ConsumablesPerPart = st.ConsumablesPerPart,
                    ToolingCostPerRun = st.ToolingCostPerRun,
                    IsExternal = st.IsExternal,
                    ExternalVendorCostPerPart = st.ExternalVendorCostPerPart,
                    ExternalShippingCost = st.ExternalShippingCost,
                    ExternalMarkupPercent = st.ExternalMarkupPercent,
                    YieldPercent = st.YieldPercent, Notes = st.Notes,
                });
            }
            copy.Parts.Add(pc);
        }
        return await CreateAsync(copy, createdBy);
    }

    private async Task<string> NextStudyNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"CS-{year}-";
        var last = await _db.CostStudies
            .Where(s => s.StudyNumber.StartsWith(prefix))
            .OrderByDescending(s => s.StudyNumber)
            .Select(s => s.StudyNumber)
            .FirstOrDefaultAsync();
        var n = 1;
        if (last != null && int.TryParse(last.Substring(prefix.Length), out var parsed))
            n = parsed + 1;
        return $"{prefix}{n:D4}";
    }

    // ── Parts ─────────────────────────────────────────────────

    public async Task<CostStudyPart> AddPartAsync(int studyId, CostStudyPart part, string modifiedBy)
    {
        part.CostStudyId = studyId;
        if (part.DisplayOrder == 0)
        {
            var maxOrder = await _db.CostStudyParts
                .Where(p => p.CostStudyId == studyId)
                .MaxAsync(p => (int?)p.DisplayOrder) ?? 0;
            part.DisplayOrder = maxOrder + 1;
        }
        _db.CostStudyParts.Add(part);
        await TouchStudyAsync(studyId, modifiedBy);
        await _db.SaveChangesAsync();
        return part;
    }

    public async Task UpdatePartAsync(CostStudyPart part, string modifiedBy)
    {
        _db.CostStudyParts.Update(part);
        await TouchStudyAsync(part.CostStudyId, modifiedBy);
        await _db.SaveChangesAsync();
    }

    public async Task DeletePartAsync(int partId, string modifiedBy)
    {
        var p = await _db.CostStudyParts.FindAsync(partId);
        if (p is null) return;
        var studyId = p.CostStudyId;
        _db.CostStudyParts.Remove(p);
        await TouchStudyAsync(studyId, modifiedBy);
        await _db.SaveChangesAsync();
    }

    public async Task<CostStudyPart?> SeedPartFromCatalogAsync(int studyId, int partId, string modifiedBy)
    {
        var src = await _db.Parts
            .Include(p => p.MaterialEntity)
            .Include(p => p.AdditiveBuildConfig)
            .Include(p => p.Pricing)
            .FirstOrDefaultAsync(p => p.Id == partId);
        if (src is null) return null;

        var build = src.AdditiveBuildConfig;
        var weight = src.Pricing?.MaterialWeightPerUnitKg ?? (decimal)(src.EstimatedWeightKg ?? 0);
        var matCost = src.MaterialEntity?.CostPerKg ?? 0m;

        var newPart = new CostStudyPart
        {
            PartId = src.Id,
            PartNumber = src.PartNumber,
            Name = src.Name,
            Description = src.Description,
            OrderQuantity = 1,
            MaterialId = src.MaterialId,
            MaterialName = src.MaterialEntity?.Name ?? src.Material,
            MaterialCostPerKg = matCost,
            WeightPerPartKg = weight,
            MaterialScrapPercent = 5,
            IsAdditive = src.ManufacturingApproach?.IsAdditive ?? true,
            PartsPerPlate = build?.GetPartsPerBuild(1) ?? 1,
            PlateBuildHours = build?.GetStackDuration(1) ?? 0,
            StackLevel = 1,
            MachineHourlyRate = 200m,
            ConsumablesPerPlate = 0m,
        };
        return await AddPartAsync(studyId, newPart, modifiedBy);
    }

    // ── Stages ────────────────────────────────────────────────

    public async Task<CostStudyStage> AddStageAsync(int partId, CostStudyStage stage, string modifiedBy)
    {
        stage.CostStudyPartId = partId;
        if (stage.DisplayOrder == 0)
        {
            var maxOrder = await _db.CostStudyStages
                .Where(s => s.CostStudyPartId == partId)
                .MaxAsync(s => (int?)s.DisplayOrder) ?? 0;
            stage.DisplayOrder = maxOrder + 1;
        }
        _db.CostStudyStages.Add(stage);
        var part = await _db.CostStudyParts.FindAsync(partId);
        if (part is not null)
            await TouchStudyAsync(part.CostStudyId, modifiedBy);
        await _db.SaveChangesAsync();
        return stage;
    }

    public async Task<CostStudyStage> AddStageFromCatalogAsync(int partId, int productionStageId, string modifiedBy)
    {
        var stage = await _db.ProductionStages
            .Where(s => s.Id == productionStageId)
            .FirstOrDefaultAsync();
        var profile = await _db.StageCostProfiles
            .Where(p => p.ProductionStageId == productionStageId)
            .FirstOrDefaultAsync();

        var newStage = new CostStudyStage
        {
            ProductionStageId = productionStageId,
            StageName = stage?.Name ?? "Stage",
            Category = stage?.Department,
            SetupMinutes = stage?.DefaultSetupMinutes ?? 0,
            MinutesPerPart = 0,
            HourlyRate = profile?.FullyLoadedHourlyRate ?? stage?.DefaultHourlyRate ?? 85m,
            OperatorCount = profile?.OperatorsRequired ?? 1,
            OverheadPercent = profile?.OverheadPercent ?? 0,
            MaterialCostPerPart = stage?.DefaultMaterialCost ?? 0,
            ConsumablesPerPart = profile?.ConsumablesPerPart ?? 0,
            ToolingCostPerRun = profile?.ToolingCostPerRun ?? 0,
            IsExternal = stage?.IsExternalOperation ?? false,
            ExternalVendorCostPerPart = profile?.ExternalVendorCostPerPart ?? 0,
            ExternalShippingCost = profile?.ExternalShippingCost ?? 0,
            ExternalMarkupPercent = profile?.ExternalMarkupPercent ?? 0,
            YieldPercent = 100,
        };
        return await AddStageAsync(partId, newStage, modifiedBy);
    }

    public async Task UpdateStageAsync(CostStudyStage stage, string modifiedBy)
    {
        _db.CostStudyStages.Update(stage);
        var part = await _db.CostStudyParts.FindAsync(stage.CostStudyPartId);
        if (part is not null)
            await TouchStudyAsync(part.CostStudyId, modifiedBy);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteStageAsync(int stageId, string modifiedBy)
    {
        var s = await _db.CostStudyStages.FindAsync(stageId);
        if (s is null) return;
        var partId = s.CostStudyPartId;
        _db.CostStudyStages.Remove(s);
        var part = await _db.CostStudyParts.FindAsync(partId);
        if (part is not null)
            await TouchStudyAsync(part.CostStudyId, modifiedBy);
        await _db.SaveChangesAsync();
    }

    public async Task ReorderStagesAsync(int partId, List<int> orderedStageIds, string modifiedBy)
    {
        var stages = await _db.CostStudyStages
            .Where(s => s.CostStudyPartId == partId)
            .ToListAsync();
        for (var i = 0; i < orderedStageIds.Count; i++)
        {
            var s = stages.FirstOrDefault(x => x.Id == orderedStageIds[i]);
            if (s is not null) s.DisplayOrder = i + 1;
        }
        var part = await _db.CostStudyParts.FindAsync(partId);
        if (part is not null)
            await TouchStudyAsync(part.CostStudyId, modifiedBy);
        await _db.SaveChangesAsync();
    }

    private async Task TouchStudyAsync(int studyId, string modifiedBy)
    {
        var study = await _db.CostStudies.FindAsync(studyId);
        if (study is null) return;
        study.LastModifiedDate = DateTime.UtcNow;
        study.LastModifiedBy = modifiedBy;
    }

    // ── Cost Computation ─────────────────────────────────────

    public CostStudyPartBreakdown ComputePartCost(CostStudy study, CostStudyPart part)
    {
        var qty = Math.Max(1, part.OrderQuantity);
        var slsPerPart = part.IsAdditive ? part.SlsBuildCostPerPart : 0m;
        var rawPerPart = part.EffectiveMaterialCostPerPart;

        var breakdown = new CostStudyPartBreakdown
        {
            PartId = part.Id,
            PartNumber = part.PartNumber,
            Name = part.Name,
            Quantity = qty,
            SlsBuildCostPerPart = slsPerPart,
            RawMaterialCostPerPart = rawPerPart,
            NreAmortized = part.AmortizeNreAcrossOrder,
        };

        foreach (var s in part.Stages.OrderBy(x => x.DisplayOrder))
        {
            var totalMin = s.TotalMinutesForOrder(qty);
            var timeCost = (decimal)(totalMin / 60.0) * s.HourlyRate * Math.Max(1, s.OperatorCount);
            var overhead = timeCost * (decimal)(s.OverheadPercent / 100.0);

            var effectiveQty = qty;
            if (s.YieldPercent > 0 && s.YieldPercent < 100)
                effectiveQty = (int)Math.Ceiling(qty * (100.0 / s.YieldPercent));

            var material = (s.MaterialCostPerPart + s.ConsumablesPerPart) * effectiveQty;

            var batches = 1;
            if (s.BatchMinutes > 0 && s.BatchSize > 0)
                batches = (int)Math.Ceiling((double)effectiveQty / s.BatchSize);
            var tooling = s.ToolingCostPerRun * batches;

            decimal external = 0m;
            if (s.IsExternal || s.ExternalVendorCostPerPart > 0 || s.ExternalShippingCost > 0)
            {
                // Stage-level markup takes precedence; fall back to the study-wide vendor markup default.
                var markupPct = s.ExternalMarkupPercent > 0 ? s.ExternalMarkupPercent : study.DefaultVendorMarkupPercent;
                external = (s.ExternalVendorCostPerPart * effectiveQty) + s.ExternalShippingCost;
                external *= (1 + (decimal)(markupPct / 100.0));
            }

            var total = timeCost + overhead + material + tooling + external;
            breakdown.Stages.Add(new StageCostLine
            {
                StageId = s.Id,
                StageName = s.StageName,
                Category = s.Category,
                IsExternal = s.IsExternal,
                TotalMinutes = totalMin,
                LaborAndMachineCost = timeCost,
                OverheadCost = overhead,
                MaterialCost = material,
                ToolingCost = tooling,
                ExternalCost = external,
                TotalCost = total,
                CostPerPart = qty > 0 ? total / qty : 0,
            });
        }

        breakdown.StageCostSubtotal = breakdown.Stages.Sum(s => s.TotalCost);

        // NRE / setup — amortized into order cost by default, otherwise reported separately
        var nreTotal = part.EngineeringNreCost + part.ToolingNreCost + part.FirstArticleAndCertCost;
        breakdown.NreCostTotal = nreTotal;

        // Packaging: per-part × qty + fixed per-order
        breakdown.PackagingCostTotal = (part.PackagingCostPerPart * qty) + part.PackagingCostPerOrder;

        // Freight: fixed per-order + markup
        breakdown.FreightCostTotal = part.FreightCostPerOrder * (1 + (decimal)(part.FreightMarkupPercent / 100.0));

        // Order subtotal before study-level overheads
        var baseOrder =
            slsPerPart * qty +
            rawPerPart * qty +
            breakdown.StageCostSubtotal +
            breakdown.PackagingCostTotal +
            breakdown.FreightCostTotal;

        if (part.AmortizeNreAcrossOrder)
            baseOrder += nreTotal;

        breakdown.OrderCostBeforeOverheads = baseOrder;
        breakdown.ContingencyAmount = baseOrder * (decimal)(study.ContingencyPercent / 100.0);
        var withContingency = baseOrder + breakdown.ContingencyAmount;
        breakdown.AdminOverheadAmount = withContingency * (decimal)(study.AdminOverheadPercent / 100.0);
        breakdown.TotalOrderCost = withContingency + breakdown.AdminOverheadAmount;

        // Sales price: computed from margin, optionally overridden, optionally reduced by terms discount
        var marginMult = 1 + (decimal)(study.TargetMarginPercent / 100.0);
        var cpp = breakdown.CostPerPart;
        breakdown.ComputedSellPricePerPart = Math.Round(cpp * marginMult, 2, MidpointRounding.AwayFromZero);

        if (part.SalesPriceOverridePerPart.HasValue && part.SalesPriceOverridePerPart.Value > 0)
        {
            breakdown.SalesPricePerPart = part.SalesPriceOverridePerPart.Value;
            breakdown.SalesPriceIsOverride = true;
        }
        else
        {
            breakdown.SalesPricePerPart = breakdown.ComputedSellPricePerPart;
            breakdown.SalesPriceIsOverride = false;
        }

        breakdown.PaymentTermsDiscountAmount =
            breakdown.SuggestedOrderPrice * (decimal)(study.PaymentTermsDiscountPercent / 100.0);

        return breakdown;
    }

    public StudyCostBreakdown ComputeStudyCost(CostStudy study)
    {
        var result = new StudyCostBreakdown { StudyId = study.Id, Name = study.Name };
        foreach (var p in study.Parts.OrderBy(x => x.DisplayOrder))
            result.Parts.Add(ComputePartCost(study, p));
        return result;
    }

    // ── CSV Export ────────────────────────────────────────────

    public string GenerateCsv(CostStudy study)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        var breakdown = ComputeStudyCost(study);

        sb.AppendLine("Cost Study Report");
        sb.AppendLine($"Study,{Esc(study.Name)}");
        sb.AppendLine($"Number,{Esc(study.StudyNumber)}");
        sb.AppendLine($"Customer,{Esc(study.CustomerName ?? "")}");
        sb.AppendLine($"Project,{Esc(study.ProjectName ?? "")}");
        sb.AppendLine($"Status,{Esc(study.Status)}");
        sb.AppendLine($"Generated,{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        sb.AppendLine("Part #,Part Name,Qty,SLS $/pt,Raw Mat $/pt,Stage $ Total,NRE Total,Packaging Total,Freight Total,Contingency $,G&A $,Total Order Cost,Cost/Part,Computed Sell/Part,Sales Price/Part,Override?,Order Price,Terms Discount,Net Order,Margin $,Margin %");
        foreach (var p in breakdown.Parts)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Esc(p.PartNumber), Esc(p.Name), p.Quantity.ToString(inv),
                p.SlsBuildCostPerPart.ToString("F2", inv),
                p.RawMaterialCostPerPart.ToString("F2", inv),
                p.StageCostSubtotal.ToString("F2", inv),
                p.NreCostTotal.ToString("F2", inv),
                p.PackagingCostTotal.ToString("F2", inv),
                p.FreightCostTotal.ToString("F2", inv),
                p.ContingencyAmount.ToString("F2", inv),
                p.AdminOverheadAmount.ToString("F2", inv),
                p.TotalOrderCost.ToString("F2", inv),
                p.CostPerPart.ToString("F2", inv),
                p.ComputedSellPricePerPart.ToString("F2", inv),
                p.SalesPricePerPart.ToString("F2", inv),
                p.SalesPriceIsOverride ? "Yes" : "No",
                p.SuggestedOrderPrice.ToString("F2", inv),
                p.PaymentTermsDiscountAmount.ToString("F2", inv),
                p.NetOrderPrice.ToString("F2", inv),
                p.MarginAmount.ToString("F2", inv),
                p.MarginPercent.ToString("F1", inv),
            }));
        }
        sb.AppendLine();
        sb.AppendLine($"Study Total Cost,{breakdown.TotalCost.ToString("F2", inv)}");
        sb.AppendLine($"Study Suggested Total,{breakdown.SuggestedTotal.ToString("F2", inv)}");
        sb.AppendLine($"Study Margin $,{breakdown.TotalMargin.ToString("F2", inv)}");
        sb.AppendLine($"Study Margin %,{breakdown.MarginPercent.ToString("F1", inv)}");
        sb.AppendLine();

        sb.AppendLine("Stage Detail");
        sb.AppendLine("Part #,Stage,Category,Minutes,Labor+Machine,Overhead,Material,Tooling,External,Total,Cost/Part");
        foreach (var p in breakdown.Parts)
        {
            foreach (var s in p.Stages)
            {
                sb.AppendLine(string.Join(',', new[]
                {
                    Esc(p.PartNumber), Esc(s.StageName), Esc(s.Category ?? ""),
                    s.TotalMinutes.ToString("F1", inv),
                    s.LaborAndMachineCost.ToString("F2", inv),
                    s.OverheadCost.ToString("F2", inv),
                    s.MaterialCost.ToString("F2", inv),
                    s.ToolingCost.ToString("F2", inv),
                    s.ExternalCost.ToString("F2", inv),
                    s.TotalCost.ToString("F2", inv),
                    s.CostPerPart.ToString("F2", inv),
                }));
            }
        }

        return sb.ToString();
    }

    private static string Esc(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
