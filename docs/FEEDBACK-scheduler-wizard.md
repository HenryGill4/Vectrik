# Scheduler Wizard — Bug Reports & UX Recommendations

> Tested: 2026-03-25 by Claude, stepping through the Next Build Advisor wizard on the Production Scheduler page.
> Login: admin / admin123. Two EOS M4 Onyx DMLS printers with auto-changeover.

---

## Critical Bug: Wizard Resets to Step 1 on Parent Re-render

### Symptom
When the user clicks "Next" on Step 1 (Schedule Options), the wizard briefly advances to Step 2 (Plate Composition) but then snaps back to Step 1. This makes it impossible to complete the wizard without very fast clicking or lucky timing.

### Root Cause
**File**: `Components/Pages/Scheduler/Modals/NextBuildAdvisor.razor`, lines 599-618

```csharp
protected override async Task OnParametersSetAsync()
{
    if (!IsVisible) return;

    _step = 0;                          // ← RESETS STEP TO 0 EVERY TIME
    _scheduling = false;
    _startAfterOverride = null;
    // ... clears all state ...

    if (_selectedMachineId > 0)
        await LoadRecommendationAsync();  // ← RELOADS FROM SCRATCH
}
```

The parent component (`Components/Pages/Scheduler/Index.razor`) has an **auto-refresh timer** (line 311-321) that calls `StateHasChanged()` periodically. Every parent re-render causes Blazor to call `OnParametersSetAsync` on all child components, including the wizard. Since `IsVisible` is still `true`, the wizard resets `_step = 0` and reloads everything, destroying the user's progress.

### Fix
Add a guard to only initialize on first open, not on every parameter update:

```csharp
private bool _initialized;

protected override async Task OnParametersSetAsync()
{
    if (!IsVisible)
    {
        _initialized = false;  // Reset so next open re-initializes
        return;
    }

    if (_initialized) return;  // Don't reset if already open
    _initialized = true;

    _step = 0;
    // ... rest of initialization ...
}
```

**Also**: The parent's auto-refresh timer should skip refreshing while any modal is open:
```csharp
// In Index.razor timer callback:
if (_disposed || !_autoRefreshEnabled || _loading || _refreshing || _scheduling
    || _showBuildAdvisor || _showScheduleWizard) return;
```

### Severity: **P0 — Blocks all SLS scheduling**

---

## Bug: "+ New" Dropdown Closes Immediately on Some Clicks

### Symptom
The `+ New` dropdown button in the toolbar sometimes navigates to the dashboard or closes immediately when clicked with browser automation. The `@onfocusout` handler on the parent div (line 21 of `SchedulerToolbar.razor`) causes the dropdown to close when focus leaves the container.

### Root Cause
**File**: `Components/Pages/Scheduler/Components/SchedulerToolbar.razor`, line 21:
```razor
<div class="sched-new-dropdown" @onfocusout="() => _newDropdownOpen = false">
```

The `focusout` event bubbles, so clicking a dropdown item briefly removes focus from the container, closing the menu before the click registers. This is a race condition.

### Fix
Use a small delay or `mousedown` instead:
```razor
@onfocusout="HandleFocusOut"
```
```csharp
private async Task HandleFocusOut()
{
    await Task.Delay(150);  // Allow click to register
    _newDropdownOpen = false;
}
```

### Severity: **P1 — Intermittent, frustrating**

---

## UX Issue: Step 1 (Schedule Options) Is Confusing

### Problem
Step 1 shows "Schedule Options" with changeover cards like:
- "Changeover: Thu Mar 26, 05:30 — Operator available — Single, 24.0h, 1 parts"
- "Changeover: Thu Mar 26, 04:24 — NO operator — Single, 24.0h, 1 parts"

A user managing two DMLS printers would not understand:
1. **What "Changeover" means in this context** — It's the time the *previous* build needs to be removed, not when this build starts or ends
2. **Why there are only two options** — Both show "Single, 24.0h" with nearly identical times (05:30 vs 04:24). There's no double/triple stack option shown
3. **What "1 parts" means** — A user expects to see which parts will be printed, not a count of "1 parts"
4. **The times are in UTC not local** — "05:30" for a US shop is likely 12:30 AM or 1:30 AM depending on timezone. The user sees "Operator available" but the time shown looks like middle-of-night
5. **No machine context** — The card doesn't show which machine is currently printing what, or when the current build ends

### Recommended Redesign for Step 1

Step 1 should answer: **"What's happening on this machine right now, and when can I start the next build?"**

```
┌─────────────────────────────────────────────────────────┐
│ Machine: EOS M4 Onyx #1                           [▼]  │
│                                                         │
│ Current Status:                                         │
│   Build #47 (DM-SUP-FULL-0001) — 18.5h remaining       │
│   Est. completion: Thu Mar 26, 11:30 AM                 │
│   Cooldown chamber: 1 of 2 slots occupied               │
│                                                         │
│ Next Build Slot:                                        │
│   Earliest start: Thu Mar 26, 11:30 AM (after current)  │
│   Changeover needed by: Fri Mar 27, ~11:30 AM           │
│   Operator on shift: ✓ Yes (Day Shift, 6 AM - 6 PM)    │
│                                                         │
│ ─── Stack Options ───────────────────────────────────── │
│                                                         │
│ ✓ [RECOMMENDED] Single Stack                            │
│   Duration: 24.0h │ End: Fri Mar 27, 11:30 AM          │
│   Changeover: Fri Mar 27 11:30 AM ✓ During day shift   │
│                                                         │
│   Double Stack                                          │
│   Duration: 36.0h │ End: Sat Mar 28, 11:30 PM          │
│   Changeover: Sat 11:30 PM ✗ No operator (weekend)     │
│                                                         │
│   Triple Stack                                          │
│   Duration: 48.0h │ End: Sun Mar 29, 11:30 AM          │
│   Changeover: Sun 11:30 AM ✗ No operator (weekend)     │
│                                                         │
│ Override start time: [________________] (optional)       │
└─────────────────────────────────────────────────────────┘
```

### Key Changes Needed:
1. **Show machine status at the top** — What's currently printing, when does it end, cooldown chamber status
2. **Show all stack level options** — The user needs to compare single/double/triple to see which lands changeover during shifts
3. **Use local times, 12-hour format** — "11:30 AM" not "05:30"
4. **Explain the changeover context** — "During day shift" is clearer than "Operator available"
5. **Move "Override Start After" below the options** — It's an override, not the primary input
6. **Show the part(s) that will be printed** — Even briefly: "Top demand: DM-SUP-FULL-0001 (34 remaining)"

---

## UX Issue: Step 2 (Plate Composition) Needs More Context

### Current State
Step 2 shows:
- Recommendation text: "Print 1x DM-SUP-FULL-0001 — 24.0h, single stack"
- An editable plate with part, stack dropdown, positions input, total
- Demand coverage bar (1/34 = 3%)
- Expandable "All Outstanding Demand"

### Problems
1. **No connection to the schedule option chosen in Step 1** — The plate always shows single stack with 1 position. Changing stack level in Step 1 should update the plate composition recommendation
2. **"1 x Single" dropdown is confusing** — The label says "Stack" but the dropdown shows "1 x Single". Users managing DMLS printers think of stacking as "how many parts tall am I going vertically". The label should say "Stack Height" and options should be "Single (1 high)", "Double (2 high)", "Triple (3 high)"
3. **"Positions" is ambiguous** — A DMLS operator thinks of "positions" as "how many spots on the plate". But the input just shows "1" with no context of what the max is. Should show "1 of 4 positions" or similar
4. **Demand coverage at 3% feels demoralizing** — With 34 parts needed and only 1 per build, the user sees they'd need 34 builds. The wizard should suggest: "At 1 part per build, this will take ~34 builds (34 days). Consider double stacking: 17 builds (~17 days)."
5. **"+ Add Part" has no guidance** — When should a user add a fill part? The wizard should suggest fills when there's plate capacity remaining

### Recommended Changes:
1. When user selects a stack level in Step 1, carry it to Step 2 and auto-calculate positions
2. Show build plate visual (even a simple grid) showing occupied positions vs available capacity
3. Show estimated total builds to fulfill all demand for this part
4. Suggest fill parts automatically when plate has unused capacity
5. Show the print duration change when user modifies the plate composition

---

## UX Issue: Step 3 (Program Details) — Slicer Data Is Premature

### Current State
Step 3 asks for slicer data: print duration, layer count, build height, powder estimate, slicer software, slicer version, slicer file name, and file upload.

### Problem
In a real DMLS workflow, the operator typically:
1. Decides what to print next (Steps 1-2 of the wizard)
2. **Schedules the build** to reserve the machine time slot
3. **Then** prepares the build file in slicer software (Magics, Materialise, etc.)
4. **Then** uploads the slicer data before the build starts

Asking for slicer data *before scheduling* is backwards. The user hasn't prepared the build file yet — they're still deciding what goes on the plate.

### Recommendation
1. **Move slicer data to a separate workflow** — After scheduling, the build appears in the "Programs" view. The user can add slicer data there before the build starts.
2. **Make Step 3 the confirmation step** (current Step 4) — Go from Plate Composition directly to Confirm
3. **Keep the "Skip" option prominent** — Most users will skip slicer data during scheduling and add it later. Make this the default behavior.
4. **Alternatively**: Collapse Steps 3 and 4 into one step with slicer data as an optional expandable section at the bottom of the confirmation page

---

## UX Issue: Step 4 (Confirm) — Good but Needs Polish

### Current State
Shows a summary grid with Machine, Print Time, Start, End, Total Parts, Part Types, Changeover status, and Files. Also shows "After this build: Depowder + Wire EDM stages will be auto-scheduled."

### What's Good
- Summary is comprehensive
- Shows changeover safety status
- Mentions downstream auto-scheduling
- "Schedule & Queue Next" button is excellent for batch scheduling

### What Needs Improvement
1. **No cost estimate** — With machines at $200/hr, a 24h build is $4,800. Show the estimated build cost
2. **No visual timeline** — Show a mini Gantt bar showing where this build fits relative to what's already scheduled
3. **The downstream note is too vague** — "Depowder + Wire EDM stages will be auto-scheduled" — when? Which machines? How long?
4. **No warning about schedule conflicts** — If this build overlaps with another scheduled build, there should be a clear warning

---

## Overall Flow Recommendation: Simplify to 3 Steps

The current 4-step flow is:
1. Schedule Options (machine, time slot, stack level)
2. Plate Composition (which parts, how many, stacking)
3. Program Details (slicer data — premature)
4. Confirm & Schedule

### Recommended 3-Step Flow:
1. **Machine & Timing** — Show machine status, pick time slot, compare stack options
2. **Build Plate** — Configure parts on plate, see demand coverage, see print duration
3. **Review & Schedule** — Full summary with cost, timeline preview, schedule button

Slicer data entry should be a separate workflow accessible from the Programs page or a "Prepare Build" action on scheduled builds.

---

## Additional Issues Found

### 1. Both machines show identical schedule options
When switching between EOS M4 Onyx #1 and #2, the schedule options are identical (same times, same changeover windows). This is correct if both machines are idle, but the UI doesn't show whether each machine is currently running a build. The user can't tell which machine is actually free vs busy.

**Fix**: Show current machine status (Idle/Printing/Down) next to the machine name in the dropdown.

### 2. No "Schedule Both Machines" workflow
A user with 2 printers wants to schedule both machines at once. The current wizard handles one machine at a time. The "Schedule & Queue Next" button is good for sequential builds on the *same* machine, but there's no equivalent for "now schedule the other machine."

**Fix**: After scheduling, offer "Schedule other machine (EOS M4 Onyx #2)" as a quick action.

### 3. Build Plate Capacity = 2 is not surfaced
The machines have `BuildPlateCapacity = 2` (cooldown chamber holds 2 builds). This is critical operational context — if both cooldown slots are full, the machine stops. The wizard doesn't show cooldown chamber status.

**Fix**: Show "Cooldown Chamber: 1/2 occupied" in the machine status area of Step 1.

### 4. Changeover time calculation doesn't account for cooldown
The changeover time shown is when the build finishes printing. But in reality, the operator doesn't need to be present when the build *finishes printing* — they need to be present when the build *needs to be removed from cooldown*, which is when the *next* build finishes and needs to enter cooldown.

**Fix**: Clarify the terminology. "Changeover" = "the time you need an operator to remove a plate from cooldown." The current build auto-starts the next one; the operator only needs to act when the cooldown chamber would overflow.

### 5. No shift visibility
The wizard mentions "Operator available" / "NO operator" but doesn't show the actual shift schedule. The user should see:
- What shift pattern is assigned to each machine
- When operators are on shift this week
- Visual overlay showing shift windows on the timeline

### 6. "1 parts" grammar
Throughout the wizard, "1 parts" should be "1 part" (singular). Minor but sloppy.

### 7. UTC vs Local Time
All times appear to be in UTC. For a US-based shop, this is confusing. Times should display in the user's local timezone.

### 8. Setup Affinity Query Bug (SchedulingService.cs ~line 205)
The CNC setup affinity logic queries the last part processed on a machine to determine if a setup changeover is needed. However, it uses `FirstOrDefaultAsync()` **without explicit ordering**, so EF Core may return any matching execution rather than the most recent one. This could cause incorrect setup time calculations.

**Fix**: Add `.OrderByDescending(e => e.ScheduledEndAt)` before `.FirstOrDefaultAsync()` to ensure the most recent execution is used.

---

## Priority Summary

| Priority | Issue | Impact |
|----------|-------|--------|
| P0 | Wizard resets on parent re-render | Blocks all SLS scheduling |
| P1 | Dropdown closes on focusout race | Intermittent frustration |
| P1 | Step 1 confusing — no machine status, unclear options | Users don't understand what they're choosing |
| P2 | Slicer data step is premature | Slows down workflow |
| P2 | No cost estimate on confirm | Missing decision-critical info |
| P2 | UTC times instead of local | Confusing for all users |
| P3 | "1 parts" grammar | Polish |
| P3 | No "schedule other machine" flow | Nice-to-have |
| P3 | Build plate capacity not surfaced | Operational context missing |
