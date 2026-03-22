> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# CHUNK-05: Workflow Engine Wiring

> **Size**: L (Large) — ~10-15 file edits, 1 new page
> **ROADMAP tasks**: H6.19, H6.20, H6.21, H6.22
> **Prerequisites**: H1-H5 complete

---

## Scope

Wire the existing `IWorkflowEngine` into WO release, Quote approval, and NCR
disposition flows. Build the Admin Workflows configuration page. When no workflow
is defined for an entity type, the action proceeds immediately (backward compatible).

---

## Files to Read First

| File | Why |
|------|-----|
| `Services/IWorkflowEngine.cs` | Understand workflow API |
| `Services/WorkflowEngine.cs` | Understand execution logic |
| `Models/WorkflowDefinition.cs` | Workflow model (if exists) |
| `Components/Pages/WorkOrders/Details.razor` | Wire release approval |
| `Components/Pages/Quotes/Details.razor` | Wire quote approval |
| `Components/Pages/Quality/Ncr.razor` | Wire NCR disposition approval |

---

## Tasks

### 1. Wire WO release to workflow (H6.19)
**File**: `Components/Pages/WorkOrders/Details.razor`
- When user clicks "Release" and a workflow is defined for "WorkOrder.Release":
  - Instead of immediately changing status, call `WorkflowEngine.StartAsync()`
  - Show "Pending Approval" status badge
  - Show approval chain progress (who needs to approve, who has)
- When NO workflow is defined: proceed immediately (current behavior)

### 2. Wire quote approval to workflow (H6.20)
**File**: `Components/Pages/Quotes/Details.razor`
- When user clicks "Accept" and a workflow is defined for "Quote.Approve":
  - Start workflow approval chain
  - Show pending state
- When NO workflow: proceed immediately

### 3. Wire NCR disposition to workflow (H6.21)
**File**: `Components/Pages/Quality/Ncr.razor`
- When user changes NCR disposition and a workflow is defined for "NCR.Disposition":
  - Start workflow approval chain
- When NO workflow: proceed immediately

### 4. Build Admin Workflows page (H6.22)
**New File**: `Components/Pages/Admin/Workflows.razor`
**Route**: `/admin/workflows`

UI:
- List existing workflow definitions
- Create new workflow:
  - Entity Type dropdown: WorkOrder.Release, Quote.Approve, NCR.Disposition
  - Approval steps (ordered list):
    - Step 1: Role = "Manager", Any member can approve
    - Step 2: Role = "Admin", Specific user
  - Add/remove steps
- Edit existing workflows
- Delete with ConfirmDialog
- Add nav link in Admin section of NavMenu

### 5. Add pending approval display component
**New File**: `Components/Shared/ApprovalStatus.razor`

A reusable component that shows the current state of a workflow instance:
```razor
<ApprovalStatus EntityType="WorkOrder.Release" EntityId="@workOrderId" />
```

Displays:
- Current step name and required approver
- Completed steps with approver name + timestamp
- "Approve" / "Reject" buttons for current approver

---

## Implementation Notes

- The workflow engine should be opt-in: if `WorkflowEngine.HasWorkflow(entityType)`
  returns false, skip the approval flow entirely
- Store workflow instances (in-progress approvals) in a `WorkflowInstance` table
- Each approval step creates a `WorkflowStepResult` record
- When the final step is approved, the original action completes (status change)
- When any step is rejected, the workflow is cancelled and the user is notified

---

## Verification

1. Build passes
2. Without any workflows configured: WO release, Quote accept, NCR disposition
   all work exactly as before (no regression)
3. Create a "WorkOrder.Release" workflow with 2 approval steps
4. Release a WO → status shows "Pending Approval" instead of "Released"
5. Log in as the first approver → approve → advances to step 2
6. Log in as second approver → approve → WO status changes to "Released"
7. Admin → Workflows page lists all configured workflows with CRUD

---

## Files Modified (fill in after completion)

- `Services/IWorkflowEngine.cs` — Added `HasWorkflowAsync`, `GetAllDefinitionsAsync`, `SaveDefinitionAsync`, `DeleteDefinitionAsync`
- `Services/WorkflowEngine.cs` — Implemented all new interface methods (CRUD for definitions, HasWorkflow check)
- `Components/Shared/ApprovalStatus.razor` — NEW: Reusable component showing workflow step progress with Approve/Reject buttons
- `Components/Pages/WorkOrders/Details.razor` — Injected `IWorkflowEngine`, wired Release → workflow, added `ApprovalStatus` display + approval handlers
- `Components/Pages/Quotes/Details.razor` — Injected `IWorkflowEngine`, wired AcceptAndConvert → workflow, added `ApprovalStatus` display + approval handlers
- `Components/Pages/Quality/Ncr.razor` — Injected `IWorkflowEngine`, wired disposition change → workflow, added pending approval badge in table + approve/reject handlers
- `Components/Pages/Admin/Workflows.razor` — NEW: Full CRUD page for workflow definitions with step editor
- `Components/Layout/NavMenu.razor` — Added Workflows nav link in Admin section
- `Components/Pages/Admin/Index.razor` — Added Workflows card
