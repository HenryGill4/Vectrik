# Machine Setup Dispatch System — Implementation Plan

## Overview

Vectrik currently has no digital system for dispatching machine setups to operators. The scheduler creates jobs and stage executions, but the bridge between "this execution is scheduled" and "operator, go set up Machine X with Program Y" doesn't exist. For SLS specifically, changeover window / cooldown chamber management has no operator dispatch — missed windows cause costly machine downtime.

This system closes that gap with an intelligent dispatch engine that balances competing goals: minimize changeovers, meet due dates, maximize throughput — with SLS build management, maintenance awareness, and progressive learning built in.

## Architecture

```
WorkOrders → Jobs → StageExecutions
                         ↓
              SetupDispatchService (Intelligence Engine)
                    ↓              ↓
         SetupDispatch        SetupHistory (immutable ledger)
           ↓        ↓              ↓
    Scheduler     Operator    LearningEngine
    Dispatch      Setup       (EMA setup times,
    Board         Queue        operator proficiency)
```

---

## Phase 1: Foundation (Core Data + Manual Dispatch) — COMPLETE

### New Models
- **SetupDispatch** — atomic dispatch unit with lifecycle (Queued→Assigned→InProgress→PendingVerification→Verified→Completed), links to Machine, MachineProgram, StageExecution, Job, Part
- **SetupHistory** — immutable ledger for learning, records actual setup/changeover times
- **OperatorSetupProfile** — per-operator per-machine proficiency via EMA
- **DispatchConfiguration** — per-machine or global dispatch settings (weights, queue depth, etc.)

### New Enums
- DispatchStatus, DispatchType, MachineSetupState

### Existing Model Changes
- Machine: CurrentProgramId?, SetupState, LastSetupChangeAt?
- StageExecution: SetupDispatchId?
- MachineProgram: ActualAverageSetupMinutes?, SetupSampleCount, SetupVarianceMinutes?

### Service
- ISetupDispatchService / SetupDispatchService — manual CRUD + lifecycle transitions

### SignalR
- DispatchHub + IDispatchNotifier / DispatchNotifier (tenant/machine/operator groups)

### UI
- DispatchBoard.razor (scheduler "Setup" tab, machine swim lanes)
- SetupQueue.razor (operator tablet view at /shopfloor/setup-queue)

### Infrastructure
- NumberSequence: "DSP-00001" auto-numbering
- EF migration for all new tables + field additions
- All 421 tests pass

---

## Phase 2: SLS Build Dispatch Integration + Intelligence Engine

### 2A. Changeover Window Dispatch (Type: Changeover) — HIGHEST PRIORITY

**Problem:** SLS machines run 24/7 with auto-changeover, but the cooldown chamber holds N plates. If an operator doesn't clear the chamber before it fills, the machine goes DOWN.

**Auto-changeover rules per SLS machine:**
- `Machine.AutoChangeoverEnabled` controls whether machine auto-starts next build
- When enabled: machine starts next build automatically, but operator MUST clear cooldown chamber before it fills
- When disabled: machine waits for operator to manually start next build
- Admin UI toggle on machine settings page to enable/disable per machine

**Changeover alert escalation based on shift remaining:**
- Dispatch created when print completion is estimated within shift window
- Priority formula: `base_priority + (urgency_bonus * (1 - time_remaining_ratio)²)`
  - >2 hours remaining in shift: normal priority (50)
  - 1-2 hours remaining: elevated (70)
  - 30-60 min remaining: high (85) + visual warning
  - <30 min remaining: URGENT (95) + audio/push notification
  - Shift ended with chamber not cleared: CRITICAL (100) + machine marked at risk
- If operator doesn't clear chamber before shift end → dispatch persists into next shift at CRITICAL priority
- DispatchBoard shows countdown timer to shift end on changeover cards
- SetupQueue shows fullscreen alert when changeover is overdue

**Integration with ProgramSchedulingService:**
- `AnalyzeChangeoverAsync()` → creates/updates changeover dispatch priority
- `DetectChangeoverConflictsAsync()` → generates urgent dispatches for chamber overflow risk
- On dispatch completion: resets chamber counter, updates Machine.SetupState

### 2B. SLS Engineer Plate Layout Notification (Type: PlateLayout)

**Key principle: Engineers design plates, operators just press start.**

**Workflow:**
1. BuildAdvisorService detects unmet demand (parts needed, no Ready programs in queue)
2. System creates a PlateLayout dispatch targeted at SLS engineers (role-based routing via TargetRoleId)
3. Dispatch content: demand summary (which parts, how many, due dates), machine availability, recommended plate composition from `BuildAdvisorService.OptimizePlateAsync()`
4. Engineer receives notification → opens NextBuildAdvisor → designs plate layout → saves program
5. When program saved as Ready: PlateLayout dispatch auto-completes
6. If engineer ignores: priority escalates based on demand due dates

**New enum value:** Add `PlateLayout` to DispatchType
**New field on SetupDispatch:** `TargetRoleId?` (FK to OperatorRole) — routes dispatch to engineers vs operators
**Role routing:** PlateLayout dispatches appear on engineer's dispatch board, NOT operator SetupQueue

### 2C. Print Start — Operator Just Presses Start

- Created after BuildPlateLoad dispatch completes (plate physically loaded)
- Content: pre-print checklist (enclosure closed, powder level, gas flow, bed temp)
- Operator confirms checklist → presses "START PRINT" button
- System records `MachineProgram.PrintStartedAt`, transitions `ScheduleStatus` → Printing
- Simple, fast — large touch target "START PRINT" button on tablet view

### 2D. Print Completion + Mandatory Inspection Sheet

**Before a build can be released from PostPrint, operator must complete inspection.**

**Inspection checklist on dispatch:**
- SetupDispatch gets new field: `InspectionChecklistJson` (same `SignOffChecklistItem` pattern as StageExecution)
- Checklist items configurable per machine or per production stage

**Default SLS post-print inspection items:**
1. Visual inspection — no obvious defects, warping, delamination
2. Powder removal — build chamber and plate cleaned
3. Part count — all parts present and accounted for (cross-ref ProgramParts)
4. Dimensional spot-check — key dimensions within tolerance
5. Build plate condition — no damage, cracks, or excessive wear
6. Photos uploaded — before/after plate removal (optional)

**Workflow:**
- Print timer expires / machine signals completion → PrintCompletion dispatch created (Type: Teardown)
- Operator fills in inspection checklist items (required items must all be checked)
- If any item FAILS: dispatch transitions to Blocked, NCR auto-suggested
- If all items PASS: dispatch completes → MachineProgram transitions PostPrint → Completed
- Inspection results saved to SetupHistory for audit trail + learning

**Gating rule:** `CompleteDispatchAsync()` validates all required checklist items signed off before allowing completion on dispatches that have a checklist

### 2E. Intelligence Engine (General CNC/EDM Setup Scoring)

**Scoring Algorithm (composite 0-100):**

**Due Date Score** (weight: configurable, default 0.45)
- Traces StageExecution → Job → WorkOrderLine → WorkOrder.DueDate
- Overdue: 100, within 8h: 95, within 24h: 80, within 48h: 60, within 5d: 40
- Priority multiplier: Emergency 1.5x, Rush 1.3x, High 1.1x, Normal 1.0x, Low 0.8x
- Critical path bonus: +15 if completing this unlocks 2+ downstream stages

**Changeover Score** (weight: configurable, default 0.35)
- Same program as Machine.CurrentProgramId: 100 (no changeover)
- Same fixture, different program: 70
- Different fixture, same part family: 50
- Full changeover: max(0, 100 - estimatedChangeoverMinutes * 2)

**Throughput Score** (weight: configurable, default 0.20)
- Count downstream waiting executions unlocked: min(100, count * 15)
- Batch fill bonus: +20 if grouping fills >= 80% capacity
- Machine idle bonus: +15 if machine currently idle

**Maintenance Modifier** (-50 to +20, applied after weighted sum)
- Job overruns maintenance window: -50
- Job fits comfortably: +10
- Machine in maintenance buffer window: +20 for short jobs

**Formula:** `final = clamp((dueDate * w1) + (changeover * w2) + (throughput * w3) + maintenanceMod, 0, 100)`

**Auto-Dispatch Generation:**
- `DispatchGenerationBackgroundService` (IHostedService) — periodic timer
- Controlled by `dispatch.auto_enabled` SystemSetting + per-machine `AutoDispatchEnabled`
- Default: auto-dispatches require scheduler approval

---

## Phase 3: Maintenance Integration

- Maintenance modifier in scoring (-50 to +20)
- Auto-generate Maintenance-type dispatches when maintenance imminent
- Integrate with ProgramToolingItem wear tracking
- Maintenance alerts in both Dispatch Board and Operator Queue

---

## Phase 4: Learning & Operator Proficiency

- Setup-specific EMA learning (extends LearningService pattern)
- Changeover-specific EMA (from-program → to-program transitions)
- SLS-specific: actual changeover times vs estimated, actual print durations feeding back
- Operator proficiency auto-calculation (1-5 scale based on median comparison)
  - Expert (5): ≤70% of median, Advanced (4): ≤85%, Competent (3): ≤100%, Learning (2): ≤120%, Novice (1): >120%
- Proficiency-based auto-assignment (opt-in)

---

## Phase 5: Configuration & Polish

- Admin settings page with weight sliders and live preview
- Per-machine configuration UI (including auto-changeover toggle)
- Historical analytics: SLS downtime tracking, changeover compliance rate, setup time trends
- Browser/audio notifications for operators
- Seed data for demo tenant

---

## SystemSettings Keys

| Key | Default | Purpose |
|-----|---------|---------|
| `dispatch.auto_enabled` | false | Master auto-dispatch switch |
| `dispatch.lookahead_hours` | 8 | Planning horizon |
| `dispatch.changeover_weight` | 0.35 | Changeover optimization weight |
| `dispatch.duedate_weight` | 0.45 | Due date urgency weight |
| `dispatch.throughput_weight` | 0.20 | Throughput maximization weight |
| `dispatch.maintenance_buffer_hours` | 4 | Short-job routing trigger |
| `dispatch.max_queue_depth` | 3 | Default max dispatches per machine |
| `dispatch.setup_ema_alpha` | 0.3 | EMA smoothing for setup times |
| `dispatch.batch_grouping_window_hours` | 4 | Batch same-setup jobs within window |
| `dispatch.require_scheduler_approval` | true | Auto dispatches need approval |
| `dispatch.sls_load_lead_hours` | 2 | How far ahead to create BuildPlateLoad dispatches |
| `dispatch.changeover_alert_hours` | 1 | When to escalate changeover priority |
| `dispatch.changeover_urgent_minutes` | 30 | Threshold for URGENT escalation |
| `dispatch.plate_layout_auto_notify` | true | Auto-create PlateLayout dispatches for unmet demand |

---

## Key Design Decisions

1. **SetupDispatch decoupled from StageExecution** — dispatches can be cancelled/deferred/batched without corrupting execution tracking
2. **Scoring weights are tenant-configurable** — high-mix shops prioritize changeover; defense shops prioritize due dates
3. **EMA learning reuses existing LearningService pattern** — same math, same configurable alpha
4. **Background auto-dispatch is opt-in** — manual first, build trust, enable per-machine gradually
5. **Separate DispatchHub** — different consumers/payloads than MachineStateHub
6. **SetupHistory is append-only** — clean training data + full audit trail
7. **Machine.CurrentProgramId tracks floor state** — bridges scheduler intent vs reality for accurate changeover scoring
8. **SLS changeover alerts escalate exponentially as shift end approaches** — prevents downtime from missed windows
9. **Engineers design plates, operators just start prints** — PlateLayout dispatches route to engineers by role
10. **Build status transitions gated by dispatch completion** — Scheduled→Printing requires plate loaded, PostPrint→Completed requires inspection passed
11. **Mandatory inspection checklist before build release** — reuses SignOffChecklistItem pattern, blocks completion if required items not signed off
12. **Auto-changeover is a per-machine setting** — some machines may have it disabled for safety/process reasons
