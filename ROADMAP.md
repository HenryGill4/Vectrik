# OpCentrix V3 — Unified Implementation Roadmap

> **Created**: 2026-03-17
> **Status**: IN PROGRESS — Phase 1 Hardening
> **Purpose**: Single source of truth for all implementation work. Supersedes
> `SPRINT_PLAN.md`, `OPCENTRIX_ARCHITECTURE_DECISIONS.md` phase tracker, and
> `docs/STAGED-IMPLEMENTATION-PLAN.md`.

---

## How This Roadmap Works

1. **AI agents**: Read this file first every session. Find the current phase,
   find the first unchecked `[ ]` task — that's where to resume.
2. After completing a task, mark it `[x]`.
3. After completing a full stage, update the status badge in the Stage Map.
4. Module-specific implementation details remain in `docs/phase-N/MODULE-XX-*.md`.
   This file is the **master sequencer** — those files are the **detailed blueprints**.
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
├── Stage H1: Admin Pages Hardening                    [NOT STARTED]
├── Stage H2: Core Workflow Hardening (Quotes/WOs)     [NOT STARTED]
├── Stage H3: Shop Floor & Scheduling Hardening        [NOT STARTED]
├── Stage H4: Quality & Inventory Hardening            [NOT STARTED]
├── Stage H5: Analytics, Builds & Tracking Hardening   [NOT STARTED]
├── Stage H6: Cross-Cutting Wiring (Feature flags,     [NOT STARTED]
│             custom fields, numbering, workflows)

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

### Stage H1 — Admin Pages Hardening
**Duration**: 2–3 days | **Prereqs**: None

Every admin page needs: form validation, confirmation dialogs, error handling,
toast feedback, proper service layer usage (no direct DbContext).

#### Admin/Machines.razor
- [ ] H1.1 — Replace direct `DbContext` injection with `IMachineService` (create if needed)
- [ ] H1.2 — Add required field validation (Name, Type required)
- [ ] H1.3 — Add `ConfirmDialog` before machine deletion
- [ ] H1.4 — Add numeric range validation for build volume fields
- [ ] H1.5 — Add edit/delete for machine components (currently add-only)
- [ ] H1.6 — Add try-catch with `Toast.ShowError()` around all save operations
- [ ] H1.7 — Add empty state ("No machines configured yet")

#### Admin/Parts.razor
- [ ] H1.8 — Add required field validation (Part Number, Name)
- [ ] H1.9 — Add `ConfirmDialog` before deleting stage requirements
- [ ] H1.10 — Add numeric validation for stack counts (min 1, max reasonable)
- [ ] H1.11 — Validate stacking config (at least one stack type enabled if SLS)
- [ ] H1.12 — Add duplicate part number check on create

#### Admin/Users.razor
- [ ] H1.13 — Add password strength validation (min 8 chars, mixed case)
- [ ] H1.14 — Add `ConfirmDialog` before user deletion
- [ ] H1.15 — Prevent deletion of last admin user
- [ ] H1.16 — Add visual feedback for stage assignment toggles
- [ ] H1.17 — Validate email format

#### Admin/Materials.razor
- [ ] H1.18 — Add required field validation (Name, Category)
- [ ] H1.19 — Add `ConfirmDialog` before deletion
- [ ] H1.20 — Replace raw ID input for compatible materials with searchable dropdown
- [ ] H1.21 — Add try-catch error handling

#### Admin/Settings.razor
- [ ] H1.22 — Add key format validation (lowercase, dots, no spaces)
- [ ] H1.23 — Add setting descriptions/tooltips explaining each setting
- [ ] H1.24 — Add `ConfirmDialog` before deletion
- [ ] H1.25 — Add type-aware value editing (toggle for booleans, number input for numerics)

#### Admin/Features.razor
- [ ] H1.26 — Add `ConfirmDialog` before disabling a feature ("This will hide X module for all users")
- [ ] H1.27 — Show dependent features (disabling inventory should warn about purchasing)

#### Admin/CustomFields.razor
- [ ] H1.28 — Add field preview (show how the field will render)
- [ ] H1.29 — Add `ConfirmDialog` before deleting a field ("Existing data for this field will be hidden")

#### Admin/Numbering.razor
- [ ] H1.30 — Add validation that counter value doesn't exceed digit capacity
- [ ] H1.31 — Hide numbering configs for entities that don't exist yet (PurchaseOrder, Vendor, Shipment) — gate behind feature flags
- [ ] H1.32 — Add warning when changing prefix on existing sequences

#### Admin/Branding.razor
- [ ] H1.33 — Replace raw logo URL with file upload using `FileUpload` component
- [ ] H1.34 — Add CAGE code validation (exactly 5 alphanumeric chars)
- [ ] H1.35 — Add DoDAAC validation (6 chars)
- [ ] H1.36 — Add DUNS validation (9 digits)

#### Admin/Stages.razor (already PRODUCTION — verify only)
- [ ] H1.37 — Verify deletion confirmation exists
- [ ] H1.38 — Verify custom field JSON is validated before save

---

### Stage H2 — Core Workflow Hardening (Quotes, Work Orders, Portal)
**Duration**: 2–3 days | **Prereqs**: None

#### Quotes/Index.razor (already PRODUCTION — verify only)
- [ ] H2.1 — Verify pagination works correctly with filters
- [ ] H2.2 — Add bulk status actions (multi-select for archiving expired quotes)

#### Quotes/Edit.razor
- [ ] H2.3 — Add required field validation (Customer Name, at least one line item)
- [ ] H2.4 — Add numeric validation on quantities and costs (> 0, max reasonable value)
- [ ] H2.5 — Add unsaved changes warning on navigation away
- [ ] H2.6 — Wire `INumberSequenceService` for auto-generated quote numbers
- [ ] H2.7 — Wire `IPricingEngineService` for live cost recalculation on line item changes
- [ ] H2.8 — Add `CustomFieldsEditor` integration for quote-level custom fields
- [ ] H2.9 — Wire `IDocumentTemplateService` for quote PDF generation

#### Quotes/Details.razor
- [ ] H2.10 — Add `ConfirmDialog` before "Accept & Convert to WO" (irreversible)
- [ ] H2.11 — Add `ConfirmDialog` before "Reject"
- [ ] H2.12 — Wire quote revision history display
- [ ] H2.13 — Add print/export button using document template

#### Quotes/RfqInbox.razor
- [ ] H2.14 — Add "Convert to Quote" flow (pre-populate quote from RFQ data)
- [ ] H2.15 — Add "Decline with reason" modal
- [ ] H2.16 — Add RFQ detail view modal

#### WorkOrders/Index.razor (already PRODUCTION — verify only)
- [ ] H2.17 — Verify Kanban drag-and-drop or status change works
- [ ] H2.18 — Verify fulfillment progress calculation is accurate

#### WorkOrders/Create.razor
- [ ] H2.19 — Add required field validation (Customer, at least one line)
- [ ] H2.20 — Wire `INumberSequenceService` for auto-generated WO numbers
- [ ] H2.21 — Add part selection dropdown with search (not just ID)
- [ ] H2.22 — Add `CustomFieldsEditor` integration
- [ ] H2.23 — Add "Create from Quote" pre-population flow

#### WorkOrders/Details.razor
- [ ] H2.24 — Add `ConfirmDialog` before status changes (Release, Cancel, Close)
- [ ] H2.25 — Wire `IWorkflowEngine` for release approval workflow
- [ ] H2.26 — Add comment CRUD (edit, delete own comments)
- [ ] H2.27 — Show job generation status after release
- [ ] H2.28 — Wire material reservation display from inventory

#### WorkOrders/JobDetail.razor
- [ ] H2.29 — Add stage timeline visualization (progress through routing)
- [ ] H2.30 — Add reschedule button per stage execution
- [ ] H2.31 — Wire delay log display

#### Portal/Rfq.razor (already PRODUCTION — verify only)
- [ ] H2.32 — Verify file attachment works for drawings
- [ ] H2.33 — Add CAPTCHA or rate limiting for spam prevention

---

### Stage H3 — Shop Floor & Scheduling Hardening
**Duration**: 2–3 days | **Prereqs**: None

#### ShopFloor/Index.razor
- [ ] H3.1 — Add `ConfirmDialog` before "Complete" and "Fail" actions
- [ ] H3.2 — Add reason/notes modal for "Fail" action (require failure reason)
- [ ] H3.3 — Add reason/notes modal for "Pause" action
- [ ] H3.4 — Wire elapsed time display to real-time updates (timer)
- [ ] H3.5 — Add empty state ("No work assigned to you")
- [ ] H3.6 — Wire stage-specific partials to load based on stage type
- [ ] H3.7 — Add "View Work Instructions" button (link to Module 03 when built)

#### ShopFloor/Stage.razor
- [ ] H3.8 — Add proper queue sorting (priority, then due date)
- [ ] H3.9 — Wire start/complete/fail buttons to `IStageService`
- [ ] H3.10 — Add delay logging UI when stage takes longer than estimated

#### ShopFloor/Partials/*.razor (10 partials)
- [ ] H3.11 — Verify each partial binds to real stage execution data
- [ ] H3.12 — SLSPrinting: Wire build package selection, layer tracking
- [ ] H3.13 — CNCMachining: Wire setup/runtime fields, program number display
- [ ] H3.14 — QualityControl: Wire inspection form to `IQualityService`
- [ ] H3.15 — Shipping: Wire packing/shipping form to work order fulfillment
- [ ] H3.16 — GenericStage: Ensure custom fields render via `CustomFieldsEditor`
- [ ] H3.17 — All partials: Add "Log Delay" button with reason picker

#### Scheduler/Index.razor (already PRODUCTION — verify only)
- [ ] H3.18 — Verify Gantt renders from real job/stage data (not mock)
- [ ] H3.19 — Verify reschedule drag-and-drop updates database
- [ ] H3.20 — Add conflict detection alert when double-booking a machine

#### Scheduler/Capacity.razor
- [ ] H3.21 — Verify capacity bars use real machine hours vs scheduled hours
- [ ] H3.22 — Add date range selector for capacity view
- [ ] H3.23 — Add drill-down (click machine bar → see scheduled jobs)

#### Machines/Index.razor
- [ ] H3.24 — Replace direct `DbContext` with service layer
- [ ] H3.25 — Add click-through to machine detail or admin edit
- [ ] H3.26 — Wire SignalR state updates to live-refresh status badges
- [ ] H3.27 — Add machine action buttons (acknowledge alarm, view history)

---

### Stage H4 — Quality & Inventory Hardening
**Duration**: 2 days | **Prereqs**: None

#### Quality/Dashboard.razor (already PRODUCTION — verify only)
- [ ] H4.1 — Verify KPIs calculate from real data
- [ ] H4.2 — Add clickable KPIs (FPY → quality report, NCR count → NCR list)

#### Quality/Ncr.razor
- [ ] H4.3 — Add NCR creation form with required fields (Part, Type, Severity, Description)
- [ ] H4.4 — Wire disposition workflow using `IWorkflowEngine`
- [ ] H4.5 — Add `ConfirmDialog` before disposition (Use As-Is, Rework, Scrap, Return to Vendor)
- [ ] H4.6 — Wire `INumberSequenceService` for auto-generated NCR numbers
- [ ] H4.7 — Add file attachment support (photos of defects)
- [ ] H4.8 — Add "Create CAPA" button from NCR

#### Quality/Capa.razor
- [ ] H4.9 — Add CAPA creation form with required fields
- [ ] H4.10 — Wire Kanban board (Open → In Progress → Verified → Closed)
- [ ] H4.11 — Add due date tracking with overdue highlighting
- [ ] H4.12 — Add effectiveness verification step

#### Quality/Spc.razor
- [ ] H4.13 — Wire SPC chart rendering with real measurement data
- [ ] H4.14 — Add part/characteristic selector
- [ ] H4.15 — Display Cp/Cpk calculations
- [ ] H4.16 — Add out-of-control alerts (Nelson rules or Western Electric rules)

#### Inventory/Dashboard.razor (already PRODUCTION — minor polish)
- [ ] H4.17 — Add clickable low-stock alerts → navigate to item detail
- [ ] H4.18 — Add reorder suggestion → "Create Material Request" action

#### Inventory/Items.razor (already PRODUCTION — verify)
- [ ] H4.19 — Verify delete has confirmation dialog
- [ ] H4.20 — Wire feature flags for DLMS fields (GFM/GFE) — hide when `dlms.gfm` is off

---

### Stage H5 — Analytics, Builds, Tracking & Maintenance Hardening
**Duration**: 2 days | **Prereqs**: None

#### Analytics/* (mostly PRODUCTION — minor polish)
- [ ] H5.1 — Verify all analytics pages use date range consistently
- [ ] H5.2 — Add export/download button for each report (CSV or PDF)
- [ ] H5.3 — Analytics/Search: Add search scope indicator and result count

#### Builds/Index.razor
- [ ] H5.4 — Remove "Generate Spoof Build Data" button (replace with real workflow)
- [ ] H5.5 — Wire build creation to real `IBuildPlanningService`
- [ ] H5.6 — Add build status workflow (Planning → Ready → Running → Complete)
- [ ] H5.7 — Add part assignment to build packages
- [ ] H5.8 — Add validation on build creation form (machine required, at least one part)
- [ ] H5.9 — Wire stacking efficiency display from `PartService.CalculateStackEfficiency`

#### Tracking/Index.razor (already PRODUCTION — verify)
- [ ] H5.10 — Verify search returns real part instance data
- [ ] H5.11 — Add barcode scan input mode (focus on scan field)

#### Maintenance/Index.razor
- [ ] H5.12 — Add drill-down from alert → create maintenance work order
- [ ] H5.13 — Add action buttons (Acknowledge, Schedule, Complete)

#### Maintenance/WorkOrders.razor
- [ ] H5.14 — Add work order creation form with required fields
- [ ] H5.15 — Add status workflow (Open → Assigned → In Progress → Complete)
- [ ] H5.16 — Wire action log (track what was done during maintenance)

#### Maintenance/Rules.razor
- [ ] H5.17 — Add `ConfirmDialog` before rule deletion
- [ ] H5.18 — Add rule preview ("This will trigger every 500 hours or 30 days")

#### Home.razor (Dashboard)
- [ ] H5.19 — Verify all KPI cards link to their respective detail pages
- [ ] H5.20 — Add recent activity feed (last 10 actions across all modules)
- [ ] H5.21 — Add quick-action buttons (New Quote, New WO, Clock In)

---

### Stage H6 — Cross-Cutting Wiring
**Duration**: 2–3 days | **Prereqs**: H1–H5

Wire the customization infrastructure (feature flags, custom fields, numbering,
workflows, document templates) into all existing pages.

#### Feature Flags
- [ ] H6.1 — Wrap every nav section in `NavMenu.razor` with `ITenantFeatureService.IsEnabled()` checks
- [ ] H6.2 — Add "Module not enabled" guard page for every feature area (Inventory, Quality, Builds, etc.)
- [ ] H6.3 — Gate DLMS-specific UI fields behind `Features.IsEnabled("dlms")`
- [ ] H6.4 — Gate SLS-specific features (Builds, stacking) behind `Features.IsEnabled("sls")`

#### Custom Fields
- [ ] H6.5 — Add `CustomFieldsEditor` to Quote create/edit forms
- [ ] H6.6 — Add `CustomFieldsEditor` to Work Order create/edit forms
- [ ] H6.7 — Add `CustomFieldsEditor` to Part create/edit forms (admin)
- [ ] H6.8 — Add `CustomFieldsEditor` to NCR create form
- [ ] H6.9 — Add `CustomFieldsEditor` to Inventory Item create/edit form
- [ ] H6.10 — Display custom field values on detail/read pages

#### Number Sequences
- [ ] H6.11 — Wire `INumberSequenceService.NextAsync("Quote")` into quote creation
- [ ] H6.12 — Wire `INumberSequenceService.NextAsync("WorkOrder")` into WO creation
- [ ] H6.13 — Wire `INumberSequenceService.NextAsync("NCR")` into NCR creation
- [ ] H6.14 — Wire `INumberSequenceService.NextAsync("Job")` into job generation

#### Document Templates
- [ ] H6.15 — Create default quote PDF template
- [ ] H6.16 — Wire "Print/Export" button on quote detail page
- [ ] H6.17 — Create default work order traveler template
- [ ] H6.18 — Wire "Print Traveler" button on WO detail page

#### Workflow Engine
- [ ] H6.19 — Wire WO release to workflow approval (when workflow defined)
- [ ] H6.20 — Wire quote approval to workflow (when workflow defined)
- [ ] H6.21 — Wire NCR disposition to workflow approval (when workflow defined)
- [ ] H6.22 — Build `/admin/workflows` page for configuring approval chains

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
| This roadmap | `ROADMAP.md` |
| Architecture patterns & registries | `docs/MASTER_CONTEXT.md` |
| DLMS/customization architecture | `docs/DLMS-CUSTOMIZATION-ARCHITECTURE.md` |
| Module detail plans | `docs/phase-N/MODULE-XX-*.md` |
| DI registration | `Program.cs` |
| Tenant DB schema | `Data/TenantDbContext.cs` |
| All enums | `Models/Enums/ManufacturingEnums.cs` |
| Navigation | `Components/Layout/NavMenu.razor` |
| Styles | `wwwroot/css/site.css` |

---

*This roadmap supersedes `SPRINT_PLAN.md` and the phase tracker in
`OPCENTRIX_ARCHITECTURE_DECISIONS.md`. Those files are retained for historical
reference but should not be used for planning.*
