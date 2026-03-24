> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# Sprint 6: Part Tracker (Visibility)

> **Status**: NOT STARTED
> **Goal**: "Where is my part?" answered instantly with pipeline visualization.
> **Depends on**: Sprint 4 (parts flowing through stages)

---

## Tasks

```
[ ] 6.1  Search bar — search by WO number, part number, or serial number
[ ] 6.2  Search results — list matching WO lines with part + qty + status
[ ] 6.3  Pipeline visualization — stages as columns, WO lines as progress bars
[ ] 6.4  Pipeline coloring — NotStarted (gray), InProgress (blue), Completed (green), Failed (red)
[ ] 6.5  Click WO line → expand to show serial numbers (post-engraving) with individual positions
[ ] 6.6  Click serial number → full stage history (every stage, operator, duration, timestamps, notes)
[ ] 6.7  Filter by: stage, status, overdue, customer
[ ] 6.8  Overdue highlighting — red text for WO lines past due date with incomplete stages
[ ] 6.9  Batch tracking (pre-engraving) — show "20 parts at Depowdering" without serial numbers
[ ] 6.10 Verify: track a WO from creation through all stages, see serial numbers after engraving
```

---

## Acceptance Criteria

- Search finds results by WO#, part#, or serial#
- Pipeline shows all stages as columns with parts positioned at current stage
- Pre-engraving: batch quantities shown per stage
- Post-engraving: individual serial numbers shown with their stage position
- Serial number detail shows full history timeline
- Overdue items are visually highlighted
- Touch-friendly: works on iPad

## Files to Touch

- `Components/Pages/Tracking/Index.razor` — major rewrite
- `Services/PartTrackerService.cs` — query logic
