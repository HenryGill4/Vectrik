# NextBuildAdvisor Wizard — Code Review (2026-03-31)

## Overview

Full code-level review of the refactored NextBuildAdvisor wizard after decomposition from 1,700 lines into sub-components. All files reviewed, every user path traced, state transitions verified.

**Build**: 0 errors, 479/479 tests pass

## Files Reviewed

| File | Lines | Role |
|------|-------|------|
| `NextBuildAdvisor.razor` | 1,183 | Main wizard modal, state management, scheduling execution |
| `AdvisorStepMachine.razor` | 167 | Step 0: Machine selection, demand overview, recommendation |
| `AdvisorStepProgram.razor` | 229 | Step 1: Program selection with search/filter |
| `AdvisorStepSchedule.razor` | 457 | Steps 2-3: Slicer entry, plate composition, review |
| `AdvisorWizardState.cs` | 72 | Shared state object for sub-components |
| `PlateComposer.razor` | 165 | Plate composition UI (certified + free-form) |
| `PlatePartEntry.razor` | 97 | Per-part row in plate editor |
| `PlateQuadrantGrid.razor` | 46 | Visual plate grid |
| `PlateDemandSidebar.razor` | 43 | Part picker sidebar |
| `PlateLayoutEditor.razor` | 262 | Certified layout WYSIWYG editor |
| `PlateComposerModels.cs` | 25 | EditablePlateAllocation model |

---

## Bugs Found & Fixed

### BUG-W1 [MEDIUM]: `_existingNeedsSlicerData` not reset in queue/switch flows

**File**: `NextBuildAdvisor.razor:1033, 1052`
**Impact**: If user schedules a program needing slicer data, then clicks "Schedule & Queue Next" or "Schedule & Switch Machine", the `_existingNeedsSlicerData` flag stays `true`. This causes the step indicator to show "Slicer" instead of "Schedule" for the next wizard run, and the AdvanceStep logic could route to the wrong step.
**Fix**: Added `_existingNeedsSlicerData = false;` to both `ScheduleAndQueueNextAsync()` and `ScheduleAndSwitchMachineAsync()`.

### BUG-W2 [MEDIUM]: Circuit reconnection crash in `LoadRecommendationAsync`

**File**: `NextBuildAdvisor.razor:527-531`
**Impact**: If the Blazor circuit disconnects during `LoadRecommendationAsync` (which makes multiple async DB calls), a `TaskCanceledException` or `ObjectDisposedException` would be caught by the generic `catch (Exception ex)` and try to call `Toast.ShowError()` — which itself could throw since the component is disposed. This matches the reported "crash when Blazor circuit reconnected".
**Fix**: Added explicit `catch (TaskCanceledException)` and `catch (ObjectDisposedException)` handlers before the generic catch, matching the pattern already used in `ExecuteScheduleAsync`.

### BUG-W3 [LOW]: `<details open>` attribute always renders as open

**File**: `AdvisorStepSchedule.razor:344`
**Impact**: `@(State.ActiveChangeoverAligned ? "" : "open")` — in HTML, `<details open="">` is equivalent to `<details open>`, meaning the timing options section is always open regardless of changeover alignment. This makes the UI always show the timing options expanded, even when changeover is safe.
**Fix**: Changed to `open="@(!State.ActiveChangeoverAligned)"` which lets Blazor handle the boolean attribute correctly.

---

## Wizard Path Analysis

### Path 1: Existing program with slicer data
- **Step 0** (Machine): `_step = 0` → machine dropdown works, demand renders, recommendation card shows
- **Step 1** (Program): `_step = 1` → program list renders with search/filter, click selects and highlights
- **AdvanceStep**: `_selectedExistingProgram != null && !_existingNeedsSlicerData` → skips to `_step = 3`
- **Step 3** (Review): Review card renders with all summary data, "Schedule This Build" button enabled
- **Schedule**: `ExecuteScheduleAsync` → `ScheduleBuildPlateRunAsync` (creates run copy) → toast + close
- **VERDICT**: Works correctly

### Path 2: Existing program WITHOUT slicer data
- **Step 0** → **Step 1**: Select program with `HasSlicerData == false` → `_existingNeedsSlicerData = true`
- **AdvanceStep**: Goes to `_step = 2` (slicer entry form)
- **Step 2**: Slicer form renders with required Print Duration field
- **AdvanceStep**: `_step++` → `_step = 3`, calculates build cost
- **Step 3** (Review): Shows review with slicer data included
- **Schedule**: Calls `UpdateSlicerDataAsync` before scheduling
- **VERDICT**: Works correctly

### Path 3: Create New Program
- **Step 0** → **Step 1**: Click "+ Create New Program" → `_creatingNewProgram = true`
- **AdvanceStep**: `_selectedExistingProgram == null` → `ApplySelectedOptionToPlate()` → `CreateDraftProgramAsync()` → `_step = 2`
- **Step 2**: PlateComposer renders (certified or free-form), slicer fields, file upload
- **AdvanceStep**: `_step++` → `_step = 3`, calculates build cost
- **Step 3** (Review): Shows new program name, plate composition, timing
- **Schedule**: Creates program if not already created, schedules directly
- **VERDICT**: Works correctly

### Path 4: Schedule & Queue Next
- After scheduling: `_step = 0`, `_startAfterOverride = ActiveSlot?.PrintEnd` (smart: starts after current build ends)
- Resets all program state, clears schedule options, calls `LoadRecommendationAsync()`
- **VERDICT**: Works correctly (after BUG-W1 fix)

### Path 5: Schedule & Switch Machine
- After scheduling: `_step = 0`, switches `_selectedMachineId` to other machine, clears override
- Loads fresh recommendation for the other machine
- **VERDICT**: Works correctly (after BUG-W1 fix)

### Path 6: Back Navigation
- **Step 3 → Step 1** (existing program, no slicer): Direct jump, scrolls to selected program
- **Step 3 → Step 2** (existing needing slicer or new): `_step--`
- **Step 2 → Step 1**: `_step--`, scrolls to selected program
- **Step 1 → Step 0**: `_step--`
- Selected program preserved on back (not cleared until new selection)
- **VERDICT**: Works correctly

---

## Edge Cases Verified

| Edge Case | Behavior | Status |
|-----------|----------|--------|
| Empty demand (`_demand = []`) | Recommendation card hidden, no programs auto-selected, plate empty | OK |
| No available programs | "No existing programs available" message shown, "+ Create New" still works | OK |
| Empty plate allocations | "Schedule This Build" button disabled (`!_plateAllocations.Any()`) | OK |
| Cancel mid-flow | Close button calls `Close()` which cleans up draft programs | OK |
| Circuit disconnect during scheduling | `TaskCanceledException` / `ObjectDisposedException` caught in `ExecuteScheduleAsync` | OK |
| Circuit disconnect during loading | Now caught explicitly (after BUG-W2 fix) | OK (fixed) |
| No SLS machines | `SlsMachines` empty → `_selectedMachineId = 0` → `LoadRecommendationAsync` skipped | OK |
| Single SLS machine | `OtherMachine` is null → "Schedule & Switch" button hidden | OK |

---

## State Management Quality

### What's Good
- `AdvisorWizardState` is a clean read-only snapshot rebuilt on each render via `BuildWizardState()`
- Sub-components use `EventCallback` for all mutations — no direct state writes
- `_initialized` flag prevents re-initialization on parameter changes
- Draft program cleanup on close prevents orphan records
- `TaskCanceledException` / `ObjectDisposedException` handling in scheduling path

### Minor Concerns (Not Bugs)
- `_showPartPicker` field (line 190) is assigned but never used in rendering (CS0414 warning) — leftover from refactor
- `FormatFileSize` duplicated in both `NextBuildAdvisor.razor` and `AdvisorStepSchedule.razor`
- `BuildWizardState()` creates a new object on every render call — acceptable for this scale but could be memoized

---

## Conclusion

The wizard refactor is clean and well-structured. The sub-component decomposition follows good Blazor patterns with cascaded state and event callbacks. **3 bugs fixed** (2 medium, 1 low). Build succeeds, all 479 tests pass.

The circuit reconnection crash was likely caused by BUG-W2 — unhandled `TaskCanceledException` in `LoadRecommendationAsync` during circuit disconnect/reconnect, which would cascade into a `Toast.ShowError` call on a disposed circuit.
