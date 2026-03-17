# Opcentrix V3 — Staged Implementation Plan

> **Purpose**: This document defines the exact build order, stage-by-stage,
> for implementing all 18 modules. Each stage is 2–4 weeks of work and
> produces a testable, shippable increment. MES features are layered on top
> of ERP features in the correct dependency order.

---

## Build Philosophy

1. **Data models first** — every stage starts by adding/extending models and
   generating a migration before writing any service or UI code.
2. **Services before UI** — business logic is testable independent of Blazor.
3. **Horizontal slices** — each stage delivers a thin, end-to-end feature
   (model → service → page) rather than building all models, then all services,
   then all pages.
4. **ERP feeds MES** — ERP modules (Quotes, WOs, Inventory) create the data
   that MES modules (Shop Floor, Instructions, Quality) consume on the floor.
5. **Foundation modules first** — Parts/PDM provides the part routing that
   every downstream module reads.

---

## Stage Map Overview

```
PHASE 0 — Customization & DLMS Foundation
├── Stage 0.5: Tenant Customization Infrastructure     ← MUST be built first

PHASE 1 — Core Production Engine
├── Stage 1: Parts/PDM Enhancement (Module 08)         ← data foundation
├── Stage 2: Estimating & Quoting (Module 01)          ← ERP entry point
├── Stage 3: Work Order Management (Module 02)         ← ERP→MES bridge
├── Stage 4: Shop Floor & Scheduling (Module 04)       ← MES core
├── Stage 5: Visual Work Instructions (Module 03)      ← MES operator UX
├── Stage 6: Quality Systems / QMS (Module 05)         ← QMS core
├── Stage 7: Inventory Control (Module 06)             ← ERP support for MES
├── Stage 8: Reporting & Analytics (Module 07)         ← cross-cutting insights

PHASE 2 — Operational Depth
├── Stage 9:  Job Costing & Financial (Module 09)      ← ERP financials
├── Stage 10: Time Clock & Labor (Module 13)           ← MES labor tracking
├── Stage 11: Cutting Tools & Fixtures (Module 10)     ← MES tool management
├── Stage 12: Calibration & Maintenance (Module 11)    ← QMS equipment
├── Stage 13: Purchasing & Vendors (Module 12)         ← ERP supply chain
├── Stage 14: Document Control (Module 14)             ← QMS compliance docs

PHASE 2.5 — Integration Sprint
├── Stage 15: Cross-module wiring, edge cases, E2E tests

PHASE 3 — Platform Maturity
├── Stage 16: Shipping & Receiving (Module 15)         ← ERP logistics
├── Stage 17: CRM & Contact Management (Module 16)    ← ERP sales
├── Stage 18: CMMC & Compliance (Module 17)            ← QMS compliance
├── Stage 19: Training / LMS (Module 18)               ← QMS knowledge
├── Stage 20: API Layer & Customer Portal              ← platform maturity
├── Stage 21: DLMS Transaction Services                ← defense logistics
```

---

## PHASE 0: Customization & DLMS Foundation

### Stage 0.5 — Tenant Customization Infrastructure
**Duration**: 2 weeks | **Prereqs**: Foundation ✅
**Why first**: Every downstream module uses feature flags, custom fields, and
configurable number sequences. Building this infrastructure before Stage 1
means every entity we create from day one supports customer customization.

> **Full architecture**: See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md)

| Step | Description | Files |
|------|-------------|-------|
| 0.1 | Create `TenantFeatureFlag` model (Id, TenantCode, FeatureKey, IsEnabled, EnabledAt) | `Models/Platform/TenantFeatureFlag.cs` |
| 0.2 | Add `DbSet<TenantFeatureFlag>` to `PlatformDbContext` | `Data/PlatformDbContext.cs` |
| 0.3 | Generate platform migration: `dotnet ef migrations add AddFeatureFlags --context PlatformDbContext --output-dir Data/Migrations/Platform` | |
| 0.4 | Create `ITenantFeatureService` / `TenantFeatureService` — `IsEnabled(key)`, `GetEnabledFeatures()`, checks current tenant's flags from PlatformDbContext | `Services/ITenantFeatureService.cs`, `Services/TenantFeatureService.cs` |
| 0.5 | Seed default feature flags for demo tenant (core modules on, DLMS off, advanced off) | `Program.cs` |
| 0.6 | Create `CustomFieldConfig` model (Id, EntityType, FieldDefinitionsJson) — stores `List<CustomFieldDefinition>` per entity type | `Models/CustomFieldConfig.cs` |
| 0.7 | Create `ICustomFieldService` / `CustomFieldService` — get config by entity type, validate values against config | `Services/ICustomFieldService.cs`, `Services/CustomFieldService.cs` |
| 0.8 | Build `<CustomFieldsEditor>` shared Razor component — auto-renders form fields from config JSON, two-way binds `CustomFieldValues` | `Components/Shared/CustomFieldsEditor.razor` |
| 0.9 | Create `INumberSequenceService` / `NumberSequenceService` — `NextAsync("WorkOrder")` reads prefix + digits from `SystemSetting`, stores last-used counter | `Services/INumberSequenceService.cs`, `Services/NumberSequenceService.cs` |
| 0.10 | Create `WorkflowDefinition`, `WorkflowStep`, `WorkflowInstance` models | `Models/WorkflowDefinition.cs`, `Models/WorkflowStep.cs`, `Models/WorkflowInstance.cs` |
| 0.11 | Create `DocumentTemplate` model (Id, Name, EntityType, TemplateHtml, HeaderHtml, FooterHtml, CssOverrides, IsDefault) | `Models/DocumentTemplate.cs` |
| 0.12 | Add DbSets for `CustomFieldConfig`, `WorkflowDefinition`, `WorkflowStep`, `WorkflowInstance`, `DocumentTemplate` to `TenantDbContext` | `Data/TenantDbContext.cs` |
| 0.13 | Generate tenant migration: `dotnet ef migrations add AddCustomizationFoundation --context TenantDbContext --output-dir Data/Migrations/Tenant` | |
| 0.14 | Seed all `SystemSetting` keys from DLMS-CUSTOMIZATION-ARCHITECTURE.md Part 2, Layer 1 table (company, numbering, quality, shipping, inventory, costing, dlms, workflow categories) | `Program.cs` |
| 0.15 | Build `/admin/features` page — toggle features on/off per tenant | `Components/Pages/Admin/Features.razor` |
| 0.16 | Build `/admin/custom-fields` page — visual field designer per entity type | `Components/Pages/Admin/CustomFields.razor` |
| 0.17 | Build `/admin/numbering` page — configure prefixes, digit counts, separators | `Components/Pages/Admin/Numbering.razor` |
| 0.18 | Build `/admin/branding` page — company name, logo, CAGE code, DoDAAC, address | `Components/Pages/Admin/Branding.razor` |
| 0.19 | Create `IWorkflowEngine` / `WorkflowEngine` — start instance, advance step, check pending approvals | `Services/IWorkflowEngine.cs`, `Services/WorkflowEngine.cs` |
| 0.20 | Create `IDocumentTemplateService` / `DocumentTemplateService` — render template + merge fields → HTML → PDF | `Services/IDocumentTemplateService.cs`, `Services/DocumentTemplateService.cs` |
| 0.21 | Update NavMenu to check `ITenantFeatureService` for conditional nav sections | `Components/Layout/NavMenu.razor` |
| 0.22 | Register all new services in DI as Scoped | `Program.cs` |

**Deliverable**: Every tenant can toggle features on/off, define custom fields
on any entity, configure number sequences, and set company branding. The
workflow engine and template renderer are available for all downstream modules
to use. DLMS-specific settings are configurable but hidden until the `dlms`
feature flag is enabled.

**DLMS foundation in this stage**: CAGE code, DoDAAC, DUNS in SystemSettings.
Feature flags for `dlms`, `dlms.iuid`, `dlms.wawf`, `dlms.gfm`, `dlms.cdrl`.
These are all OFF by default — defense shops enable what they need.

---

## PHASE 1: Core Production Engine

### Stage 1 — Parts / PDM Enhancement (Module 08)
**Duration**: 2–3 weeks | **Prereqs**: Foundation ✅
**Why first**: Every module downstream reads part routing, revision, and drawing data.

| Step | Description | Files |
|------|-------------|-------|
| 1.1 | Add `CustomerPartNumber`, `DrawingNumber`, `Revision`, `RevisionDate`, `EstimatedWeightKg`, `RawMaterialSpec` to `Part` model | `Models/Part.cs` |
| 1.2 | Create `PartDrawing` model (file attachments: PDF, DXF, STEP, images) | `Models/PartDrawing.cs` |
| 1.3 | Create `PartRevisionHistory` model (snapshot at each revision bump) | `Models/PartRevisionHistory.cs` |
| 1.4 | Create `PartNote` model (engineering notes per part) | `Models/PartNote.cs` |
| 1.5 | Add DbSets + relationships to `TenantDbContext` | `Data/TenantDbContext.cs` |
| 1.6 | Generate migration: `dotnet ef migrations add AddPartsPdm --context TenantDbContext --output-dir Data/Migrations/Tenant` | |
| 1.7 | Create `IPartFileService` / `PartFileService` (upload, retrieve, delete drawings) | `Services/IPartFileService.cs`, `Services/PartFileService.cs` |
| 1.8 | Extend `PartService` with revision bump, similarity search, routing management | `Services/PartService.cs` |
| 1.9 | Build `/parts` list page with search, filters, thumbnail preview | `Components/Pages/Parts/Index.razor` |
| 1.10 | Build `/parts/{id}` tabbed detail page (Overview, Routing, Drawings, Revisions, Notes) | `Components/Pages/Parts/Detail.razor` |
| 1.11 | Build drawing upload component (drag-drop, multi-file) | `Components/Shared/FileUpload.razor` |
| 1.12 | Add "Parts" nav section to NavMenu | `Components/Layout/NavMenu.razor` |
| 1.13 | Register `IPartFileService` in DI | `Program.cs` |

**Deliverable**: Managers can browse parts with full routing and attached drawings.
Operators can view current revision. Part data is the source of truth for all downstream modules.

> **🛡️ DLMS/Customization Notes for this stage**:
> - Add `CustomFieldValues` (JSON) column to `Part` model
> - Add `ItarClassification` (None, ITAR, EAR, CUI) enum + field to `Part`
> - Add `IsDefensePart` flag — controls visibility of DLMS-specific fields in UI
> - Use `INumberSequenceService` for auto-generated part numbers when `numbering.part_auto` is true
> - Render `<CustomFieldsEditor>` on part create/edit forms
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

### Stage 2 — Estimating & Quoting (Module 01)
**Duration**: 2–3 weeks | **Prereqs**: Stage 1 (Parts/PDM)
**Why here**: Quotes are the entry point for all work. They reference parts and their routings.

| Step | Description | Files |
|------|-------------|-------|
| 2.1 | Extend `Quote` model: `EstimatedLaborCost`, `EstimatedMaterialCost`, `EstimatedOverheadCost`, `TargetMarginPct`, `RevisionNumber`, `SentAt`, `AcceptedAt` | `Models/Quote.cs` |
| 2.2 | Extend `QuoteLine`: `LaborMinutes`, `SetupMinutes`, `MaterialCostEach`, `OutsideProcessCost` | `Models/Quote.cs` |
| 2.3 | Create `QuoteRevision` model (snapshot per revision) | `Models/QuoteRevision.cs` |
| 2.4 | Create `RfqRequest` model (customer portal submissions) | `Models/RfqRequest.cs` |
| 2.5 | Add DbSets + migration | `Data/TenantDbContext.cs` |
| 2.6 | Create `IPricingEngineService` / `PricingEngineService` — cost from routing + materials + overhead | `Services/IPricingEngineService.cs`, `Services/PricingEngineService.cs` |
| 2.7 | Complete `QuoteService` — full CRUD, revision management, quote-to-WO conversion | `Services/QuoteService.cs` |
| 2.8 | Build `/quotes` list page with status filter tabs | `Components/Pages/Quotes/Index.razor` |
| 2.9 | Build `/quotes/new` and `/quotes/{id}/edit` — visual BOM builder with live margin preview | `Components/Pages/Quotes/Edit.razor` |
| 2.10 | Build `/quotes/{id}` detail page (summary, lines, revision history, convert button) | `Components/Pages/Quotes/Details.razor` |
| 2.11 | Build `/portal/rfq` public RFQ form (`[AllowAnonymous]`) | `Components/Pages/Portal/Rfq.razor` |
| 2.12 | Register services in DI | `Program.cs` |

**Deliverable**: Full quoting workflow — create quote from parts, calculate costs/margins,
send to customer, receive acceptance, convert to work order.

> **🛡️ DLMS/Customization Notes for this stage**:
> - Add `CustomFieldValues` (JSON) column to `Quote` model
> - Add `ContractNumber`, `IsDefenseContract` fields to `Quote`
> - Use `INumberSequenceService.NextAsync("Quote")` for quote numbers
> - Build first `DocumentTemplate` — quote PDF output using `IDocumentTemplateService`
> - Build `/admin/templates` page (at least quote template editing)
> - When `dlms` feature flag is on, show defense contract fields in quote form
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

### Stage 3 — Work Order Management (Module 02)
**Duration**: 2 weeks | **Prereqs**: Stage 2 (Quoting)
**Why here**: Work orders bridge ERP (quotes, customers) to MES (shop floor execution).

| Step | Description | Files |
|------|-------------|-------|
| 3.1 | Create `WorkOrderComment` model (threaded comments on WOs) | `Models/WorkOrderComment.cs` |
| 3.2 | Extend `WorkOrder`: `ShipByDate`, `PromisedDate`, `ActualShipDate`, approval fields | `Models/WorkOrder.cs` |
| 3.3 | Add DbSets + migration | `Data/TenantDbContext.cs` |
| 3.4 | Complete `WorkOrderService` — auto-generate jobs from routing, status lifecycle, fulfillment tracking | `Services/WorkOrderService.cs` |
| 3.5 | Build `/workorders` list with status Kanban view + table view toggle | `Components/Pages/WorkOrders/Index.razor` |
| 3.6 | Build `/workorders/new` — create WO (from quote or manual) with line items | `Components/Pages/WorkOrders/Create.razor` |
| 3.7 | Build `/workorders/{id}` — detail with lines, jobs, comments, status controls | `Components/Pages/WorkOrders/Details.razor` |
| 3.8 | Build `/workorders/{woId}/jobs/{jobId}` — job detail with stage timeline | `Components/Pages/WorkOrders/JobDetail.razor` |
| 3.9 | Wire quote-to-WO conversion end-to-end (accept quote → creates WO with lines) | `Services/QuoteService.cs` |

**Deliverable**: Complete order lifecycle — quote acceptance auto-creates WO,
WO auto-generates jobs from part routing, managers track fulfillment.

> **🛡️ DLMS/Customization Notes for this stage**:
> - Add `CustomFieldValues` (JSON) column to `WorkOrder` model
> - Add `ContractNumber`, `ContractLineItem` (CLIN), `IsDefenseContract` to `WorkOrder`
> - Use `INumberSequenceService.NextAsync("WorkOrder")` for WO numbers
> - Wire `IWorkflowEngine` for WO release approval (first real workflow usage)
> - Build `/admin/workflows` page (at least WO approval workflow editing)
> - When `dlms` feature flag is on, show contract fields in WO form
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

### Stage 4 — Shop Floor & Scheduling (Module 04) ⭐ MES CORE
**Duration**: 3–4 weeks | **Prereqs**: Stage 3 (Work Orders)
**Why here**: This is the MES heart — operators execute work on the floor.

| Step | Description | Files |
|------|-------------|-------|
| 4.1 | Extend `StageExecution`: `MachineId`, `ScheduledStartAt`, `ScheduledEndAt`, `ActualStartAt`, `ActualEndAt`, `PauseResumeLog` | `Models/StageExecution.cs` |
| 4.2 | Add enums: `StageExecutionAction` (Start, Pause, Resume, Complete, Fail, Skip) | `Models/Enums/ManufacturingEnums.cs` |
| 4.3 | Add migration | |
| 4.4 | Complete `StageService` — operator workflows: Start, Pause, Resume, Complete, Fail, Skip with validation | `Services/StageService.cs` |
| 4.5 | Create `IOeeService` / `OeeService` — OEE calculation (Availability × Performance × Quality) | `Services/IOeeService.cs`, `Services/OeeService.cs` |
| 4.6 | Build `/shopfloor` operator queue page — "What should I work on now?" prioritized by due date | `Components/Pages/ShopFloor/Index.razor` |
| 4.7 | Complete shop floor partials — wire Start/Complete/Fail buttons to `StageService` | `Components/Pages/ShopFloor/Partials/*.razor` |
| 4.8 | Wire `/shopfloor/stage/{slug}` to show queued + active stage executions with operator actions | `Components/Pages/ShopFloor/Stage.razor` |
| 4.9 | Build `/scheduler` Gantt view — wire `scheduler.js` to real job/stage data | `Components/Pages/Scheduler/Index.razor` |
| 4.10 | Build `/scheduler/capacity` — machine load vs. available capacity bar chart | `Components/Pages/Scheduler/Capacity.razor` |
| 4.11 | Add SignalR broadcast when stage status changes (real-time Gantt updates) | `Hubs/MachineStateHub.cs` |
| 4.12 | Register `IOeeService` in DI | `Program.cs` |

**Deliverable**: Operators see their queue, start/complete stages, log issues.
Managers see a Gantt scheduler and capacity dashboard. Machine utilization is tracked.

---

### Stage 5 — Visual Work Instructions (Module 03) ⭐ MES OPERATOR UX
**Duration**: 2–3 weeks | **Prereqs**: Stage 4 (Shop Floor)
**Why here**: Once operators are executing stages, they need step-by-step guidance.

| Step | Description | Files |
|------|-------------|-------|
| 5.1 | Create `WorkInstruction`, `WorkInstructionStep`, `WorkInstructionMedia`, `WorkInstructionRevision`, `OperatorFeedback` models | `Models/WorkInstruction.cs` |
| 5.2 | Add DbSets + relationships + migration | `Data/TenantDbContext.cs` |
| 5.3 | Create `IWorkInstructionService` / `WorkInstructionService` — CRUD, versioning, feedback | `Services/IWorkInstructionService.cs`, `Services/WorkInstructionService.cs` |
| 5.4 | Build `/admin/work-instructions` — list all instructions by part+stage | `Components/Pages/Admin/WorkInstructions/Index.razor` |
| 5.5 | Build `/admin/work-instructions/{id}/edit` — rich step editor with drag-reorder, image upload | `Components/Pages/Admin/WorkInstructions/Edit.razor` |
| 5.6 | Build `/shopfloor/instructions/{id}` — clean operator viewer (large images, clear steps, swipe-through) | `Components/Pages/ShopFloor/InstructionViewer.razor` |
| 5.7 | Embed instruction link in stage execution view — "View Instructions" button | `Components/Pages/ShopFloor/Partials/*.razor` |
| 5.8 | Build operator feedback modal — "Flag this step" with reason | |
| 5.9 | Build `/admin/work-instructions/feedback` — review flagged steps | `Components/Pages/Admin/WorkInstructions/Feedback.razor` |
| 5.10 | Register service in DI | `Program.cs` |

**Deliverable**: Admins create visual, step-by-step instructions per part per stage.
Operators see instructions during stage execution. Feedback loop captures confusion points.

---

### Stage 6 — Quality Systems / QMS (Module 05) ⭐ MES QUALITY
**Duration**: 3–4 weeks | **Prereqs**: Stage 4 (Shop Floor)
**Why here**: Quality inspection happens during/after stage execution. NCR/CAPA handle failures.

| Step | Description | Files |
|------|-------------|-------|
| 6.1 | Create `InspectionPlan` + `InspectionPlanCharacteristic` models (reusable templates) | `Models/Quality/InspectionPlan.cs` |
| 6.2 | Create `InspectionMeasurement` model (actual recorded values per characteristic) | `Models/Quality/InspectionMeasurement.cs` |
| 6.3 | Create `NonConformanceReport` model (NCR with severity, disposition) | `Models/Quality/NonConformanceReport.cs` |
| 6.4 | Create `CorrectiveAction` model (CAPA linked to NCR) | `Models/Quality/CorrectiveAction.cs` |
| 6.5 | Create `SpcDataPoint` model (measurement time series for SPC charts) | `Models/Quality/SpcDataPoint.cs` |
| 6.6 | Add DbSets + relationships + migration | `Data/TenantDbContext.cs` |
| 6.7 | Create `IQualityService` / `QualityService` — inspection CRUD, NCR lifecycle, CAPA workflow | `Services/IQualityService.cs`, `Services/QualityService.cs` |
| 6.8 | Create `ISpcService` / `SpcService` — mean, UCL, LCL, Cp, Cpk calculations | `Services/ISpcService.cs`, `Services/SpcService.cs` |
| 6.9 | Build `/quality` dashboard — first-pass yield, scrap rate, NCR trend, SPC summary | `Components/Pages/Quality/Index.razor` |
| 6.10 | Build `/quality/ncr` — NCR list + create/edit with disposition workflow | `Components/Pages/Quality/Ncr.razor` |
| 6.11 | Build `/quality/capa` — CAPA board (Kanban: Open → In Progress → Verified → Closed) | `Components/Pages/Quality/Capa.razor` |
| 6.12 | Build `/quality/spc` — SPC control charts with trend detection | `Components/Pages/Quality/Spc.razor` |
| 6.13 | Embed inspection entry in shop floor QC stage partial | `Components/Pages/ShopFloor/Partials/QualityControl.razor` |
| 6.14 | Register services in DI | `Program.cs` |

**Deliverable**: Full QMS — inspection plans, measurement recording, NCR/CAPA workflow,
SPC charts. Operators record measurements during QC stage. Quality team manages NCRs.

> **🛡️ DLMS/Customization Notes for this stage**:
> - Create `FairForm1` (Part Number Accountability), `FairForm2` (Product Accountability),
>   `FairForm3` (Characteristic Accountability) models for structured AS9102 FAIR
> - Auto-populate FAIR forms from part routing + material certs + measurements
> - Add `DocumentTemplate` for FAIR PDF export in standard AS9102 format
> - Add `CustomFieldValues` (JSON) to `QCInspection` / inspection models
> - Wire `IWorkflowEngine` for NCR disposition approval chain
> - Use `INumberSequenceService.NextAsync("NCR")` for NCR numbers
> - When `quality.require_fair` setting is true, require FAIR on first articles
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

### Stage 7 — Inventory Control (Module 06)
**Duration**: 2–3 weeks | **Prereqs**: Stage 3 (Work Orders)
**Why here**: Material reservations happen at WO release. Receiving feeds production.

| Step | Description | Files |
|------|-------------|-------|
| 7.1 | Create `InventoryItem`, `StockLocation`, `InventoryLot`, `InventoryTransaction`, `MaterialRequest` models | `Models/Inventory/*.cs` |
| 7.2 | Add DbSets + migration | `Data/TenantDbContext.cs` |
| 7.3 | Create `IInventoryService` / `InventoryService` — stock CRUD, transactions, reservations | `Services/IInventoryService.cs`, `Services/InventoryService.cs` |
| 7.4 | Create `IMaterialPlanningService` / `MaterialPlanningService` — BOM explosion, shortage detection | `Services/IMaterialPlanningService.cs`, `Services/MaterialPlanningService.cs` |
| 7.5 | Build `/inventory` dashboard — stock levels, low-stock alerts, recent transactions | `Components/Pages/Inventory/Index.razor` |
| 7.6 | Build `/inventory/items` — item list with search + location filter | `Components/Pages/Inventory/Items.razor` |
| 7.7 | Build `/inventory/items/{id}/ledger` — stock transaction history | `Components/Pages/Inventory/Ledger.razor` |
| 7.8 | Build `/inventory/receive` — receiving workflow (barcode scan ready) | `Components/Pages/Inventory/Receive.razor` |
| 7.9 | Wire material reservation into WO release workflow | `Services/WorkOrderService.cs` |
| 7.10 | Register services in DI | `Program.cs` |

**Deliverable**: Track raw material stock, auto-reserve on WO release,
receive material with lot tracking, detect shortages before production starts.

> **🛡️ DLMS/Customization Notes for this stage**:
> - Add `IsGovernmentFurnished`, `ContractNumber`, `AccountabilityCode`, `CustodianUserId` to `InventoryItem`
> - Add `CustomFieldValues` (JSON) to `InventoryItem`
> - GFM/GFE fields only visible when `Features.IsEnabled("dlms.gfm")` is true
> - Lot tracking toggleable via `inventory.track_lots` setting
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

### Stage 8 — Reporting & Analytics (Module 07)
**Duration**: 2 weeks | **Prereqs**: Stages 4, 6, 7 (data to report on)
**Why here**: All core modules are generating data. Now build the cross-cutting views.

| Step | Description | Files |
|------|-------------|-------|
| 8.1 | Create `DashboardLayout`, `SavedReport` models | `Models/DashboardLayout.cs`, `Models/SavedReport.cs` |
| 8.2 | Add DbSets + migration | `Data/TenantDbContext.cs` |
| 8.3 | Create `IReportingService` / `ReportingService` — OTD, quality KPIs, cost analysis, OEE | `Services/IReportingService.cs`, `Services/ReportingService.cs` |
| 8.4 | Enhance home dashboard with real KPI data from all modules | `Components/Pages/Home.razor` |
| 8.5 | Build `/analytics` main analytics dashboard with time range selector | `Components/Pages/Analytics/Index.razor` |
| 8.6 | Build `/analytics/on-time-delivery` — OTD trend chart | `Components/Pages/Analytics/Otd.razor` |
| 8.7 | Build `/analytics/quality` — quality metrics (yield, scrap, NCR trend) | `Components/Pages/Analytics/Quality.razor` |
| 8.8 | Build `/analytics/oee` — OEE dashboard per machine | `Components/Pages/Analytics/Oee.razor` |
| 8.9 | Build `/analytics/cost` — cost analysis (estimated vs. actual) | `Components/Pages/Analytics/Cost.razor` |
| 8.10 | Build `/search` — universal search across parts, WOs, quotes, NCRs | `Components/Pages/Search/Index.razor` |
| 8.11 | Register service in DI | `Program.cs` |

**Deliverable**: Management-level analytics — OTD, OEE, quality trends, cost analysis.
Universal search finds anything in the system.

---

## PHASE 1 CHECKPOINT ✅

After Stage 8, you have a **complete core MES/ERP/QMS**:
- 📋 Quote → Work Order → Job → Stage Execution → Shipping lifecycle
- 🏭 Operators execute stages with instructions, log quality data
- 📊 Managers see Gantt scheduling, OEE, capacity, analytics
- ✅ Quality team runs inspections, NCRs, CAPAs, SPC
- 📦 Inventory is tracked with lot control and material reservations
- 🔍 Universal search and cross-cutting reporting

**A shop can run daily operations entirely in the system.**

---

## PHASE 2: Operational Depth

### Stage 9 — Job Costing & Financial Data (Module 09)
**Duration**: 2–3 weeks | **Prereqs**: Stages 4, 8

| Step | Description | Files |
|------|-------------|-------|
| 9.1 | Create `CostEntry`, `OverheadRate`, `LaborRate` models | `Models/Costing/*.cs` |
| 9.2 | Add DbSets + migration | |
| 9.3 | Create `IJobCostingService` / `JobCostingService` — actual cost accumulation per job | |
| 9.4 | Create `IProfitabilityService` / `ProfitabilityService` — estimated vs. actual margin | |
| 9.5 | Build `/admin/rates` — labor rate + overhead rate configuration | |
| 9.6 | Build `/analytics/cost` enhancement — job-level P&L | |
| 9.7 | Wire cost entries into stage completion (auto-log labor + machine cost) | |

> **🛡️ DLMS/Customization Notes for this stage**:
> - Overhead method selectable per tenant via `costing.overhead_method` setting ("percentage" or "activity")
> - Default margin configurable via `costing.default_margin_pct` setting
> - Prepare WAWF invoice data structure (ContractNumber, CLIN, amounts) — full WAWF output in Stage 21
> - Wire `IWorkflowEngine` for PO approval by dollar threshold
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

### Stage 10 — Time Clock & Labor Tracking (Module 13)
**Duration**: 2 weeks | **Prereqs**: Stage 4 (Shop Floor), Stage 9 (Costing)

| Step | Description | Files |
|------|-------------|-------|
| 10.1 | Create `TimeEntry`, `OperatorSkill`, `ShiftDefinition` models | `Models/Labor/*.cs` |
| 10.2 | Add DbSets + migration | |
| 10.3 | Create `ITimeClockService` / `TimeClockService` — clock in/out, break tracking | |
| 10.4 | Create `ILaborAnalyticsService` / `LaborAnalyticsService` — utilization, efficiency | |
| 10.5 | Build `/kiosk` — operator time clock (touch-friendly, kiosk mode) | |
| 10.6 | Build `/labor/entries` — manager time entry review | |
| 10.7 | Build `/admin/skills` — skill matrix (operator × stage capability) | |
| 10.8 | Build `/analytics/labor` — labor utilization dashboard | |
| 10.9 | Auto-log time entries when operators start/complete stages | |

---

### Stage 11 — Cutting Tools & Fixtures (Module 10)
**Duration**: 2 weeks | **Prereqs**: Stage 4 (Shop Floor)

| Step | Description | Files |
|------|-------------|-------|
| 11.1 | Create `CuttingTool`, `ToolInstance`, `ToolUsageLog`, `ToolKit`, `ToolKitItem`, `Fixture` models | `Models/Tooling/*.cs` |
| 11.2 | Add DbSets + migration | |
| 11.3 | Create `IToolManagementService` / `ToolManagementService` — lifecycle, wear tracking | |
| 11.4 | Build `/toolcrib` — tool crib dashboard with wear alerts | |
| 11.5 | Build `/toolcrib/tools/{id}` — tool instance detail with usage history | |
| 11.6 | Build `/toolcrib/kits/{id}/edit` — tool kit builder (group tools per part/stage) | |
| 11.7 | Wire tool usage logging into stage execution | |

---

### Stage 12 — Calibration & Maintenance CMMS (Module 11)
**Duration**: 2 weeks | **Prereqs**: Foundation maintenance ✅, Stage 6 (Quality)

| Step | Description | Files |
|------|-------------|-------|
| 12.1 | Create `GageEquipment`, `CalibrationRecord`, `MaintenanceRequest` models | `Models/Maintenance/*.cs` |
| 12.2 | Add DbSets + migration | |
| 12.3 | Create `ICalibrationService` / `CalibrationService` — gage tracking, due dates, certificates | |
| 12.4 | Build `/maintenance/calibration` — calibration registry with due-date tracking | |
| 12.5 | Build `/maintenance/request` — operator maintenance request form | |
| 12.6 | Enhance maintenance dashboard with calibration alerts | |
| 12.7 | Wire calibration status into quality inspection validation | |

---

### Stage 13 — Purchasing & Vendors (Module 12)
**Duration**: 2–3 weeks | **Prereqs**: Stage 7 (Inventory), Stage 6 (Quality)

| Step | Description | Files |
|------|-------------|-------|
| 13.1 | Create `Vendor`, `VendorItem`, `PurchaseOrder`, `PurchaseOrderLine`, `VendorScorecard` models | `Models/Purchasing/*.cs` |
| 13.2 | Add DbSets + migration | |
| 13.3 | Create `IPurchasingService` / `PurchasingService` — PO lifecycle, approval, receiving | |
| 13.4 | Create `IVendorService` / `VendorService` — vendor management, scorecards | |
| 13.5 | Build `/purchasing/orders` — PO list with status filters | |
| 13.6 | Build `/purchasing/orders/{id}` — PO detail with line items, receiving | |
| 13.7 | Build `/purchasing/vendors` — vendor list with quality scorecards | |
| 13.8 | Wire PO receiving into inventory transactions | |
| 13.9 | Wire vendor quality data from NCRs into scorecards | |

> **🛡️ DLMS/Customization Notes for this stage**:
> - Add `CustomFieldValues` (JSON) to `PurchaseOrder` and `Vendor`
> - Use `INumberSequenceService.NextAsync("PurchaseOrder")` for PO numbers
> - Wire `IWorkflowEngine` for PO approval chain (configurable per tenant)
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

### Stage 14 — Document Control (Module 14)
**Duration**: 2 weeks | **Prereqs**: Stage 6 (Quality)

| Step | Description | Files |
|------|-------------|-------|
| 14.1 | Create `DocumentCategory`, `ControlledDocument`, `DocumentRevision`, `DocumentApproval`, `DocumentReadRecord` models | `Models/Documents/*.cs` |
| 14.2 | Add DbSets + migration | |
| 14.3 | Create `IDocumentControlService` / `DocumentControlService` — upload, approve, distribute, acknowledge | |
| 14.4 | Build `/documents` — document library with category tree | |
| 14.5 | Build `/documents/{id}` — document detail with revision history, approval workflow | |
| 14.6 | Build `/documents/my-acknowledgments` — operator pending reads | |
| 14.7 | Wire controlled documents into quality inspection plans | |

> **🛡️ DLMS/Customization Notes for this stage**:
> - Add `IsCdrl`, `CdrlNumber`, `ContractNumber`, `DueDate`, `DeliveryStatus` to `ControlledDocument`
> - CDRL fields only visible when `Features.IsEnabled("dlms.cdrl")` is true
> - Add CDRL deliverable dashboard — what's due, submitted, overdue
> - Wire `IWorkflowEngine` for document revision approval (Author → Reviewer → Approver)
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

## PHASE 2 CHECKPOINT ✅

After Stage 14:
- 💰 Full job costing with estimated vs. actual P&L per job
- ⏱️ Operator time clock with auto-logging from stage execution
- 🔧 Tool crib with wear tracking and predictive replacement alerts
- 🔬 Calibration registry with due-date enforcement
- 📦 Purchase orders with vendor scorecards
- 📄 Controlled document management with approval + acknowledgment

---

### Stage 15 — Cross-Module Integration Sprint
**Duration**: 1–2 weeks

| Task | Description |
|------|-------------|
| 15.1 | Wire all cross-module dependencies (see dependency map in MASTER_CONTEXT) |
| 15.2 | Stage completion → auto-log cost entry + time entry + tool usage |
| 15.3 | NCR creation → auto-trigger vendor scorecard update if vendor-caused |
| 15.4 | PO receipt → auto-update inventory + notify material requestor |
| 15.5 | Calibration expiry → block inspection using expired gage |
| 15.6 | Document revision → require re-acknowledgment from operators |
| 15.7 | End-to-end smoke test: Quote → WO → Stage Execution → QC → Ship |

---

## PHASE 3: Platform Maturity

### Stage 16 — Shipping & Receiving (Module 15)
**Duration**: 2 weeks | **Prereqs**: Stage 3 (Work Orders), Stage 7 (Inventory)

| Step | Description |
|------|-------------|
| 16.1 | Create `Shipment`, `ShipmentLine` models |
| 16.2 | Create `IShippingService` — packing lists, BOL generation, label data |
| 16.3 | Build `/shipping` — shipping queue from completed WO lines |
| 16.4 | Build `/shipping/create/{woId}` — shipment wizard |
| 16.5 | Wire shipped quantities back to WO fulfillment tracking |

> **🛡️ DLMS/Customization Notes for this stage**:
> - Add `WawfDocumentNumber`, `ContractNumber`, `ContractLineItem`, `DoDAAC` to `Shipment`
> - Add `CustomFieldValues` (JSON) to `Shipment`
> - Add `DocumentTemplate` entries for packing list, BOL, CoC — all customizable per tenant
> - Use `INumberSequenceService.NextAsync("Shipment")` for shipment numbers
> - When `dlms` feature is enabled: show defense shipping fields, enable ASN generation
> - When `shipping.require_coc` is true, require CoC document before shipment close
> - IUID barcode printing on labels when `dlms.iuid` feature is enabled
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

### Stage 17 — CRM & Contact Management (Module 16)
**Duration**: 2 weeks | **Prereqs**: Stage 2 (Quoting), Stage 16 (Shipping)

| Step | Description |
|------|-------------|
| 17.1 | Create `Customer`, `Contact`, `CustomerActivity`, `Opportunity` models |
| 17.2 | Create `ICrmService` — customer management, activity logging, pipeline |
| 17.3 | Build `/crm/customers`, `/crm/customers/{id}`, `/crm/pipeline` |
| 17.4 | Link customers to quotes and work orders (replace free-text CustomerName) |
| 17.5 | Build customer portal: `/portal/login`, `/portal/dashboard`, `/portal/orders`, `/portal/certs` |

---

### Stage 18 — CMMC & Compliance (Module 17)
**Duration**: 2–3 weeks | **Prereqs**: Stage 14 (Document Control)

| Step | Description |
|------|-------------|
| 18.1 | Create `ComplianceFramework`, `ComplianceControl`, `ComplianceSelfAssessment` models (tenant) |
| 18.2 | Create `AuditAccessLog` model (platform-level) |
| 18.3 | Create `IComplianceService`, `IAuditLogService` |
| 18.4 | Build `/compliance`, `/compliance/assess/{frameworkId}` |
| 18.5 | Build `/admin/audit-log`, `/admin/security` |
| 18.6 | Seed AS9100, ISO 13485, CMMC Level 2 framework controls |

> **🛡️ DLMS/Customization Notes for this stage**:
> - Add DFARS clause tracking (252.204-7012, 252.204-7019, 252.204-7020, etc.)
> - Wire ITAR/EAR classification from Part model into compliance checks
> - Add CUI marking enforcement when `dlms` feature is enabled
> - Compliance frameworks are pre-seeded but fully customizable per tenant
> - See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md) Part 3

---

### Stage 19 — Training & LMS (Module 18)
**Duration**: 2 weeks | **Prereqs**: Stage 14 (Document Control)

| Step | Description |
|------|-------------|
| 19.1 | Create `TrainingCourse`, `TrainingLesson`, `TrainingEnrollment`, `TrainingCompletion`, `KnowledgeArticle` models |
| 19.2 | Create `ITrainingService`, `IKnowledgeBaseService` |
| 19.3 | Build `/training/my`, `/training/courses/{id}` |
| 19.4 | Build `/knowledge`, `/knowledge/write` |
| 19.5 | Build `/admin/training` — course management |
| 19.6 | Wire training requirements to stage execution (block unqualified operators) |

---

### Stage 20 — API Layer & Customer Portal Polish
**Duration**: 2 weeks | **Prereqs**: All modules complete

| Step | Description |
|------|-------------|
| 20.1 | Add minimal REST API controllers for key entities (Parts, WOs, Quotes, Inventory) |
| 20.2 | API authentication (API key per tenant) |
| 20.3 | Polish customer portal (order status, cert downloads, RFQ) |
| 20.4 | Webhook support for external integrations |

---

### Stage 21 — DLMS Transaction Services 🛡️
**Duration**: 2–3 weeks | **Prereqs**: Stage 16 (Shipping), Stage 18 (Compliance)
**Why here**: All the DLMS data fields have been added throughout Stages 1–18.
Now build the services that generate actual DLMS transaction outputs.

| Step | Description | Files |
|------|-------------|-------|
| 21.1 | Create `IDlmsService` / `DlmsService` — generate DLMS 856 ASN data package from `Shipment` | `Services/IDlmsService.cs`, `Services/DlmsService.cs` |
| 21.2 | Create `IIuidService` / `IuidService` — generate UII codes (Construct 1 & 2), register with DoD IUID Registry API | `Services/IIuidService.cs`, `Services/IuidService.cs` |
| 21.3 | Add WAWF invoice data export — generate receiving report + invoice package from `Shipment` + `CostEntry` data | `Services/DlmsService.cs` |
| 21.4 | Add MILSTRIP requisition import — parse inbound 511 transactions into `WorkOrder` | `Services/DlmsService.cs` |
| 21.5 | Build `/admin/dlms` settings page — CAGE, DoDAAC, IUID construct type, WAWF config | `Components/Pages/Admin/Dlms.razor` |
| 21.6 | Add Data Matrix barcode generation for IUID labels | `Services/IuidService.cs` |
| 21.7 | Build DLMS transaction log viewer — audit trail of all DLMS transactions sent/received | `Components/Pages/Admin/DlmsLog.razor` |
| 21.8 | Register DLMS services in DI (conditional on `dlms` feature flag) | `Program.cs` |
| 21.9 | End-to-end DLMS smoke test: Defense quote → WO with contract → production → FAIR → ASN + WAWF | |

> All DLMS features require `Features.IsEnabled("dlms")`. Non-defense shops
> never see any of this. Defense shops enable the specific DLMS sub-features
> they need (`dlms.iuid`, `dlms.wawf`, `dlms.gfm`, `dlms.cdrl`).

**Deliverable**: Full DLMS transaction support for defense manufacturing.
ASN generation, WAWF invoice export, IUID barcode marking, MILSTRIP import.
All behind feature flags — zero impact on commercial-only customers.

---

## PHASE 3 CHECKPOINT ✅

After Stage 21:
- 🚚 Shipping with packing lists, BOL, and CoC
- 👥 CRM with customer portal
- 🔒 Multi-framework compliance (CMMC, AS9100, ISO)
- 📚 LMS with contextual in-app training
- 🔌 REST API for external integrations
- 🛡️ **DLMS transaction support (ASN, WAWF, IUID, MILSTRIP)**
- ⚙️ **Per-tenant customization (feature flags, custom fields, workflows, templates, numbering)**

**Full ProShop ERP parity + defense logistics + tenant self-service customization.**

---

## MES Feature Cross-Reference

These are the MES-specific features and which stages deliver them:

| MES Feature | Stage | Module |
|------------|-------|--------|
| Operator work queue ("what do I work on next?") | Stage 4 | M04 |
| Stage execution (start/pause/complete/fail) | Stage 4 | M04 |
| Real-time Gantt scheduler | Stage 4 | M04 |
| Machine capacity planning | Stage 4 | M04 |
| OEE tracking | Stage 4 | M04 |
| Visual work instructions (step-by-step) | Stage 5 | M03 |
| Operator feedback on instructions | Stage 5 | M03 |
| In-process inspection (measurement recording) | Stage 6 | M05 |
| SPC control charts (Cp/Cpk) | Stage 6 | M05 |
| NCR / CAPA workflow | Stage 6 | M05 |
| Operator time clock / kiosk | Stage 10 | M13 |
| Auto time logging from stage execution | Stage 10 | M13 |
| Skill matrix (who can work what) | Stage 10 | M13 |
| Tool wear tracking + predictive alerts | Stage 11 | M10 |
| Tool kit management per part/stage | Stage 11 | M10 |
| Gage calibration with block-if-expired | Stage 12 | M11 |
| Real-time machine state (SignalR) | Foundation ✅ | — |
| Barcode/camera scan for receiving | Stage 7 | M06 |
| Operator training requirements per stage | Stage 19 | M18 |
| Per-tenant feature flags (module toggling) | Stage 0.5 | Foundation |
| No-code custom fields on all entities | Stage 0.5 | Foundation |
| Configurable number sequences | Stage 0.5 | Foundation |
| Configurable approval workflows | Stage 0.5 | Foundation |
| Customer-branded document templates | Stage 0.5 | Foundation |
| DLMS ASN (856) generation | Stage 21 | DLMS |
| WAWF invoice export | Stage 21 | DLMS |
| IUID barcode marking | Stage 21 | DLMS |
| GFM/GFE property tracking | Stage 7 | M06 |
| CDRL deliverable tracking | Stage 14 | M14 |
| Structured AS9102 FAIR | Stage 6 | M05 |

---

## How to Use This Plan

1. **Start at Stage 0.5** (customization foundation) then proceed through Stage 1+.
2. Each stage begins with models → migration → services → UI.
3. After each stage, verify the build compiles: `dotnet build`.
4. After completing a stage, update the checkbox in the corresponding
   `docs/phase-N/MODULE-XX-*.md` file and the status in `MASTER_CONTEXT.md`.
5. Stages within a phase can occasionally be parallelized (e.g., Stage 5
   and Stage 6 are both independent of each other after Stage 4), but the
   numbered order is the recommended serial path.
6. **DLMS notes**: Each stage includes a 🛡️ callout box with DLMS-specific
   fields and customization integration points. Follow these alongside the
   main steps. See [DLMS-CUSTOMIZATION-ARCHITECTURE.md](DLMS-CUSTOMIZATION-ARCHITECTURE.md)
   for the full architecture.

**Current status**: Foundation fixes complete. Ready to begin **Stage 0.5 (Customization Foundation)**.
