# Module 02: Sales & Work Order Management

## Status: [ ] Not Started
## Category: ERP
## Phase: 1 — Core Production Engine
## Priority: P1 - Critical

---

## Overview

Work Orders are the backbone of production. A Work Order represents a customer
commitment, containing one or more line items (parts/quantities). Each line item
spawns one or more Jobs that flow through the shop. This module implements the
complete quote-to-ship lifecycle with real-time status tracking, Kanban boards,
and cross-module traceability.

**ProShop Improvements**: Unified search across all WO data, Kanban swim-lane
boards, @mention collaboration on WOs, and real-time cost tracking against budget.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `WorkOrder` model (Status, Priority, Lines) | ✅ Exists | `Models/WorkOrder.cs` |
| `WorkOrderLine` model (PartId, Qty, JobId) | ✅ Exists | `Models/WorkOrderLine.cs` |
| `WorkOrderStatus` enum | ✅ Exists | `Models/Enums/ManufacturingEnums.cs` |
| `Job` model (Status, Priority, StageExecutions) | ✅ Exists | `Models/Job.cs` |
| `JobStatus`, `JobPriority` enums | ✅ Exists | `Models/Enums/ManufacturingEnums.cs` |
| `WorkOrderService` / `IWorkOrderService` (stub) | ✅ Exists | `Services/WorkOrderService.cs` |
| `JobService` / `IJobService` (stub) | ✅ Exists | `Services/JobService.cs` |
| `/workorders` page (index + details UI stub) | ✅ Exists | `Components/Pages/WorkOrders/` |

**Gap**: Services are stubs; no job creation from WO lines, no stage execution spawning, no real-time cost tracking, no comments/collaboration.

---

## What Needs to Be Built

### 1. Database Model Extensions
- Add `CustomerName`, `CustomerPO`, `DueDate`, `BudgetedCost`, `ActualCost` to `WorkOrder`
- Add `JobNote` → `WorkOrderComment` (generalize for WO-level comments)
- Add `MustLeaveByDate` to `Job` (ProShop feature for on-time scheduling)
- Add `StageExecution` completeness: link to operator, timestamps, actual hours

### 2. Service Layer
- Complete `WorkOrderService` with full lifecycle
- Complete `JobService` with stage execution management
- `WorkOrderToJobConverter` — spawns jobs + stage executions from WO lines
- `JobCostTracker` — real-time labor + material cost accumulation

### 3. UI Components
- **WO List** — with priority/status columns and quick-filter
- **WO Detail** — lines, jobs, real-time status per job, cost vs. budget
- **WO Kanban** — swim-lane board by status
- **Job Detail** — stage execution list with progress, assigned operator
- **Comments/Notes** — threaded notes on WO and Job level

---

## Implementation Steps

### Step 1 — Extend WorkOrder Model
**File**: `Models/WorkOrder.cs`
Add:
```csharp
public string CustomerName { get; set; } = string.Empty;
public string? CustomerPO { get; set; }
public int? QuoteId { get; set; }
public Quote? Quote { get; set; }
public DateTime? DueDate { get; set; }
public DateTime? ReleasedAt { get; set; }
public decimal BudgetedCost { get; set; }
public decimal ActualCostToDate { get; set; }
public string? InternalNotes { get; set; }
```

### Step 2 — Extend Job Model
**File**: `Models/Job.cs`
Add:
```csharp
public DateTime? MustLeaveByDate { get; set; }
public decimal BudgetedHours { get; set; }
public decimal ActualHoursToDate { get; set; }
public string? AssignedToUserId { get; set; }
public User? AssignedToUser { get; set; }
public decimal EstimatedCost { get; set; }
public decimal ActualCostToDate { get; set; }
public int WorkOrderLineId { get; set; }
public WorkOrderLine WorkOrderLine { get; set; } = null!;
```

### Step 3 — WorkOrderComment Model
**New File**: `Models/WorkOrderComment.cs`
```csharp
public class WorkOrderComment
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int? ParentCommentId { get; set; }          // for threaded replies
    public WorkOrderComment? ParentComment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
}
```
Add to `TenantDbContext.cs`: `public DbSet<WorkOrderComment> WorkOrderComments { get; set; }`

### Step 4 — Complete WorkOrderService
**File**: `Services/WorkOrderService.cs`

Implement all interface methods:
```csharp
Task<List<WorkOrder>> GetAllAsync(WorkOrderStatus? status = null);
Task<WorkOrder?> GetByIdAsync(int id);           // include Lines, Jobs, Comments
Task<WorkOrder> CreateAsync(WorkOrder wo);
Task UpdateAsync(WorkOrder wo);
Task DeleteAsync(int id);
Task ReleaseAsync(int id);                       // Draft → Released; spawns Jobs
Task CompleteAsync(int id);                      // mark complete when all jobs done
Task<List<WorkOrderComment>> GetCommentsAsync(int workOrderId);
Task AddCommentAsync(WorkOrderComment comment);
Task<decimal> GetActualCostAsync(int workOrderId);
```

### Step 5 — Complete JobService
**File**: `Services/JobService.cs`

Implement:
```csharp
Task<List<Job>> GetAllAsync(JobStatus? status = null);
Task<Job?> GetByIdAsync(int id);                 // include StageExecutions, Notes
Task<Job> CreateFromWorkOrderLineAsync(int workOrderLineId);
Task UpdateAsync(Job job);
Task<List<StageExecution>> GetStageExecutionsAsync(int jobId);
Task StartStageAsync(int stageExecutionId, string operatorUserId);
Task CompleteStageAsync(int stageExecutionId, string operatorUserId, decimal actualHours);
Task FailStageAsync(int stageExecutionId, string reason);
Task<Job> SpawnJobsForWorkOrderAsync(int workOrderId); // creates Jobs+StageExecutions
```

**Stage Execution Spawn Logic** (in `SpawnJobsForWorkOrderAsync`):
- For each `WorkOrderLine`, create a `Job`
- Look up `PartStageRequirements` for the part (ordered by `ExecutionOrder`)
- Create a `StageExecution` per requirement with `Status = NotStarted`
- Set `Job.BudgetedHours` = sum of stage estimated hours
- Set `Job.MustLeaveByDate` from `WorkOrder.DueDate` minus shipping buffer

### Step 6 — Work Order List Page
**File**: `Components/Pages/WorkOrders/Index.razor`

UI requirements:
- Header: "Work Orders" + "New Work Order" button + search bar
- Status filter tabs: All | Draft | Released | In Progress | Complete | On Hold
- Table columns: WO #, Customer, Parts, Priority, Due Date, Budget, Actual Cost, Status
- Priority badge: Emergency (red), Rush (orange), High (yellow), Normal (blue), Low (grey)
- Cost over-budget indicator: red text when `ActualCostToDate > BudgetedCost`
- Click row → `/workorders/{id}`
- Kanban view toggle (button to switch between table and Kanban)

### Step 7 — Work Order Kanban View
**File**: `Components/Pages/WorkOrders/Kanban.razor`

UI requirements:
- Columns: Draft | Released | In Progress | On Hold | Complete
- Each card shows: WO number, Customer, Due date, Job count, Priority badge
- Drag-and-drop to change status (use `@ondragstart` / `@ondrop` Blazor events)
- Overdue cards highlighted with red border

### Step 8 — Work Order Detail Page
**File**: `Components/Pages/WorkOrders/Details.razor`

UI requirements:
- Header section: WO number, Customer, PO ref, Due date, Status badge, Priority
- **Tabs**:
  - **Jobs** tab: list of jobs from this WO with per-job status and progress bar
  - **Cost** tab: budgeted vs. actual cost breakdown (labor, materials, outside processing)
  - **Comments** tab: threaded comment feed with "Add comment" input
  - **History** tab: status change audit trail with timestamps
- "Release WO" button (Draft state only) — triggers job/stage-execution spawning
- "Edit" button → edit modal or navigate to edit page

### Step 9 — Job Detail Page
**File**: `Components/Pages/WorkOrders/JobDetail.razor`
**Route**: `/workorders/{workOrderId:int}/jobs/{jobId:int}`

UI requirements:
- Job header: Part name, Qty, Priority, Status, Assigned operator
- **Stage execution table**:
  - Stage name, Estimated hours, Actual hours, Operator, Start time, End time, Status
  - Status badges with progress indicator
- **Notes** section: job-level notes with timestamps
- Budget vs. actual hours progress bar (red if over budget)

### Step 10 — New Work Order Form
**File**: `Components/Pages/WorkOrders/New.razor`
**Route**: `/workorders/new`

UI requirements:
- Customer name (text field or CRM lookup when Module 16 is built)
- Customer PO number
- Due date picker
- Priority selector
- Budget field (auto-fills from linked quote)
- **Add Lines** section: select Part, enter Qty, see estimated cost per line
- "Create & Release" button — creates WO and immediately releases (spawns jobs)
- "Create as Draft" button — saves as draft for review

### Step 11 — EF Core Migration
```bash
dotnet ef migrations add AddWorkOrderEnhancements --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Work Orders can be created with multiple line items
- [ ] "Release" action spawns Jobs and StageExecutions automatically
- [ ] Stage executions ordered correctly per `PartStageRequirement.ExecutionOrder`
- [ ] Job detail shows stage progression with actual vs. estimated hours
- [ ] Real-time cost tracking updates as stages complete
- [ ] Budget overrun shows visual warning
- [ ] Comments/notes can be added to Work Orders
- [ ] Kanban board reflects current WO status
- [ ] WO converts from accepted Quote with all lines pre-filled
- [ ] MustLeaveByDate calculated from WO due date

---

## Dependencies

- **Module 01** (Estimating & Quoting) — Quote-to-WO conversion
- **Module 04** (Shop Floor) — StageExecution workflow driven from here
- **Module 07** (Analytics) — WO completion data feeds KPI dashboards
- **Module 08** (Parts/PDM) — Part routing lookup for job spawning
- **Module 09** (Job Costing) — Actual cost accumulation
