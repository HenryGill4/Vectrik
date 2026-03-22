> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# Sprint 2: Admin Pages (Full CRUD)

> **Status**: COMPLETE
> **Goal**: Admins can fully configure parts, stages, machines, users, materials, and settings.
> **Depends on**: Sprint 1 (login + nav working)

---

## Tasks

```
[x] 2.1  Admin/Parts — stacking config section (single/double/triple durations, parts per build, enable toggles)
[x] 2.2  Admin/Parts — stage requirement assignment panel (pick stages, set order, estimated hours per stage)
[x] 2.3  Admin/Parts — batch stage durations (SLS, depowdering, heat treatment, wire EDM)
[x] 2.4  Admin/Stages — full CRUD with all fields (slug, icon, color, department, display order, duration, rate)
[x] 2.5  Admin/Stages — custom form builder (add/remove/reorder fields in CustomFieldsConfig)
[x] 2.6  Admin/Stages — batch/machine/serial toggles
[x] 2.7  Admin/Machines — full form with all fields (type, model, location, department, build volume, hourly rate)
[x] 2.8  Admin/Machines — component management (add/remove MachineComponents for maintenance)
[x] 2.9  Admin/Users — role dropdown with all roles, department, stage assignment (AssignedStageIds checkboxes)
[x] 2.10 Admin/Users — password set/reset
[x] 2.11 Admin/Materials — full form (density, cost/kg, supplier, compatible materials)
[x] 2.12 Admin/Settings — grouped by category tabs (General, Branding, Serial, Debug)
[x] 2.13 Verify: create a Part with stacking + 5 stage requirements → saved correctly
[x] 2.14 Verify: create a custom stage with 3 custom fields → appears in nav
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
