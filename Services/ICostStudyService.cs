using Vectrik.Models;

namespace Vectrik.Services;

public interface ICostStudyService
{
    Task<List<CostStudy>> GetAllAsync(string? status = null, string? search = null);
    Task<CostStudy?> GetByIdAsync(int id);
    Task<CostStudy> CreateAsync(CostStudy study, string createdBy);
    Task UpdateAsync(CostStudy study, string modifiedBy);
    Task DeleteAsync(int id);
    Task<CostStudy> DuplicateAsync(int sourceId, string newName, string createdBy);

    Task<CostStudyPart> AddPartAsync(int studyId, CostStudyPart part, string modifiedBy);
    Task UpdatePartAsync(CostStudyPart part, string modifiedBy);
    Task DeletePartAsync(int partId, string modifiedBy);
    Task<CostStudyPart?> SeedPartFromCatalogAsync(int studyId, int partId, string modifiedBy);

    Task<CostStudyStage> AddStageAsync(int partId, CostStudyStage stage, string modifiedBy);
    Task<CostStudyStage> AddStageFromCatalogAsync(int partId, int productionStageId, string modifiedBy);
    Task UpdateStageAsync(CostStudyStage stage, string modifiedBy);
    Task DeleteStageAsync(int stageId, string modifiedBy);
    Task ReorderStagesAsync(int partId, List<int> orderedStageIds, string modifiedBy);

    CostStudyPartBreakdown ComputePartCost(CostStudy study, CostStudyPart part);
    StudyCostBreakdown ComputeStudyCost(CostStudy study);

    string GenerateCsv(CostStudy study);
}

public class StageCostLine
{
    public int StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsExternal { get; set; }

    public double TotalMinutes { get; set; }
    public decimal LaborAndMachineCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal ToolingCost { get; set; }
    public decimal ExternalCost { get; set; }
    public decimal TotalCost { get; set; }

    public decimal CostPerPart { get; set; }
}

public class CostStudyPartBreakdown
{
    public int PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }

    // Per-part cost buckets (extended to entire order by multiplying by Quantity where noted)
    public decimal SlsBuildCostPerPart { get; set; }
    public decimal RawMaterialCostPerPart { get; set; }
    public decimal StageCostSubtotal { get; set; }  // total over whole order (not per part)

    public List<StageCostLine> Stages { get; set; } = new();

    // Order-level costs
    public decimal NreCostTotal { get; set; }          // engineering + tooling + FAI + certs (amortized or billed)
    public bool NreAmortized { get; set; }             // if true, NreCostTotal is folded into CostPerPart; else tracked separately
    public decimal PackagingCostTotal { get; set; }    // per-part × qty + per-order
    public decimal FreightCostTotal { get; set; }      // per-order + markup

    public decimal OrderCostBeforeOverheads { get; set; }

    public decimal ContingencyAmount { get; set; }
    public decimal AdminOverheadAmount { get; set; }
    public decimal TotalOrderCost { get; set; }
    public decimal CostPerPart => Quantity > 0 ? TotalOrderCost / Quantity : 0;

    // Sales pricing
    public decimal ComputedSellPricePerPart { get; set; }   // margin-based suggestion
    public decimal SalesPricePerPart { get; set; }          // = override if set, else computed
    public bool SalesPriceIsOverride { get; set; }
    public decimal PaymentTermsDiscountAmount { get; set; }
    public decimal SuggestedOrderPrice => SalesPricePerPart * Quantity;
    public decimal NetOrderPrice => SuggestedOrderPrice - PaymentTermsDiscountAmount;
    public decimal MarginAmount => NetOrderPrice - TotalOrderCost;
    public double MarginPercent => TotalOrderCost > 0 ? (double)(MarginAmount / TotalOrderCost) * 100.0 : 0;
}

public class StudyCostBreakdown
{
    public int StudyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CostStudyPartBreakdown> Parts { get; set; } = new();

    public decimal TotalCost => Parts.Sum(p => p.TotalOrderCost);
    public decimal SuggestedTotal => Parts.Sum(p => p.SuggestedOrderPrice);
    public decimal TermsDiscountTotal => Parts.Sum(p => p.PaymentTermsDiscountAmount);
    public decimal NetTotal => Parts.Sum(p => p.NetOrderPrice);
    public decimal TotalMargin => NetTotal - TotalCost;
    public double MarginPercent => TotalCost > 0 ? (double)(TotalMargin / TotalCost) * 100.0 : 0;
}
