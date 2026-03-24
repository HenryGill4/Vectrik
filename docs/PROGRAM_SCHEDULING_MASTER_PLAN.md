# 🎯 Program-Based Scheduling System — Master Plan

## Executive Summary

This plan transitions the scheduling system from legacy BuildPackage-based scheduling to a fully program-centric approach. MachineProgram becomes the single source of truth for all scheduling, including downstream operations (depowder, EDM, post-processing).

---

## Current State Analysis

### ❌ Issues Identified
1. **ProgramScheduleWizard Black Screen** — Modal appears but content may not render due to:
   - Missing CSS for `.wizard-steps`, `.wizard-content`, etc.
   - Lines not having Part navigation loaded
   - Async initialization not completing

2. **Legacy Build References Still Present** — BuildPackage, BuildTemplate, and build-based scheduling still active

3. **Downstream Operations Not Linked** — When scheduling SLS print, depowder/EDM/post-processing stages not automatically handled

4. **No Program Creation Prompts** — System doesn't prompt to create programs when they don't exist

---

## Phase 1: Fix ProgramScheduleWizard (Critical)

### Step 1.1: Add Wizard CSS
Create/update `wwwroot/css/scheduler-wizard.css`:
- `.wizard-steps` — horizontal step indicator
- `.wizard-step`, `.wizard-step--active`, `.wizard-step--done`
- `.wizard-step-connector`
- `.wizard-content`
- `.program-wizard-parts`, `.program-wizard-part`
- `.program-wizard-match`, `.program-wizard-match-option`

### Step 1.2: Fix Initialization
- Add loading state to wizard
- Ensure `Part`, `Part.ManufacturingApproach`, `Part.AdditiveBuildConfig` are loaded
- Add null checks and fallback UI

### Step 1.3: Debug/Error Handling
- Add try-catch around `InitializeSelectionsAsync`
- Show error message in wizard if initialization fails
- Log issues to console for debugging

---

## Phase 2: Remove Legacy Build System

### Step 2.1: Identify Legacy References
Files to update/remove:
- `Components/Pages/Scheduler/Views/WorkOrdersView.razor` — Remove build references
- `Components/Pages/Scheduler/Views/BuildsView.razor` — Transition to ProgramsView
- `Components/Pages/Builds/*` — Mark deprecated or redirect
- `Services/IBuildPlanningService.cs` — Already marked obsolete
- `Services/IBuildSchedulingService.cs` — Already marked obsolete
- `Services/IBuildTemplateService.cs` — Already marked obsolete

### Step 2.2: Update WorkOrdersView
- Remove "Legacy Build" button
- Remove BuildPackage stat from demand dashboard
- Remove GetInBuildQty and GetUnlinkedBuildQty helpers (or mark deprecated)
- Remove template-based scheduling cards
- Keep only "Schedule via Programs" flow

### Step 2.3: Create ProgramsView for Scheduler
Replace BuildsView with ProgramsView showing:
- All BuildPlate programs by status
- Program timeline (similar to build timeline)
- Quick actions: Edit, Schedule, Create Run
- Filter by status: Draft, Ready, Scheduled, Printing, PostPrint, Completed

---

## Phase 3: Downstream Program Integration

### Step 3.1: Manufacturing Process Integration
When scheduling a BuildPlate program, the system must:
1. Check if part has ManufacturingProcess defined
2. For each stage after SLS print, check for linked MachineProgram
3. If no program exists, either:
   - Use default parameters (auto-create placeholder execution)
   - Prompt user to create/assign a program

### Step 3.2: Create DownstreamProgramService
```csharp
public interface IDownstreamProgramService
{
    /// <summary>
    /// Get required downstream programs for a BuildPlate after SLS print.
    /// Returns stages that need programs with their assignment status.
    /// </summary>
    Task<List<DownstreamProgramRequirement>> GetRequiredProgramsAsync(int buildPlateProgramId);
    
    /// <summary>
    /// Validate all downstream programs are ready before scheduling.
    /// Returns validation result with missing programs.
    /// </summary>
    Task<DownstreamValidationResult> ValidateDownstreamReadinessAsync(int buildPlateProgramId);
    
    /// <summary>
    /// Auto-create placeholder programs for stages that don't have one.
    /// Uses default parameters from ProductionStage configuration.
    /// </summary>
    Task<List<MachineProgram>> CreatePlaceholderProgramsAsync(
        int buildPlateProgramId, 
        List<int> stageIdsNeedingPrograms,
        string createdBy);
}

public record DownstreamProgramRequirement(
    int ProcessStageId,
    string StageName,
    string MachineType,
    int? AssignedProgramId,
    string? AssignedProgramName,
    bool IsRequired,
    bool HasDefaultParameters);

public record DownstreamValidationResult(
    bool IsValid,
    List<DownstreamProgramRequirement> MissingPrograms,
    List<string> Warnings);
```

### Step 3.3: Update ScheduleBuildPlateAsync
Modify `ProgramSchedulingService.ScheduleBuildPlateAsync`:
1. Before scheduling, call `ValidateDownstreamReadinessAsync`
2. If missing programs and `autoCreatePlaceholders = true`, create them
3. If missing programs and `autoCreatePlaceholders = false`, throw with details
4. Create StageExecutions for all downstream stages with linked programs

### Step 3.4: Downstream Program Prompt Modal
Create `DownstreamProgramSetupModal.razor`:
- Shows after user initiates schedule
- Lists all downstream stages and their program status
- For each missing program:
  - Option A: Create with defaults (quick)
  - Option B: Navigate to program editor (detailed)
  - Option C: Skip (if stage is optional)
- "Continue to Schedule" button when all required stages have programs

---

## Phase 4: Enhanced Program Schedule Wizard

### Step 4.1: Wizard Flow Redesign

**New Steps:**
1. **Select Parts** — Choose which parts to schedule
2. **Match SLS Programs** — Select/create BuildPlate programs
3. **Configure Runs** — Set number of print runs per program
4. **Downstream Setup** — Assign/create downstream programs
5. **Review & Schedule** — Final review with timeline preview

### Step 4.2: Add Downstream Setup Step
In wizard step 4:
- For each selected program, show downstream requirements
- Color coding: 🟢 Program assigned, 🟡 Default available, 🔴 Required but missing
- Inline actions:
  - Quick-assign existing program
  - Create program with defaults
  - Mark as "schedule later"

### Step 4.3: Timeline Preview
In final step:
- Show Gantt-style preview of what will be scheduled
- Machine assignments
- Estimated completion date
- Changeover windows
- Conflicts/warnings

### Step 4.4: Add Wizard Styles
Add comprehensive CSS for wizard:
```css
/* Wizard Step Indicator */
.wizard-steps { display: flex; align-items: center; justify-content: center; gap: 8px; margin-bottom: 24px; }
.wizard-step { display: flex; flex-direction: column; align-items: center; gap: 4px; }
.wizard-step-num { width: 32px; height: 32px; border-radius: 50%; background: var(--bg-secondary); display: flex; align-items: center; justify-content: center; font-weight: 600; }
.wizard-step--active .wizard-step-num { background: var(--accent); color: white; }
.wizard-step--done .wizard-step-num { background: var(--success); color: white; }
.wizard-step-connector { flex: 1; height: 2px; background: var(--border); max-width: 60px; }
.wizard-step-connector--done { background: var(--success); }

/* Wizard Content */
.wizard-content { min-height: 300px; }
.wizard-hint { color: var(--text-muted); margin-bottom: 16px; }

/* Part Selection */
.program-wizard-parts { display: flex; flex-direction: column; gap: 8px; }
.program-wizard-part { display: flex; align-items: center; gap: 12px; padding: 12px; background: var(--bg-secondary); border-radius: 8px; cursor: pointer; transition: all 0.15s; }
.program-wizard-part:hover { background: var(--bg-tertiary); }
.program-wizard-part--selected { border: 2px solid var(--accent); }

/* Program Matching */
.program-wizard-match { margin-bottom: 16px; padding: 16px; background: var(--bg-secondary); border-radius: 8px; }
.program-wizard-match-options { display: flex; flex-direction: column; gap: 8px; margin-top: 12px; }
.program-wizard-match-option { display: flex; align-items: center; gap: 12px; padding: 10px; background: var(--bg-primary); border: 1px solid var(--border); border-radius: 6px; cursor: pointer; }
.program-wizard-match-option:hover { border-color: var(--accent); }
.program-wizard-match-option--selected { border-color: var(--accent); background: var(--accent-light); }

/* Downstream Programs */
.downstream-stage { padding: 12px; background: var(--bg-secondary); border-radius: 6px; margin-bottom: 8px; }
.downstream-stage--ready { border-left: 3px solid var(--success); }
.downstream-stage--default { border-left: 3px solid var(--warning); }
.downstream-stage--missing { border-left: 3px solid var(--error); }
```

---

## Phase 5: Program Lifecycle Management

### Step 5.1: Program Status Transitions
Define clear lifecycle:
```
None → Ready → Scheduled → Printing → PostPrint → Completed
         ↓
      Cancelled
```

### Step 5.2: Auto-Status Updates
- When slicer data entered: `None → Ready`
- When scheduled: `Ready → Scheduled`
- When machine starts: `Scheduled → Printing`
- When machine completes: `Printing → PostPrint`
- When all downstream complete: `PostPrint → Completed`

### Step 5.3: Status Change Hooks
Create events/notifications for status changes:
- Notify operators when programs become ready
- Alert scheduling when programs need downstream setup
- Track time in each status for analytics

---

## Phase 6: Testing & Validation

### Step 6.1: Unit Tests
Add tests for:
- `DownstreamProgramService.GetRequiredProgramsAsync`
- `DownstreamProgramService.ValidateDownstreamReadinessAsync`
- `ProgramSchedulingService` with downstream integration

### Step 6.2: Integration Tests
- Full WO → Program → Schedule flow
- Downstream program creation
- Timeline generation with multiple machines

### Step 6.3: UI Tests
- Wizard navigation
- Downstream setup modal
- Error handling and validation messages

---

## Implementation Order

### Sprint 1: Critical Fixes
1. ✅ Fix ProgramScheduleWizard CSS (add missing styles)
2. ✅ Fix wizard initialization (loading states, error handling)
3. ✅ Ensure wizard works end-to-end

### Sprint 2: Legacy Removal
4. ✅ Remove legacy build buttons from WorkOrdersView
5. ✅ Update demand stats to program-only
6. ✅ Create redirect from /builds to /programs

### Sprint 3: Downstream Integration
7. ✅ Create IDownstreamProgramService
8. ✅ Update ScheduleBuildPlateAsync to validate downstream
9. ✅ Create DownstreamProgramSetupModal

### Sprint 4: Enhanced Wizard
10. ✅ Add downstream setup step to wizard
11. ✅ Add timeline preview
12. ✅ Polish wizard UI

### Sprint 5: Testing & Polish
13. ✅ Add comprehensive tests
14. ✅ Performance optimization
15. ✅ Documentation

---

## Files to Create/Modify

### New Files
- `wwwroot/css/scheduler-wizard.css`
- `Services/IDownstreamProgramService.cs`
- `Services/DownstreamProgramService.cs`
- `Components/Pages/Scheduler/Modals/DownstreamProgramSetupModal.razor`
- `Components/Pages/Scheduler/Views/ProgramsView.razor`

### Modified Files
- `Components/Pages/Scheduler/Modals/ProgramScheduleWizard.razor` — Enhanced wizard
- `Components/Pages/Scheduler/Views/WorkOrdersView.razor` — Remove legacy
- `Components/Pages/Scheduler/Index.razor` — Add ProgramsView tab
- `Services/ProgramSchedulingService.cs` — Downstream integration
- `Program.cs` — Register new services

### Deprecated/Removed
- `Components/Pages/Scheduler/Views/BuildsView.razor` → Redirect to ProgramsView
- `Services/BuildSchedulingService.cs` → Already obsolete
- `Services/BuildPlanningService.cs` → Already obsolete

---

## Success Criteria

1. ✅ ProgramScheduleWizard renders correctly and schedules programs
2. ✅ No legacy build buttons in demand view
3. ✅ Downstream programs automatically validated/created
4. ✅ Full WO → Program → Schedule → Production flow works
5. ✅ All 15+ scheduling tests pass
6. ✅ Timeline shows both programs and downstream stages

---

## Notes for Implementation

- Keep backward compatibility for existing scheduled builds (read-only)
- Use feature flag for gradual rollout if needed
- Monitor performance with large program counts
- Consider batch operations for multiple programs

---

*Last Updated: 2025-07-17*
*Author: Claude (Copilot)*
