# Scheduler Finalization — Implementation Plan

## Status: Phases A-G COMPLETE — Phase F (Supporting Features) Remaining

---

## Executive Summary

The scheduler is ~80% complete. The core architecture is sound: `BuildAdvisorService` provides demand aggregation and plate optimization, `ProgramSchedulingService` handles SLS slot-finding and build scheduling, `SchedulingService` handles CNC/downstream stage scheduling, and all 5 scheduler views (Gantt, Demand, Programs, Floor, Data) are functional.

Three categories of remaining work:

**A. SLS scheduling path consolidation** — `UnifiedScheduleWizard` (1564 lines) still handles both SLS and CNC. Its SLS plate composition is WO-centric (single-WO, manual build variations), while `NextBuildAdvisor` has the correct machine-centric approach with cross-WO demand aggregation. The wizard needs to become CNC-only, and all SLS scheduling must flow through the NextBuildAdvisor.

**B. NextBuildAdvisor enhancement** — The advisor works end-to-end (3-step wizard, session tracking, Schedule & Queue Next), but lacks manual plate composition controls (add/remove parts, quantity overrides, stack level per part). It also cannot be pre-populated from a specific WO line.

**C. Supporting features** — The WorkOrderPanel on the Gantt sidebar fires `OnScheduleRequested` which opens the old wizard. Shipping has no persistence. Inventory is disconnected from production output.

---

## Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| SLS scheduling entry point | NextBuildAdvisor exclusively | Machine-centric one-build-at-a-time matches real SLS workflow. Cross-WO demand optimization is critical for plate utilization. |
| UnifiedScheduleWizard future | CNC-only (strip SLS code) | Keeps the WO-to-job flow for CNC parts. Removes ~400 lines of SLS build variation code. |
| WO-to-SLS routing | Pre-populate NextBuildAdvisor with `InitialPartId` | When "Schedule Build" is clicked on a WO line, advisor opens with that part pinned as primary, but still shows full demand context. |
| Plate composition editing | In-advisor inline editing (Step 2) | No separate component needed — editing controls live directly in Step 2. |
| Per-tenant customization | Existing systems already sufficient | Stages, approaches, machines, shifts, changeover rules, stack configs, feature flags already provide per-tenant config. Two small settings gaps to fill. |

---

## Phase A: NextBuildAdvisor Enhancement (Plate Composition Editing)

**Goal**: Make NextBuildAdvisor the definitive SLS plate composition tool with full manual override.

### A1. Add `InitialPartId` parameter
**Files**: `NextBuildAdvisor.razor`, `IBuildAdvisorService.cs`, `BuildAdvisorService.cs`
**Complexity**: Low

- Add `[Parameter] public int? InitialPartId { get; set; }` to NextBuildAdvisor
- In `OnParametersSetAsync`, if `InitialPartId` is set, force that part as primary in `OptimizePlateAsync`
- Add `int? forcePrimaryPartId = null` parameter to `OptimizePlateAsync` — reorders candidates to put that part first

### A2. Manual plate composition controls (Step 2)
**File**: `NextBuildAdvisor.razor`
**Complexity**: Medium

Current Step 2 is read-only. Add interactive controls:

1. **Editable plate parts list** — Per-part: stack level dropdown, positions input, remove button
2. **Mutable state** — `List<EditablePlateAllocation> _plateAllocations` initialized from recommendation
3. **"Add Part" button** — picker from the demand table below
4. **"Reset to Recommendation" button** — restores original plate
5. **Live demand coverage bars** — real-time as user edits (like UnifiedScheduleWizard's build coverage bars)
6. **Recalculate print duration** — from `PartAdditiveBuildConfig.GetStackDuration()`, max across all parts
7. **Changeover re-evaluation** — re-check operator availability at new buildEnd

**UI Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Recommendation: Print 76x Body-A — 40.5h, 1x single stack  │
│ [Reset to Recommendation]                                   │
├─────────────────────────────────────────────────────────────┤
│ PLATE COMPOSITION (76 parts, ~40.5h)                        │
│                                                             │
│ PRIMARY  Body-A    Stack: [1x ▾] Positions: [38 ] = 38 pts │
│                    Demand: 42 remaining  [✕ Remove]         │
│                                                             │
│ FILL     End-Cap-B Stack: [1x ▾] Positions: [12 ] = 12 pts │
│                    Demand: 15 remaining  [✕ Remove]         │
│                                                             │
│ [+ Add Part]                                                │
├─────────────────────────────────────────────────────────────┤
│ DEMAND COVERAGE                                             │
│ Body-A:    38/42 (90%) ████████████████████░░                │
│ End-Cap-B: 12/15 (80%) ████████████████░░░░                  │
├─────────────────────────────────────────────────────────────┤
│ ▶ All Outstanding Demand (5 parts)       [expand/collapse]  │
│   Part       Remaining  Due      Priority                   │
│   Body-A     42         Mar 28   Rush      [+ Add to Plate] │
│   End-Cap-B  15         Mar 30   Normal    [+ Add to Plate] │
│   ...                                                       │
└─────────────────────────────────────────────────────────────┘
```

### A3. Update `ExecuteScheduleAsync` to use edited plate
**File**: `NextBuildAdvisor.razor`
**Complexity**: Low

Change `ExecuteScheduleAsync` to build the `MachineProgram` from `_plateAllocations` (user-edited) instead of `_recommendation.Plate.Parts`. Recalculate `EstimatedPrintHours` from the edited allocations.

---

## Phase B: SLS/CNC Routing Split

**Goal**: Remove SLS from UnifiedScheduleWizard. Route all SLS scheduling through NextBuildAdvisor.

### B1. Strip SLS from UnifiedScheduleWizard
**File**: `UnifiedScheduleWizard.razor`
**Complexity**: Medium-High (removes ~400 lines)

**Remove**:
- Step 2 SLS build variations (lines 131-236): `_buildVariations`, `AutoSuggestBuilds`, `AddBuildVariation`, stack selectors
- Step 3 program matching for SLS (lines 276-387): `BuildGroups`, program source selection
- Step 5 downstream programs (SLS-only)
- SLS execution in `ExecuteScheduleAsync` (lines 1413-1500)
- Inner classes: `BuildVariation`, `BuildPartConfig`, `BuildGroup`

**Keep**:
- Step 1 part selection (filter to CNC-only)
- Step 2 CNC job configuration (lines 239-269)
- Step 4 schedule options (reusable for CNC)
- CNC execution path

**Simplify steps**: 5 steps → 3 steps (Select CNC Parts → Configure Jobs → Review & Schedule)

### B2. Route WO page "Schedule Build" to NextBuildAdvisor
**File**: `WorkOrdersView.razor`
**Complexity**: Medium

- For SLS lines (`IsAdditiveLine`): open NextBuildAdvisor with `InitialPartId = line.PartId`
- For CNC lines: continue using UnifiedScheduleWizard
- "Schedule All" on mixed WOs: show separate buttons for SLS and CNC
- Add `NextBuildAdvisor` modal instance + state fields (`_showAdvisor`, `_advisorPartId`)

### B3. Route WorkOrderPanel sidebar to NextBuildAdvisor
**File**: `WorkOrderPanel.razor`, `Index.razor`
**Complexity**: Low

Add per-line action buttons: SLS lines fire `OnNextBuildRequested(partId)`, CNC lines fire `OnScheduleRequested()`.

### B4. Toolbar dropdown
**File**: `SchedulerToolbar.razor`
**Complexity**: Low

Replace single "+ New Job" with dropdown: "SLS Build" (opens advisor) / "CNC Job" (opens wizard).

---

## Phase C: Gantt View Polish (Verification)

All items below are already implemented. Verification pass only.

- **C1**: "+" zone on SLS machine rows — `GanttMachineRow.razor` lines 78-89, CSS `.gantt-next-build-zone`
- **C2**: Drag-and-drop rescheduling — `gantt-viewport.js` + `Index.razor.HandleBarDragAsync`
- **C3**: Changeover visualization — `GanttBuildBar.razor` changeover segments
- **C4**: Shift shading — `GanttViewport.razor.GetNonShiftPeriods()`

---

## Phase D: WorkOrdersView Improvements

### D1. Demand coverage per SLS line
**Complexity**: Low

Add inline indicator: `42 remaining / 38 per build = 2 builds needed`
Uses `PartAdditiveBuildConfig.PlannedPartsPerBuildSingle`.

### D2. Estimated completion date
**Complexity**: Medium

New method on `IBuildAdvisorService`:
```csharp
Task<DateTime> EstimateCompletionDateAsync(int partId, int quantity);
```

Based on: machine availability, build duration at optimal stack, builds needed, downstream durations.
Display as "Est. complete: Apr 15" per WO line.

---

## Phase E: Per-Tenant Customization Audit

### Already configurable per tenant:
| Area | Mechanism | Status |
|------|-----------|--------|
| Production stages & order | `ProductionStage` + `DisplayOrder` via Admin > Stages | Complete |
| Manufacturing approaches | `ManufacturingProcess` + `ProcessStage` per-part | Complete |
| Machine types & capabilities | `Machine` entity + stage assignments | Complete |
| Shift patterns | `OperatingShift` + `MachineShiftAssignment` | Complete |
| Changeover rules | `Machine.AutoChangeoverEnabled` + `ChangeoverMinutes` | Complete |
| Stack levels | `PartAdditiveBuildConfig.EnableDoubleStack/EnableTripleStack` | Complete |
| Build plate configs | `PartAdditiveBuildConfig.PlannedPartsPerBuild*` | Complete |
| CNC setup changeover | `ProcessStage.SetupChangeoverMinutes` | Complete |
| Feature flags | `TenantFeatureFlag` + `FeatureGate` component | Complete |
| Custom fields | `CustomFieldService` + JSON on WO, Part, etc. | Complete |
| Scheduler settings | `SystemSettings` with `scheduler.*` keys | Complete |
| Build plate dimensions | `Machine.BuildLengthMm/BuildWidthMm/BuildHeightMm` | Complete |
| Material constraints | `Machine.SupportedMaterials` + `MachineProgram.MaterialId` | Complete |

### Gaps to fill:
1. **`scheduler.max_part_types_per_plate`** — currently hardcoded to 4 in `OptimizePlateAsync`. Add `SystemSettings` key.
2. **`scheduler.auto_create_downstream_programs`** — currently always creates placeholders. Add setting (default true).

---

## Phase F: Supporting Features

### F1. Inventory integration
**Current**: Inventory module exists but disconnected from production output.
**Gap**: No auto-receipt when production job completes on final stage.
**Implementation**: In stage completion handler, call `InventoryService.ReceiveProductionOutput()` to auto-receive produced parts.

### F2. Shipping
**Current**: Shop floor partial with hardcoded carriers, no persistence. `WorkOrderLine.ShippedQuantity` and `WorkOrder.ActualShipDate` columns exist but are never updated.
**Implementation**: Create `ShipmentService`, `Shipment` model, update shipped quantities, gate with `module.shipping` flag.

### F3. Analytics
**Current**: Analytics pages exist but no scheduler-specific metrics.
**Implementation**: On-time delivery rate, scheduling effectiveness (predicted vs actual), machine utilization trends, build throughput per week.

### F4. Shop floor integration
**Enhancement**: Show upcoming builds on SLS operator view — next 3 builds, changeover times, operator availability.

---

## Phase G: Code Cleanup

- **G1**: Remove dead SLS code from UnifiedScheduleWizard after Phase B (drops ~1564 → ~800 lines)
- **G2**: Verify `ScheduleIssueReport` component is connected and useful
- **G3**: Check if `BuildSuggestionService` is superseded by `BuildAdvisorService` — remove if so

---

## Dependencies

```
Phase A (Advisor Enhancement)  ─── independent ──────────────────
Phase B (SLS/CNC Split)        ─── depends on A (advisor must support InitialPartId)
Phase C (Gantt Polish)         ─── independent (verification only)
Phase D (WO Improvements)      ─── partially depends on B (routing changes)
Phase E (Per-Tenant Audit)     ─── independent (config changes only)
Phase F (Supporting Features)  ─── independent (can be done in any order)
Phase G (Cleanup)              ─── depends on B (after SLS is stripped)
```

**Execution order**: A → B → C → D → G → E → F

---

## Data Model Changes

**No new migrations needed.** All required columns already exist:
- `ProcessStage.SetupChangeoverMinutes` — migration exists (unmerged)
- `WorkOrderLine.ShippedQuantity` — exists
- `WorkOrder.ActualShipDate` — exists
- `ProgramPart.WorkOrderLineId` — exists

**Future migration** (Phase F2 only): `Shipment` table for shipping module.

---

## What Already Exists vs What Needs Building

### Already built (verification/wiring only):
- `BuildAdvisorService` — full demand aggregation, plate optimization, bottleneck analysis
- `NextBuildAdvisor.razor` — 3-step wizard, session tracking, Schedule & Queue Next
- `GanttMachineRow.razor` "+" zone — positioned, clickable, routes to advisor
- Drag-and-drop, shift shading, changeover visualization
- Feature flag system, custom fields, shift management
- CNC setup changeover logic

### Needs building:
- **NextBuildAdvisor manual plate editing** (Phase A2) — new UI controls
- **`InitialPartId` parameter** on NextBuildAdvisor (Phase A1) — small addition
- **`forcePrimaryPartId`** on `OptimizePlateAsync` (Phase A1) — service change
- **SLS code removal** from UnifiedScheduleWizard (Phase B1) — large deletion
- **WO page routing** to advisor for SLS lines (Phase B2) — medium wiring
- **Toolbar dropdown** (Phase B4) — small UI addition
- **Estimated completion date** on WO lines (Phase D2) — new service method + UI
- **Two `SystemSettings` keys** for per-tenant config (Phase E)
