# Plan: SLS Printing Operator Panel Redesign

## Goal
Transform the SLS Printing partial from a basic progress tracker into a full **situational awareness dashboard** that gives operators everything they need for their shift — build context, time-to-completion, changeover awareness, live telemetry, and machine queue visibility.

## Current State
- Shows build plate info (machine, material, parts, duration)
- Shows slicer data (layers, height, powder)
- Shows build contents table (part#, name, qty, WO#)
- Has a **manual** layer input for progress tracking
- Shows upcoming builds table (next 3 scheduled)
- Has a placeholder telemetry section with "—" values and a "requires SignalR" banner
- `MockMachineProvider` exists but generates random data disconnected from real builds
- `MachineSyncService` polls every 10s, saves to DB, pushes via SignalR hub
- `MachineStateHub` + `IMachineStateNotifier` already wired
- `IProgramSchedulingService` has `GetMachineTimelineAsync`, `AnalyzeChangeoverAsync`, `DetectChangeoverConflictsAsync`

## Design — Operator's Mental Model

### Section Layout (top to bottom, importance-ordered)

```
┌─────────────────────────────────────────────────────────┐
│ ⏱️ BUILD STATUS HERO                                    │
│  Build: BP-00042  •  Printing  •  Layer 1,247 / 2,800  │
│  ████████████████░░░░░░░░  44.5%                        │
│  Started 6h 12m ago  •  ~7h 48m remaining  •  ETA 3:15 PM │
│  Bed 185°C  •  Chamber 42°C  •  O₂ 0.04%  •  ⚡ 320W  │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ 🔄 CHANGEOVER & NEXT BUILD                              │
│  ┌──────────────────┐  ┌──────────────────────────────┐ │
│  │ Changeover       │  │ Next Build                   │ │
│  │ 3:15 - 3:45 PM   │  │ BP-00043 (32 parts)          │ │
│  │ ✅ Operator avail │  │ Duration: 22.5h              │ │
│  │ Chamber: 1/2 used│  │ Material: PA12-GF (same)     │ │
│  └──────────────────┘  │ Starts: ~3:45 PM             │ │
│                        └──────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ 📦 BUILD CONTENTS                                       │
│  Part#         Name              Qty   WO#     Due      │
│  DM-SUP-FULL   Atlas 556         12    WO-042  Mar 28 🔴│
│  PSA-HSG-001   Housing v2         8    WO-045  Apr 2    │
│  PSA-PLG-003   Plug Assembly      6    WO-045  Apr 2    │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ 📋 MACHINE QUEUE (3 of 6 builds scheduled)              │
│  ▶ BP-00042  PRINTING  44.5%  ETA 3:15 PM              │
│    BP-00043  Scheduled  3:45 PM  22.5h  32 parts        │
│    BP-00044  Scheduled  Tomorrow 2:15 AM  18.2h         │
│  ⚠️ BP-00044 changeover at 2:15 AM — NO OPERATOR       │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ 🔧 MACHINE HEALTH                                       │
│  EOS M4 Onyx #1  •  Connected  •  Last maint: 12 days  │
│  Operating hours: 4,821h  •  Next maint: 18 days        │
│  Build volume: 450×450×400mm  •  Capacity: 2 plates     │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ Slice File Data  (collapsed by default, click to expand)│
│ LogDelayPanel                                           │
│ WorkInstructionBanner                                   │
└─────────────────────────────────────────────────────────┘
```

## Implementation Phases

### Phase 1: Build-Aware Mock Telemetry Provider
**Files:** `Services/MachineProviders/MockMachineProvider.cs`

Currently the mock generates totally random values. Fix it to:
- Accept `IProgramSchedulingService` or query `TenantDbContext` for the currently-printing `MachineProgram` on this machine
- Compute `CurrentLayer` based on elapsed time vs. `EstimatedPrintHours` and `LayerCount`:
  ```
  elapsedFraction = (UtcNow - PrintStartedAt) / EstimatedPrintHours
  currentLayer = (int)(elapsedFraction * LayerCount) + small jitter
  ```
- Generate telemetry values that are **consistent within a build** (not random each poll):
  - Bed temp: ~180-195°C when building (gradual ramp in first 30 min, then stable with ±2° noise)
  - Chamber temp: ~35-45°C when building (slow rise over hours)
  - O₂: 0.02-0.06% when building (stable with tiny noise)
  - Laser power: proportional to part density (200-400W range)
  - Humidity: 20-30% (stable)
- When machine is idle/cooling: temps decay, no layer progress
- Status should reflect actual `ScheduleStatus`: Building → Cooling → Idle

### Phase 2: SLSPrinting.razor — Build Status Hero Section
**Files:** `Components/Pages/ShopFloor/Partials/SLSPrinting.razor`

Replace the current scattered layout with a hero card at the top:
- **Progress bar** driven by telemetry (from `MachineStateRecord`) not manual input
- **Time tracking**: elapsed (from `PrintStartedAt`), remaining (estimated from progress), ETA
- **Inline telemetry strip**: bed temp, chamber temp, O₂, laser power — single row, compact
- **Status badge**: Printing / Preheating / Cooling / Idle
- Keep manual layer input as **override** (smaller, below the auto-tracked value) — useful when telemetry disconnects

Inject `IMachineProvider` to call `GetCurrentStateAsync(machineId)` on load.
No SignalR subscription yet (Phase 5) — just poll on page load for now.

### Phase 3: Changeover & Next Build Card
**Files:** `Components/Pages/ShopFloor/Partials/SLSPrinting.razor`

New card below the hero:
- **Changeover timing**: Call `AnalyzeChangeoverAsync(machineId, estimatedBuildEnd)`
  - Show start/end time, operator availability (green check / red warning)
  - Show cooldown chamber status: "X of Y slots used" (from `Machine.BuildPlateCapacity` vs currently printing/post-print builds)
- **Next build preview**: Load next scheduled `MachineProgram` after current
  - Name, part count, estimated duration, material (highlight if different = changeover waste)
  - Estimated start time (current build ETA + changeover minutes)

### Phase 4: Enhanced Build Contents & Machine Queue
**Files:** `Components/Pages/ShopFloor/Partials/SLSPrinting.razor`

**Build Contents** — add to existing table:
- WO due date column with urgency coloring (red if overdue, orange if due within 3 days)
- Priority badge from `Job.Priority`

**Machine Queue** — replace basic "Upcoming Builds" table:
- Show current build at top with PRINTING badge and progress %
- Next 3-5 scheduled builds with times and durations
- Inline changeover conflict warnings (from `DetectChangeoverConflictsAsync`)
- Visual timeline bar (mini horizontal bar showing build blocks proportionally)

### Phase 5: Machine Health Card
**Files:** `Components/Pages/ShopFloor/Partials/SLSPrinting.razor`

New card near bottom:
- Machine name, connection status
- Maintenance dates: last, next, operating hours (from `Machine` model)
- Build volume, plate capacity, auto-changeover status
- Alert if maintenance overdue (`NextMaintenanceDate < UtcNow`)

### Phase 6: SignalR Live Updates (stretch)
**Files:** `Components/Pages/ShopFloor/Partials/SLSPrinting.razor`

- Subscribe to `MachineStateHub` for this machine's group
- Update telemetry values in real-time (every 10s from `MachineSyncService`)
- Auto-update progress bar, layer count, ETA without page refresh
- This hooks into the existing `MachineSyncService` → `MachineStateNotifier` → `MachineStateHub` pipeline

### Phase 7: Slicer Data Collapse & Polish
- Move slicer data into a collapsible `<details>` element (useful but not primary info)
- Ensure LogDelayPanel and WorkInstructionBanner stay at bottom
- Add CSS for hero card styling, telemetry strip, urgency colors, queue timeline

## Service Dependencies (all already exist)
| Service | Method | Purpose |
|---------|--------|---------|
| `IMachineProgramService` | `GetByIdAsync` | Current build details |
| `IMachineProgramService` | `GetProgramsForMachineAsync` | Queue + next build |
| `IMachineProvider` | `GetCurrentStateAsync` | Live telemetry (mock for now) |
| `IProgramSchedulingService` | `AnalyzeChangeoverAsync` | Changeover timing + operator availability |
| `IProgramSchedulingService` | `DetectChangeoverConflictsAsync` | Queue conflict warnings |
| `IMachineService` | (via Machine nav property) | Machine specs, maintenance dates |

## Data Flow
```
MachineSyncService (background, 10s poll)
  → MockMachineProvider.GetCurrentStateAsync(machineId)
    → reads currently-printing MachineProgram for this machine
    → computes realistic layer/temp/progress based on elapsed time
    → returns MachineStateRecord
  → saves to TenantDbContext.MachineStateRecords
  → pushes to MachineStateHub via IMachineStateNotifier

SLSPrinting.razor (on load)
  → calls IMachineProvider.GetCurrentStateAsync for initial state
  → calls AnalyzeChangeoverAsync for changeover card
  → calls DetectChangeoverConflictsAsync for queue warnings
  → (Phase 6) subscribes to SignalR for live updates
```

## Future OPC UA Path
When real machines connect:
1. Create `OpcUaMachineProvider : IMachineProvider` alongside `MockMachineProvider`
2. `MachineProviderFactory` already routes to the correct provider per machine
3. `MachineConnectionSettings` already has `ProviderType` field for routing
4. Everything else (sync service, SignalR hub, UI) works unchanged
5. Zero changes to `SLSPrinting.razor` — it just reads `MachineStateRecord`

## Not In Scope
- Real OPC UA integration (future, separate feature)
- Multi-machine dashboard (operator panel is per-stage-execution)
- Historical telemetry charts (future analytics feature)
- Predictive maintenance alerts (future ML feature)
