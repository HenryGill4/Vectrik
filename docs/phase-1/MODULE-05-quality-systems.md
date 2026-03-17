# Module 05: Quality Systems & Inspection (QMS)

## Status: [x] Complete
## Category: QMS
## Phase: 1 — Core Production Engine
## Priority: P1 - Critical

---

## Overview

The Quality Management System covers in-process inspection, non-conformance
reporting (NCR), corrective/preventive actions (CAPA), First Article Inspection
Reports (FAIR), and full traceability for AS9100, ISO 13485, and ITAR compliance.

**ProShop Improvements**: SPC charting (X-bar/R-charts, Cp/Cpk), automated control
limit calculations, trend alerts before out-of-spec, digital FAIR auto-population,
and supplier quality scorecards.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `QCInspection` model (basic pass/fail) | ✅ Exists | `Models/QCInspection.cs` |
| `QCChecklistItem` model | ✅ Exists | `Models/QCChecklistItem.cs` |
| `PartInstance` tracking model | ✅ Exists | `Models/PartInstance.cs` |
| `PartInstanceStatus` enum | ✅ Exists | `Models/Enums/ManufacturingEnums.cs` |
| QC stage view partial | ✅ Exists | `Components/Pages/ShopFloor/StageViews/QualityControl.razor` |

**Gap**: No NCR model, no CAPA workflow, no SPC data model, no FAIR model, no inspection plan templates, no certificate management.

---

## What Needs to Be Built

### 1. Database Models (New / Extended)
- Extend `QCInspection` with dimensional results, attachments, FAIR flag
- `InspectionPlan` + `InspectionPlanCharacteristic` — reusable templates
- `NonConformanceReport` (NCR) — defect capture with severity
- `CorrectiveAction` (CAPA) — root cause + remediation workflow
- `SpcDataPoint` — measurement history for statistical tracking
- `Certificate` — material certs, calibration certs, compliance docs

### 2. Service Layer (New)
- `QualityService` — inspection entry, NCR creation, CAPA management
- `SpcService` — SPC calculations (mean, UCL, LCL, Cp, Cpk)
- `FairService` — First Article Inspection Report generation
- `CertificateService` — cert storage and expiry alerting

### 3. UI Components (New)
- **Inspection Entry** — in-process measurement recording (embedded in shop floor)
- **NCR Management** — create, track, resolve non-conformances
- **CAPA Module** — root cause analysis, action items, verification
- **SPC Charts** — control charts with trend detection
- **FAIR Generator** — auto-populated first article report
- **Quality Dashboard** — first-pass yield, scrap rate, NCR trend

---

## Implementation Steps

### Step 1 — Extend QCInspection Model
**File**: `Models/QCInspection.cs`
Ensure/add:
```csharp
public int JobId { get; set; }
public Job Job { get; set; } = null!;
public int? PartInstanceId { get; set; }
public PartInstance? PartInstance { get; set; }
public int? InspectionPlanId { get; set; }
public InspectionPlan? InspectionPlan { get; set; }
public string InspectorUserId { get; set; } = string.Empty;
public bool IsFair { get; set; } = false;            // Is this a First Article?
public InspectionResult OverallResult { get; set; }  // Pass, Fail, Conditional
public string? Notes { get; set; }
public int? NonConformanceReportId { get; set; }     // linked NCR if failed
public DateTime InspectedAt { get; set; } = DateTime.UtcNow;
public ICollection<QCChecklistItem> ChecklistItems { get; set; } = new List<QCChecklistItem>();
public ICollection<InspectionMeasurement> Measurements { get; set; } = new List<InspectionMeasurement>();

public enum InspectionResult { Pass, Fail, Conditional, Pending }
```

### Step 2 — Create InspectionMeasurement Model
**New File**: `Models/InspectionMeasurement.cs`
```csharp
public class InspectionMeasurement
{
    public int Id { get; set; }
    public int QcInspectionId { get; set; }
    public QCInspection Inspection { get; set; } = null!;
    public string CharacteristicName { get; set; } = string.Empty;
    public string? DrawingCallout { get; set; }       // e.g., "Dia. A", "Dim. 5"
    public decimal NominalValue { get; set; }
    public decimal TolerancePlus { get; set; }
    public decimal ToleranceMinus { get; set; }
    public decimal ActualValue { get; set; }
    public decimal Deviation { get; set; }            // ActualValue - NominalValue
    public bool IsInSpec { get; set; }                // calculated
    public string? InstrumentUsed { get; set; }       // e.g., "CMM", "Micrometer"
    public string? GageId { get; set; }               // calibrated gage reference
}
```

### Step 3 — Create InspectionPlan + Characteristic Models
**New File**: `Models/InspectionPlan.cs`
```csharp
public class InspectionPlan
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public bool IsDefault { get; set; }
    public ICollection<InspectionPlanCharacteristic> Characteristics { get; set; } = new List<InspectionPlanCharacteristic>();
}

public class InspectionPlanCharacteristic
{
    public int Id { get; set; }
    public int InspectionPlanId { get; set; }
    public InspectionPlan InspectionPlan { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? DrawingCallout { get; set; }
    public decimal NominalValue { get; set; }
    public decimal TolerancePlus { get; set; }
    public decimal ToleranceMinus { get; set; }
    public string? InstrumentType { get; set; }
    public bool IsKeyCharacteristic { get; set; }     // KC = critical feature
    public int DisplayOrder { get; set; }
}
```

### Step 4 — Create NCR Model
**New File**: `Models/NonConformanceReport.cs`
```csharp
public class NonConformanceReport
{
    public int Id { get; set; }
    public string NcrNumber { get; set; } = string.Empty;    // Auto-generated: NCR-2024-0001
    public int? JobId { get; set; }
    public Job? Job { get; set; }
    public int? PartInstanceId { get; set; }
    public PartInstance? PartInstance { get; set; }
    public NcrType Type { get; set; }                        // InProcess, Incoming, Customer
    public string Description { get; set; } = string.Empty;
    public string? QuantityAffected { get; set; }
    public NcrSeverity Severity { get; set; }
    public NcrDisposition Disposition { get; set; }          // Rework, Scrap, UseAsIs, ReturnToVendor
    public NcrStatus Status { get; set; } = NcrStatus.Open;
    public string ReportedByUserId { get; set; } = string.Empty;
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public int? CorrectiveActionId { get; set; }
    public CorrectiveAction? CorrectiveAction { get; set; }
}

public enum NcrType { InProcess, IncomingMaterial, CustomerReturn, Audit }
public enum NcrSeverity { Minor, Major, Critical }
public enum NcrDisposition { Rework, Scrap, UseAsIs, ReturnToVendor, PendingReview }
public enum NcrStatus { Open, InReview, Dispositioned, Closed }
```

### Step 5 — Create CorrectiveAction Model
**New File**: `Models/CorrectiveAction.cs`
```csharp
public class CorrectiveAction
{
    public int Id { get; set; }
    public string CapaNumber { get; set; } = string.Empty;  // CAPA-2024-0001
    public CapaType Type { get; set; }                       // Corrective, Preventive
    public string ProblemStatement { get; set; } = string.Empty;
    public string? RootCauseAnalysis { get; set; }           // 5-Why or Fishbone text
    public string? ImmediateAction { get; set; }
    public string? LongTermAction { get; set; }
    public string? PreventiveAction { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? EffectivenessVerification { get; set; }
    public CapaStatus Status { get; set; } = CapaStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum CapaType { Corrective, Preventive }
public enum CapaStatus { Open, InProgress, PendingVerification, Closed }
```

### Step 6 — Create SpcDataPoint Model
**New File**: `Models/SpcDataPoint.cs`
```csharp
public class SpcDataPoint
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public string CharacteristicName { get; set; } = string.Empty;
    public decimal MeasuredValue { get; set; }
    public decimal NominalValue { get; set; }
    public decimal TolerancePlus { get; set; }
    public decimal ToleranceMinus { get; set; }
    public int? QcInspectionId { get; set; }
    public int? JobId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 7 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<InspectionPlan> InspectionPlans { get; set; }
public DbSet<InspectionPlanCharacteristic> InspectionPlanCharacteristics { get; set; }
public DbSet<InspectionMeasurement> InspectionMeasurements { get; set; }
public DbSet<NonConformanceReport> NonConformanceReports { get; set; }
public DbSet<CorrectiveAction> CorrectiveActions { get; set; }
public DbSet<SpcDataPoint> SpcDataPoints { get; set; }
```

### Step 8 — Create QualityService
**New File**: `Services/QualityService.cs`
**New File**: `Services/IQualityService.cs`

Key methods:
```csharp
// Inspection
Task<QCInspection> CreateInspectionFromPlanAsync(int jobId, int planId, string tenantCode);
Task<QCInspection> SaveInspectionResultsAsync(QCInspection inspection, string tenantCode);
Task<List<QCInspection>> GetInspectionHistoryAsync(int partId, string tenantCode);

// NCR
Task<NonConformanceReport> CreateNcrAsync(NonConformanceReport ncr, string tenantCode);
Task UpdateNcrAsync(NonConformanceReport ncr, string tenantCode);
Task<string> GenerateNcrNumberAsync(string tenantCode);  // NCR-{year}-{seq}
Task<List<NonConformanceReport>> GetOpenNcrsAsync(string tenantCode);

// CAPA
Task<CorrectiveAction> CreateCapaAsync(CorrectiveAction capa, string tenantCode);
Task UpdateCapaAsync(CorrectiveAction capa, string tenantCode);
Task<string> GenerateCapaNumberAsync(string tenantCode);

// SPC
Task RecordSpcDataPointAsync(SpcDataPoint point, string tenantCode);
Task<SpcChartData> GetSpcDataAsync(int partId, string characteristic, int sampleCount, string tenantCode);
```

### Step 9 — Create SpcService
**New File**: `Services/SpcService.cs`
**New File**: `Services/ISpcService.cs`

```csharp
public interface ISpcService
{
    SpcCalculationResult Calculate(List<decimal> values, decimal nominal, decimal toPlus, decimal toMinus);
}

public record SpcCalculationResult(
    decimal Mean,
    decimal StdDev,
    decimal Ucl,       // Upper Control Limit = Mean + 3*StdDev
    decimal Lcl,       // Lower Control Limit = Mean - 3*StdDev
    decimal Usl,       // Upper Spec Limit = Nominal + ToPlus
    decimal Lsl,       // Lower Spec Limit = Nominal - ToMinus
    decimal Cp,        // (USL-LSL) / (6*StdDev)
    decimal Cpk,       // min((USL-Mean)/(3*StdDev), (Mean-LSL)/(3*StdDev))
    bool HasOutOfControl  // any point outside UCL/LCL
);
```

### Step 10 — Quality Dashboard Page
**New File**: `Components/Pages/Quality/Dashboard.razor`
**Route**: `/quality`

UI requirements:
- KPI row: First Pass Yield %, Scrap Rate %, Open NCRs, Open CAPAs, Parts in Inspection
- **Trend Chart**: First pass yield over last 30 days (line chart)
- **NCR Summary Table**: recent NCRs with severity badges, status, days open
- **Part Quality Leaderboard**: parts with highest NCR count
- Quick links: "Log Inspection", "Create NCR", "CAPA Board"

### Step 11 — NCR Management Page
**New File**: `Components/Pages/Quality/Ncr.razor`
**Route**: `/quality/ncr`

UI requirements:
- Filter by status, severity, type, date range
- Table: NCR#, Part, Job, Type, Severity, Description (truncated), Status, Days Open, Actions
- "New NCR" button → create NCR modal
- Click row → NCR detail modal or page
- NCR detail: all fields editable by quality role, link to CAPA, disposition selector

### Step 12 — CAPA Board Page
**New File**: `Components/Pages/Quality/Capa.razor`
**Route**: `/quality/capa`

UI requirements:
- Kanban-style board: Open | In Progress | Pending Verification | Closed
- Each card: CAPA#, Problem (truncated), Owner, Due date (red if overdue)
- Click card → CAPA detail with all fields, root cause analysis text areas

### Step 13 — SPC Charts Page
**New File**: `Components/Pages/Quality/Spc.razor`
**Route**: `/quality/spc`

UI requirements:
- Part selector + characteristic selector
- **X-bar Control Chart**: plotted values, UCL/LCL lines, out-of-control points in red
- **Cp/Cpk display**: large numerical display with color (green ≥1.33, yellow 1.0-1.33, red <1.0)
- **R-Chart**: range between consecutive measurements
- Sample count slider (last 20, 30, 50, 100)

### Step 14 — Integrate QC into Shop Floor
**File**: `Components/Pages/ShopFloor/StageViews/QualityControl.razor`

Update to:
- Load `InspectionPlan` for the current job's part (default plan)
- Render each characteristic as an input field (nominal, tolerance, actual)
- Auto-calculate `IsInSpec` per measurement
- If any measurement out of spec → auto-prompt to create NCR
- "Save & Pass" and "Save & Fail" buttons
- On fail → create NCR and link to inspection

### Step 15 — EF Core Migration
```bash
dotnet ef migrations add AddQualitySystems --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Inspection plan can be created for a part with characteristics and tolerances
- [ ] During QC stage, inspection plan auto-loads with measurement entry fields
- [ ] Out-of-spec measurements are flagged in red
- [ ] Failing inspection auto-prompts NCR creation with job/part pre-filled
- [ ] NCR can be created, assigned disposition, and closed
- [ ] CAPA links to NCR with root cause and action items
- [ ] SPC chart renders with UCL/LCL and highlights out-of-control points
- [ ] Cp/Cpk calculated and displayed with green/yellow/red indicator
- [ ] Quality dashboard KPIs match actual inspection data
- [ ] NCR number and CAPA number auto-generated in sequence

---

## Dependencies

- **Module 02** (Work Orders) — Job reference on inspection/NCR
- **Module 04** (Shop Floor) — QC stage integration
- **Module 11** (Calibration/Maintenance) — Gage calibration status
- **Module 14** (Document Control) — Controlled inspection plans

---

## Future Enhancements (Post-MVP)

- Digital FAIR (First Article Inspection Report) auto-generation as PDF
- Supplier quality scorecards tied to incoming material NCRs (Module 12)
- AS9100 / ISO 13485 compliance checklist engine
- Integration with CMM output files (import actual values automatically)
- Automated trend alerts (email/notification when Cpk trending toward 1.0)
