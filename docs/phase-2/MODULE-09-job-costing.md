# Module 09: Job Costing & Financial Data

## Status: [ ] Not Started
## Category: ERP
## Phase: 2 — Operational Depth
## Priority: P2 - High

---

## Overview

Job Costing provides real-time visibility into the financial performance of every
job — tracking labor, material, tooling, and overhead costs against quoted budgets.
Margin erosion alerts fire during production so managers can act before a job loses
money. Profitability is tracked by customer, part family, and machine center.

**ProShop Improvements**: Real-time estimated vs. actual comparison at every routing
step, margin erosion alerts during production, activity-based overhead allocation
models, open API for any accounting integration, and profitability trending by
customer/part family/machine center.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `Job.EstimatedCost`, `Job.ActualCostToDate` (added in M02) | ✅ M02 | `Models/Job.cs` |
| `WorkOrder.BudgetedCost`, `ActualCostToDate` (added in M02) | ✅ M02 | `Models/WorkOrder.cs` |
| `StageExecution.ActualHours` (added in M04) | ✅ M04 | `Models/StageExecution.cs` |
| `Machine.HourlyRate` | ✅ Exists | `Models/Machine.cs` |
| `InventoryTransaction` (material consumption) | ✅ M06 | `Models/InventoryTransaction.cs` |

**Gap**: No cost rollup service, no overhead allocation model, no margin alerts, no profitability analytics, no accounting export.

---

## What Needs to Be Built

### 1. Database Models (New)
- `CostEntry` — individual cost line items per job (audit trail of all charges)
- `OverheadRate` — configurable overhead allocation rates
- `LaborRate` — operator/role-based labor rates for costing

### 2. Service Layer (New)
- `JobCostingService` — real-time cost accumulation and variance analysis
- `ProfitabilityService` — roll-up analysis by customer, part, time period

### 3. UI Components (New)
- **Job Cost Card** — embedded in job detail showing live cost vs. budget
- **Cost Analysis Dashboard** — estimated vs. actual with drill-down
- **Margin Erosion Alert** — notification when job goes over budget threshold

---

## Implementation Steps

### Step 1 — Create CostEntry Model
**New File**: `Models/CostEntry.cs`
```csharp
public class CostEntry
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job Job { get; set; } = null!;
    public CostCategory Category { get; set; }       // Labor, Material, Tooling, Overhead, OutsideProcess
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }           // Quantity × UnitCost
    public int? StageExecutionId { get; set; }       // Source: labor from stage
    public int? InventoryTransactionId { get; set; } // Source: material from inventory
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public enum CostCategory { Labor, Material, Tooling, Overhead, OutsideProcess, Other }
```

### Step 2 — Create OverheadRate Model
**New File**: `Models/OverheadRate.cs`
```csharp
public class OverheadRate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;      // e.g., "Shop Overhead", "G&A"
    public OverheadMethod Method { get; set; }             // FlatPerHour, PctOfLabor, PctOfTotal
    public decimal Rate { get; set; }                      // $/hr or % depending on method
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
}

public enum OverheadMethod { FlatPerHour, PercentOfLabor, PercentOfTotalDirectCost }
```

### Step 3 — Create LaborRate Model
**New File**: `Models/LaborRate.cs`
```csharp
public class LaborRate
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;   // e.g., "Machinist", "Inspector"
    public decimal RatePerHour { get; set; }
    public decimal OvertimeRatePerHour { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
}
```

### Step 4 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<CostEntry> CostEntries { get; set; }
public DbSet<OverheadRate> OverheadRates { get; set; }
public DbSet<LaborRate> LaborRates { get; set; }
```

### Step 5 — Create JobCostingService
**New File**: `Services/JobCostingService.cs`
**New File**: `Services/IJobCostingService.cs`

```csharp
public interface IJobCostingService
{
    // Real-time cost retrieval
    Task<JobCostSummary> GetJobCostSummaryAsync(int jobId, string tenantCode);
    Task<List<CostEntry>> GetCostEntriesAsync(int jobId, string tenantCode);

    // Cost recording (called by other services)
    Task RecordLaborCostAsync(int jobId, int stageExecutionId, decimal hours,
                               string operatorUserId, string tenantCode);
    Task RecordMaterialCostAsync(int jobId, int transactionId, decimal qty,
                                  decimal unitCost, string tenantCode);
    Task RecordOverheadAsync(int jobId, string tenantCode);      // applies overhead rates to labor

    // Budget variance
    Task<decimal> GetVariancePctAsync(int jobId, string tenantCode);
    Task<bool> IsBudgetAlertThresholdExceededAsync(int jobId, string tenantCode);
}

public record JobCostSummary(
    int JobId,
    decimal EstimatedCost,
    decimal ActualLaborCost,
    decimal ActualMaterialCost,
    decimal ActualToolingCost,
    decimal ActualOverheadCost,
    decimal ActualOutsideProcessCost,
    decimal TotalActualCost,
    decimal VarianceDollars,
    decimal VariancePct,
    bool IsOverBudget
);
```

**Integration points**:
- Call `RecordLaborCostAsync` from `StageService.CompleteStageAsync` (Module 04)
- Call `RecordMaterialCostAsync` from `InventoryService.ConsumeForJobAsync` (Module 06)
- Call `RecordOverheadAsync` after each labor entry to apply overhead rates

### Step 6 — Create ProfitabilityService
**New File**: `Services/ProfitabilityService.cs`
**New File**: `Services/IProfitabilityService.cs`

```csharp
public interface IProfitabilityService
{
    Task<List<CustomerProfitability>> GetByCustomerAsync(DateTime from, DateTime to, string tenantCode);
    Task<List<PartProfitability>> GetByPartAsync(DateTime from, DateTime to, string tenantCode);
    Task<List<MachineCenterProfitability>> GetByMachineCenterAsync(DateTime from, DateTime to, string tenantCode);
}

public record CustomerProfitability(string CustomerName, decimal TotalRevenue,
    decimal TotalActualCost, decimal GrossProfit, decimal MarginPct, int JobCount);

public record PartProfitability(string PartNumber, string PartName, decimal AvgQuotedCost,
    decimal AvgActualCost, decimal AvgVariancePct, int RunCount);
```

### Step 7 — Job Cost Card Component
**New File**: `Components/Shared/JobCostCard.razor`

A reusable component embedded in Job Detail page:
```razor
@* Usage: <JobCostCard JobId="@job.Id" TenantCode="@tenantCode" /> *@
<div class="cost-card @(summary.IsOverBudget ? "over-budget" : "")">
    <div class="cost-row">
        <span>Labor</span>
        <span>@summary.ActualLaborCost.ToString("C")</span>
    </div>
    <div class="cost-row">
        <span>Materials</span>
        <span>@summary.ActualMaterialCost.ToString("C")</span>
    </div>
    <div class="cost-row">
        <span>Overhead</span>
        <span>@summary.ActualOverheadCost.ToString("C")</span>
    </div>
    <div class="cost-row total">
        <span>Total Actual</span>
        <span>@summary.TotalActualCost.ToString("C")</span>
    </div>
    <div class="cost-row budget">
        <span>Budget</span>
        <span>@summary.EstimatedCost.ToString("C")</span>
    </div>
    <div class="variance @(summary.IsOverBudget ? "text-red" : "text-green")">
        Variance: @summary.VarianceDollars.ToString("+0.00;-0.00") (@summary.VariancePct.ToString("F1")%)
    </div>
    @if (summary.IsOverBudget)
    {
        <div class="alert-banner">⚠️ Job exceeds budget</div>
    }
</div>
```

### Step 8 — Cost Analysis Report Page
**New File**: `Components/Pages/Analytics/CostAnalysis.razor`
(Already defined in Module 07 — connect to `JobCostingService` and `ProfitabilityService` here)

### Step 9 — Admin: Labor & Overhead Rate Configuration
**New File**: `Components/Pages/Admin/Rates.razor`
**Route**: `/admin/rates`

UI requirements:
- **Labor Rates** table: role name, $/hr, overtime $/hr — CRUD
- **Overhead Rates** table: name, method, rate — CRUD
- "Effective From" date pickers for rate change history

### Step 10 — Margin Erosion Notifications
Wire into `StageService.CompleteStageAsync` (Module 04):
```csharp
// After recording labor cost:
var isAlert = await _costingService.IsBudgetAlertThresholdExceededAsync(jobId, tenantCode);
if (isAlert)
{
    // Broadcast via SignalR to all managers in tenant
    await _machineStateNotifier.BroadcastCostAlertAsync(tenantCode, jobId);
    // OR: create a notification record for the job manager
}
```

**Alert threshold**: configurable in `SystemSettings` key `"cost_alert_threshold_pct"` (default: 90%).

### Step 11 — EF Core Migration
```bash
dotnet ef migrations add AddJobCosting --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Completing a stage automatically records a `CostEntry` for labor
- [ ] Consuming material automatically records a `CostEntry` for materials
- [ ] Overhead is applied to each job based on configured overhead rates
- [ ] Job detail shows live cost vs. budget breakdown by category
- [ ] Budget alert fires when actual cost exceeds configured threshold %
- [ ] Over-budget jobs have visual indicator in job lists
- [ ] Labor rates can be configured per role in admin
- [ ] Overhead rates can be configured with different allocation methods
- [ ] Profitability by customer report shows revenue, cost, margin
- [ ] Profitability by part shows average actual vs. quoted cost per run

---

## Dependencies

- **Module 02** (Work Orders) — Job records with budget
- **Module 04** (Shop Floor) — Labor entries from stage completion
- **Module 06** (Inventory) — Material consumption records
- **Module 07** (Analytics) — Cost data feeds financial KPI dashboard

---

## Future Enhancements (Post-MVP)

- Accounting system integration (QuickBooks, Sage, Xero) — push job cost to invoice
- WIP (Work in Process) valuation report for balance sheet
- Overhead variance analysis (absorbed vs. actual overhead)
- Job costing by customer with contract profitability tracking
