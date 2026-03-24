# Opcentrix V3 — System Review vs ProShop ERP

> **Created**: 2026-03-18
> **Purpose**: Gap analysis comparing Opcentrix V3 to ProShop ERP, with focus on
> additive manufacturing differentiation and the multi-part SLS build plate flow.

---

## Competitive Positioning

ProShop ERP is a 30+ module web-based ERP/MES/QMS targeting small-to-medium
contract manufacturers ($715/mo starting). It excels at CNC machine shops and
has no native additive manufacturing workflow — it's generic enough to work
but doesn't model build plates, nesting, powder tracking, or batch-stage flows.

**Opcentrix's differentiation**: Purpose-built for additive + subtractive shops
with native SLS/LPBF build plate management, stacking optimization, powder/gas
tracking, OPC UA machine telemetry, and build-level stage execution.

---

## Feature-by-Feature Comparison

### Where Opcentrix MATCHES ProShop (already built)

| ProShop Feature | Opcentrix Equivalent | Status |
|----------------|---------------------|--------|
| Parts Module (single source of truth, routings) | `Part` + `PartStageRequirement` + `Parts/Edit.razor` | Done |
| Work Orders (real-time tracking) | `WorkOrder` + `WorkOrderLine` + auto-job generation | Done |
| QMS — NCR | `NonConformanceReport` + `Quality/Ncr.razor` | Done |
| QMS — CAPA | `CorrectiveAction` + `Quality/Capa.razor` | Done |
| QMS — Inspection | `QCInspection` + `InspectionPlan` | Done |
| QMS — SPC | `SpcDataPoint` + `Quality/Spc.razor` | Done |
| Scheduling | `Scheduler/Index.razor` (Gantt) + `Scheduler/Capacity.razor` | Done |
| Inventory | `InventoryItem` + `InventoryLot` + Dashboard/Items/Ledger/Receive | Done |
| Equipment & CMMS | `Machine` + `MaintenanceRule` + `MaintenanceWorkOrder` | Done |
| Job Costing (basic) | `PricingEngineService` + stage cost tracking | Partial |
| Estimating / Quoting | `Quote` + `QuoteLine` + RFQ portal | Done |
| Dashboards | 5 analytics pages + home KPI dashboard | Done |
| Custom Fields | `CustomFieldConfig` + `CustomFieldsEditor` | Done |
| Multi-tenant SaaS | Platform + Tenant DB separation | Done |
| PWA / Mobile | Service worker + responsive CSS | Done |

### Where Opcentrix EXCEEDS ProShop

| Feature | Details |
|---------|---------|
| **SLS Build Planning** | `BuildPackage` with multi-part assignment, `BuildFileInfo` (layers, powder, positions) |
| **Stacking Optimization** | `Part.AllowStacking`, single/double/triple stack config, `GetRecommendedStackLevel()` |
| **Machine Telemetry** | OPC UA endpoint support, SignalR real-time state updates |
| **Part Instance Tracking** | `PartInstance` + `PartInstanceStageLog` — individual serial tracking |
| **Stage-Specific UIs** | 10 specialized shop floor partials (SLS, CNC, EDM, Heat Treat, etc.) |
| **Defense/DLMS** | ITAR classification, CUI handling, defense contract fields |
| **Build File Import** | Layer count, build height, print time, powder estimate, part position JSON |
| **Build Package Planning** | Visual build planning with multi-part assignment per build plate |

### Where ProShop LEADS (Opcentrix gaps)

| ProShop Feature | Opcentrix Status | Priority |
|----------------|-----------------|----------|
| **Work Instructions** (media-rich, step-by-step) | Button exists but disabled | Phase 1B |
| **BOM** (multi-level, 100% traceable) | `PartBomItem` model exists, no UI tab yet | High |
| **Purchasing / PO** | Planned (Module 12) | Phase 2 |
| **Shipping / Receiving** | Planned (Module 15) | Phase 3 |
| **CRM / Contacts** | Planned (Module 16) | Phase 3 |
| **Document Control** | Planned (Module 14) | Phase 2 |
| **Time Clock / Labor** | Planned (Module 13) | Phase 2 |
| **COTS Management** | Not planned | Low |
| **Fixtures Module** | Planned (Module 10) | Phase 2 |
| **25+ Dashboards** | 5 analytics + 1 home | Expand over time |
| **ISO/AS9100 Compliance** | FAIR planned (Phase 1C), standards not modeled | Phase 1C |

---

## Critical Architecture Gap: SLS Build Plate Flow

### The Problem

The SLS stage treats builds as single-part-per-job. The real manufacturing flow is:

```
Multiple parts from different WOs → packed onto one build plate → printed as a unit
→ depowdered as a unit → EDM'd off the plate → individual parts split to separate routings
```

The `BuildPackage` model already supports multi-part builds in the planning phase,
but it's **disconnected from production execution**. When a build goes to the shop
floor, it flows through `StageExecution → Job → Part` (singular).

### What Needs to Change

See the SLS Build Plate Architecture section in `ROADMAP.md` for the implementation plan.

### Key Concepts

1. **Build-level stages**: SLS Printing, Depowdering, Wire EDM — the build plate
   moves as a unit. Duration comes from the slice file, not per-part estimates.

2. **Part-level stages**: Everything after EDM — Heat Treatment, Surface Finishing,
   CNC Machining, QC, Shipping. Each part follows its own routing.

3. **Build revision control**: The build layout (which parts, what positions, what
   parameters) is revision-tracked. Changes to the build create new revisions.

4. **Duration derivation**: SLS build time comes from the slicer (stored in
   `BuildFileInfo.EstimatedPrintTimeHours`), not from `Part.SlsBuildDurationHours`.
   This is the real-world source of truth.

5. **Hours-per-part allocation**: Build duration is allocated across parts based on
   volume/weight ratio or equal split. This feeds into job costing.

---

## Summary Assessment

Opcentrix V3 is already competitive with ProShop on core ERP/MES/QMS capabilities
and **significantly ahead** on additive manufacturing workflow support. The critical
gap is that the multi-part build plate flow doesn't connect planning to execution.
Fixing this — along with consolidating the planning docs into one clear roadmap —
is the immediate priority.

### Sources

- [ProShop ERP Features](https://proshoperp.com/)
- [ProShop Specs & Modules](https://proshoperp.com/proshop-specs/)
- [ProShop Module Details](https://proshoperp.com/resources/long-module-sheet/)
- [ProShop Reviews - SelectHub](https://www.selecthub.com/p/manufacturing-software/proshop-erp/)
- [ProShop Reviews - Capterra](https://www.capterra.com/p/155436/ProShop/)
- [Materialise CO-AM Platform](https://www.materialise.com/en/industrial/software/co-am-software-platform)
- [AMIS Runtime AM Automation](https://3dprintingindustry.com/news/amis-launches-runtime-software-for-industrial-additive-manufacturing-automation-249064/)
- [SLS Nesting Best Practices](https://sinterit.com/blog/sls-technology/pack-density-in-sls-3d-printing/)
