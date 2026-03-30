# Build Out Workflows Feature

## Context
The workflow/approval system has solid foundations (models, service, admin CRUD) but was abandoned before becoming fully usable. The admin can define workflows for WorkOrder/Quote/NCR but: users can't see pending approvals, the approval component hard-codes "System" as actor, entity type coverage is limited, and there's no seed data. Multiple entity types that should support workflows (CAPA, Parts, Maintenance, Shipping, Programs) have zero integration.

## Plan

### 1. Fix ApprovalStatus Component — Use Real User Context
**File:** `Components/Shared/ApprovalStatus.razor`
- Inject `AuthenticationStateProvider`, resolve current user ID and role from claims
- Replace hard-coded `"System"` with actual user ID on approve/reject
- Only show Approve/Reject buttons if current user's role matches the step's `AssignToRole`
- Show "Assigned to: {role}" label when user can't act

### 2. Expand Supported Entity Types
**File:** `Components/Pages/Admin/Workflows.razor` (entity type dropdown, ~line 88-93)
- Add entity types: CAPA, Part, Shipment, MachineProgram, MaintenanceWorkOrder
- These match real manufacturing approval needs

### 3. Create "My Approvals" Page
**New file:** `Components/Pages/Approvals/Index.razor`
- Route: `/approvals`
- Get current user's role, call `GetPendingForRoleAsync(role)`
- Display table: Entity Type, Entity ID/Name, Workflow Name, Current Step, Assigned Role, Started date
- Each row links to the relevant entity detail page
- Inline approve/reject with comment field
- Add nav link in NavMenu (visible to non-operator roles)

### 4. Ensure WO/Quote/NCR Integration is Complete
**Files:**
- `Components/Pages/WorkOrders/Details.razor` — Verify Release triggers workflow; gate Complete/Cancel behind workflow if defined
- `Components/Pages/Quotes/Details.razor` — Already triggers on Accept; also gate Draft→Sent if workflow defined
- `Components/Pages/Quality/Ncr.razor` — Already triggers on Disposition; verify it's fully working

### 5. Add New Entity Integrations
**Files:**
- `Components/Pages/Quality/Capa.razor` — Gate status transitions (especially closure) behind workflow if defined
- `Components/Pages/Parts/Detail.razor` or `Edit.razor` — Gate revision changes behind workflow if defined
- `Components/Pages/Maintenance/WorkOrders.razor` — Gate completion behind workflow if defined
- `Components/Pages/Programs/Views/ApprovalQueueView.razor` — Gate Draft→Released program transition
- `Components/Pages/ShopFloor/Partials/Shipping.razor` — Gate shipment release behind workflow if defined

Pattern for each: inject `IWorkflowEngine`, check `HasWorkflowAsync(entityType)`, call `StartAsync` if workflow exists, show `<ApprovalStatus>` when pending.

### 6. Add Workflow Seed Data
**File:** `Services/DataSeedingService.cs`
- Add `SeedWorkflowsAsync()` called from main seed method
- Create default workflows:
  - **"Work Order Release"** — EntityType: WorkOrder, 2 steps: Supervisor → Manager
  - **"Quote Approval"** — EntityType: Quote, 1 step: Manager
  - **"NCR Disposition"** — EntityType: NCR, 2 steps: Quality → Engineering
  - **"CAPA Closure"** — EntityType: CAPA, 2 steps: Quality → Manager
  - **"Program Release"** — EntityType: MachineProgram, 1 step: Engineering

### 7. Admin Workflows Page Polish
**File:** `Components/Pages/Admin/Workflows.razor`
- Add instance count badge per definition (active/completed)
- Show "Connected to: WO Details, Quote Details, ..." for each entity type

### 8. Add Admin Dashboard + Approvals Links to Sidebar Nav
**File:** `Components/Layout/NavMenu.razor`
- Add `/admin` link in the Settings section, visible to admin roles
- Add `/approvals` link visible to non-operator roles

## Files to Modify
- `Components/Shared/ApprovalStatus.razor` — auth fix, role gating
- `Components/Pages/Admin/Workflows.razor` — entity types, polish
- `Components/Pages/WorkOrders/Details.razor` — complete workflow integration
- `Components/Pages/Quotes/Details.razor` — verify/extend workflow integration
- `Components/Pages/Quality/Ncr.razor` — verify integration
- `Components/Pages/Quality/Capa.razor` — add workflow integration
- `Components/Pages/Parts/Detail.razor` or `Edit.razor` — add workflow integration
- `Components/Pages/Maintenance/WorkOrders.razor` — add workflow integration
- `Components/Pages/Programs/Views/ApprovalQueueView.razor` — add workflow integration
- `Components/Pages/ShopFloor/Partials/Shipping.razor` — add workflow integration
- `Services/DataSeedingService.cs` — seed workflows
- `Components/Layout/NavMenu.razor` — add approvals + admin links

## Files to Create
- `Components/Pages/Approvals/Index.razor` — My Approvals dashboard

## Verification
- `dotnet build --no-restore` — confirm no compile errors
- `dotnet test --no-restore` — all tests pass
- Seed data creates workflows visible in Admin > Workflows
- My Approvals page shows pending items for the logged-in user's role
- Entity pages show approval status and gate actions when workflows are defined
