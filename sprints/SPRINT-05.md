# Sprint 5: Scheduler (Gantt + Job Management)

> **Status**: NOT STARTED
> **Goal**: Visual Gantt scheduling with job creation, drag-drop, and conflict detection.
> **Depends on**: Sprint 2 (parts/machines configured), Sprint 3 (WOs exist)

---

## Tasks

```
[ ] 5.1  Gantt chart rendering — machines as rows, time as X axis (canvas or HTML/CSS)
[ ] 5.2  Job bars — colored by status, show part number + quantity
[ ] 5.3  Job creation — Part picker auto-fills duration from stacking config
[ ] 5.4  Job creation — link to WorkOrderLine (optional)
[ ] 5.5  Job editing — click job bar → edit modal (reschedule, change machine, update qty)
[ ] 5.6  Time navigation — scroll/zoom, day/week/month views
[ ] 5.7  Shift overlay — gray out non-working hours from OperatingShift table
[ ] 5.8  Conflict detection — highlight overlapping jobs on same machine in red
[ ] 5.9  Predecessor chain — link jobs, show dependency arrows
[ ] 5.10 Drag-drop — move job to different time slot, update ScheduledStart/End
[ ] 5.11 Drag-drop — move job to different machine row
[ ] 5.12 Filters — by machine, status, priority, material
[ ] 5.13 Job status actions — Start (InProgress), Pause, Complete, Cancel
[ ] 5.14 Maintenance blocking — show maintenance windows as blocked zones
[ ] 5.15 Verify: create 5 jobs across 3 machines → Gantt renders → drag to reschedule
```

---

## Acceptance Criteria

- Gantt shows all machines as rows with job bars positioned by time
- Job creation auto-calculates end time from Part stacking duration
- Drag-drop updates job schedule in DB
- Overlapping jobs on same machine are highlighted
- Shift non-working hours are grayed out
- Maintenance RequiresShutdown blocks are visible
- Predecessor arrows are drawn between linked jobs
- Filters narrow the view
- Touch-friendly: 44px tap targets, works on iPad

## Technical Approach

- Use `scheduler.js` with HTML/CSS grid or canvas rendering
- Blazor component passes job data as JSON to JS interop
- JS handles drag-drop and calls Blazor methods to persist changes
- Time scale: 1 hour = fixed pixel width, scrollable

## Files to Touch

- `Components/Pages/Scheduler/Index.razor` — major rewrite
- `wwwroot/js/scheduler.js` — Gantt rendering engine
- `Services/JobService.cs` — overlap detection, schedule validation
- `Services/MaintenanceService.cs` — blocked machine windows
