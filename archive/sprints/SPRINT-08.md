> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# Sprint 8: Maintenance System

> **Status**: NOT STARTED
> **Goal**: Preventive maintenance rules, work orders, and scheduler integration.
> **Depends on**: Sprint 2 (machines + components configured), Sprint 5 (scheduler)

---

## Tasks

```
[ ] 8.1  Machine components — CRUD (add recoater arm, filter, sieve station, etc.)
[ ] 8.2  Maintenance rules — configure per component (trigger type, threshold, severity, early warning %)
[ ] 8.3  Rule evaluation — check hours/builds/dates against thresholds
[ ] 8.4  Maintenance dashboard — upcoming PM tasks sorted by urgency
[ ] 8.5  Maintenance dashboard — overdue items with red alerts
[ ] 8.6  Maintenance work order CRUD — type, priority, technician assignment
[ ] 8.7  Maintenance WO lifecycle — Open → Assigned → InProgress → Completed
[ ] 8.8  Maintenance WO — hours, cost, parts used, work performed fields
[ ] 8.9  Action logging — record maintenance actions with timestamps
[ ] 8.10 Scheduler integration — RequiresShutdown blocks machine on Gantt
[ ] 8.11 Dashboard integration — maintenance alerts on main dashboard
[ ] 8.12 Machine status page — show next PM due, hours until PM
[ ] 8.13 Verify: create rule → machine hits threshold → alert appears → create WO → complete → counter resets
```

---

## Acceptance Criteria

- Components can be added to machines
- Rules trigger alerts based on hours/builds/dates
- Early warning fires at configured percentage
- Maintenance WOs track assignment, hours, cost
- RequiresShutdown blocks machine on scheduler
- Action log records all maintenance activities
- Dashboard shows maintenance alerts

## Files to Touch

- `Components/Pages/Maintenance/Index.razor` — dashboard rewrite
- `Components/Pages/Maintenance/WorkOrders.razor` — full CRUD
- `Components/Pages/Maintenance/Rules.razor` — rule configuration
- `Components/Pages/Admin/Machines.razor` — components panel (from Sprint 2)
- `Services/MaintenanceService.cs` — rule evaluation + WO lifecycle
