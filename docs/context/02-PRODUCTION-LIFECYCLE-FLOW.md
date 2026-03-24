# Production Lifecycle Flow

> **Status**: Current — describes the intended end-to-end flow
> **See also**: `docs/fixes/` for deviations from this flow caused by known bugs

---

## Overview

```
Work Order → Build Plate → Print → Post-Print → Per-Part Processing → Complete
```

A part's journey has two phases:
1. **Build Phase** — part is on a build plate with other parts (build-level stages)
2. **Part Phase** — part is released from the plate as a `PartInstance`, goes through batch and per-part stages

---

## Step-by-Step Flow

### 1. Work Order Created
- `WorkOrder` created with one or more `WorkOrderLine` records
- Each line: `PartId` + `Quantity` + `DueDate`
- Status: `Released` or `InProgress`

### 2. Build Package Created
- User creates a `BuildPackage` (a build plate)
- Adds `BuildPackagePart` records (parts + quantities)
- Enters slicer data (`EstimatedDurationHours`, `IsSlicerDataEntered = true`)
- Status transitions: `Draft → Sliced → Ready`

### 3. Build Scheduled — `BuildSchedulingService.ScheduleBuildAsync(buildPackageId, machineId)`
```
ScheduleBuildAsync()
  → FindEarliestBuildSlotAsync(machineId, duration, notBefore)
  → package.ScheduledDate = slot.PrintStart
  → package.Status = Scheduled
  → package.MachineId = machineId
  → CreateBuildStageExecutionsAsync(buildPackageId, "Scheduler")
      → loads ManufacturingProcess for each part
      → filters ProcessStages where ProcessingLevel == Build
      → deduplicates by ProductionStageId (first occurrence wins)
      → creates Job (Scope=Build, ManufacturingProcessId set)
      → creates StageExecution[] with ProcessStageId set, MachineId resolved
  → CreatePartStageExecutionsAsync(buildPackageId, "Scheduler", startAfter=lastBuildStageEnd)
      → loads ManufacturingProcess for each part
      → creates Job per (PartId, WorkOrderLineId) group (Scope=Part)
      → creates StageExecution[] for Batch + Part level stages with ProcessStageId set
  → AutoScheduleJobAsync(jobId) called for each per-part job
      → ResolveMachines() for each execution
      → FindEarliestSlotOnMachine() picks best non-conflicting slot
```

### 4. Print Starts
- Operator clicks "Start Print" in Scheduler / Builds view
- `package.Status = Printing`
- `package.PrintStartedAt = now`
- Build-level `StageExecution` for SLS Printing starts

### 5. Print Completes
- `package.Status = PostPrint`
- `package.PrintCompletedAt = now`
- Post-print build-level stages execute (depowder, heat treat, etc.)
- Each stage: `StageService.StartStageExecutionAsync()` → `CompleteStageExecutionAsync()`

### 6. Plate Released — `BuildSchedulingService.ReleasePlateAsync(buildPackageId, releasedBy)`
```
ReleasePlateAsync()
  → Verify all build-level StageExecutions are Completed or Skipped
  → package.Status = Completed
  → package.PlateReleasedAt = now
  → For each BuildPackagePart × Quantity:
      → Create PartInstance (TemporaryTrackingId = "TMP-{buildId}-{index}")
  → Complete the build-level Job
  → Load ManufacturingProcess for parts
  → BatchService.CreateBatchesFromBuildAsync() → creates ProductionBatch records
  → Assign PartInstances to batches (round-robin by capacity)
  → CreatePartStageExecutionsAsync() — idempotent, reuses prefilled jobs if exist
  → AutoScheduleJobAsync() for any jobs not yet machine-scheduled
```

### 7. Plate Release Trigger (Alternative Path)
- `StageService.CompleteStageExecutionAsync()` also checks for plate release trigger
- If `execution.ProcessStageId` matches `ManufacturingProcess.PlateReleaseStageId`:
  - Calls `CreatePartStageExecutionsAsync()` automatically
- This is the "push" path vs the manual `ReleasePlateAsync()` path
- ⚠️ Requires `ProcessStageId` to be set on the execution (see FIX-02, FIX-05)

### 8. Per-Part Processing
- `Job` (Scope=Part) has `StageExecution[]` for Batch + Part level stages
- Operators pick up work from ShopFloor queue
- `StageService.StartStageExecutionAsync(executionId, operatorUserId, operatorName)`
  - Sets `execution.Status = InProgress`
  - Sets `machine.Status = Running`
  - Sets `job.Status = InProgress` if not already
- `StageService.CompleteStageExecutionAsync(executionId)`
  - Sets `execution.Status = Completed`
  - Updates `machine.Status = Idle`
  - Accumulates `machine.TotalOperatingHours`
  - Runs EMA learning via `ProcessStageId` (if set)
  - Checks if all job stages done → `job.Status = Completed`
  - Updates WO fulfillment if job has WorkOrderLineId

### 9. Serial Number Assignment
- At laser engraving stage: `PartInstance.SerialNumber` assigned
- `PartInstance.IsSerialAssigned = true`
- `DisplayIdentifier` switches from `TemporaryTrackingId` to `SerialNumber`

### 10. Part Instance Complete
- Final stage completes
- `PartInstance.Status = Completed` (or Passed/Failed based on QC)
- Part is shipped / delivered against `WorkOrderLine`

---

## Batch Lifecycle (Parallel to Per-Part Processing)

```
ProductionBatch (Status: Open)
  → Parts assigned to batch
  → batch.Status = Sealed (locked for transport)
  → batch.Status = InProcess (at a machine/station)
  → batch.CurrentProcessStageId updated as batch moves through stages
  → batch.AssignedMachineId set when at a machine
  → batch.Status = Completed (all parts done at this level)
  → (or) batch.Status = Dissolved (parts reassigned to different batches)
```

`BatchPartAssignment` is an **immutable audit log** — every assign/remove is recorded with timestamp, operator, and `AtProcessStageId`. This is critical for ITAR traceability.

---

## Key Relationships

```
WorkOrder
  └── WorkOrderLine (partId, qty, dueDate)
        └── BuildPackagePart (qty on this plate)
              └── BuildPackage (the plate)
                    ├── Job (Scope=Build) ← build-level scheduling
                    │     └── StageExecution[] (Build-level stages)
                    └── PartInstance[] (one per physical part, created at plate release)
                          └── ProductionBatch (groups of parts)

ManufacturingProcess (1:1 with Part)
  └── ProcessStage[] (ordered by ExecutionOrder)
        ├── Build-level stages → create build StageExecutions
        ├── Batch-level stages → create per-part StageExecutions (batched)
        └── Part-level stages  → create per-part StageExecutions (individual)
```
