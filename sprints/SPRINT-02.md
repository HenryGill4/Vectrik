# Sprint 2: Admin Pages (Full CRUD)

> **Status**: NOT STARTED
> **Goal**: Admins can fully configure parts, stages, machines, users, materials, and settings.
> **Depends on**: Sprint 1 (login + nav working)

---

## Tasks

```
[ ] 2.1  Admin/Parts — stacking config section (single/double/triple durations, parts per build, enable toggles)
[ ] 2.2  Admin/Parts — stage requirement assignment panel (pick stages, set order, estimated hours per stage)
[ ] 2.3  Admin/Parts — batch stage durations (SLS, depowdering, heat treatment, wire EDM)
[ ] 2.4  Admin/Stages — full CRUD with all fields (slug, icon, color, department, display order, duration, rate)
[ ] 2.5  Admin/Stages — custom form builder (add/remove/reorder fields in CustomFieldsConfig)
[ ] 2.6  Admin/Stages — batch/machine/serial toggles
[ ] 2.7  Admin/Machines — full form with all fields (type, model, location, department, build volume, hourly rate)
[ ] 2.8  Admin/Machines — component management (add/remove MachineComponents for maintenance)
[ ] 2.9  Admin/Users — role dropdown with all roles, department, stage assignment (AssignedStageIds checkboxes)
[ ] 2.10 Admin/Users — password set/reset
[ ] 2.11 Admin/Materials — full form (density, cost/kg, supplier, compatible materials)
[ ] 2.12 Admin/Settings — grouped by category tabs (General, Branding, Serial, Debug)
[ ] 2.13 Verify: create a Part with stacking + 5 stage requirements → saved correctly
[ ] 2.14 Verify: create a custom stage with 3 custom fields → appears in nav
```

---

## Acceptance Criteria

- Parts form includes all stacking fields from §7A Part spec
- Parts can be assigned to stages with execution order and estimated hours
- Stages can have custom fields added/removed (text, number, dropdown, checkbox)
- Machines show component list with add/remove
- Users have role dropdown and stage assignment checkboxes
- Settings are grouped by category
- All changes persist to SQLite

## Files to Touch

- `Components/Pages/Admin/Parts.razor` — expand form significantly
- `Components/Pages/Admin/Stages.razor` — custom form builder UI
- `Components/Pages/Admin/Machines.razor` — expand form + components panel
- `Components/Pages/Admin/Users.razor` — role + stage assignment
- `Components/Pages/Admin/Materials.razor` — expand form
- `Components/Pages/Admin/Settings.razor` — category tabs
- Services: `IPartService`, `IStageService`, `IMaintenanceService` (for components)
