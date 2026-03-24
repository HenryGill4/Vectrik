# OpCentrix V3 — System Overview

> **Status**: Current — reflects actual codebase state as of 2026-03-22
> **See also**: `docs/fixes/` for known bugs that deviate from this intended design

---

## Two Routing Systems (Legacy vs New)

The system is in transition. The new system (`ManufacturingProcess → ProcessStage`) is the **authoritative source of truth**. The legacy system (`PartStageRequirement`) is `[Obsolete]` and exists only as a fallback for parts that haven't been migrated.

| | Legacy | New |
|---|--------|-----|
| **Model** | `PartStageRequirement` | `ManufacturingProcess` → `ProcessStage` |
| **Scope** | Per-part, flat list | Per-part, hierarchical with Build/Batch/Part levels |
| **Machine ID type** | `string` (matches `Machine.MachineId`) | `int` (matches `Machine.Id`) |
| **Duration model** | Flat minutes | Compound: PerBuild / PerBatch / PerPart |
| **Batch support** | None | Yes (`ProcessingLevel.Batch`, `BatchCapacityOverride`) |
| **EMA learning** | Via `PartStageRequirement` fields | Via `ProcessStage.ActualAverageDurationMinutes` |
| **Status** | `[Obsolete]` — do not use for new parts | Active — use for all new parts |

**Rule**: When creating or scheduling jobs, always check for an active `ManufacturingProcess` first. Only fall back to `PartStageRequirements` if none exists.

---

## Two Machine ID Systems

`Machine` has **two** identifier fields. They serve different purposes and must not be mixed.

| Field | Type | Purpose | Used By |
|-------|------|---------|---------|
| `Machine.Id` | `int` (PK) | Database foreign key | All new models, all FKs in StageExecution, Job, ProcessStage, etc. |
| `Machine.MachineId` | `string` (unique, max 50) | Human-readable business ID (e.g. "SLS-001", "EDM-02") | Legacy `PartStageRequirement`, `ProductionStage.DefaultMachineId` |

**Every FK to a machine in modern code uses `Machine.Id` (int).** The only exception is `ProductionStage.DefaultMachineId` which stores `Machine.MachineId` (string) by design.

See `docs/fixes/FIX-01-ProductionStage-AssignedMachineIds-Lookup.md` for where `ProductionStage.AssignedMachineIds` stores int PKs but is incorrectly looked up as string MachineIds (root cause of Gantt misrouting).

---

## Key Models

### `Machine`
- `Id` (int PK) — database key, used in all FKs
- `MachineId` (string) — human business identifier
- `IsAdditiveMachine` — computed: `MachineType == "SLS" || "Additive"`
- `IsAvailableForScheduling` — must be true to receive scheduled work

### `ManufacturingProcess`
- 1:1 with `Part`
- Contains ordered `ICollection<ProcessStage> Stages`
- `DefaultBatchCapacity` — parts per batch (default 60)
- `PlateReleaseStageId` — which `ProcessStage` completion triggers plate release

### `ProcessStage`
- Child of `ManufacturingProcess`
- `ProcessingLevel` (Build | Batch | Part) — critical for routing
- `AssignedMachineId` (`int?`) — FK to `Machine.Id`
- `PreferredMachineIds` (`string?`) — comma-separated `Machine.Id` ints
- `DurationMode` / `RunDurationMode` / `SetupDurationMode` — how duration scales
- `ProcessStageId` is set on `StageExecution` when created from this system

### `ProductionStage` (Global Catalog)
- Shared stage definitions (SLS Printing, QC, Wire EDM, etc.)
- `AssignedMachineIds` (`string?`) — comma-separated `Machine.Id` **ints** stored as strings
- `DefaultMachineId` (`string?`) — stores `Machine.MachineId` (human business ID) ← different type!
- Used as the `ProductionStageId` FK on `ProcessStage` and `StageExecution`

### `StageExecution`
- Central scheduling entity
- `JobId` — parent job
- `ProductionStageId` — which stage type this is (catalog reference)
- `ProcessStageId` (`int?`) — link to process-specific config (null for legacy-created executions)
- `MachineId` (`int?`) — FK to `Machine.Id`
- `ScheduledStartAt` / `ScheduledEndAt` — scheduler output
- `BuildPackageId` — set for build-level executions only

### `PartStageRequirement` (OBSOLETE)
- `[Obsolete]` — do not create new records
- `AssignedMachineId` (`string?`) — matches `Machine.MachineId` (NOT `Machine.Id`)
- `PreferredMachineIds` (`string?`) — comma-separated `Machine.MachineId` strings
- Still read by `SchedulingService.ResolveMachines()` as a fallback (steps 4-5)

---

## Service Responsibilities

| Service | Responsibility |
|---------|---------------|
| `JobService` | CRUD for jobs; creates `StageExecution` records from routing (should prefer `ManufacturingProcess`) |
| `SchedulingService` | Auto-schedule: assigns machines and time slots; `ResolveMachines()` priority chain |
| `BuildPlanningService` | Build-level and per-part `StageExecution` creation from `ManufacturingProcess`; idempotent |
| `BuildSchedulingService` | Orchestrates build scheduling; calls planning + scheduling; handles plate release |
| `StageService` | Stage execution lifecycle (start/complete/pause/fail); EMA learning; plate release trigger |
| `ManufacturingProcessService` | CRUD for `ManufacturingProcess` + `ProcessStage`; duration calculation; validation |
| `BatchService` | `ProductionBatch` lifecycle; part assignment/removal; consolidation |

---

## Branch Information

Active work branch: `claude/fix-scheduling-routing-JHUOI`
