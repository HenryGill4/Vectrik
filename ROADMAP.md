# OpCentrix V3 — Unified Implementation Roadmap

> **Created**: 2026-03-17
> **Updated**: 2026-03-19 — Added work-chunk execution system, collapsed completed stages.
> **Status**: IN PROGRESS — Phase 1A H6 (Cross-Cutting Wiring) + Phase 1D (Part System & Build Plate)
> **Purpose**: Single source of truth for ALL implementation work.
>
> **Execution system**: See `docs/chunks/QUEUE.md` for the ordered work queue.
> Each chunk in `docs/chunks/CHUNK-XX-*.md` is a self-contained unit of work
> sized to fit within a single AI agent session.
>
> **Supersedes**: `SPRINT_PLAN.md`, `OPCENTRIX_ARCHITECTURE_DECISIONS.md`,
> `docs/STAGED-IMPLEMENTATION-PLAN.md`, `docs/PART-SYSTEM-INTEGRATION-PLAN.md`,
> `sprints/SPRINT-*.md`. These files are in `archive/` for historical reference only.
>
> **Competitive analysis**: See `docs/SYSTEM-REVIEW-VS-PROSHOP.md` for the full
> feature-by-feature comparison with ProShop ERP.

---

## How This Roadmap Works

1. **AI agents**: Open `docs/chunks/QUEUE.md` — find the first unchecked `[ ]`
   chunk. Open that chunk file for your assignment. Each chunk is self-contained
   with files-to-read, tasks, and verification steps.
2. After completing a chunk, mark it `[x]` in `QUEUE.md` and fill in the
   "Files Modified" section at the bottom of the chunk file.
3. After completing all chunks in a stage, update the status badge in the Stage Map below.
4. This file is the **master sequencer**. Chunk files are the **detailed blueprints**.
   Module-specific plans remain in `docs/phase-N/MODULE-XX-*.md` for reference.
5. `docs/MASTER_CONTEXT.md` has the architecture patterns, model registry, service
   registry, and route registry. Keep it updated as you build.

---

## Current State (as of 2026-03-17)

### What Exists
- Multi-tenant architecture (platform + tenant DBs) — **working**
- Authentication & authorization — **working**
- 54 entity models, 45+ services, 65+ Razor pages — **built**
- SignalR real-time machine state — **working**
- PWA support — **working**
- Data seeding (demo tenant + admin) — **working**

### The Problem
Many pages marked "complete" are **functional scaffolding**, not production-ready:
- Missing form validation (required fields, format checks, numeric ranges)
- Missing confirmation dialogs on destructive actions (delete, status changes)
- Some pages use direct `DbContext` instead of service layer
- Missing empty state handling ("No items yet" messages)
- Inconsistent error handling (some try-catch, some not)
- Some features use spoof/mock data instead of real service calls
- Authorization checks inconsistent (some pages check roles, others don't)
- DLMS/customization cross-cutting patterns not wired into existing pages

### Undocumented Features (SLS-Specific)
These were built but not in the original staged plan. They are now officially
part of the product:
- **Build Planning**: `BuildJob`, `BuildPackage`, `BuildFileInfo`, `BuildJobPart`
  models + `IBuildPlanningService` + `/builds` page
- **Part Instance Tracking**: `PartInstance`, `PartInstanceStageLog` models +
  `IPartTrackerService` + `/tracking` page
- **Maintenance Module**: `MachineComponent`, `MaintenanceRule`,
  `MaintenanceWorkOrder`, `MaintenanceActionLog` models +
  `IMaintenanceService` + `/maintenance` pages

---

## Stage Map Overview

```
PHASE 1A — HARDENING (Production-readiness for existing code)
├── Stage H1: Admin Pages Hardening                    [COMPLETE]
├── Stage H2: Core Workflow Hardening (Quotes/WOs)     [COMPLETE]
├── Stage H3: Shop Floor & Scheduling Hardening        [COMPLETE]
├── Stage H4: Quality & Inventory Hardening            [COMPLETE]
├── Stage H5: Analytics, Builds & Tracking Hardening   [COMPLETE]
├── Stage H6: Cross-Cutting Wiring (Feature flags,     [NOT STARTED]  ← RESUME HERE
│             custom fields, numbering, workflows)

PHASE 1D — PART SYSTEM & BUILD PLATE ★ NEW ★
├── Stage PI: Part System Integration (Material FK,    [NOT STARTED]
│             PricingEngine, BOM, cleanup)
├── Stage BP: SLS Build Plate Multi-Part Flow          [NOT STARTED]
│             (Build-level stages, revision control,
│              slice-derived durations, part separation)

PHASE 1B — MISSING PHASE 1 MODULE
├── Stage 5:  Visual Work Instructions (Module 03)     [NOT STARTED]

PHASE 1C — FAIR & DLMS FOUNDATION
├── Stage 6F: AS9102 FAIR Forms (Module 05 extension)  [NOT STARTED]

PHASE 2 — OPERATIONAL DEPTH
├── Stage 9:  Job Costing & Financial (Module 09)      [NOT STARTED]
├── Stage 10: Time Clock & Labor (Module 13)           [NOT STARTED]
├── Stage 11: Cutting Tools & Fixtures (Module 10)     [NOT STARTED]
├── Stage 12: Calibration & CMMS (Module 11)           [NOT STARTED]
├── Stage 13: Purchasing & Vendors (Module 12)         [NOT STARTED]
├── Stage 14: Document Control (Module 14)             [NOT STARTED]
├── Stage 15: Cross-Module Integration Sprint          [NOT STARTED]

PHASE 3 — PLATFORM MATURITY
├── Stage 16: Shipping & Receiving (Module 15)         [NOT STARTED]
├── Stage 17: CRM & Contact Management (Module 16)    [NOT STARTED]
├── Stage 18: CMMC & Compliance (Module 17)            [NOT STARTED]
├── Stage 19: Training / LMS (Module 18)               [NOT STARTED]
├── Stage 20: API Layer & Customer Portal              [NOT STARTED]
├── Stage 21: DLMS Transaction Services                [NOT STARTED]

PHASE 4 — TESTING & POLISH
├── Stage 22: Unit & Integration Tests                 [NOT STARTED]
├── Stage 23: E2E Smoke Tests                          [NOT STARTED]
├── Stage 24: Performance & Security Audit             [NOT STARTED]
```

---

## PHASE 1A: HARDENING

> **Goal**: Every page that currently exists becomes truly production-ready.
> No new features — only fixing, validating, and connecting what's already built.

---

### Stage H1 — Admin Pages Hardening ✅
**Status**: COMPLETE — All admin pages hardened with validation, confirmation dialogs,
error handling, toast feedback, and service layer usage. Minor polish items (H1.11,
H1.12, H1.16, H1.20, H1.28, H1.32–H1.38) deferred or pending verification.

### Stage H2 — Core Workflow Hardening ✅
**Status**: COMPLETE — Quotes, work orders, RFQ inbox, and portal hardened.
Cross-cutting wiring items (number sequences H2.6/H2.20, custom fields H2.8/H2.22,
document templates H2.9/H2.13, workflows H2.25) deferred to H6 chunks.

### Stage H3 — Shop Floor & Scheduling Hardening ✅
**Status**: COMPLETE — All shop floor pages, 10 stage partials, scheduler Gantt +
capacity, and machine dashboard hardened. LogDelayPanel added to all partials.
Direct DbContext removed from Machines/Index and Scheduler.

### Stage H4 — Quality & Inventory Hardening ✅
**Status**: COMPLETE — Quality dashboard, NCR, CAPA, SPC, inventory dashboard and
items all hardened. Cross-cutting items (workflows H4.4, number sequences H4.6,
feature flags H4.20) deferred to H6 chunks.

### Stage H5 — Analytics, Builds, Tracking & Maintenance Hardening ✅
**Status**: COMPLETE — Analytics export added, build workflow replaced spoof data,
tracking scan mode added, maintenance WOs + rules hardened, home dashboard KPIs
linked. Stacking efficiency (H5.9) deferred.

---

### Stage H6 — Cross-Cutting Wiring
**Duration**: 2–3 days | **Prereqs**: H1–H5

Wire the customization infrastructure (feature flags, custom fields, numbering,
workflows, document templates) into all existing pages.

#### Feature Flags
- [x] H6.1 — Wrap every nav section in `NavMenu.razor` with `ITenantFeatureService.IsEnabled()` checks
- [x] H6.2 — Add "Module not enabled" guard page for every feature area (Inventory, Quality, Builds, etc.)
- [x] H6.3 — Gate DLMS-specific UI fields behind `Features.IsEnabled("dlms")`
- [x] H6.4 — Gate SLS-specific features (Builds, stacking) behind `Features.IsEnabled("sls")`

#### Custom Fields
- [x] H6.5 — Add `CustomFieldsEditor` to Quote create/edit forms
- [x] H6.6 — Add `CustomFieldsEditor` to Work Order create/edit forms
- [x] H6.7 — Add `CustomFieldsEditor` to Part create/edit forms (admin)
- [x] H6.8 — Add `CustomFieldsEditor` to NCR create form
- [x] H6.9 — Add `CustomFieldsEditor` to Inventory Item create/edit form
- [x] H6.10 — Display custom field values on detail/read pages

#### Number Sequences
- [x] H6.11 — Wire `INumberSequenceService.NextAsync("Quote")` into quote creation
- [x] H6.12 — Wire `INumberSequenceService.NextAsync("WorkOrder")` into WO creation
- [x] H6.13 — Wire `INumberSequenceService.NextAsync("NCR")` into NCR creation
- [x] H6.14 — Wire `INumberSequenceService.NextAsync("Job")` into job generation

#### Document Templates
- [x] H6.15 — Create default quote PDF template
- [x] H6.16 — Wire "Print/Export" button on quote detail page
- [x] H6.17 — Create default work order traveler template
- [x] H6.18 — Wire "Print Traveler" button on WO detail page

#### Workflow Engine
- [x] H6.19 — Wire WO release to workflow approval (when workflow defined)
- [x] H6.20 — Wire quote approval to workflow (when workflow defined)
- [x] H6.21 — Wire NCR disposition to workflow approval (when workflow defined)
- [x] H6.22 — Build `/admin/workflows` page for configuring approval chains

---

## PHASE 1D: PART SYSTEM & BUILD PLATE FLOW

> **Why now**: The Part model has 12 known disconnects (material FK, pricing engine,
> BOM) and the SLS build plate flow is the core differentiator vs ProShop ERP.
> Fixing these before adding new modules prevents compounding technical debt.
>
> **Absorbed from**: `docs/PART-SYSTEM-INTEGRATION-PLAN.md` (Phases A-E)

---

### Stage PI — Part System Integration
**Duration**: 3–5 sessions | **Prereqs**: H1–H5 complete

Fix the 12 disconnects between the Part model and the rest of the system.

#### PI-A: Part ↔ Material FK (data integrity)
- [ ] PI.1 — `Part.MaterialId` (int?) FK and `MaterialEntity` nav property already exist — verify migration applied
- [ ] PI.2 — Update `TenantDbContext.OnModelCreating` with FK relationship config
- [x] PI.3 — Fix `PricingEngineService` to use FK instead of string match, use `PartStageRequirement` overrides (EstimatedHours, HourlyRateOverride, MaterialCost)
- [ ] PI.4 — Update `Parts/Edit.razor` material dropdown to sync both `MaterialId` + `Material` string
- [ ] PI.5 — Update `PartService` queries to `.Include(p => p.MaterialEntity)`
- [x] PI.6 — Backfill `MaterialId` from `Material` string in `DataSeedingService`

#### PI-B: PricingEngine + Quote Accuracy
- [ ] PI.7 — Rewrite `PricingEngineService.CalculatePartCostAsync` to use `PartStageRequirement` data (EstimatedHours, HourlyRateOverride, SetupTimeMinutes, MaterialCost)
- [ ] PI.8 — Add `StageMaterialCost` + `SetupCost` fields to `PricingBreakdown`
- [ ] PI.9 — Verify `QuoteService.CalculateEstimatedCostAsync` uses corrected engine

#### PI-C: Cleanup + Audit Fixes
- [x] PI.10 — Mark `Part.RequiredStages` as `[Obsolete]`, remove `[Required]` (already done — verify)
- [x] PI.11 — Make `PartService.ValidatePartAsync` truly async, add duplicate PartNumber check
- [x] PI.12 — Fix `WorkOrders/Create.razor` to capture auth user instead of "System"
- [x] PI.13 — Set `Job.SlsMaterial` from `Part.Material` during job generation in `WorkOrderService`

#### PI-D: Downstream Usage + Part Cloning
- [ ] PI.14 — Add "Usage" tab to `Parts/Detail.razor` (active WOs, Jobs, Quotes, NCRs)
- [ ] PI.15 — Add `GetPartUsageSummaryAsync` to `IPartService` + `PartService`
- [ ] PI.16 — Add `ClonePartAsync` to `IPartService` + `PartService` (deep-copy routing, not history)
- [ ] PI.17 — Add Clone button + modal to `Parts/Detail.razor`

#### PI-E: Part ↔ Inventory BOM
- [ ] PI.18 — `PartBomItem` model already exists — verify migration applied
- [ ] PI.19 — Add BOM tab to `Parts/Edit.razor` (add inventory items/materials, qty per unit)
- [ ] PI.20 — Update `MaterialPlanningService` to check BOM on WO release

---

### Stage BP — SLS Build Plate Multi-Part Flow
**Duration**: 5–8 sessions | **Prereqs**: Stage PI complete

> **The manufacturing reality**: In SLS, multiple different parts from different
> work orders are packed onto one build plate. The build plate moves as a unit
> through SLS Printing → Depowdering → Wire EDM. After EDM cuts parts off the
> plate, individual parts split to their own routings (heat treat, finishing, etc.)

#### Build Plate Concept

```
                     BUILD-LEVEL STAGES (plate moves as unit)
                    ┌─────────┐    ┌──────────────┐    ┌──────────┐
  BuildPackage ──►  │   SLS   │──►│ Depowdering  │──►│ Wire EDM │
  (mixed parts)     │ Printing│    │              │    │ (cutoff) │
                    └─────────┘    └──────────────┘    └──────────┘
                                                             │
                         ┌───────────────────────────────────┤
                         │              PARTS SEPARATE       │
                         ▼                                   ▼
                   ┌───────────┐                      ┌───────────┐
                   │ Part A    │                      │ Part B    │
                   │ Heat Treat│                      │ Surface   │
                   │ CNC       │                      │ Finishing │
                   │ QC        │                      │ QC        │
                   │ Ship      │                      │ Ship      │
                   └───────────┘                      └───────────┘
```

#### BP-1: Model Changes

- [x] BP.1 — Add `BuildPackageRevision` model for revision control:
  ```
  BuildPackageRevision { Id, BuildPackageId, RevisionNumber, RevisionDate,
    ChangedBy, ChangeNotes, PartsSnapshotJson, ParametersSnapshotJson }
  ```
- [x] BP.2 — Add to `BuildPackage`:
  - `int? CurrentRevision` — current rev number
  - `string? BuildParameters` — JSON for build-level params (layer thickness, laser power, etc.)
- [x] BP.3 — Add to `StageExecution`:
  - `int? BuildPackageId` — nullable FK to link build-level executions to a BuildPackage
- [x] BP.4 — Add `IsBuildLevelStage` boolean to `ProductionStage` — marks stages where the
  build plate moves as a unit (SLS Printing, Depowdering, Wire EDM)
- [x] BP.5 — EF migration for all model changes
- [x] BP.6 — Seed `IsBuildLevelStage = true` for "sls-printing", "depowdering", "wire-edm" stages

#### BP-2: Duration from Slice File

- [x] BP.7 — SLS build duration comes from `BuildFileInfo.EstimatedPrintTimeHours` (the slicer's
  estimate), NOT from `Part.SlsBuildDurationHours`. When a BuildPackage has a BuildFileInfo,
  the stage execution's EstimatedHours = BuildFileInfo.EstimatedPrintTimeHours
- [x] BP.8 — Hours-per-part allocation: `BuildFileInfo.EstimatedPrintTimeHours / BuildPackage.TotalPartCount`
  (simple equal split — can refine to volume-weighted later)
- [x] BP.9 — Update `Part.SlsPerPartHours` computed property to check if part is in an active
  BuildPackage with a BuildFileInfo and use that duration instead of the part-level estimate
- [x] BP.10 — When BuildFileInfo is saved/updated, auto-update the BuildPackage's
  `EstimatedDurationHours` and all linked StageExecution EstimatedHours

#### BP-3: Build-Level Stage Execution

- [x] BP.11 — Update `IBuildPlanningService` with:
  - `Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int buildPackageId, string createdBy)`
    Creates StageExecutions for all build-level stages (SLS → Depowder → EDM) linked to the BuildPackage
  - `Task CreatePartStageExecutionsAsync(int buildPackageId, string createdBy)`
    After EDM, creates individual StageExecutions for each part's remaining routing stages
- [x] BP.12 — When BuildPackage status → "Scheduled": call `CreateBuildStageExecutionsAsync`
  - Creates one StageExecution per build-level stage, all linked to BuildPackageId
  - SLS execution gets EstimatedHours from BuildFileInfo
  - The Job on these executions can be a "build job" referencing the build package, not a single part
- [x] BP.13 — When the Wire EDM stage completes: call `CreatePartStageExecutionsAsync`
  - For each BuildPackagePart, look up the Part's routing (PartStageRequirements)
  - Skip build-level stages (SLS, Depowder, EDM — already done)
  - Create individual Jobs + StageExecutions for remaining stages per part
  - Link each Job to the WorkOrderLine via BuildPackagePart.WorkOrderLineId

#### BP-4: Build Revision Control

- [x] BP.14 — Add `CreateRevisionAsync(int buildPackageId, string changedBy, string? notes)` to
  `IBuildPlanningService` — snapshots current parts list and parameters to `BuildPackageRevision`
- [x] BP.15 — Auto-create revision when parts are added/removed from a BuildPackage
- [x] BP.16 — Auto-create revision when BuildFileInfo is updated (new slice file imported)
- [x] BP.17 — Add revision history display to `Builds/Index.razor` — expandable section
  showing all revisions with date, who, what changed

#### BP-5: Enhanced SLS Printing UI

- [x] BP.18 — Update `SLSPrinting.razor` to detect if execution has a BuildPackageId
  - If yes: show ALL parts in the build with quantities and WO references
  - If no: show single-part view (backward compatible)
- [x] BP.19 — Show build-level info: machine, material, estimated print time from slice,
  layer count, build height, powder estimate (all from BuildFileInfo)
- [x] BP.20 — Show which work orders each part belongs to (BuildPackagePart.WorkOrderLineId → WorkOrder.OrderNumber)
- [x] BP.21 — Add build progress tracking (layer count vs total, % complete)
- [x] BP.22 — When completing the SLS stage, advance the build to depowdering (not individual parts)

#### BP-6: Part Separation After EDM

- [x] BP.23 — When EDM stage completes, show a "Part Separation" confirmation UI:
  - List all parts that were on the build
  - Operator confirms each part is separated and accounted for
  - Creates individual PartInstance records with serial numbers
  - Triggers `CreatePartStageExecutionsAsync` for downstream routing
- [x] BP.24 — Handle partial separation: if some parts are damaged/scrapped during EDM,
  operator can mark them as failed → auto-create NCR
- [x] BP.25 — Update `PartInstance` tracking to link back to the originating BuildPackage

#### BP-7: Scheduling & Cost Integration

- [x] BP.26 — Update `Scheduler/Index.razor` Gantt to show build-level executions as a single
  block spanning all parts (not one bar per part)
- [x] BP.27 — Update `Scheduler/Capacity.razor` to account for build plate as single machine occupation
- [x] BP.28 — Build cost (powder, gas, laser time) allocated across parts for job costing:
  `partCost = buildCost * (partQty / totalPartsInBuild)`

#### Verification Checklist

After Stage BP is complete, verify:
1. Create a BuildPackage with 3 different parts from 2 different work orders
2. Import/set build file info (layer count, print time, powder)
3. Schedule the build → see StageExecutions created for SLS, Depowder, EDM
4. Start SLS stage → SLSPrinting.razor shows all 3 parts with WO references
5. Complete SLS → build advances to Depowdering (not individual parts)
6. Complete Depowdering → build advances to EDM
7. Complete EDM → part separation UI, individual jobs created for downstream stages
8. Each part follows its own routing (heat treat, finishing, QC, etc.)
9. Hours-per-part reflects slice time divided across parts
10. Build revision history tracks changes to part list

---

## PHASE 1B: MISSING PHASE 1 MODULE

### Stage 5 — Visual Work Instructions (Module 03)
**Duration**: 1–2 weeks | **Prereqs**: Stage H3 (Shop Floor hardened)
**Plan file**: `docs/phase-1/MODULE-03-visual-work-instructions.md`

| Step | Description |
|------|-------------|
| 5.1 | Create `WorkInstruction`, `WorkInstructionStep`, `WorkInstructionMedia`, `WorkInstructionRevision`, `OperatorFeedback` models |
| 5.2 | Add DbSets + relationships + migration |
| 5.3 | Create `IWorkInstructionService` / `WorkInstructionService` — CRUD, versioning, feedback |
| 5.4 | Build `/admin/work-instructions` — list all instructions by part+stage |
| 5.5 | Build `/admin/work-instructions/{id}/edit` — rich step editor with drag-reorder, image upload |
| 5.6 | Build `/shopfloor/instructions/{id}` — clean operator viewer (large images, clear steps) |
| 5.7 | Embed "View Instructions" button in stage execution views |
| 5.8 | Build operator feedback modal — "Flag this step" with reason |
| 5.9 | Build `/admin/work-instructions/feedback` — review flagged steps |
| 5.10 | Register service in DI |
| 5.11 | Add nav links (Admin: "Work Instructions", ShopFloor: contextual link) |

---

## PHASE 1C: FAIR & DLMS FOUNDATION

### Stage 6F — AS9102 FAIR Forms (Module 05 extension)
**Duration**: 1 week | **Prereqs**: Stage H4 (Quality hardened)

| Step | Description |
|------|-------------|
| 6F.1 | Create `FairForm1` (Part Number Accountability) model |
| 6F.2 | Create `FairForm2` (Product Accountability) model |
| 6F.3 | Create `FairForm3` (Characteristic Accountability) model |
| 6F.4 | Add DbSets + migration |
| 6F.5 | Extend `IQualityService` with FAIR generation methods |
| 6F.6 | Auto-populate FAIR from part routing + material certs + measurements |
| 6F.7 | Add `DocumentTemplate` for FAIR PDF in AS9102 format |
| 6F.8 | Add "Generate FAIR" button on part detail page (Quality tab) |
| 6F.9 | Gate behind `quality.require_fair` setting |

---

## PHASE 1 CHECKPOINT

After completing Phases 1A + 1B + 1C:
- Every existing page is production-ready with validation, error handling, and confirmations
- Custom fields, feature flags, numbering, and workflows are wired everywhere
- Visual work instructions let operators follow step-by-step guides
- AS9102 FAIR support for first-article inspection
- SLS and maintenance features are properly documented and gated
- **A shop can run daily operations entirely in the system**

---

## PHASE 2: OPERATIONAL DEPTH

Detailed implementation steps for each stage are in `docs/phase-2/MODULE-XX-*.md`.

### Stage 9 — Job Costing & Financial Data (Module 09)
**Duration**: 2–3 weeks | **Prereqs**: Phase 1 complete
**Plan file**: `docs/phase-2/MODULE-09-job-costing.md`

**Key deliverables**:
- `CostEntry`, `OverheadRate`, `LaborRate` models
- `IJobCostingService` — actual cost accumulation per job
- `IProfitabilityService` — estimated vs actual margin
- `/admin/rates` — labor rate + overhead rate configuration
- Enhanced `/analytics/cost` — job-level P&L
- Auto-log cost entries on stage completion

---

### Stage 10 — Time Clock & Labor Tracking (Module 13)
**Duration**: 2 weeks | **Prereqs**: Stage 9
**Plan file**: `docs/phase-2/MODULE-13-time-clock-labor.md`

**Key deliverables**:
- `TimeEntry`, `OperatorSkill`, `ShiftDefinition` models
- `ITimeClockService` — clock in/out, break tracking
- `/kiosk` — operator time clock (touch-friendly)
- `/labor/entries` — manager review
- `/admin/skills` — skill matrix
- Auto-log time from stage start/complete

---

### Stage 11 — Cutting Tools & Fixtures (Module 10)
**Duration**: 2 weeks | **Prereqs**: Phase 1 complete
**Plan file**: `docs/phase-2/MODULE-10-cutting-tools-fixtures.md`

**Key deliverables**:
- `CuttingTool`, `ToolInstance`, `ToolUsageLog`, `ToolKit`, `ToolKitItem`, `Fixture` models
- `IToolManagementService` — lifecycle, wear tracking, predictive alerts
- `/toolcrib` — dashboard with wear alerts
- `/toolcrib/tools/{id}` — instance detail
- Tool usage logging wired into stage execution

---

### Stage 12 — Calibration & Maintenance CMMS (Module 11)
**Duration**: 2 weeks | **Prereqs**: Phase 1 complete (builds on existing maintenance)
**Plan file**: `docs/phase-2/MODULE-11-calibration-maintenance.md`

**Key deliverables**:
- `GageEquipment`, `CalibrationRecord`, `MaintenanceRequest` models
- `ICalibrationService` — gage tracking, due dates, certificates
- `/maintenance/calibration` — calibration registry
- `/maintenance/request` — operator maintenance request form
- Block quality inspection with expired gages

---

### Stage 13 — Purchasing & Vendors (Module 12)
**Duration**: 2–3 weeks | **Prereqs**: Stage 9, Phase 1 complete
**Plan file**: `docs/phase-2/MODULE-12-purchasing-vendor.md`

**Key deliverables**:
- `Vendor`, `VendorItem`, `PurchaseOrder`, `PurchaseOrderLine`, `VendorScorecard` models
- `IPurchasingService` — PO lifecycle, approval, receiving
- `IVendorService` — vendor management, quality scorecards
- `/purchasing/orders` and `/purchasing/vendors` pages
- PO receiving → inventory transaction wiring
- Vendor NCR data → scorecard aggregation

---

### Stage 14 — Document Control (Module 14)
**Duration**: 2 weeks | **Prereqs**: Phase 1 complete
**Plan file**: `docs/phase-2/MODULE-14-document-control.md`

**Key deliverables**:
- `DocumentCategory`, `ControlledDocument`, `DocumentRevision`, `DocumentApproval`, `DocumentReadRecord` models
- `IDocumentControlService` — upload, approve, distribute, acknowledge
- `/documents` — library with category tree
- `/documents/my-acknowledgments` — operator pending reads
- Workflow engine for revision approval

---

### Stage 15 — Cross-Module Integration Sprint
**Duration**: 1–2 weeks | **Prereqs**: Stages 9–14

| Task | Description |
|------|-------------|
| 15.1 | Stage completion → auto-log cost entry + time entry + tool usage |
| 15.2 | NCR creation → auto-trigger vendor scorecard update if vendor-caused |
| 15.3 | PO receipt → auto-update inventory + notify material requestor |
| 15.4 | Calibration expiry → block inspection using expired gage |
| 15.5 | Document revision → require re-acknowledgment from operators |
| 15.6 | End-to-end smoke test: Quote → WO → Stage Execution → QC → Ship |
| 15.7 | Verify all number sequences generate correctly across modules |
| 15.8 | Verify all custom fields save/load correctly across modules |
| 15.9 | Verify feature flags properly hide/show all module UI |

---

## PHASE 2 CHECKPOINT

After Phase 2:
- Full job costing with estimated vs actual P&L
- Operator time clock with auto-logging
- Tool crib with wear tracking and predictive alerts
- Calibration registry with enforcement
- Purchase orders with vendor scorecards
- Controlled document management
- All cross-module integrations wired

---

## PHASE 3: PLATFORM MATURITY

Detailed steps in `docs/phase-3/MODULE-XX-*.md`.

### Stage 16 — Shipping & Receiving (Module 15)
**Duration**: 2 weeks | **Prereqs**: Phase 2 complete
**Plan file**: `docs/phase-3/MODULE-15-shipping-receiving.md`

**Key deliverables**: Shipment model, packing lists, BOL, CoC generation,
IUID labels (when DLMS enabled), fulfillment tracking back to WO.

### Stage 17 — CRM & Contact Management (Module 16)
**Duration**: 2 weeks | **Prereqs**: Phase 2 complete
**Plan file**: `docs/phase-3/MODULE-16-crm-contact.md`

**Key deliverables**: Customer/Contact models, activity logging, sales pipeline,
customer portal (login, order status, cert downloads, RFQ).

### Stage 18 — CMMC & Compliance (Module 17)
**Duration**: 2–3 weeks | **Prereqs**: Stage 14
**Plan file**: `docs/phase-3/MODULE-17-cmmc-compliance.md`

**Key deliverables**: Compliance frameworks (CMMC, AS9100, ISO), self-assessment,
audit access log, DFARS clause tracking, CUI marking.

### Stage 19 — Training / LMS (Module 18)
**Duration**: 2 weeks | **Prereqs**: Stage 14
**Plan file**: `docs/phase-3/MODULE-18-training-lms.md`

**Key deliverables**: Courses, lessons, enrollments, completions, knowledge base,
training requirements blocking unqualified operators.

### Stage 20 — API Layer & Customer Portal Polish
**Duration**: 2 weeks | **Prereqs**: All modules

**Key deliverables**: REST API controllers for key entities, API key auth per
tenant, webhook support, polished customer portal.

### Stage 21 — DLMS Transaction Services
**Duration**: 2–3 weeks | **Prereqs**: Stage 16, Stage 18

**Key deliverables**: `IDlmsService` (856 ASN generation), `IIuidService`
(UII codes + DoD registry), WAWF invoice export, MILSTRIP import, `/admin/dlms`
settings page, transaction log viewer.

---

## PHASE 3 CHECKPOINT

Full ProShop ERP parity + defense logistics + tenant self-service customization.

---

## PHASE 4: TESTING & POLISH

### Stage 22 — Unit & Integration Tests
- [ ] Create `OpCentrix.Tests` project (xUnit)
- [ ] Model validation tests (all 54 models)
- [ ] Service unit tests (all 45+ services)
- [ ] Service integration tests (DB round-trip)

### Stage 23 — E2E Smoke Tests
- [ ] Full lifecycle: Quote → WO → Job → Stage → QC → Ship
- [ ] Multi-tenant isolation verification
- [ ] Feature flag toggle verification
- [ ] DLMS workflow verification

### Stage 24 — Performance & Security Audit
- [ ] Load test with realistic data volumes
- [ ] SQL injection review (EF parameterization)
- [ ] XSS review (Blazor encoding)
- [ ] Auth bypass review
- [ ] File upload security (type validation, size limits, path traversal)

---

## Cross-Module Dependency Map

```
Module 08 (Parts/PDM) ✅
    └─► Module 01 (Quoting) ✅
    └─► Module 02 (Work Orders) ✅
    └─► Module 03 (Work Instructions) ← PHASE 1B
    └─► Module 05 (Quality) ✅

Module 02 (Work Orders) ✅
    └─► Module 04 (Shop Floor) ✅
    └─► Module 06 (Inventory) ✅
    └─► Module 09 (Job Costing) ← Phase 2
    └─► Module 15 (Shipping) ← Phase 3

Module 04 (Shop Floor) ✅
    └─► Module 09 (Job Costing) ← Phase 2
    └─► Module 10 (Tools) ← Phase 2
    └─► Module 13 (Time Clock) ← Phase 2

Module 05 (Quality) ✅
    └─► Module 11 (Maintenance/Cal) ← Phase 2
    └─► Module 12 (Purchasing) ← Phase 2
    └─► Module 14 (Documents) ← Phase 2

Module 06 (Inventory) ✅
    └─► Module 12 (Purchasing) ← Phase 2
    └─► Module 10 (Tools) ← Phase 2

Module 09 (Job Costing)
    └─► Module 07 (Analytics) ✅

Module 16 (CRM)
    └─► Module 01 (Quoting) ✅
    └─► Module 15 (Shipping) ← Phase 3
```

---

## Architecture Principles (Quick Reference)

These apply to ALL work. See `docs/MASTER_CONTEXT.md` for full details.

1. **Multi-tenant**: Services inject `TenantDbContext` via DI. Never use direct DbContext in pages.
2. **Service layer**: Every entity has `IXxxService` + `XxxService`. Register as `Scoped`.
3. **Feature flags**: Gate optional modules with `ITenantFeatureService.IsEnabled(key)`.
4. **Custom fields**: Every major entity has `CustomFieldValues` JSON column. Use `<CustomFieldsEditor>`.
5. **Number sequences**: Use `INumberSequenceService.NextAsync("EntityType")` for auto-numbering.
6. **Workflows**: Use `IWorkflowEngine` for approval chains.
7. **Document templates**: Use `IDocumentTemplateService` for printable outputs.
8. **DLMS fields**: Add defense-specific columns but only show when `dlms` feature enabled.
9. **Shared components**: Use `AppModal`, `ConfirmDialog`, `Pagination`, `ToastService` everywhere.
10. **CSS variables**: Use `--accent`, `--bg-card`, etc. Never hardcode colors.
11. **Migrations**: `dotnet ef migrations add AddXxx --context TenantDbContext --output-dir Data/Migrations/Tenant`

---

## File Reference

| What | Where |
|------|-------|
| This roadmap (START HERE) | `ROADMAP.md` |
| **Work queue (agent assignments)** | **`docs/chunks/QUEUE.md`** |
| Chunk execution guide | `docs/chunks/README.md` |
| Individual work chunks | `docs/chunks/CHUNK-XX-*.md` |
| ProShop competitive analysis | `docs/SYSTEM-REVIEW-VS-PROSHOP.md` |
| Architecture patterns & registries | `docs/MASTER_CONTEXT.md` |
| DLMS/customization architecture | `docs/DLMS-CUSTOMIZATION-ARCHITECTURE.md` |
| Module detail plans | `docs/phase-N/MODULE-XX-*.md` |
| DI registration | `Program.cs` |
| Tenant DB schema | `Data/TenantDbContext.cs` |
| All enums | `Models/Enums/ManufacturingEnums.cs` |
| Navigation | `Components/Layout/NavMenu.razor` |
| Styles | `wwwroot/css/site.css` |

---

## Archived / Superseded Documents

These files are in `archive/` (or root for legacy). Do **NOT** use for planning:

| File | Reason |
|------|--------|
| `SPRINT_PLAN.md` | Superseded by this roadmap |
| `OPCENTRIX_ARCHITECTURE_DECISIONS.md` | Phase tracker superseded |
| `docs/STAGED-IMPLEMENTATION-PLAN.md` | Superseded by this roadmap |
| `docs/PART-SYSTEM-INTEGRATION-PLAN.md` | Absorbed into Stage PI above |
| `sprints/SPRINT-01.md` through `SPRINT-11.md` | Historical sprint logs |
