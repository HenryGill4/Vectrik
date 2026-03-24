> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# CHUNK-11: Build Plate Scheduling + Cost Integration

> **Size**: M (Medium) — ~5-7 file edits
> **ROADMAP tasks**: BP.26, BP.27, BP.28
> **Prerequisites**: CHUNK-10 complete

---

## Scope

Update the Scheduler Gantt and Capacity views to handle build plate executions
as single machine-occupation blocks, and wire build cost allocation across parts.

---

## Files to Read First

| File | Why |
|------|-----|
| `Components/Pages/Scheduler/Index.razor` | Gantt view — show build blocks |
| `Components/Pages/Scheduler/Capacity.razor` | Capacity — account for build plates |
| `Services/ISchedulingService.cs` | Scheduling interface |
| `Services/SchedulingService.cs` | Scheduling implementation |
| `Services/PricingEngineService.cs` | Cost allocation for builds |
| `Models/StageExecution.cs` | BuildPackageId from CHUNK-08 |

---

## Tasks

### 1. Gantt: Build-level execution blocks (BP.26)
**File**: `Components/Pages/Scheduler/Index.razor`
- When rendering Gantt bars, detect StageExecutions with a BuildPackageId
- Render build-level executions as a SINGLE wider bar (not one bar per part)
- Bar label: "Build [PackageName] — SLS Printing" (or Depowder, EDM)
- Tooltip: list all parts in the build with quantities
- After part separation (EDM complete), individual part executions render as
  normal individual bars

### 2. Capacity: Build plate as single occupation (BP.27)
**File**: `Components/Pages/Scheduler/Capacity.razor`
- When calculating machine utilization, count a build-level execution as ONE
  occupation on the machine (not N occupations for N parts)
- The duration = build duration (from slice file), not sum of per-part durations
- Update the drill-down modal to show "Build: [name]" instead of individual parts
  for build-level executions

### 3. Build cost allocation (BP.28)
**File**: `Services/PricingEngineService.cs` (or create a `BuildCostService`)
- Add method: `Task<Dictionary<int, decimal>> AllocateBuildCostAsync(int buildPackageId)`
- Build cost = powder cost + gas cost + laser time cost (from BuildFileInfo + rates)
- Allocation formula: `partCost = buildCost * (partQty / totalPartsInBuild)`
- This will be used by Job Costing (Phase 2) but the allocation logic should be
  built now

---

## Verification

1. Build passes
2. Schedule a build package → Gantt shows ONE bar for the SLS printing stage
   (not separate bars for each part)
3. Capacity view correctly shows the machine occupied for the build duration
4. After EDM completion and part separation, individual stages show as normal bars
5. Build cost allocation returns per-part cost breakdown

---

## Files Modified (fill in after completion)

- `Components/Pages/Scheduler/Index.razor` — Gantt bars grouped by BuildPackageId+ProductionStageId into single wider bars with 📦 icon; table view shows "📦 Build [name]" for build-level rows
- `Components/Pages/Scheduler/Capacity.razor` — Drill-down modal shows "📦 Build [name]" for build-level executions instead of part number
- `Services/StageService.cs` — Added `.Include(e => e.BuildPackage)` to `GetScheduledExecutionsAsync`
- `Services/IPricingEngineService.cs` — Added `AllocateBuildCostAsync` method + `BuildCostAllocation` result class
- `Services/PricingEngineService.cs` — Implemented `AllocateBuildCostAsync` with powder/gas/laser cost calculation and per-part allocation by quantity ratio
