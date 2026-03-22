# OpCentrix V3 — Scheduler & Configuration Refinement Plan

> **Status**: Active — refined plan based on deep codebase audit (2026-03-22)
> **Branch**: `claude/fix-scheduling-routing-JHUOI`
> **Prerequisite**: All 308 tests passing, Phases 1-14 complete (scheduling fixes, multi-machine distribution, changeover optimization)
> **Principle**: _No hardcoded logic. Everything is a setting. Seed with sensible defaults. Admins edit everything._

---

## Table of Contents

1. [Audit: Hardcoded Logic Inventory](#1-audit-hardcoded-logic-inventory)
2. [Configuration Strategy](#2-configuration-strategy)
3. [New System Settings to Seed](#3-new-system-settings-to-seed)
4. [Model Changes](#4-model-changes)
5. [Service Changes](#5-service-changes)
6. [Scheduler UI Redesign](#6-scheduler-ui-redesign)
7. [Production Flow Walkthrough](#7-production-flow-walkthrough)
8. [Admin UI Enhancements](#8-admin-ui-enhancements)
9. [Execution Phases](#9-execution-phases)
10. [Previous Work (Phases 1-14)](#10-previous-work-phases-1-14)

---

## 1. Audit: Hardcoded Logic Inventory

Full codebase audit of every hardcoded assumption that should be a configurable setting.

### 🔴 Critical — Must Fix (Logic Errors from Hardcoding)

| # | Location | Hardcoded Value | Problem | Fix |
|---|----------|----------------|---------|-----|
| H1 | `Machine.cs:92-94` | `MachineType == "SLS" \|\| "Additive"` | `IsAdditiveMachine` computed property uses string literals. Adding a new additive machine type (e.g., "DMLS", "MJF", "EBM") won't work. | Add `bool IsAdditiveMachine` as a **persisted DB column** on Machine (default `false`). Seed existing SLS machines with `true`. Remove the computed `[NotMapped]` property. |
| H2 | `BuildSchedulingService.cs:420` | `m.MachineType == "SLS" \|\| m.MachineType == "Additive"` | `FindBestBuildSlotAsync` filters additive machines with same hardcoded strings. New additive machine types won't be found. | Replace with `m.IsAdditiveMachine` (after H1 fix). |
| H3 | `LearningService.cs:9-10` | `Alpha = 0.3`, `AutoSwitchThreshold = 3` | EMA smoothing factor and auto-switch threshold are `private const`. Admin cannot tune learning speed or decide how many samples before auto-switching. | Move to SystemSettings: `scheduling.ema_alpha` = `"0.3"`, `scheduling.ema_auto_switch_samples` = `"3"`. |
| H4 | `BuildPlanningService.cs:828` | `DefaultBuildNameTemplate = "{PARTS}-{DATE}-{SEQ}"` | Build naming template is a hardcoded `private const`. Admin cannot customize build naming convention. | Move to SystemSettings: `builds.name_template` = `"{PARTS}-{DATE}-{SEQ}"`. |
| H5 | `BuildPlanningService.cs:858` | `machine?.Name ?? "SLS"` | Fallback machine name in build name generation uses "SLS" literal. | Use `machine?.MachineId ?? machine?.Name ?? "Unknown"`. |

### 🟡 Important — Should Fix (UI Behavior Hardcoded)

| # | Location | Hardcoded Value | Problem | Fix |
|---|----------|----------------|---------|-----|
| H6 | `Index.razor:285` | `TimeSpan.FromSeconds(30)` | Auto-refresh interval is hardcoded to 30 seconds. Too fast for some shops, too slow for high-throughput. | SystemSettings: `scheduler.auto_refresh_seconds` = `"30"`. |
| H7 | `Index.razor:186-190` | `-7/+14/+21 days`, `6.0 px/hr` | Viewport date range and default zoom are hardcoded. Different shops have different planning horizons. | SystemSettings: `scheduler.gantt_lookback_days` = `"7"`, `scheduler.gantt_lookahead_days` = `"14"`, `scheduler.gantt_data_range_days` = `"21"`, `scheduler.gantt_default_zoom` = `"6.0"`. |
| H8 | `Capacity.razor:26-29` | `7, 14, 30, 90` day range buttons | Capacity dashboard quick-range buttons are hardcoded. | These are reasonable UX defaults. **Keep as-is** — they are UI affordances, not business logic. |
| H9 | `GanttView.razor:124` | `m.IsAdditiveMachine` sort priority | Gantt machine ordering puts additive first. Some shops want CNC first. | Add a `SortPriority` or `GanttDisplayOrder` on Machine (already has `Priority` field — reuse it). The sort is: `OrderBy(m => m.Priority).ThenBy(m => m.MachineType)`. After H1, stop special-casing `IsAdditiveMachine` in sort. |
| H10 | `SchedulingService.cs:446-450` | `m.IsAdditiveMachine` filter | ResolveMachines excludes additive machines from batch/part-level fallback. Correct intent but relies on H1 computed property. | After H1 fix, this naturally uses the DB flag. No logic change needed — just flows from H1. |

### ✅ Already Configurable (No Change Needed)

| Item | Where Configured | Admin Editable? |
|------|-----------------|-----------------|
| Machine changeover minutes | `Machine.ChangeoverMinutes` per machine | ✅ via `/admin/machines/{id}` |
| Auto-changeover enabled | `Machine.AutoChangeoverEnabled` per machine | ✅ via `/admin/machines/{id}` |
| Build plate capacity | `Machine.BuildPlateCapacity` per machine | ✅ via `/admin/machines/{id}` |
| Stage default durations | `ProductionStage.DefaultDurationHours` | ✅ via `/admin/stages` |
| Stage hourly rates | `ProductionStage.DefaultHourlyRate` | ✅ via `/admin/stages` |
| Stage setup minutes | `ProductionStage.DefaultSetupMinutes` | ✅ via `/admin/stages` |
| Stage icons & colors | `ProductionStage.StageIcon`, `.StageColor` | ✅ via `/admin/stages` |
| Stage machine assignments | `ProductionStage.AssignedMachineIds` | ✅ via `/admin/stages` |
| Operating shifts | `OperatingShift` table | ✅ via `/admin/shifts` |
| Machine priority | `Machine.Priority` (1-10) | ✅ via `/admin/machines/{id}` |
| Manufacturing approach | `ManufacturingApproach.RequiresBuildPlate` | ✅ via DB (used correctly by `IsAdditiveLine()`) |
| Process stage durations | `ProcessStage.SetupTimeMinutes`, `.RunTimeMinutes` | ✅ via process editor |
| Process stage machine prefs | `ProcessStage.AssignedMachineId`, `.PreferredMachineIds` | ✅ via process editor |
| Batch capacity | `ManufacturingProcess.DefaultBatchCapacity` | ✅ via process editor |
| Plate release trigger | `ManufacturingProcess.PlateReleaseStageId` | ✅ via process editor |
| All existing SystemSettings | 40+ settings across 10 categories | ✅ via `/admin/settings` |

---

## 2. Configuration Strategy

### Principle: Three Tiers

```
Tier 1: SystemSetting (global defaults — admin editable at /admin/settings)
  ↓ overridden by
Tier 2: Entity-level config (Machine, ProductionStage, ManufacturingProcess — admin editable in entity editors)
  ↓ overridden by
Tier 3: Per-record config (ProcessStage, StageExecution — set per part process or per job)
```

### SystemSettings Service Pattern

Services that need settings should load them once at startup or per-request via a helper:

```csharp
// Pattern: SettingsHelper reads from DB, caches per-request
var alpha = await GetSettingAsync<double>("scheduling.ema_alpha", 0.3);
var refreshSeconds = await GetSettingAsync<int>("scheduler.auto_refresh_seconds", 30);
```

The `GetSettingAsync<T>` method returns the DB value if it exists, otherwise the default. This means the system works out of the box with seeded defaults, but admins can override anything.

---

## 3. New System Settings to Seed

Add these to `DataSeedingService.SeedSystemSettingsAsync()`:

```
Category: Scheduling
─────────────────────────────────────────────────────────────────
scheduling.ema_alpha              | 0.3   | EMA smoothing factor for duration learning (0.0-1.0, higher = faster adaptation)
scheduling.ema_auto_switch_samples| 3     | Number of actual samples before auto-switching estimate source to "Auto"
scheduling.parallel_build_limit   | 0     | Max concurrent builds per additive machine (0 = unlimited by plate capacity)

Category: Scheduler UI
─────────────────────────────────────────────────────────────────
scheduler.auto_refresh_seconds    | 30    | Auto-refresh interval for the scheduler dashboard (seconds)
scheduler.gantt_lookback_days     | 7     | Days of history to show on the Gantt chart
scheduler.gantt_lookahead_days    | 14    | Days of future to show in the Gantt viewport
scheduler.gantt_data_range_days   | 21    | Total days of data to query for Gantt rendering
scheduler.gantt_default_zoom      | 6.0   | Default pixels-per-hour zoom on the Gantt chart

Category: Builds
─────────────────────────────────────────────────────────────────
builds.name_template              | {PARTS}-{DATE}-{SEQ} | Template for auto-generated build names. Tokens: {PARTS}, {MACHINE}, {DATE}, {SEQ}, {MATERIAL}
builds.default_batch_capacity     | 60    | Default batch capacity for new manufacturing processes
```

---

## 4. Model Changes

### 4A. Machine.cs — Replace Computed Property with DB Column

**Current** (lines 88-94):
```csharp
[NotMapped]
public bool IsAdditiveMachine =>
    MachineType.Equals("SLS", StringComparison.OrdinalIgnoreCase)
    || MachineType.Equals("Additive", StringComparison.OrdinalIgnoreCase);
```

**New**:
```csharp
/// <summary>
/// Whether this machine is an additive/build-plate machine (SLS, DMLS, MJF, EBM, etc.).
/// Persisted in DB so admins can set this for any machine type.
/// </summary>
public bool IsAdditiveMachine { get; set; }
```

- **Migration**: Add column with `DEFAULT 0`, then `UPDATE` existing rows where `MachineType IN ('SLS', 'Additive')` to set `IsAdditiveMachine = 1`.
- **Seed update**: Set `IsAdditiveMachine = true` on M4-1, M4-2 in `GetExpectedMachines()` and `SeedMachinesAsync()`.
- **Admin UI**: Add checkbox on machine edit form: "☑ Additive Machine (uses build plates)".
- **Impact**: Every consumer of `IsAdditiveMachine` (SchedulingService, GanttView, Index.razor, WorkOrdersView) automatically works — the property name stays the same.

### 4B. No Other Model Changes Required

All other models are already configurable through their entity editors. The SystemSettings additions (section 3) don't require model changes — they use the existing `SystemSetting` entity.

---

## 5. Service Changes

### 5A. LearningService.cs — Read EMA Constants from Settings

**Current** (lines 9-10):
```csharp
private const double Alpha = 0.3;
private const int AutoSwitchThreshold = 3;
```

**New**: Inject `TenantDbContext`, read from SystemSettings with fallback:
```csharp
private async Task<double> GetAlphaAsync()
{
    var setting = await _db.SystemSettings
        .FirstOrDefaultAsync(s => s.Key == "scheduling.ema_alpha");
    return setting is not null && double.TryParse(setting.Value, out var v) ? v : 0.3;
}
```

Same pattern for `AutoSwitchThreshold`.

### 5B. BuildPlanningService.cs — Read Build Name Template from Settings

**Current** (line 828):
```csharp
private const string DefaultBuildNameTemplate = "{PARTS}-{DATE}-{SEQ}";
```

**New**: Read from `builds.name_template` SystemSetting, fall back to `"{PARTS}-{DATE}-{SEQ}"`.

Also fix line 858: change `"SLS"` fallback to `"Unknown"`.

### 5C. BuildSchedulingService.cs — Use `IsAdditiveMachine` Flag

**Current** (line 420):
```csharp
&& (m.MachineType == "SLS" || m.MachineType == "Additive"))
```

**New** (after H1 migration):
```csharp
&& m.IsAdditiveMachine)
```

### 5D. Index.razor — Read Scheduler UI Settings

**Current** (line 285): `TimeSpan.FromSeconds(30)`
**Current** (lines 186-190): Hardcoded viewport ranges

**New**: Load settings in `OnInitializedAsync`:
```csharp
var refreshSec = await GetSettingAsync("scheduler.auto_refresh_seconds", 30);
var lookback = await GetSettingAsync("scheduler.gantt_lookback_days", 7);
var lookahead = await GetSettingAsync("scheduler.gantt_lookahead_days", 14);
var dataRange = await GetSettingAsync("scheduler.gantt_data_range_days", 21);
var defaultZoom = await GetSettingAsync("scheduler.gantt_default_zoom", 6.0);
```

### 5E. GanttView.razor — Machine Sort by Priority (Remove IsAdditive Special Case)

**Current** (lines 121-128):
```csharp
.OrderByDescending(m => m.IsAdditiveMachine)  // additive first
.ThenBy(m => m.Priority)
```

**New**:
```csharp
.OrderBy(m => m.Priority)  // admins control order via Priority field
.ThenBy(m => m.MachineType, StringComparer.OrdinalIgnoreCase)
.ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
```

Admins who want additive first just set those machines to Priority 1 (which they already are in seed data).

---

## 6. Scheduler UI Redesign

### Current State (6 tabs)

| Tab | View File | Lines | Purpose |
|-----|-----------|-------|---------|
| 🔲 Gantt | `GanttView.razor` | 153 | Machine Gantt chart |
| 📦 Builds | `BuildsView.razor` | 415 | SLS build pipeline + per-machine timelines |
| 📋 Orders | `WorkOrdersView.razor` | 1,076 | Demand dashboard, WO cards, scheduling actions |
| 🔀 Part Path | `PartPathView.razor` | 211 | Per-job Gantt, part/priority filters |
| 🔧 Stages | `StagesView.razor` | 225 | Stage queue cards with metrics |
| 📊 Table | `TableView.razor` | 351 | Flat execution table, search, bulk actions, CSV |

**Problems**:
1. `Index.razor` loads ALL data (11 queries) on every 30s refresh regardless of active tab
2. WorkOrdersView at 1,076 lines is too large — mixes demand analysis with build scheduling actions
3. PartPathView and TableView overlap — both show execution lists, just different visualizations
4. No KPI strip or action queue — operators must hunt across tabs for "what should I do next"

### Proposed: 5 Tabs

| Tab | Name | Primary User | Purpose |
|-----|------|-------------|---------|
| 1 | **🏭 Production** | Supervisors, Managers | Gantt + KPI strip + next-action queue. The "big picture" view. |
| 2 | **📋 Demand** | Schedulers | Work orders, demand stats, scheduling actions. Renamed from "Orders". |
| 3 | **📦 Builds** | SLS Operators, Schedulers | Build pipeline (unchanged — it's well-structured). |
| 4 | **🔧 Floor** | Operators | Stage queue cards + active work. Renamed from "Stages". Operator-focused. |
| 5 | **📊 Data** | Analysts, Managers | Table view + Part Path as a toggle. CSV export. |

### Tab 1: 🏭 Production (Gantt + KPI + Actions)

**Components**:
- `KpiStrip.razor` (NEW) — 6-8 stat cards at the top:
  - Jobs in Progress | Due Today | Overdue | Avg Lead Time | Machine Utilization % | Unassigned Tasks | Builds Active | Conflicts
  - Each card: value, trend arrow (vs yesterday), click to filter Gantt
  - Data: computed from existing `_executions`, `_buildPackages`, `_workOrders`
- `ActionQueue.razor` (NEW) — collapsible sidebar or panel:
  - Top 10 "next actions" ranked by urgency: overdue stages → approaching due dates → unassigned executions → ready builds
  - Click action → navigate to relevant tab/modal
  - Uses existing data, no new queries
- `GanttView.razor` (existing, minor changes):
  - Remove `IsAdditiveMachine` sort priority (use `Priority` field)
  - Add department grouping headers (SLS, Post-Process, Machining, etc.) 
  - Keep existing legend, unassigned row, conflict highlighting
- `UnscheduledSidebar.razor` (existing, no change)

**Data loading**: Only loads Gantt-specific data when this tab is active (executions, machines, machine timelines).

### Tab 2: 📋 Demand (Work Orders)

**Split from 1,076-line WorkOrdersView into**:
- `DemandView.razor` (~400 lines) — demand stats strip, WO list/cards, per-WO scheduling actions
- `WoCard.razor` (NEW, ~200 lines) — extracted: single WO card with lines, status badges, action buttons
- Scheduling modals remain as separate components (already extracted)

**Changes**:
- Remove inline build creation logic (move to Builds tab or modal)
- Simplify to: "Here is demand. Click to schedule." → triggers appropriate modal

**Data loading**: Only loads work orders, build templates, suggestions when this tab is active.

### Tab 3: 📦 Builds (unchanged)

`BuildsView.razor` (415 lines) — already well-structured:
- Pipeline workflow indicator (Draft → Sliced → Ready → Scheduled → Printing → PostPrint)
- Per-machine SLS timelines
- Build actions (schedule, split, etc.)

**Data loading**: Only loads build packages and SLS machine timelines when active.

### Tab 4: 🔧 Floor (Operator View)

`FloorView.razor` (rename from StagesView, enhance):
- Stage queue cards with metrics (queued/active/done/failed)
- **NEW**: "My Work" section at top — filtered by logged-in operator's assigned stages
- **NEW**: Stage transition buttons — operator can start/complete/pause directly from card
- Hours queued bar chart per stage
- Batch info overlay

**Data loading**: Only loads executions and stage data when active.

### Tab 5: 📊 Data (Table + Part Path Toggle)

`DataView.razor` (merge TableView + PartPathView):
- Toggle: "📊 Table" | "🔀 Part Path"
- Table mode: existing flat execution table with search, filters, bulk actions, CSV export
- Part Path mode: existing per-job Gantt
- Shared filter bar (status, priority, date range, part search)

**Data loading**: Only loads execution data when active.

### Lazy Loading Architecture

```
Index.razor
├── OnInitializedAsync(): Load machines, stages, parts (shared reference data — small, cached)
├── OnTabChanged(tab):
│   ├── "production" → LoadGanttDataAsync()     → executions, timelines, unscheduled
│   ├── "demand"     → LoadDemandDataAsync()     → workOrders, buildTemplates, suggestions
│   ├── "builds"     → LoadBuildsDataAsync()     → buildPackages, slsMachines
│   ├── "floor"      → LoadFloorDataAsync()       → executions (filtered by status)
│   └── "data"       → LoadDataViewAsync()         → executions (all in date range)
├── Auto-refresh timer: Only refreshes the ACTIVE tab's data
└── Tab data cached: Don't re-query when switching back unless stale (>30s by default)
```

This reduces initial load from 11 queries to ~4 (reference data only), and ongoing refreshes from 11 queries to ~3-4 (active tab only).

---

## 7. Production Flow Walkthrough

End-to-end flow through the system, from order to shipment, with every decision point checked for configurability.

### Step 1: Work Order Created
- **Input**: Customer PO or internal request → WO created at `/workorders/create`
- **Config**: `numbering.wo_prefix` + `numbering.wo_digits` → WO number format (e.g., "WO-00042")
- **Config**: `DefaultDueDateDays` → default due date offset
- **Status**: Draft → Released
- ✅ All configurable

### Step 2: Part Identified, Manufacturing Process Loaded
- **Input**: WO lines reference Parts. Each Part has a `ManufacturingProcess` (1:1).
- **Config**: ManufacturingProcess defines the full stage sequence, processing levels, durations, batch capacity, plate release trigger.
- **Config**: `ManufacturingApproach.RequiresBuildPlate` determines additive vs subtractive routing.
- ✅ All configurable via Part editor → Process tab

### Step 3: Build Created (Additive Parts)
- **Input**: For additive parts, a BuildPackage is created from a BuildTemplate or manually.
- **Config**: `builds.name_template` → auto-generated build name
- **Config**: Machine selection uses `Machine.IsAdditiveMachine` flag (H1 fix)
- **Config**: `Machine.BuildPlateCapacity` → how many plates a machine can queue
- ✅ Configurable after H1/H4 fixes

### Step 4: Build Scheduled
- **Input**: Build gets a time slot on an additive machine.
- **Config**: `Machine.AutoChangeoverEnabled` + `Machine.ChangeoverMinutes` → gap between builds
- **Config**: `OperatingShift` → shift-aware scheduling (or 24/7 for continuous machines)
- **Config**: `FindBestBuildSlotAsync` uses `Machine.IsAdditiveMachine` (H2 fix)
- ✅ Configurable after H2 fix

### Step 5: Build Prints → Build-Level Post-Processing
- **Input**: SLS printing completes. Build-level stages execute (Depowdering, Heat Treatment, Wire EDM).
- **Config**: `ProcessStage` with `ProcessingLevel.Build` — durations, machines, all from ManufacturingProcess
- **Config**: Plate release trigger from `ManufacturingProcess.PlateReleaseStageId`
- ✅ All configurable

### Step 6: Plate Release → Parts Individualized → Batches Created
- **Input**: Plate release trigger stage completes → PartInstances created → assigned to ProductionBatches.
- **Config**: `ManufacturingProcess.DefaultBatchCapacity` → batch size
- **Config**: `ProcessStage.BatchCapacityOverride` → per-stage batch size override
- ✅ All configurable

### Step 7: Batch/Part Stages Execute
- **Input**: Sandblasting (batch), CNC Machining (per-part), Laser Engraving (batch), QC (per-part).
- **Config**: Each ProcessStage has compound duration: `SetupDurationMode` + `RunDurationMode` + times
- **Config**: Machine assignment via 9-step `ResolveMachines()` priority chain
- **Config**: EMA learning updates estimates: `scheduling.ema_alpha` smoothing factor (H3 fix)
- ✅ Configurable after H3 fix

### Step 8: Quality Control
- **Input**: QC inspection per-part.
- **Config**: `ProcessStage.RequiresQualityCheck` flag, `quality.require_fair` setting
- **Config**: Work Instructions + Sign-Off Checklists per stage
- ✅ All configurable

### Step 9: Packaging & Shipping
- **Input**: Final stages complete → job status → Completed.
- **Config**: `shipping.require_coc` → Certificate of Conformance requirement
- **Config**: `shipping.default_carrier` → default carrier
- ✅ All configurable

### Summary: Config Coverage

| Flow Step | Hardcoded Items Found | Fix ID |
|-----------|----------------------|--------|
| WO Creation | None | — |
| Part/Process | None | — |
| Build Creation | Build name template, "SLS" fallback | H4, H5 |
| Build Scheduling | Additive machine filter | H1, H2 |
| Build Post-Process | None | — |
| Plate Release | None | — |
| Batch/Part Stages | EMA constants | H3 |
| QC | None | — |
| Shipping | None | — |
| Scheduler UI | Refresh interval, viewport ranges, sort order | H6, H7, H9 |

**10 items to fix. Everything else is already configurable.** The system is in remarkably good shape.

---

## 8. Admin UI Enhancements

### 8A. Machine Edit Form — Add "Additive Machine" Checkbox

In `/admin/machines/{id}` edit form, add:
```html
<label><input type="checkbox" @bind="machine.IsAdditiveMachine" /> Additive Machine (uses build plates)</label>
```

This replaces the invisible computed property with an explicit admin-visible toggle.

### 8B. Settings Page — Scheduling Category

The existing `/admin/settings` page with category tabs automatically shows new settings. No UI changes needed — just seed the new settings (section 3) and they appear in the appropriate category tabs.

### 8C. Admin/Stages.razor — Add Machine Assignment Editor

The Stages admin page currently shows basic info but doesn't let admins edit `AssignedMachineIds` (which machines can run each stage). Add a multi-select or checkbox list of machines to the stage edit form.

**Current**: Stage edit form has Info, Options, Custom Fields tabs.
**Add to Options tab**: "Assigned Machines" section with checkboxes for each active machine.

---

## 9. Execution Phases

### Phase A: Hardcoded Elimination (H1-H5) — No UI changes
**Scope**: Model migration, service fixes, seed updates
**Files**: Machine.cs, BuildSchedulingService.cs, LearningService.cs, BuildPlanningService.cs, DataSeedingService.cs
**Tests**: Update existing tests for new IsAdditiveMachine column
**Risk**: Low — behavioral equivalence, just source of truth moves from code to DB

| Step | Task | Files |
|------|------|-------|
| A1 | Add `IsAdditiveMachine` bool column to Machine | `Machine.cs`, migration |
| A2 | Seed `IsAdditiveMachine = true` for SLS machines | `DataSeedingService.cs` |
| A3 | Replace `MachineType == "SLS"` in BuildSchedulingService | `BuildSchedulingService.cs:420` |
| A4 | Remove `[NotMapped]` computed property from Machine | `Machine.cs:88-94` |
| A5 | Add machine edit checkbox in admin UI | `Admin/Machines/{id}` page |
| A6 | Move EMA constants to SystemSettings | `LearningService.cs`, `DataSeedingService.cs` |
| A7 | Move build name template to SystemSettings | `BuildPlanningService.cs`, `DataSeedingService.cs` |
| A8 | Fix "SLS" fallback string | `BuildPlanningService.cs:858` |

### Phase B: Seed New System Settings
**Scope**: Add scheduling/UI settings to seed data
**Files**: DataSeedingService.cs
**Risk**: None — additive only, existing settings unchanged

| Step | Task |
|------|------|
| B1 | Add 11 new SystemSettings to `SeedSystemSettingsAsync()` |
| B2 | Add `EnsureSystemSettingsAsync()` for existing databases (idempotent) |

### Phase C: Scheduler UI Settings Integration
**Scope**: Read settings from DB instead of hardcoded values
**Files**: Index.razor, GanttView.razor
**Risk**: Low — same defaults, just sourced from DB

| Step | Task |
|------|------|
| C1 | Index.razor: Load refresh interval from SystemSettings |
| C2 | Index.razor: Load viewport/data range from SystemSettings |
| C3 | GanttView.razor: Sort by Priority only (remove IsAdditive special case) |

### Phase D: Lazy Loading per Tab
**Scope**: Split monolithic LoadData() into per-tab methods
**Files**: Index.razor
**Risk**: Medium — must ensure tab switch doesn't lose state, auto-refresh only hits active tab

| Step | Task |
|------|------|
| D1 | Create per-tab load methods (LoadGanttDataAsync, LoadDemandDataAsync, etc.) |
| D2 | OnTabChanged handler: call appropriate load method |
| D3 | Auto-refresh timer: only refresh active tab's data |
| D4 | Tab data staleness: track last-loaded timestamp per tab |

### Phase E: UI Tab Consolidation
**Scope**: Restructure to 5-tab layout, extract components
**Files**: Index.razor, SchedulerToolbar.razor, new KpiStrip.razor, new ActionQueue.razor, new WoCard.razor, new FloorView.razor, new DataView.razor
**Risk**: Medium-High — large UI restructure, must not break existing functionality

| Step | Task |
|------|------|
| E1 | Create KpiStrip.razor component |
| E2 | Create ActionQueue.razor component |
| E3 | Integrate into GanttView (Production tab) |
| E4 | Extract WoCard.razor from WorkOrdersView |
| E5 | Rename/refactor WorkOrdersView → DemandView |
| E6 | Rename StagesView → FloorView, add "My Work" section |
| E7 | Merge TableView + PartPathView → DataView with toggle |
| E8 | Update SchedulerToolbar for 5-tab layout |
| E9 | Update Index.razor tab switching |

### Phase F: Admin UI Polish
**Scope**: Machine editor checkbox, stage machine assignment editor
**Files**: Machine edit page, Admin/Stages.razor

| Step | Task |
|------|------|
| F1 | Add IsAdditiveMachine checkbox to machine edit form |
| F2 | Add machine assignment multi-select to stage edit form |

### Execution Order & Dependencies

```
Phase A (H1-H5, H-fixes) ← MUST DO FIRST
    ↓
Phase B (seed settings) ← depends on A for new Machine column
    ↓
Phase C (read settings in UI) ← depends on B for settings existing
    ↓
Phase D (lazy loading) ← independent of A-C, but easier after C
    ↓
Phase E (tab consolidation) ← depends on D for lazy loading architecture
    ↓
Phase F (admin polish) ← depends on A for IsAdditiveMachine column
```

**Phases A + B can be done first as a tight, testable unit.**
**Phase C is a small follow-up.**
**Phases D + E are the big UI refactor — can be planned in detail when A-C are solid.**
**Phase F can be done anytime after A.**

---

## 10. Previous Work (Phases 1-14)

All previous phases are complete ✅:

- **Phases 1-4**: Core Manufacturing Process entities, service layer, UI, cleanup
- **Phase 5-7**: String→int Machine ID fixes across SchedulingService, GanttView, StagesView
- **Phase 8-10**: SLS overlap fixes, machine timeline loading, continuous operation warnings
- **Phase 11-12**: Machine assignments for all stages, surface finishing, additive seeding
- **Phase 13**: Debug page enhancements with DB stats
- **Phase 14**: Multi-machine SLS build distribution & same-build changeover optimization

All 308 tests passing ✅. Build successful ✅.
