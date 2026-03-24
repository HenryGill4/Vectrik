> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# Part System Integration Plan — Wiring the Part Model to the Full System

> **Purpose**: This document maps every disconnect between the `Part` model and the
> rest of the Opcentrix MES, then provides a step-by-step implementation plan to
> fix them. Follow the phases in order — each builds on the previous.

---

## Executive Summary

The Part model is the nucleus of the entire system — every module touches it.
After auditing all 18+ files that reference `Part`, there are **12 concrete
disconnects** that prevent data from flowing correctly between modules. This plan
fixes all of them across 4 phases.

### Current Data Flow (What Works ✅)
```
Part → PartStageRequirement → Job → StageExecution → Shop Floor ✅
Part → QuoteLine (PartId FK) ✅
Part → WorkOrderLine (PartId FK) → Job generation from routing ✅
Part → InspectionPlan (PartId FK) ✅
Part → NCR (PartId FK) ✅
Part → QCInspection (PartId FK) ✅
Part → SpcDataPoint (PartId FK) ✅
Part → PartDrawing / PartRevisionHistory / PartNote (PDM) ✅
Part → Edit.razor new create/edit page ✅
```

### Current Disconnects (What's Broken ❌)
```
1.  Part.Material is a free-text string — no FK to Material table
2.  PricingEngine ignores PartStageRequirement overrides (hours, rates, costs)
3.  No Part → InventoryItem link (no BOM / material reservation concept)
4.  RequiredStages JSON field is redundant with PartStageRequirements
5.  SLS batch duration fields are hardcoded stage names in the Part model
6.  WorkOrder/Create.razor hardcodes CreatedBy = "System"
7.  Job.SlsMaterial is an orphan string with no source
8.  Part.ManufacturingApproach defaults to "SLS-Based" (not generic)
9.  Part validation doesn't check for duplicate PartNumber
10. Admin/Parts.razor is stale and still linked from some flows
11. No Part cloning / templating capability
12. Part detail page doesn't show downstream usage (WOs, Quotes, Jobs)
```

---

## Phase A: Fix the Part ↔ Material Relationship (Model + Service)

**Why first**: Material cost flows into PricingEngine → Quotes → Job Costing.
Everything downstream is wrong if this link is broken.

### Current State
- `Part.Material` = `string` (e.g. `"Ti-6Al-4V Grade 5"`)
- `PricingEngineService` does: `Materials.FirstOrDefault(m => m.Name == part.Material)` — fragile string match
- `InventoryItem.MaterialId` = `int?` FK to Material ✅ (Inventory is linked properly)
- `Material.Id` exists as the authoritative PK

### Changes

#### A1. Add `MaterialId` FK to Part model
**File**: `Models/Part.cs`
```csharp
// Add nullable FK (nullable to avoid breaking existing data)
public int? MaterialId { get; set; }
public virtual Material? MaterialEntity { get; set; }

// Keep the existing string Material field for backward compat / display
// It becomes the "resolved material name" for display purposes
```

- Keep `Part.Material` (string) as a denormalized display field
- Add `Part.MaterialId` (int?) as the real FK
- Navigation property: `MaterialEntity` (not `Material` — name clash)

#### A2. Update TenantDbContext relationship
**File**: `Data/TenantDbContext.cs` — in `OnModelCreating`:
```csharp
modelBuilder.Entity<Part>()
    .HasOne(p => p.MaterialEntity)
    .WithMany()
    .HasForeignKey(p => p.MaterialId)
    .OnDelete(DeleteBehavior.SetNull);
```

#### A3. Update PricingEngineService to use FK
**File**: `Services/PricingEngineService.cs`
- Change material lookup from string-match to FK join
- Use `PartStageRequirement.EstimatedHours` (not `ProductionStage.DefaultDurationHours`)
- Use `PartStageRequirement.HourlyRateOverride` when present
- Use `PartStageRequirement.MaterialCost` per stage

```csharp
// Before (broken):
var estimatedMinutes = (req.ProductionStage?.DefaultDurationHours ?? 0) * 60;
var setupMinutes = req.ProductionStage?.DefaultSetupMinutes ?? 0;

// After (correct):
var estimatedHours = req.EstimatedHours ?? req.ProductionStage?.DefaultDurationHours ?? 0;
var estimatedMinutes = estimatedHours * 60;
var setupMinutes = req.SetupTimeMinutes ?? req.ProductionStage?.DefaultSetupMinutes ?? 0;
var hourlyRate = req.HourlyRateOverride ?? req.ProductionStage?.DefaultHourlyRate ?? laborRate;
```

#### A4. Update Part edit page to sync both fields
**File**: `Components/Pages/Parts/Edit.razor`
- When user selects from material dropdown → set both `MaterialId` and `Material` (string)
- Material string = display name; MaterialId = real FK

#### A5. Update PartService to include MaterialEntity
**File**: `Services/PartService.cs`
- Add `.Include(p => p.MaterialEntity)` to `GetPartByIdAsync`, `GetPartDetailAsync`, `GetAllPartsAsync`

#### A6. EF Migration
```bash
dotnet ef migrations add AddPartMaterialFk --context TenantDbContext --output-dir Data/Migrations/Tenant
```

#### A7. Data migration: backfill MaterialId from Material string
- In `DataSeedingService` or a one-time startup task:
```csharp
foreach (var part in parts.Where(p => p.MaterialId == null && !string.IsNullOrEmpty(p.Material)))
{
    var material = materials.FirstOrDefault(m => m.Name == part.Material);
    if (material != null) part.MaterialId = material.Id;
}
```

### Checklist
- [ ] A1: Add `MaterialId` + `MaterialEntity` to `Part.cs`
- [ ] A2: Configure relationship in `TenantDbContext.OnModelCreating`
- [ ] A3: Fix `PricingEngineService.CalculatePartCostAsync` to use FK + stage overrides
- [ ] A4: Update `Edit.razor` material dropdown to set both fields
- [ ] A5: Update `PartService` queries to `.Include(p => p.MaterialEntity)`
- [ ] A6: Run EF migration
- [ ] A7: Add backfill logic for existing parts

---

## Phase B: Fix PricingEngine + Quote Cost Accuracy

**Why**: Quotes currently use `ProductionStage` defaults, ignoring per-part overrides
that users carefully configure on the routing tab.

### Current State
`PricingEngineService.CalculatePartCostAsync` (line 28-35):
- Uses `req.ProductionStage?.DefaultDurationHours` → ignores `req.EstimatedHours`
- Uses `req.ProductionStage?.DefaultSetupMinutes` → ignores `req.SetupTimeMinutes`
- Ignores `req.HourlyRateOverride`
- Ignores `req.MaterialCost` per stage
- Doesn't use `req.CalculateTotalEstimatedCost()` helper that already exists

### Changes

#### B1. Rewrite PricingEngineService to use PartStageRequirement data
**File**: `Services/PricingEngineService.cs`

```csharp
public async Task<PricingBreakdown> CalculatePartCostAsync(int partId, int quantity = 1)
{
    var breakdown = new PricingBreakdown();

    var requirements = await _db.PartStageRequirements
        .Include(r => r.ProductionStage)
        .Where(r => r.PartId == partId && r.IsActive)
        .OrderBy(r => r.ExecutionOrder)
        .ToListAsync();

    foreach (var req in requirements)
    {
        var hours = req.GetEffectiveEstimatedHours(); // uses EMA learning or manual override
        var rate = req.GetEffectiveHourlyRate();       // uses override or stage default
        var setupMinutes = req.SetupTimeMinutes ?? req.ProductionStage?.DefaultSetupMinutes ?? 0;

        breakdown.TotalLaborMinutes += hours * 60;
        breakdown.TotalSetupMinutes += setupMinutes;
        breakdown.StageMaterialCost += req.MaterialCost; // per-stage material costs
    }

    // Labor cost
    var laborRate = await GetDefaultLaborRateAsync();
    breakdown.LaborCost = requirements.Sum(r =>
        (decimal)r.GetEffectiveEstimatedHours() * r.GetEffectiveHourlyRate());

    // Setup cost (uses labor rate)
    var setupHours = breakdown.TotalSetupMinutes / 60.0;
    breakdown.SetupCost = (decimal)setupHours * laborRate;

    // Raw material cost (from Part → Material FK)
    var part = await _db.Parts
        .Include(p => p.MaterialEntity)
        .FirstOrDefaultAsync(p => p.Id == partId);

    if (part?.MaterialEntity != null)
    {
        breakdown.MaterialCost = part.MaterialEntity.CostPerKg
            * (decimal)(part.EstimatedWeightKg ?? 0) * quantity;
    }

    // Stage material costs (tooling, consumables, etc.)
    breakdown.MaterialCost += breakdown.StageMaterialCost * quantity;

    // Overhead
    var overheadRate = await GetDefaultOverheadRateAsync();
    breakdown.OverheadCost = breakdown.LaborCost * (overheadRate / 100m);

    return breakdown;
}
```

#### B2. Add StageMaterialCost + SetupCost to PricingBreakdown
**File**: `Services/PricingEngineService.cs` (or wherever PricingBreakdown lives)
```csharp
public decimal StageMaterialCost { get; set; }  // per-stage material/consumable costs
public decimal SetupCost { get; set; }           // setup labor cost
```

#### B3. Update QuoteService.CalculateEstimatedCostAsync
Make sure it calls the corrected PricingEngine.

### Checklist
- [ ] B1: Rewrite `PricingEngineService.CalculatePartCostAsync`
- [ ] B2: Add missing fields to `PricingBreakdown`
- [ ] B3: Verify `QuoteService.CalculateEstimatedCostAsync` uses updated engine
- [ ] B4: Verify Quote edit page displays cost breakdown correctly

---

## Phase C: Clean Up Redundant Part Fields + Fix Audit Issues

### C1. Deprecate `RequiredStages` JSON field
**File**: `Models/Part.cs`
- `RequiredStages` (string JSON, line 108) is redundant with `PartStageRequirements` nav property
- **Don't remove** the column (would break migration history) — just stop reading/writing it
- Remove `[Required]` attribute so it doesn't fail validation
- Set default to `"[]"` and ignore it in all service code
- Add a `[Obsolete]` attribute

```csharp
[Obsolete("Use StageRequirements navigation property instead")]
[MaxLength(1000)]
public string RequiredStages { get; set; } = "[]";
```

### C2. Fix default ManufacturingApproach
**File**: `Models/Part.cs`
- Change default from `"SLS-Based"` to `"CNC Machining"` (more generic)
- The Edit.razor already has a proper dropdown with 13 options

### C3. Add Part number uniqueness validation
**File**: `Services/PartService.cs` — in `ValidatePartAsync`:
```csharp
// Check for duplicate PartNumber
var existing = await _db.Parts
    .Where(p => p.PartNumber == part.PartNumber && p.Id != part.Id)
    .AnyAsync();
if (existing)
    errors.Add($"Part number '{part.PartNumber}' already exists.");
```

Note: `ValidatePartAsync` must become truly async (currently returns `Task.FromResult`).

### C4. Fix WorkOrder Create.razor CreatedBy
**File**: `Components/Pages/WorkOrders/Create.razor`
- Line 183: `CreatedBy = "System"` → capture from auth claims
- Same pattern as Parts/Edit.razor: wrap in `<AuthorizeView>`, read `FullName` claim

### C5. Fix Job.SlsMaterial orphan field
**File**: `Services/WorkOrderService.cs` — `GenerateJobsForLineAsync`
- When creating Job, set `SlsMaterial = line.Part.Material` so the field is populated
- Long-term: Job should reference Part.MaterialId or Part.Material for display

### C6. Remove `[Required]` from `RequiredStages`
- Currently `[Required, MaxLength(1000)]` — the Required will cause validation failures
  if any code path doesn't set it (Edit.razor already sets it to `"[]"`)

### Checklist
- [ ] C1: Add `[Obsolete]` to `RequiredStages`, remove `[Required]`
- [ ] C2: Change `ManufacturingApproach` default to `"CNC Machining"`
- [ ] C3: Add duplicate PartNumber check in `ValidatePartAsync` (make it truly async)
- [ ] C4: Fix WO Create.razor to capture auth user instead of "System"
- [ ] C5: Set `Job.SlsMaterial` from Part data during job generation
- [ ] C6: Verify Edit.razor still works after changes

---

## Phase D: Add Downstream Usage + Part Cloning

### D1. Add "Usage" tab to Part Detail page
**File**: `Components/Pages/Parts/Detail.razor`
- New tab: **Usage** — shows everywhere this part is referenced
- Sections:
  - **Active Work Orders**: WO lines referencing this PartId (with status, qty, due date)
  - **Active Jobs**: Jobs for this part (status, stage, operator)
  - **Quotes**: QuoteLines referencing this PartId (status, price, date)
  - **Quality**: NCRs, Inspections, SPC data count for this part
  - **Inventory**: Any InventoryItems linked to this part's material

### D2. Add usage query methods to PartService
**File**: `Services/IPartService.cs` + `Services/PartService.cs`
```csharp
Task<PartUsageSummary> GetPartUsageSummaryAsync(int partId);
```

Returns a DTO:
```csharp
public class PartUsageSummary
{
    public List<WorkOrderLine> ActiveWorkOrderLines { get; set; } = new();
    public List<Job> ActiveJobs { get; set; } = new();
    public List<QuoteLine> RecentQuoteLines { get; set; } = new();
    public int NcrCount { get; set; }
    public int InspectionCount { get; set; }
    public int SpcDataPointCount { get; set; }
}
```

### D3. Part cloning
**File**: `Services/IPartService.cs` + `Services/PartService.cs`
```csharp
Task<Part> ClonePartAsync(int sourcePartId, string newPartNumber, string createdBy);
```

- Deep-copies: Part fields, PartStageRequirements, CustomFieldValues
- Does NOT copy: Drawings, RevisionHistory, Notes (those are history)
- Sets: new PartNumber, Revision = "A", CreatedBy, CreatedDate

**File**: `Components/Pages/Parts/Detail.razor`
- Add "📋 Clone" button next to Edit button
- Opens modal to set new PartNumber → calls ClonePartAsync → navigates to new part

### D4. Retire Admin/Parts.razor as primary editor
- The old `Admin/Parts.razor` stays for backward compat
- But remove any navigation that sends users there for create/edit
- It can remain as a simple admin list/quick-edit if needed

### Checklist
- [ ] D1: Add "Usage" tab to `Parts/Detail.razor`
- [ ] D2: Add `GetPartUsageSummaryAsync` to `IPartService` + `PartService`
- [ ] D3: Add `ClonePartAsync` to `IPartService` + `PartService`
- [ ] D4: Add Clone button + modal to `Parts/Detail.razor`
- [ ] D5: Verify no remaining navigation points go to Admin/Parts for create/edit

---

## Phase E: Part ↔ Inventory Link (BOM Foundation)

**Why**: When a Work Order is released, the system should know what raw materials
are needed and whether they're in stock. This requires linking Parts to InventoryItems.

### Current State
- `InventoryItem` has `MaterialId` FK → can link to same `Material` as Part
- But there's no direct Part → InventoryItem link
- No concept of a Bill of Materials (BOM) for a part

### Changes

#### E1. Add a simple BOM model
**File**: `Models/PartBomItem.cs` (NEW)
```csharp
public class PartBomItem
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public int? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    public int? MaterialId { get; set; }
    public Material? Material { get; set; }
    public decimal QuantityRequired { get; set; }
    public string UnitOfMeasure { get; set; } = "each";
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}
```

#### E2. Add DbSet + Nav property
- `TenantDbContext`: `public DbSet<PartBomItem> PartBomItems { get; set; }`
- `Part.cs`: `public virtual ICollection<PartBomItem> BomItems { get; set; }`

#### E3. Add BOM tab to Parts/Edit.razor
- New tab: **BOM / Materials** (shown after Routing tab)
- Add inventory items or materials to the part's BOM
- Quantity per part unit

#### E4. Wire into Material Planning
- When WO is released, `MaterialPlanningService` can check BOM items against inventory
- Future: auto-generate Material Requests for shortages

### Checklist
- [ ] E1: Create `PartBomItem` model
- [ ] E2: Add DbSet to `TenantDbContext`, nav property to `Part`
- [ ] E3: Add BOM tab to `Parts/Edit.razor`
- [ ] E4: Update `MaterialPlanningService` to check BOM on WO release
- [ ] E5: EF Migration

---

## Implementation Order

```
Phase A (Material FK)     ← Do first: fixes data integrity
Phase B (PricingEngine)   ← Do second: fixes quote accuracy
Phase C (Cleanup)         ← Do third: fixes audit trail + defaults
Phase D (Usage + Clone)   ← Do fourth: UX improvements
Phase E (BOM)             ← Do fifth: inventory integration
```

**Estimated effort**: Phases A-C are ~2-3 sessions. Phase D is ~1 session.
Phase E is ~1-2 sessions. Total: ~5-7 sessions.

---

## Files Touched per Phase

### Phase A
| File | Change |
|------|--------|
| `Models/Part.cs` | Add `MaterialId`, `MaterialEntity` |
| `Data/TenantDbContext.cs` | Configure FK relationship |
| `Services/PartService.cs` | Add `.Include(p => p.MaterialEntity)` |
| `Services/PricingEngineService.cs` | Use FK for material lookup |
| `Components/Pages/Parts/Edit.razor` | Sync MaterialId + Material string |
| `Data/Migrations/Tenant/` | New migration |

### Phase B
| File | Change |
|------|--------|
| `Services/PricingEngineService.cs` | Full rewrite of cost calc |
| `Services/IPricingEngineService.cs` | Add fields to PricingBreakdown |
| `Services/QuoteService.cs` | Verify integration |

### Phase C
| File | Change |
|------|--------|
| `Models/Part.cs` | Deprecate RequiredStages, fix defaults |
| `Services/PartService.cs` | Async validation, dupe check |
| `Components/Pages/WorkOrders/Create.razor` | Fix CreatedBy auth |
| `Services/WorkOrderService.cs` | Set Job.SlsMaterial |

### Phase D
| File | Change |
|------|--------|
| `Components/Pages/Parts/Detail.razor` | Add Usage tab, Clone button |
| `Services/IPartService.cs` | Add GetPartUsageSummaryAsync, ClonePartAsync |
| `Services/PartService.cs` | Implement both methods |

### Phase E
| File | Change |
|------|--------|
| `Models/PartBomItem.cs` | NEW model |
| `Models/Part.cs` | Add BomItems nav property |
| `Data/TenantDbContext.cs` | Add DbSet |
| `Components/Pages/Parts/Edit.razor` | Add BOM tab |
| `Services/MaterialPlanningService.cs` | Check BOM on WO release |
| `Data/Migrations/Tenant/` | New migration |

---

## Cross-Reference: Part Entity Consumers

Every service/page that reads Part data — these all benefit from the fixes:

| Consumer | How It Uses Part | Fixed By |
|----------|-----------------|----------|
| `PricingEngineService` | Material string match, ignores stage overrides | Phase A + B |
| `QuoteService` | Via PricingEngine for cost estimates | Phase B |
| `WorkOrderService.GenerateJobsForLineAsync` | Reads PartStageRequirements ✅ | Phase C (SlsMaterial) |
| `ShopFloor/Index.razor` | Displays Part.PartNumber from Job.Part ✅ | No change needed |
| `ShopFloor/Stage.razor` | Displays Part.PartNumber from Job.Part ✅ | No change needed |
| `QualityService` | NCR, Inspection by PartId ✅ | No change needed |
| `SpcService` | SpcDataPoint by PartId ✅ | No change needed |
| `AnalyticsService` | Aggregates across Part data ✅ | No change needed |
| `PartFileService` | Drawings by PartId ✅ | No change needed |
| `InventoryService` | No Part link currently | Phase E (BOM) |
| `MaterialPlanningService` | No Part BOM link | Phase E |
| `Scheduler/Index.razor` | Job.Part for display ✅ | No change needed |

---

## Validation: How to Confirm Each Phase Works

### After Phase A
1. Create a new Part → select material from dropdown → verify `MaterialId` is saved
2. Edit existing Part → material dropdown pre-selects correct material
3. Open a quote → add line with the part → estimated cost uses correct material cost

### After Phase B
1. Configure a Part with custom stage hours (e.g. 2.5hrs instead of default 1hr)
2. Create a quote with that part → verify estimated cost reflects the custom hours
3. Set HourlyRateOverride on a stage requirement → verify quote cost changes

### After Phase C
1. Create a Part with duplicate PartNumber → get validation error
2. Create a Work Order → verify CreatedBy shows your name (not "System")
3. Release WO → check generated Job.SlsMaterial matches Part.Material

### After Phase D
1. Open Part Detail → Usage tab shows active WOs, jobs, quotes referencing the part
2. Click "Clone" → enter new part number → verify clone has routing but no history

### After Phase E
1. Add BOM items to a Part (raw material + qty)
2. Release a Work Order → verify material planning flags shortages

---

*Created: Session following Parts system overhaul*
*Prerequisite: Parts/Edit.razor, Index.razor nav fix, Detail.razor nav fix (all done)*
