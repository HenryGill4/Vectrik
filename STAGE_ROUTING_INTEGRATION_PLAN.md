# Stage Routing & Manufacturing Approach Integration Plan

> **Created**: 2026-03-20
> **Status**: PHASES 1, 2, 4 COMPLETE — Phase 3 (Per-Part Plate Release Override) DEFERRED
> **Purpose**: Connect the stage/routing system with manufacturing approaches so users can configure
> how parts flow through production based on their manufacturing approach.

---

## Current State Analysis

### What Exists Today

#### 1. Manufacturing Approaches (`ManufacturingApproach` model)
- User-selectable on Part creation
- Properties:
  - `IsAdditive` → show Stacking/Batch tabs in Part editor
  - `RequiresBuildPlate` → part goes through build system before per-part stages
  - `HasPostPrintBatching` → show depowdering/heat-treatment fields
  - **`DefaultRoutingTemplate`** → JSON array of stage slugs (EXISTS BUT NOT USED)

#### 2. Production Stages (`ProductionStage` model)
- Define available stages (SLS Printing, CNC Machining, Wire EDM, QC, etc.)
- Key properties:
  - `IsBuildLevelStage` → stage applies to entire build plate (print, depowder, heat-treat, EDM)
  - `TriggerPlateRelease` → when this stage completes, release parts from plate
  - `RequiresMachineAssignment`, `AssignedMachineIds`, `Department`

#### 3. Part Stage Requirements (`PartStageRequirement` model)
- Per-part routing: which stages in what order with what overrides
- Manually configured in Part Edit → Routing tab
- Properties: `ExecutionOrder`, `EstimatedHours`, `AssignedMachineId`, `IsRequired`, etc.

#### 4. Build System Integration (from `SCHEDULER_WORKFLOW_PLAN.md`)
```
SLS Part Flow:
  WorkOrder → BuildPackage → Build-Level Stages (print, depowder, heat-treat, EDM)
    → Plate Release (triggered by TriggerPlateRelease stage) 
    → PartInstances → Per-Part Jobs (from PartStageRequirements) → Completion

CNC Part Flow:
  WorkOrder → Job → Stage Executions (from PartStageRequirements) → Completion
```

### The Gap (What's Missing)

1. **`DefaultRoutingTemplate` is defined but never applied**
   - When user selects a ManufacturingApproach, the routing tab stays empty
   - User must manually add each stage

2. **No distinction between build-level and per-part stages in routing**
   - Part routing shows ALL stages, but SLS parts only use per-part stages after plate release
   - Build-level stages (print, depowder, heat-treat, EDM) are handled by the build system
   - Per-part stages (CNC finishing, QC, engrave, coat, ship) are handled by jobs

3. **No user-configurable separation**
   - Users can't define "these stages happen at build level, these after plate release"
   - The plate release trigger is a single stage flag, not a user-configurable cutover point

4. **Manufacturing Approach doesn't suggest stage additions**
   - Changing approach to "SLS-Based" should auto-suggest stages from template
   - Currently does nothing

---

## Target Architecture

### Core Concepts

#### Routing Segments (New Concept)
For build-plate parts, the routing needs TWO segments:

```
┌─────────────────────────────────────────────────────────────────────┐
│ BUILD-LEVEL SEGMENT (shared across all parts on plate)            │
│   • Defined by ProductionStage.IsBuildLevelStage = true           │
│   • Executions created when build is scheduled                     │
│   • Ends when TriggerPlateRelease stage completes                  │
│   • Examples: SLS Print → Depowder → Heat Treat → Wire EDM        │
└─────────────────────────────────────────────────────────────────────┘
                              ↓ Plate Release
┌─────────────────────────────────────────────────────────────────────┐
│ PER-PART SEGMENT (individual part jobs after release)             │
│   • Defined by PartStageRequirement on the Part                    │
│   • Jobs created per (PartId, WorkOrderLineId) when plate releases │
│   • Examples: CNC Finishing → QC → Engrave → Coat → Ship          │
└─────────────────────────────────────────────────────────────────────┘
```

#### Manufacturing Approach Routing Templates (Enhanced)
The `DefaultRoutingTemplate` should be used AND enhanced:

```json
{
  "buildLevelStages": ["sls-printing", "depowdering", "heat-treatment", "wire-edm"],
  "perPartStages": ["cnc-machining", "qc", "laser-engraving", "packaging"],
  "plateReleaseTrigger": "wire-edm"
}
```

**Migration path**: Keep simple JSON array for backward compat, but interpret based on
`IsBuildLevelStage` flag when applying.

---

## Proposed Changes

### Phase 1: Apply DefaultRoutingTemplate (Quick Win)

When user selects a ManufacturingApproach on Part Edit, auto-populate the Routing tab
with stages from the template.

**Changes:**
1. **`Components/Pages/Parts/Edit.razor` — `OnApproachChanged`**
   - After setting `ManufacturingApproachId`, check if routing is empty
   - If empty, parse `DefaultRoutingTemplate` and add `PartStageRequirement` entries
   - Show confirmation: "Apply default routing from SLS-Based template? (5 stages)"

2. **New helper in `IPartService`**
   ```csharp
   Task<List<PartStageRequirement>> GenerateRoutingFromTemplateAsync(
       int partId, ManufacturingApproach approach, string currentUser);
   ```

**UX Flow:**
```
User selects "SLS-Based" approach
  ↓
Modal appears: "Apply default routing?"
  • SLS Printing
  • Depowdering  
  • Heat Treatment
  • Wire EDM
  • QC
  [Apply] [Skip]
  ↓
Routing tab populated with 5 stages, user can customize
```

### Phase 2: Build-Level vs Per-Part Separation

Clarify in UI which stages are build-level (handled by build system) vs per-part
(handled by jobs after plate release).

**Changes:**

1. **Part Edit → Routing tab** — Two sections:
   ```
   BUILD-LEVEL STAGES (applied to entire plate)
   ┌─────────────────────────────────────────────┐
   │ 1. SLS Printing       8.0 hrs   [handled by build system]
   │ 2. Depowdering        1.0 hrs
   │ 3. Heat Treatment     4.0 hrs
   │ 4. Wire EDM           2.0 hrs   ← Plate release trigger
   └─────────────────────────────────────────────┘
   
   PER-PART STAGES (individual jobs after release)
   ┌─────────────────────────────────────────────┐
   │ 1. CNC Finishing      2.0 hrs   M: CNC1
   │ 2. QC Inspection      0.5 hrs
   │ 3. Laser Engraving    0.25 hrs  M: LASER1
   │ 4. Packaging          0.25 hrs
   └─────────────────────────────────────────────┘
   ```

2. **Filter PartStageRequirement by `IsBuildLevelStage`**
   - Build-level stages: `ProductionStage.IsBuildLevelStage = true`
   - Per-part stages: `ProductionStage.IsBuildLevelStage = false`

3. **Info callouts explaining the flow:**
   - "Build-level stages run once for the entire build plate"
   - "Per-part stages run for each individual part after plate release"

### Phase 3: Configurable Plate Release Point

Instead of hardcoding Wire EDM as the release trigger, let users choose which
stage triggers plate release in the routing.

**Option A: Per-Stage Flag (Current)**
- `ProductionStage.TriggerPlateRelease` is already implemented
- Admin can set this on any build-level stage
- Simple but global (affects all parts using that stage)

**Option B: Per-Part Override (Recommended)**
- Add `PartStageRequirement.TriggerPlateRelease` (overrides stage default)
- Part A might release after Wire EDM
- Part B might release after Heat Treatment (no EDM needed)
- More flexible, per-part control

**Changes for Option B:**
1. **`Models/PartStageRequirement.cs`** — Add:
   ```csharp
   /// <summary>
   /// Override: When this stage completes for this part, trigger plate release.
   /// If null, falls back to ProductionStage.TriggerPlateRelease.
   /// </summary>
   public bool? TriggerPlateReleaseOverride { get; set; }
   ```

2. **`Services/StageService.cs`** — `CompleteStageExecutionAsync`:
   - Check `requirement.TriggerPlateReleaseOverride ?? stage.TriggerPlateRelease`

3. **Part Edit → Routing tab** — Add "Release Plate" toggle per build-level stage

### Phase 4: Manufacturing Approach Admin Page

Let users create/edit manufacturing approaches with full routing templates.

**Changes:**

1. **`Components/Pages/Admin/ManufacturingApproaches.razor`** — New admin page
   - List all approaches with edit capability
   - Configure: Name, Slug, Flags (IsAdditive, RequiresBuildPlate, etc.)
   - **Routing Template Editor**:
     - Drag/drop available stages into template
     - Separate build-level and per-part sections
     - Mark which stage triggers plate release

2. **Enhanced `DefaultRoutingTemplate` format**:
   ```json
   {
     "buildLevel": [
       { "stageSlug": "sls-printing", "estimatedHours": 8.0 },
       { "stageSlug": "depowdering", "estimatedHours": 1.0 },
       { "stageSlug": "heat-treatment", "estimatedHours": 4.0 },
       { "stageSlug": "wire-edm", "estimatedHours": 2.0, "triggerPlateRelease": true }
     ],
     "perPart": [
       { "stageSlug": "cnc-machining", "estimatedHours": 2.0 },
       { "stageSlug": "qc", "estimatedHours": 0.5 }
     ]
   }
   ```

3. **Backward compatibility**: If `DefaultRoutingTemplate` is a simple JSON array,
   interpret it using `ProductionStage.IsBuildLevelStage` to categorize.

---

## Implementation Order

```
Phase 1: Apply DefaultRoutingTemplate ✅ COMPLETE
  ├── [x] Edit OnApproachChanged to offer template application
  ├── [x] Add confirmation modal (Option C: auto-apply if empty, confirm if routing has stages)
  ├── [x] Generate PartStageRequirements from template
  ├── [x] Add manual "Apply Template" button on routing tab
  ├── [x] Fix SLS-Based seed template: added wire-edm
  └── [x] Fix Additive+Subtractive seed template: added heat-treatment + wire-edm

Phase 2: Build-Level vs Per-Part UI ✅ COMPLETE (pre-existing)
  ├── [x] Split routing tab into two sections
  ├── [x] Filter stages by IsBuildLevelStage
  ├── [x] Add explanatory callouts
  └── [x] Editable with "From build" hint (Option C: flexibility for costing/quoting)

Phase 3: Per-Part Plate Release Override — DEFERRED
  ├── [ ] Add TriggerPlateReleaseOverride to PartStageRequirement
  ├── [ ] Migration
  ├── [ ] Update StageService.CompleteStageExecutionAsync
  └── [ ] Add toggle in routing UI

Phase 4: Manufacturing Approach Admin ✅ COMPLETE (pre-existing)
  ├── [x] ManufacturingApproaches.razor admin page at /admin/manufacturing-approaches
  ├── [x] Routing template editor with available/selected columns
  ├── [x] Move up/down/remove stage controls
  ├── [x] Build-level stage duration editing
  └── [x] Preview panel
```

**Total estimated: 11-14 hours**

---

## Decisions (Resolved)

### 1. Template Application Behavior → **Option C** ✅
Apply if routing is empty (auto), confirm if routing already has stages (modal dialog).
Implemented in `Edit.razor` — `ApplyRoutingTemplateAsync(forceReplace)` + `_showRoutingTemplateConfirm` modal.

### 2. Build-Level Stage Editing → **Option C** ✅
Editable with "From build" hint. Flexibility for costing/quoting.
Pre-existing implementation in routing tab — build-level stages show duration inputs when a build is selected.

### 3. Per-Part Plate Release Override → **Deferred** 📌
Deferred to Phase 3. Phase 1-2 working first. Current `ProductionStage.TriggerPlateRelease` flag is sufficient.

### 4. Existing Parts Migration → **Option A + B** ✅
Existing parts untouched. New "Apply Template" button on routing tab lets users opt-in to template routing.

---

## Related Files

| File | Purpose |
|------|---------|
| `Models/ManufacturingApproach.cs` | Manufacturing approach with DefaultRoutingTemplate |
| `Models/ProductionStage.cs` | Stage definitions with IsBuildLevelStage, TriggerPlateRelease |
| `Models/PartStageRequirement.cs` | Per-part routing configuration |
| `Components/Pages/Parts/Edit.razor` | Part editor with Routing tab |
| `Services/PartService.cs` | Part CRUD including stage requirements |
| `Services/BuildPlanningService.cs` | Creates build-level stage executions |
| `Services/BuildSchedulingService.cs` | ReleasePlateAsync creates per-part jobs |
| `Services/StageService.cs` | CompleteStageExecutionAsync checks TriggerPlateRelease |
| `Services/DataSeedingService.cs` | Seeds stages and approaches with templates |

---

## Notes

- This plan builds on the completed `SCHEDULER_WORKFLOW_PLAN.md` — Section 5 (TriggerPlateRelease)
  is already implemented.
- The two-track production model (SLS vs CNC) is already working; this plan adds the
  configuration layer that lets users define the tracks.
- `DefaultRoutingTemplate` exists but is unused — Phase 1 activates it.
- Manufacturing Approach admin page doesn't exist yet — Phase 4 creates it.
