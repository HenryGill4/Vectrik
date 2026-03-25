# OpCentrix V3 — Claude Code Guide

## Project Overview
OpCentrix is a Manufacturing Execution System (MES) for SLS additive manufacturing. Built with Blazor Server (.NET 10), EF Core, SQLite. It manages the full production lifecycle from work orders through SLS printing, depowdering, wire EDM, CNC machining, finishing, QC, and shipping.

## Tech Stack
- **Framework**: Blazor Server (interactive SSR), .NET 10
- **Database**: SQLite via EF Core (tenant-isolated)
- **JS Interop**: ES modules for Gantt viewport (`wwwroot/js/gantt-viewport.js`)
- **Auth**: Cookie-based with custom claims (Role, UserId, AssignedStageIds, IsPlatform)
- **CSS**: Custom design system in `wwwroot/css/site.css` (no Tailwind/SASS)

## Architecture

### Project Structure
```
Components/
  Layout/           MainLayout, NavMenu, NavSection
  Pages/
    Scheduler/      The production scheduler (Gantt, demand, programs, floor, data views)
      Components/   GanttViewport, GanttBar, GanttMachineRow, KpiStrip, WorkOrderPanel, etc.
      Modals/       RescheduleModal, UnifiedScheduleWizard, NextBuildAdvisor (planned)
      Views/        GanttView, WorkOrdersView, BuildsView, StagesView, TableView
    Admin/          Settings pages (users, shifts, stages, materials, etc.)
    ShopFloor/      Operator-facing stage views
    ...
Services/           Business logic layer (one interface + one impl per domain)
Models/             EF Core entities
  Enums/            ManufacturingEnums.cs (all enums in one file)
Data/               TenantDbContext, migrations, seed data, SQLite databases
wwwroot/
  css/site.css      ALL custom CSS (single file, ~6000 lines)
  js/               ES modules for Gantt, dynamic forms, machine state
```

### Key Domain Models
- **WorkOrder** → **WorkOrderLine** → links to **Part**
- **Job** (Build/Batch/Part scope) → **StageExecution** (one per production stage)
- **MachineProgram** (BuildPlate/Standard type) → **ProgramPart** (parts on a plate)
- **Machine** → **MachineShiftAssignment** → **OperatingShift**
- **ProductionStage** → **ProcessStage** (per-approach stage config)
- **PartAdditiveBuildConfig** (stacking: single/double/triple stack durations + parts-per-build)

### Key Services
- **ProgramSchedulingService**: SLS build scheduling (slot finding, changeover analysis, timeline)
- **SchedulingService**: General job/stage scheduling (downstream stages, machine resolution)
- **BuildAdvisorService** (planned): Next-build recommendations, demand aggregation, plate composition
- **ShiftManagementService**: Shift CRUD, machine-shift and operator-shift assignments
- **ShiftTimeHelper**: Static helpers for shift-aware time calculations

### Production Flow (SLS)
1. SLS Print (build-level, 24/7 unmanned) → 2. Depowder (build-level) → 3. Wire EDM (build-level, parts cut off plate) → 4. CNC Machining (part-level, machine set up per part type) → 5. Finishing stages (part-level)

### Changeover System
SLS machines have auto-changeover: next build starts automatically, but an operator must remove the previous build from the cooldown chamber before the current build finishes. If no operator (nights/weekends), machine goes DOWN. The scheduler must align changeovers with operator shift windows.

## Build & Test
```bash
dotnet build --no-restore              # Build (app may lock exe if running)
dotnet test --no-restore               # Run 375+ tests
dotnet ef migrations add <Name> --context TenantDbContext --output-dir Data/Migrations/Tenant
```

## Conventions
- Services: `IFooService` interface + `FooService` implementation, registered as `AddScoped` in `Program.cs`
- Components: Razor components in appropriate `Components/Pages/<Area>/` directory
- CSS: All styles in `wwwroot/css/site.css`, class prefix matches component (`.sched-*`, `.gantt-*`, `.wo-*`)
- Modals: Use `.modal-overlay` + `.modal-content` pattern
- Toast notifications: `Toast.ShowSuccess()` / `Toast.ShowError()`
- SQLite gotchas: No `TimeSpan` in ORDER BY, no complex LINQ expressions — sort client-side after `.ToListAsync()`
- JS interop: Use `[JSInvokable("MethodName")]` for callbacks, `IJSObjectReference` for ES modules
- Responsive: Mobile breakpoints at 768px and 480px in site.css

## Current State
The scheduler has 5 views (Production/Gantt, Demand, Programs, Floor, Data). Gantt supports drag-and-drop rescheduling via JS. Work order panel shows demand alongside the Gantt. Shift-period shading shows off-hours on the Gantt. Changeover segments show on build bars.

Active development: "Next Build Advisor" — see `docs/PLAN-scheduler-advisor.md`.

## Machine Context — EOS M4 Onyx DMLS Printers

The shop runs **two EOS M4 Onyx** (also referred to as EOS M 400-4) DMLS/SLS 3D printers.

### Machine Specs (as configured in seed data)
- **Build Volume**: 450mm x 450mm x 400mm
- **Lasers**: 6 lasers, 1000W max each
- **Build Plate Capacity**: 2 (can hold 2 builds in the cooldown chamber before a manual changeover is needed)
- **Auto-Changeover**: Enabled, ~30 minutes
- **Hourly Rate**: $200/hr

### Operational Reality
- Machines run **continuously 24/7** if managed properly
- The auto-changeover system starts the next build automatically after the current build finishes
- The cooldown chamber holds **2 builds**. An operator must remove the cooled plate from the cooldown chamber **daily** (at minimum once per day during shift hours) to prevent the machine from going DOWN
- If a build finishes and the cooldown chamber is full (both slots occupied) and no operator is present, the machine **stops and goes DOWN** until an operator removes a plate
- Changeovers must be scheduled during **operator shift windows** to keep machines running continuously
- Typical print durations range from ~20-30 hours per build depending on part geometry and stacking

### Scheduling Logic
The scheduler should:
1. Find the earliest available slot after the current/last build ends
2. Calculate when the changeover would occur (build end = when operator needs to remove previous build from cooldown)
3. Check if that changeover time falls within an operator's shift
4. If yes → "Safe changeover" (recommended)
5. If no → "Machine DOWN until shift" (risky, but sometimes unavoidable)
6. Stack levels (single/double/triple) affect print duration and thus when changeover lands

## Known Bugs & Issues
See `docs/FEEDBACK-scheduler-wizard.md` for detailed bug reports and UX recommendations.
