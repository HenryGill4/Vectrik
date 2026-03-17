# Module 07: Reporting & Analytics

## Status: [x] Complete
## Category: ERP
## Phase: 1 — Core Production Engine
## Priority: P1 - Critical

---

## Overview

The Reporting & Analytics module provides real-time operational visibility through
pre-built manufacturing KPI dashboards, a drag-and-drop custom dashboard builder,
and cross-module queries without requiring Excel exports.

**ProShop Improvements**: Drag-and-drop dashboard builder, pre-built manufacturing
KPI dashboards (OEE, on-time delivery, first-pass yield, scrap rate, capacity),
natural-language query support, scheduled report distribution, and a data export
API for BI tool integration.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `AnalyticsService` / `IAnalyticsService` (stub) | ✅ Exists | `Services/AnalyticsService.cs` |
| `/analytics` dashboard page (KPI display stub) | ✅ Exists | `Components/Pages/Analytics/` |
| `LearningService` (learning/trends stub) | ✅ Exists | `Services/LearningService.cs` |
| Home dashboard with KPI widgets | ✅ Partial | `Components/Pages/Home.razor` |

**Gap**: Analytics service is mostly stubs; no cross-module queries; no custom dashboard builder; no scheduled reports; no data export API; limited KPI calculations.

---

## What Needs to Be Built

### 1. Database Models (New)
- `SavedReport` — user-saved report configurations
- `DashboardLayout` — per-user dashboard widget arrangement
- `ScheduledReport` — email distribution schedule for reports

### 2. Service Layer (Enhance)
- Complete `AnalyticsService` with real calculations from all modules
- `ReportingService` — cross-module query engine
- `KpiService` — all manufacturing KPI calculations

### 3. UI Components (New/Enhance)
- **Main Analytics Dashboard** — KPI cards + charts (complete the stub)
- **On-Time Delivery Report** — job completion vs. due dates
- **Quality Metrics Report** — first-pass yield, scrap, NCR trend
- **Machine OEE Dashboard** — per-machine OEE breakdown
- **Capacity Utilization Report** — machine load over time
- **Cost Analysis Report** — estimated vs. actual by job/part/customer
- **Custom Report Builder** — drag-and-drop widget builder

---

## Implementation Steps

### Step 1 — Create DashboardLayout Model
**New File**: `Models/DashboardLayout.cs`
```csharp
public class DashboardLayout
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string LayoutName { get; set; } = "My Dashboard";
    public bool IsDefault { get; set; }
    public string WidgetsJson { get; set; } = "[]";    // JSON array of DashboardWidget configs
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Widget config (serialized in WidgetsJson):
// { id, type, title, dataSource, filterJson, position: {col, row, width, height} }
```

### Step 2 — Create SavedReport Model
**New File**: `Models/SavedReport.cs`
```csharp
public class SavedReport
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;   // e.g. "OnTimeDelivery", "QualityTrend"
    public string FilterJson { get; set; } = "{}";           // Serialized filter parameters
    public string CreatedByUserId { get; set; } = string.Empty;
    public bool IsShared { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 3 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<DashboardLayout> DashboardLayouts { get; set; }
public DbSet<SavedReport> SavedReports { get; set; }
```

### Step 4 — Complete AnalyticsService
**File**: `Services/AnalyticsService.cs`

Implement all KPI calculations (query real tables):

```csharp
public interface IAnalyticsService
{
    // Production KPIs
    Task<int> GetActiveJobCountAsync(string tenantCode);
    Task<int> GetActiveWorkOrderCountAsync(string tenantCode);
    Task<decimal> GetOnTimeDeliveryPctAsync(DateTime from, DateTime to, string tenantCode);
    Task<decimal> GetFirstPassYieldPctAsync(DateTime from, DateTime to, string tenantCode);
    Task<decimal> GetScrapRatePctAsync(DateTime from, DateTime to, string tenantCode);

    // Machine KPIs
    Task<decimal> GetMachineUtilizationPctAsync(int? machineId, DateTime from, DateTime to, string tenantCode);
    Task<OeeData> GetOeeAsync(int? machineId, DateTime from, DateTime to, string tenantCode);

    // Financial KPIs
    Task<decimal> GetRevenuePipelineAsync(string tenantCode);          // Sum of accepted WO values
    Task<CostVarianceData> GetCostVarianceAsync(DateTime from, DateTime to, string tenantCode);
    Task<decimal> GetAvgMarginPctAsync(DateTime from, DateTime to, string tenantCode);

    // Pipeline
    Task<List<StageCount>> GetPipelineByStageAsync(string tenantCode); // Parts at each stage
    Task<List<JobStatusCount>> GetJobStatusBreakdownAsync(string tenantCode);

    // Trend data
    Task<List<DailyDataPoint>> GetDailyOutputAsync(DateTime from, DateTime to, string tenantCode);
    Task<List<DailyDataPoint>> GetDailyOnTimeAsync(DateTime from, DateTime to, string tenantCode);
    Task<List<DailyDataPoint>> GetDailyScrapRateAsync(DateTime from, DateTime to, string tenantCode);
}
```

**Key calculation logic**:
- `OnTimeDelivery`: completed jobs where `ActualEndAt <= MustLeaveByDate` / total completed
- `FirstPassYield`: inspections with result=Pass on first attempt / total inspections
- `ScrapRate`: parts with status=Scrapped / total parts through production
- `MachineUtilization`: sum of `StageExecution.ActualHours` / total shift hours available

### Step 5 — Analytics Main Dashboard
**File**: `Components/Pages/Analytics/Index.razor`

UI requirements (complete the existing stub):
- **Date Range Selector** at top (Today, Last 7 days, Last 30 days, Custom)
- **KPI Row** (6 cards):
  - Active Jobs (count)
  - On-Time Delivery % (with trend arrow vs. prior period)
  - First-Pass Yield % (with trend arrow)
  - OEE % (with trend arrow)
  - Open NCRs (count, red if > threshold)
  - Revenue Pipeline ($)
- **Production Output Chart**: bar chart - parts completed per day (last 30 days)
- **Stage Pipeline**: horizontal bar showing parts at each production stage
- **Machine Utilization**: bar chart per machine showing % utilized
- **Quality Trend**: line chart - first pass yield over time
- **Job Status Breakdown**: donut/pie chart

Blazor chart rendering options:
- Use `<canvas>` element + Chart.js via JS interop (already has `site.js` pattern)
- OR write SVG-based charts in pure Blazor (simpler, no extra dependencies)

Recommendation: SVG-based simple charts for production charts (no external dependency).
Example SVG bar chart pattern to implement in a `BarChart.razor` component:
```razor
<svg viewBox="0 0 400 200">
    @foreach (var (bar, i) in bars.Select((b, i) => (b, i)))
    {
        var x = i * barWidth + padding;
        var barH = bar.Value / maxValue * chartHeight;
        var y = chartHeight - barH + padding;
        <rect x="@x" y="@y" width="@(barWidth - 2)" height="@barH" fill="@color" />
        <text x="@(x + barWidth/2)" y="@(chartHeight + padding + 12)" text-anchor="middle">@bar.Label</text>
    }
</svg>
```

### Step 6 — On-Time Delivery Report
**New File**: `Components/Pages/Analytics/OnTimeDelivery.razor`
**Route**: `/analytics/on-time-delivery`

UI requirements:
- Date range filter + customer filter
- Summary: OTD % for period, trend vs. prior period
- Table: WO#, Customer, Due Date, Completed Date, Delta (days early/late), Status
- Late deliveries highlighted in red, early in green
- Export to CSV button

### Step 7 — Quality Metrics Report
**New File**: `Components/Pages/Analytics/QualityReport.razor`
**Route**: `/analytics/quality`

UI requirements:
- Date range filter + part filter
- KPI row: First Pass Yield, Scrap Rate, Open NCRs, Avg Cpk
- First pass yield trend chart (30 days)
- NCR by category pie chart (Material, Machine, Operator, Design)
- Scrap by part table: Part name, Total produced, Scrapped, Scrap rate %
- "View NCRs" button → links to quality module

### Step 8 — Machine OEE Dashboard
**New File**: `Components/Pages/Analytics/MachineOee.razor`
**Route**: `/analytics/oee`

UI requirements:
- Machine selector (All or individual)
- Date range filter
- OEE gauge per machine (large circular gauge: Availability × Performance × Quality)
- OEE component breakdown table: Machine, Availability %, Performance %, Quality %, OEE %
- OEE trend chart: daily OEE over selected period
- Downtime reasons breakdown: pie chart of delay categories

### Step 9 — Cost Analysis Report
**New File**: `Components/Pages/Analytics/CostAnalysis.razor`
**Route**: `/analytics/cost`

UI requirements:
- Filter: date range, customer, part
- Summary row: Total Quoted, Total Actual, Avg Variance %, Avg Margin %
- Table: Job#, Part, Customer, Quoted Cost, Actual Cost, Variance ($), Variance %, Margin %
- Color: green when actual < quoted, red when actual > quoted
- Profitability by customer bar chart

### Step 10 — Cross-Module Search
**New File**: `Components/Pages/Analytics/Search.razor`
**Route**: `/search`

UI requirements:
- Universal search bar in the main nav (all pages)
- Searches across: Work Orders, Jobs, Parts, NCRs, Customers, Serial Numbers
- Results grouped by entity type
- Keyboard shortcut: `Ctrl+K` opens search modal
- Recent searches stored in browser localStorage

Implementation:
- Add a `SearchService` that queries all relevant DbSets with `LIKE` patterns
- Return typed `SearchResult` records with entity type, id, display text, url
- Use Blazor component with `@onkeydown` to trigger modal

### Step 11 — Update Home Dashboard
**File**: `Components/Pages/Home.razor`

Replace current stub with live-wired KPI cards:
- Pull from `AnalyticsService` methods
- Loading spinners while data fetches
- Refresh on `StateHasChanged` via timer (every 60 seconds) for near-real-time feel

### Step 12 — EF Core Migration
```bash
dotnet ef migrations add AddAnalyticsModels --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Main analytics dashboard shows live KPI cards with real data
- [ ] On-time delivery % calculates correctly from completed job dates
- [ ] First pass yield % calculates from QC inspection results
- [ ] OEE chart shows availability, performance, quality components
- [ ] Cost analysis shows estimated vs. actual job costs with variance
- [ ] Stage pipeline visualization shows actual part counts at each stage
- [ ] Universal search finds records across Work Orders, Jobs, Parts, NCRs
- [ ] Date range filter changes all charts and tables simultaneously
- [ ] Analytics pages are accessible to Manager+ roles

---

## Dependencies

- **Module 02** (Work Orders) — Job completion dates, due dates
- **Module 04** (Shop Floor) — Stage execution times for OEE/utilization
- **Module 05** (Quality) — Inspection results for yield/scrap
- **Module 06** (Inventory) — Stock value for inventory KPIs
- **Module 09** (Job Costing) — Estimated vs. actual cost data

---

## Future Enhancements (Post-MVP)

- Natural language query: "Show me all jobs that ran over budget last month"
- Scheduled report email distribution (PDF snapshot of dashboard)
- Custom widget drag-and-drop dashboard builder with persistent layout
- Data export API (REST endpoint returning JSON/CSV) for external BI tools (Power BI, Tableau)
- Predictive analytics: forecast on-time delivery risk based on current schedule
