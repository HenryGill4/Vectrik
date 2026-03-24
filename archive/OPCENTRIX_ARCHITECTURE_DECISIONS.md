> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# OpCentrix — Fresh Build Architecture Spec

> **PHASE TRACKER DEPRECATED**: The phase tracker in this file has been superseded
> by **`ROADMAP.md`** (project root). This file is retained as an architecture
> reference for decisions made during the initial build.

> **Last Updated**: 2026-03-12
> **Branch**: `New-Master-Parts`
> **Status**: Phases A-C complete. See `ROADMAP.md` for current roadmap.
> **Old Project**: `OpCentrix/` (legacy, do not modify)

---

## MASTER PROGRESS TRACKER

**READ THIS FIRST. Update after completing each step. Mark `[x]` when done.**

### Phase A: Scaffold + Multi-Tenant + Models (�11)
```
[x] A1.  Create project (dotnet new webapp)
[x] A2.  Add NuGet packages
[x] A3.  Create ManufacturingEnums.cs
[x] A4.  Create Platform models (Tenant, PlatformUser)
[x] A5.  Create PlatformDbContext.cs
[x] A6.  Create leaf models (User, Machine, Material, SystemSetting, etc.)
[x] A7.  Create stage + part models (ProductionStage, Part, PartStageRequirement)
[x] A8.  Create work order + quote models
[x] A9.  Create job + execution models (Job, StageExecution, JobNote)
[x] A10. Create build models (BuildJob, BuildPackage, BuildFileInfo)
[x] A11. Create tracking + QC models (PartInstance, QCInspection, DelayLog)
[x] A12. Create maintenance models
[x] A13. Create TenantDbContext.cs (~35 DbSets)
[x] A14. Create TenantDbContextFactory.cs
[x] A15. Create TenantMiddleware.cs
[x] A16. Create ITenantContext + TenantContext
[x] A17. Create Program.cs (DI, auth, middleware)
[x] A18. Build verified (0 errors)
```
**Phase A Status**: COMPLETE

### Phase B: Services (�11)
```
[x] B1.  ITenantService + TenantService
[x] B2.  IAuthService + AuthService
[x] B3.  IPartService + PartService
[x] B4.  IJobService + JobService
[x] B5.  IWorkOrderService + WorkOrderService
[x] B6.  IQuoteService + QuoteService
[x] B7.  IBuildService + BuildService
[x] B8.  IBuildPlanningService + BuildPlanningService
[x] B9.  IStageService + StageService
[x] B10. ISerialNumberService + SerialNumberService
[x] B11. IPartTrackerService + PartTrackerService
[x] B12. ILearningService + LearningService
[x] B13. IMaintenanceService + MaintenanceService
[x] B14. IAnalyticsService + AnalyticsService
[x] B15. IMaterialService + MaterialService
[x] B16. IDataSeedingService + DataSeedingService
[x] B17. Machine providers + SignalR hubs
[x] B18. Register all services in Program.cs DI
```
**Phase B Status**: COMPLETE

### Phase C: Pages + Navigation + PWA (�11)
```
[x] C1.  _Layout.cshtml (dynamic sidebar)
[x] C2.  PWA files (manifest, service worker, icons)
[x] C3.  Login + Logout pages
[x] C4.  Platform pages (Tenants, TenantSetup, Users)
[x] C5.  Dashboard (/)
[x] C6.  Scheduler (Gantt + scheduler.js)
[x] C7.  WorkOrders (Index + Details)
[x] C8.  Quotes (Index + Details)
[x] C9.  Builds (Build Planning)
[x] C10. ShopFloor/Stage.cshtml (dynamic route)
[x] C11. Shop Floor Partials (9 built-in + 1 generic)
[x] C12. dynamic-form-renderer.js
[x] C13. Part Tracker (/Tracking)
[x] C14. Analytics Dashboard
[x] C15. Machine Status (+ machine-state-client.js)
[x] C16. Maintenance pages (Dashboard, WorkOrders, Rules)
[x] C17. Admin pages (7 pages + _PartForm partial)
[x] C18. site.css (mobile-first, dark mode)
[x] C19. site.js (sidebar, dark mode, PWA)
[x] C20. Build + run + verify full flow
```
**Phase C Status**: COMPLETE

### Phase D: Tests + Retirement (�11)
```
[ ] D1. Create OpCentrix.Tests (xUnit)
[ ] D2. Model unit tests
[ ] D3. Service unit tests
[ ] D4. Integration tests
[ ] D5. Rename projects (v2 ? OpCentrix, old ? legacy)
```
**Phase D Status**: NOT STARTED

### How to Update This Tracker
- **AI assistants**: After completing a step, change `[ ]` to `[x]` for that step.
- **AI assistants**: Update the "Phase X Status" line to: `IN PROGRESS (at step AX)` or `COMPLETE`.
- **Humans**: Same � just edit the checkboxes in this file.
- **New session**: Read this tracker FIRST to know where to resume.

---

## 0. Historical Context (Why Fresh Build)

The old project (`OpCentrix/`) had 518 files, 71,700 lines of C#. Problems:
- Two conflicting `Part` models (650-line B&T firearms model + clean `MasterPart`)
- `Job.cs` was 579 lines with laser power, argon purity, hatch spacing baked in
- `Scheduler/Index.cshtml.cs` was 1,553 lines (God-page)
- `SchedulerContext.cs` was 1,715 lines with 84 tables
- No service interfaces, no separation of concerns, no consistent patterns
- 68% of the codebase was dead weight (B&T, CRM, unused features)
- Multi-tenancy, maintenance, quotes, serial tracking would be impossible to retrofit

**Decision**: Build fresh from this spec. Old project preserved as `OpCentrix-legacy/` for reference.

### What Carries Forward (designs only, no code copy)
- `MasterPart.cs` property list ? new `Part.cs`
- `ProductionStage.cs` model design (CustomFieldsConfig pattern)
- `PartStageRequirement.cs` model design (learning EMA pattern)
- Visual design (sidebar CSS, dark mode, Tailwind)
- Machine provider interfaces (`IMachineProvider`, `MockMachineProvider`)
- SignalR hub pattern (`MachineStateHub`)
- Maintenance model designs
- Cookie auth pattern

### Default Seed Data (recreated by DataSeedingService)

**Production Stages** (9 rows):

| Id | Name | Slug | Department | DefaultDurationHours | IsBatchStage |
|----|------|------|------------|---------------------|-------------|
| 1 | SLS/LPBF Printing | sls-printing | SLS | 8.0 | true |
| 2 | Depowdering | depowdering | SLS | 1.0 | true |
| 3 | Heat Treatment | heat-treatment | Post-Process | 4.0 | true |
| 4 | Wire EDM | wire-edm | EDM | 2.0 | false |
| 5 | CNC Machining | cnc-machining | Machining | 3.0 | false |
| 6 | Laser Engraving | laser-engraving | Engraving | 0.5 | false |
| 7 | Surface Finishing | surface-finishing | Finishing | 1.5 | true |
| 8 | Quality Control | qc | Quality | 0.5 | false |
| 9 | Shipping | shipping | Shipping | 0.5 | false |

**Machines** (5 rows):

| MachineId | Name | MachineType |
|-----------|------|-------------|
| TI1 | TruPrint 1000 #1 | SLS |
| TI2 | TruPrint 1000 #2 | SLS |
| INC1 | Incidental SLS | SLS |
| EDM1 | Wire EDM #1 | EDM |
| CNC1 | CNC Mill #1 | CNC |

---

## 1. Multi-Tenant Architecture (Baked In From Day One)

### Strategy: Separate SQLite DB Per Tenant (Option B)
- One deployed app instance
- Tenant resolved from `User.TenantId` stored in auth cookie claims
- DB file per tenant: `data/tenants/{tenant-code}.db` (e.g., `data/tenants/acme.db`)
- Super admin has its own DB: `data/platform.db` (tenants table, super admin users)
- Easy isolation, easy backup, easy onboarding, easy demo

### Tenant Resolution Flow
```
Login Screen ? Username + Password (no company code field)
  ??? User record has TenantId
  ??? Auth cookie includes TenantId claim
  ??? TenantDbContextFactory reads claim ? opens correct DB file
  ??? All requests scoped to that tenant's DB automatically
```

### Data Model
```csharp
// In platform.db (super admin only)
public class Tenant
{
    public int Id { get; set; }
    public string Code { get; set; }              // "acme" ? data/tenants/acme.db
    public string CompanyName { get; set; }        // "Acme Manufacturing"
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }      // Branding
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; }          // Super admin who created it
    public string? SubscriptionTier { get; set; }  // Future: Free/Pro/Enterprise
}

// Super admin user (in platform.db)
public class PlatformUser
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string Role { get; set; }               // "SuperAdmin"
}
```

### Role Hierarchy
```
SUPER ADMIN (platform-level)
  ??? Can create/deactivate tenants
  ??? Can log into ANY tenant as admin
  ??? Can configure stages, machines, materials on behalf of any tenant
  ??? Can view cross-tenant analytics (future)
  ??? Lives in platform.db

TENANT ADMIN (per-tenant)
  ??? Full control of their company's data
  ??? Can create users, roles, stages, machines, materials
  ??? Can configure custom stage forms
  ??? Cannot see other tenants
  ??? Lives in tenant's DB

MANAGER / SUPERVISOR / SCHEDULER / OPERATOR / SPECIALIST
  ??? Standard MES roles within a tenant
  ??? Role-based page visibility (operators see only assigned stages)
  ??? Lives in tenant's DB
```

### Implementation
```csharp
// Middleware resolves tenant from auth cookie
public class TenantMiddleware
{
    // Reads TenantId from ClaimsPrincipal
    // Sets ITenantContext.Current = tenant info
    // DbContext factory uses tenant code to open correct .db file
}

// Scoped per-request
public interface ITenantContext
{
    string TenantCode { get; }
    string CompanyName { get; }
    bool IsSuperAdmin { get; }
}

// DbContext factory
public class TenantDbContextFactory
{
    // If super admin impersonating ? use target tenant's DB
    // If normal user ? use their TenantId from claims
    // Connection string: Data Source=data/tenants/{code}.db
}
```

### Super Admin Pages
```
/Platform/Tenants          ? List/create/deactivate tenants
/Platform/Tenants/Setup    ? Configure a new tenant (stages, machines, seed data)
/Platform/Users            ? Manage super admin accounts
/Platform/Impersonate      ? Log into any tenant as admin
```

---

## 2. Dynamic Stage System (Custom Stages + Operator Forms)

### Core Principle
Every manufacturing stage � both built-in and custom � is a `ProductionStage` record in the DB. The shop floor uses **one generic page** (`/ShopFloor/Stage/{stageId}`) that renders dynamically based on the stage config.

### Built-In Stages (Pre-Built Rich Pages)
These stages have dedicated, polished pages with stage-specific UI elements. They are seeded by default but can be renamed, reordered, or deactivated by the tenant admin.

| Stage | Route | Special UI Elements |
|-------|-------|-------------------|
| SLS/LPBF Printing | `/ShopFloor/Stage/sls-printing` | Live machine telemetry, SignalR, build file info, powder tracking |
| Depowdering | `/ShopFloor/Stage/depowdering` | Batch processing, powder recovery tracking |
| Heat Treatment | `/ShopFloor/Stage/heat-treatment` | Oven load tracking, temperature profiles |
| Wire EDM | `/ShopFloor/Stage/wire-edm` | Per-part cutting, wire usage |
| CNC Machining | `/ShopFloor/Stage/cnc-machining` | Per-part ops, tool tracking |
| Laser Engraving | `/ShopFloor/Stage/laser-engraving` | Serial number assignment + engraving |
| Surface Finishing | `/ShopFloor/Stage/surface-finishing` | Batch processing, finish type |
| Quality Control | `/ShopFloor/Stage/qc` | Inspection checklists, pass/fail, measurements |
| Shipping | `/ShopFloor/Stage/shipping` | Packing list, carrier, tracking number, WO fulfillment |

### How It Works
```
ProductionStage record in DB:
  ??? StageSlug: "sls-printing" (URL-safe identifier)
  ??? HasBuiltInPage: true (use the rich page template)
  ??? CustomFieldsConfig: JSON (operator form fields)
  ??? StageColor, StageIcon, Department
  ??? IsBatchStage: true/false
  ??? RequiresMachineAssignment: true/false
  ??? RequiresSerialNumber: false (only laser-engraving has this true)

Route resolution:
  /ShopFloor/Stage/{slug}
    ? If HasBuiltInPage && matching Razor partial exists ? render rich template
    ? Else ? render generic template with custom form fields from CustomFieldsConfig
```

### Custom Stage Example
A tenant admin creates "Anodizing" stage:
```json
{
  "name": "Anodizing",
  "slug": "anodizing",
  "hasBuiltInPage": false,
  "isBatchStage": true,
  "customFieldsConfig": [
    {"name": "anodizeType", "type": "dropdown", "label": "Anodize Type", "required": true, "options": ["Type II", "Type III", "Hard Coat"]},
    {"name": "color", "type": "dropdown", "label": "Color", "options": ["Clear", "Black", "Red", "Blue", "Gold"]},
    {"name": "bathTemperature", "type": "number", "label": "Bath Temperature (�F)", "min": 60, "max": 75},
    {"name": "immersionTime", "type": "number", "label": "Immersion Time (min)", "min": 1, "max": 120},
    {"name": "passedVisualInspection", "type": "checkbox", "label": "Passed Visual Inspection"}
  ]
}
```

This automatically:
1. Appears in the sidebar navigation under Shop Floor
2. Creates a shop floor page at `/ShopFloor/Stage/anodizing`
3. Shows operator queue (parts waiting for this stage)
4. Renders the custom form with the 5 fields above
5. Records form data in `StageExecution.CustomFieldValues` (JSON)
6. Updates part tracker when operator completes the stage
7. Connects to the schedule (stage has estimated duration)

### Dynamic Navigation
The sidebar's "Shop Floor" section is rendered from DB:
```csharp
// In _Layout.cshtml
@foreach (var stage in ViewData["ActiveStages"] as List<ProductionStage>)
{
    <a href="/ShopFloor/Stage/@stage.StageSlug" class="nav-subitem">
        <i class="@stage.StageIcon" style="color: @stage.StageColor"></i>
        <span>@stage.Name</span>
    </a>
}
```

---

## 3. Serial Number Tracking

### When Serial Numbers Are Assigned
Serial numbers are assigned at the **Laser Engraving** stage. Before that, parts are tracked as batch quantities. After engraving, each physical part has a unique identity.

### Data Model
```csharp
public class PartInstance
{
    public int Id { get; set; }
    public string SerialNumber { get; set; }       // e.g., "SN-2026-00001"
    public int WorkOrderLineId { get; set; }       // Which WO line this part fulfills
    public int PartId { get; set; }                // Which Part definition
    public int? CurrentStageId { get; set; }       // Current ProductionStage
    public PartInstanceStatus Status { get; set; } // InProcess, Passed, Failed, Scrapped, Shipped

    // Stage tracking (where has this serial number been?)
    public virtual ICollection<PartInstanceStageLog> StageLogs { get; set; }

    // QC results for this specific serial number
    public virtual ICollection<QCInspection> Inspections { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; }      // When serial was assigned (at engraving)
    public string CreatedBy { get; set; }           // Operator who engraved
    public DateTime LastModifiedDate { get; set; }
}

public class PartInstanceStageLog
{
    public int Id { get; set; }
    public int PartInstanceId { get; set; }
    public int ProductionStageId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string OperatorName { get; set; }
    public string? CustomFieldValues { get; set; }  // JSON from operator form
    public string? Notes { get; set; }
}
```

### Pre-Engraving vs Post-Engraving Tracking
```
BEFORE Laser Engraving: Batch tracking
  "20 parts at Depowdering" � no individual identity

AT Laser Engraving: Serial numbers assigned
  Operator enters/scans serial numbers for each part
  PartInstance records created

AFTER Laser Engraving: Individual tracking
  Each serial number tracked through remaining stages
  QC inspections linked to specific serial numbers
  Shipping packing lists reference serial numbers
```

---

## 4. Quotes & Cost Tracking

### Quote ? Work Order Flow
```
QUOTE (pre-sales)
  ??? Customer requests quote
  ??? Planner estimates: stages � hours � rates = cost
  ??? Quote sent to customer
  ??? Status: Draft ? Sent ? Accepted ? Rejected ? Expired

ACCEPTED QUOTE ? WORK ORDER
  ??? Auto-converts to Work Order with lines
  ??? Quote.Id linked to WorkOrder.QuoteId
  ??? Work order enters production planning
```

### Data Model
```csharp
public class Quote
{
    public int Id { get; set; }
    public string QuoteNumber { get; set; }         // Q-2026-0001
    public string CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public QuoteStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public decimal? QuotedPrice { get; set; }        // What we charge
    public decimal? Markup { get; set; }              // Margin
    public string? Notes { get; set; }
    public int? ConvertedWorkOrderId { get; set; }

    public virtual ICollection<QuoteLine> Lines { get; set; }
    public virtual WorkOrder? ConvertedWorkOrder { get; set; }
}

public class QuoteLine
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public int PartId { get; set; }
    public int Quantity { get; set; }
    public decimal EstimatedCostPerPart { get; set; }  // Sum of all stage costs
    public decimal QuotedPricePerPart { get; set; }
    public string? Notes { get; set; }

    public virtual Quote Quote { get; set; }
    public virtual Part Part { get; set; }
}
```

### Cost Tracking (Runtime)
Costs are calculated in `AnalyticsService`, not stored on Job:
```
Per-stage cost = (ActualHours � Machine.HourlyRate) + (Material.CostPerKg � UsageKg) + Setup
Per-job cost = Sum of all stage costs
Per-WO cost = Sum of all job costs for that work order
Margin = QuotedPrice - ActualCost
```

---

## 5. Maintenance System

### Model Summary (from old project designs, cleaned up)
```
Machine (existing)
  ??? MachineComponent[]       � Recoater Arm, Filter, Sieve Station, etc.
       ??? MaintenanceRule[]   � "Replace filter every 500 hours"
            ??? TriggerType: HoursRun | BuildsCompleted | DateInterval | CustomMeter
            ??? ThresholdValue: 500 (hours)
            ??? Severity: Info | Warning | Critical
            ??? EarlyWarningPercent: 80 (alert at 400 hours)

MaintenanceWorkOrder
  ??? MachineId, ComponentId, RuleId (links)
  ??? Type: Preventive | Corrective | Emergency | Inspection | Calibration
  ??? Status: Open ? Assigned ? InProgress ? Completed
  ??? AssignedTechnicianUserId
  ??? EstimatedHours, ActualHours
  ??? EstimatedCost, ActualCost
  ??? RequiresShutdown (blocks scheduling)
  ??? PartsUsed, WorkPerformed, Notes

MaintenanceActionLog
  ??? RuleId, MachineId, ComponentId
  ??? Action: "Performed maintenance", "Reset counter"
  ??? PerformedBy, PerformedAt
  ??? Notes
```

### Pages
```
/Maintenance                  ? Dashboard: upcoming PM, overdue items, active work orders
/Maintenance/WorkOrders       ? CRUD for maintenance work orders
/Maintenance/Rules            ? Configure maintenance rules per machine/component
/Maintenance/History          ? Historical log of all maintenance actions
```

### Integration with Scheduler
- When a maintenance work order has `RequiresShutdown = true`, the machine is blocked from scheduling during the maintenance window.
- Dashboard shows maintenance warnings alongside production KPIs.
- Machine status page shows maintenance status per machine.

---

## 6. Build File Integration (Phase 1: Debug Spoof Data)

### Concept
The Build Planning page needs build file data (layer count, build height, estimated print time, part nesting layout). In production, this comes from slicer software (Materialise Magics, Netfabb, etc.). For now, we use spoof data.

### Implementation
```
/Builds page includes:
  ??? BuildPackage CRUD (which parts go on which build plate)
  ??? Build File Info panel (read-only display):
  ?     ??? File name, layer count, build height, estimated time
  ?     ??? Part positions (X,Y on plate � future: visual preview)
  ?     ??? Estimated powder usage
  ??? Debug Form (hidden via SystemSetting "ShowDebugBuildForm"):
  ?     ??? Manual entry for all build file fields
  ?     ??? "Generate Spoof Data" button auto-fills realistic values
  ??? Future: API endpoint for slicer integration
       ??? POST /api/builds/{id}/build-file ? receives slicer export data
```

### Data Model
```csharp
public class BuildFileInfo
{
    public int Id { get; set; }
    public int BuildPackageId { get; set; }        // FK to BuildPackage
    public string? FileName { get; set; }
    public int? LayerCount { get; set; }
    public decimal? BuildHeightMm { get; set; }
    public decimal? EstimatedPrintTimeHours { get; set; }
    public decimal? EstimatedPowderKg { get; set; }
    public string? PartPositionsJson { get; set; }  // [{partId, x, y, rotation}]
    public string? SlicerSoftware { get; set; }     // "Materialise Magics", "Netfabb", "Manual"
    public string? SlicerVersion { get; set; }
    public DateTime ImportedDate { get; set; }
    public string ImportedBy { get; set; }
}
```

---

## 7. Project Structure (FINAL)

```
OpCentrix/
??? Data/
?   ??? TenantDbContext.cs                         # Per-tenant DB context
?   ??? PlatformDbContext.cs                       # Platform/super admin DB context
?   ??? TenantDbContextFactory.cs                  # Resolves tenant ? opens correct DB
??? Middleware/
?   ??? TenantMiddleware.cs                        # Resolves tenant from auth claims
??? Models/
?   ??? Enums/
?   ?   ??? ManufacturingEnums.cs                  # All enums in one file
?   ??? Platform/
?   ?   ??? Tenant.cs                              # Tenant definition (platform.db)
?   ?   ??? PlatformUser.cs                        # Super admin users (platform.db)
?   ??? Part.cs                                    # From MasterPart.cs (renamed)
?   ??? Job.cs                                     # REWRITTEN (~30 stored props)
?   ??? WorkOrder.cs                               # WorkOrder + WorkOrderLine
?   ??? Quote.cs                                   # Quote + QuoteLine
?   ??? ProductionStage.cs                         # From old project + StageSlug, HasBuiltInPage
?   ??? PartStageRequirement.cs                    # Per-part stage config + learning EMA
?   ??? PartInstance.cs                             # Serial number tracking (post-engraving)
?   ??? PartInstanceStageLog.cs                    # Per-serial stage history
?   ??? Machine.cs                                 # Slim machine model
?   ??? Material.cs                                # Material definitions
?   ??? User.cs                                    # Tenant user + roles
?   ??? BuildJob.cs                                # REWRITTEN (~25 stored props)
?   ??? BuildJobPart.cs                            # Parts in a build
?   ??? BuildPackage.cs                            # Build planning packages
?   ??? BuildFileInfo.cs                           # Slicer data (spoof for now)
?   ??? JobStage.cs                                # Per-job stage tracking
?   ??? StageExecution.cs                          # Actual hours + custom field values
?   ??? DelayLog.cs                                # Delay records
?   ??? QCInspection.cs                            # QC + checklists
?   ??? JobNote.cs                                 # Notes per job
?   ??? OperatingShift.cs                          # Shift definitions
?   ??? SystemSetting.cs                           # Key-value tenant config
?   ??? MachineStateRecord.cs                      # Telemetry snapshots
?   ??? MachineConnectionSettings.cs               # OPC UA config
?   ??? Maintenance/
?       ??? MachineComponent.cs                    # From old project
?       ??? MaintenanceRule.cs                     # From old project
?       ??? MaintenanceWorkOrder.cs                # From old project
?       ??? MaintenanceActionLog.cs                # From old project
??? Services/
?   ??? Platform/
?   ?   ??? ITenantService.cs + TenantService.cs                # Tenant CRUD + setup
?   ?   ??? ITenantContext.cs + TenantContext.cs                 # Request-scoped tenant info
?   ??? Auth/
?   ?   ??? IAuthService.cs + AuthService.cs                    # Login, logout, password hash
?   ??? IPartService.cs + PartService.cs                        # Part CRUD + stacking
?   ??? IJobService.cs + JobService.cs                          # Job CRUD + scheduling
?   ??? IWorkOrderService.cs + WorkOrderService.cs              # WO lifecycle
?   ??? IQuoteService.cs + QuoteService.cs                      # Quote CRUD + convert-to-WO
?   ??? IBuildService.cs + BuildService.cs                      # Build tracking
?   ??? IBuildPlanningService.cs + BuildPlanningService.cs       # Build packages + file info
?   ??? IStageService.cs + StageService.cs                      # Stage progression + custom forms
?   ??? IPartTrackerService.cs + PartTrackerService.cs          # Where is my part?
?   ??? ISerialNumberService.cs + SerialNumberService.cs         # Serial assignment + lookup
?   ??? IAnalyticsService.cs + AnalyticsService.cs              # KPIs + cost calc
?   ??? ILearningService.cs + LearningService.cs                # EMA duration learning
?   ??? IMaintenanceService.cs + MaintenanceService.cs          # PM rules + work orders
?   ??? IDataSeedingService.cs + DataSeedingService.cs          # Tenant seed data
?   ??? IMaterialService.cs + MaterialService.cs                 # Material CRUD + compatibility
?   ??? MachineProviders/
?       ??? IMachineProvider.cs                                  # From old project
?       ??? MockMachineProvider.cs                               # From old project
?       ??? MachineProviderFactory.cs                            # From old project
?       ??? MachineSyncService.cs                                # From old project
??? Hubs/
?   ??? MachineStateHub.cs
?   ??? IMachineStateNotifier.cs + MachineStateNotifier.cs
??? Pages/
?   ??? Index.cshtml(.cs)                                        # Dashboard
?   ??? manifest.json                                            # PWA manifest
?   ??? service-worker.js                                        # PWA offline shell
?   ??? Account/
?   ?   ??? Login.cshtml(.cs)
?   ?   ??? Logout.cshtml(.cs)
?   ??? Platform/                                                # Super admin only
?   ?   ??? Tenants.cshtml(.cs)                                  # Tenant CRUD
?   ?   ??? TenantSetup.cshtml(.cs)                              # Configure new tenant
?   ?   ??? Users.cshtml(.cs)                                    # Super admin users
?   ??? Scheduler/
?   ?   ??? Index.cshtml(.cs)                                    # Gantt scheduler
?   ??? WorkOrders/
?   ?   ??? Index.cshtml(.cs)                                    # WO list + CRUD
?   ?   ??? Details.cshtml(.cs)                                  # WO detail + lines + tracker
?   ??? Quotes/
?   ?   ??? Index.cshtml(.cs)                                    # Quote list
?   ?   ??? Details.cshtml(.cs)                                  # Quote detail + convert-to-WO
?   ??? Builds/
?   ?   ??? Index.cshtml(.cs)                                    # Build planning + file info
?   ??? ShopFloor/
?   ?   ??? Stage.cshtml(.cs)                                    # DYNAMIC: /ShopFloor/Stage/{slug}
?   ?   ??? Partials/                                            # Built-in stage templates
?   ?       ??? _SLSPrinting.cshtml                              # Rich SLS template
?   ?       ??? _Depowdering.cshtml                              # Rich depowder template
?   ?       ??? _HeatTreatment.cshtml                            # Rich heat treat template
?   ?       ??? _WireEDM.cshtml                                  # Rich EDM template
?   ?       ??? _CNCMachining.cshtml                             # Rich CNC template
?   ?       ??? _LaserEngraving.cshtml                           # Serial number assignment
?   ?       ??? _SurfaceFinishing.cshtml                         # Rich finishing template
?   ?       ??? _QualityControl.cshtml                           # Inspection checklists
?   ?       ??? _Shipping.cshtml                                 # Packing + fulfillment
?   ?       ??? _GenericStage.cshtml                             # Custom stage fallback
?   ??? Tracking/
?   ?   ??? Index.cshtml(.cs)                                    # Part tracker
?   ??? Analytics/
?   ?   ??? Index.cshtml(.cs)                                    # Analytics dashboard
?   ??? Machines/
?   ?   ??? Index.cshtml(.cs)                                    # Live machine status
?   ??? Maintenance/
?   ?   ??? Index.cshtml(.cs)                                    # Maintenance dashboard
?   ?   ??? WorkOrders.cshtml(.cs)                               # Maintenance WO CRUD
?   ?   ??? Rules.cshtml(.cs)                                    # PM rule config
?   ??? Admin/
?   ?   ??? Index.cshtml(.cs)                                    # Admin hub
?   ?   ??? Parts.cshtml(.cs)                                    # Part CRUD
?   ?   ??? ProductionStages.cshtml(.cs)                         # Stage config + custom forms
?   ?   ??? Machines.cshtml(.cs)                                 # Machine CRUD + components
?   ?   ??? Materials.cshtml(.cs)                                # Material CRUD
?   ?   ??? Users.cshtml(.cs)                                    # User/role management
?   ?   ??? Settings.cshtml(.cs)                                 # System settings
?   ?   ??? Shared/
?   ?       ??? _PartForm.cshtml
?   ??? Shared/
?   ?   ??? _Layout.cshtml                                       # Dynamic sidebar
?   ?   ??? _ValidationScriptsPartial.cshtml
?   ??? _ViewImports.cshtml
??? wwwroot/
?   ??? css/site.css
?   ??? js/
?   ?   ??? site.js                                              # Sidebar, dark mode, PWA
?   ?   ??? scheduler.js                                         # Gantt rendering
?   ?   ??? partForm.js                                          # Part form dynamics
?   ?   ??? dynamic-form-renderer.js                             # Renders CustomFieldsConfig forms
?   ?   ??? machine-state-client.js                              # SignalR client
?   ??? icons/                                                   # PWA icons (192, 512)
?   ??? lib/ (jQuery, Bootstrap)
??? Program.cs
??? appsettings.json
```

---

## 7A. Complete Model Specs (Implementation Reference)

This section contains every property for every model. AI assistants MUST use these specs when creating models � do not guess, do not read old project code.

### Enums (Models/Enums/ManufacturingEnums.cs)

```csharp
namespace OpCentrix.Models.Enums;

public enum JobStatus
{
    Draft, Scheduled, InProgress, Paused, Completed, Cancelled
}

public enum JobPriority
{
    Low, Normal, High, Rush, Emergency
}

public enum WorkOrderStatus
{
    Draft, Released, InProgress, Complete, Cancelled, OnHold
}

public enum QuoteStatus
{
    Draft, Sent, Accepted, Rejected, Expired
}

public enum BuildJobStatus
{
    Pending, Preheating, Building, Cooling, Completed, Failed, Cancelled
}

public enum BuildPackageStatus
{
    Draft, Ready, Scheduled, InProgress, Completed, Cancelled
}

public enum StageExecutionStatus
{
    NotStarted, InProgress, Completed, Skipped, Failed
}

public enum PartInstanceStatus
{
    InProcess, Passed, Failed, Scrapped, Shipped
}

public enum MaintenanceTriggerType
{
    HoursRun, BuildsCompleted, DateInterval, CustomMeter
}

public enum MaintenanceSeverity
{
    Info, Warning, Critical
}

public enum MaintenanceWorkOrderType
{
    Preventive, Corrective, Emergency, Inspection, Calibration, Upgrade
}

public enum MaintenanceWorkOrderPriority
{
    Low, Normal, High, Critical, Emergency
}

public enum MaintenanceWorkOrderStatus
{
    Open, Assigned, InProgress, Completed, Cancelled, OnHold, WaitingForParts
}

public enum MachineStatus
{
    Idle, Running, Building, Preheating, Cooling, Maintenance, Error, Offline, Setup
}
```

### Platform Models (in platform.db � see �1 for full specs)

**Tenant.cs** and **PlatformUser.cs** � already fully defined in �1.

### Core Tenant Models (in tenant DB)

#### User.cs
```
User
  Id (int, PK)
  Username (string, 50, required, unique per tenant)
  FullName (string, 100, required)
  Email (string, 100, required)
  PasswordHash (string, required)
  Role (string, 50, required)               // "Admin", "Manager", "Scheduler", "Operator", etc.
  Department (string, 100)
  IsActive (bool, default true)
  AssignedStageIds (string?, 500)            // NEW: comma-separated stage IDs this user can access
  CreatedDate (DateTime)
  LastLoginDate (DateTime?)
  LastModifiedDate (DateTime)
  CreatedBy (string, 100)
  LastModifiedBy (string, 100)
  Nav: Settings ? UserSettings?
```

**Roles**: Admin, Manager, Supervisor, Scheduler, Operator, MaintenanceTech, QCSpecialist, Analyst.
Operators see only stages listed in `AssignedStageIds`. All other roles see all stages.

#### UserSettings.cs
```
UserSettings
  Id (int, PK)
  UserId (int, FK ? User, unique)
  Theme (string, 20, default "dark")
  DashboardLayout (string?, TEXT)            // JSON for custom dashboard widget positions
  DefaultView (string?, 50)                  // Which page to land on after login
  NotificationsEnabled (bool, default true)
  Nav: User ? User
```

#### Part.cs (from old MasterPart.cs � rename class, keep all properties)
```
Part
  Id (int, PK)
  PartNumber (string, 50, required)
  Name (string, 200, required)
  Description (string, 500)
  Material (string, 100, required, default "Ti-6Al-4V Grade 5")
  ManufacturingApproach (string, 100, required, default "SLS-Based")

  // SLS Stacking
  AllowStacking (bool, default false)
  SingleStackDurationHours (double?, range 0.1-500)
  DoubleStackDurationHours (double?, range 0.1-500)
  TripleStackDurationHours (double?, range 0.1-500)
  MaxStackCount (int, range 1-10, default 1)
  PartsPerBuildSingle (int, required, range 1-100, default 1)
  PartsPerBuildDouble (int?, range 1-100)
  PartsPerBuildTriple (int?, range 1-100)
  EnableDoubleStack (bool, default false)
  EnableTripleStack (bool, default false)
  StageEstimateSingle (double?, range 0.1-500)

  // Batch Stage Durations
  SlsBuildDurationHours (double?, range 0.1-500)
  SlsPartsPerBuild (int?, range 1-100)
  DepowderingDurationHours (double?, range 0.1-100)
  DepowderingPartsPerBatch (int?, range 1-100)
  HeatTreatmentDurationHours (double?, range 0.1-100)
  HeatTreatmentPartsPerBatch (int?, range 1-100)
  WireEdmDurationHours (double?, range 0.1-100)
  WireEdmPartsPerSession (int?, range 1-100)

  // Stage Config
  RequiredStages (string, 1000, required, default "[]")   // JSON array of stage IDs

  // Status + Audit
  IsActive (bool, default true)
  CreatedDate, LastModifiedDate (DateTime)
  CreatedBy, LastModifiedBy (string, 100, required)

  Nav: StageRequirements ? PartStageRequirement[], Jobs ? Job[]

  // NotMapped computed props (copy from old MasterPart.cs):
  SlsPerPartHours, DepowderingPerPartHours, HeatTreatmentPerPartHours, WireEdmPerPartHours
  HasStackingConfiguration, EffectiveSingleDuration, HasValidDoubleStack, HasValidTripleStack
  AvailableStackLevels, GetStackDuration(level), GetPartsPerBuild(level)
  ValidateStackingConfiguration(), GetRecommendedStackLevel(qty), CalculateStackEfficiency(level, qty)
```

#### ProductionStage.cs (from old project + NEW properties)
```
ProductionStage
  Id (int, PK)
  Name (string, 100, required)
  StageSlug (string, 50, required, unique)            // NEW: URL-safe identifier "sls-printing"
  HasBuiltInPage (bool, default false)                 // NEW: true = use rich partial template
  RequiresSerialNumber (bool, default false)            // NEW: true only for laser-engraving
  DisplayOrder (int, required)
  Description (string?, 500)
  DefaultSetupMinutes (int, default 30)
  DefaultHourlyRate (decimal(8,2), default 85.00)
  RequiresQualityCheck (bool, default true)
  RequiresApproval (bool, default false)
  AllowSkip (bool, default false)
  IsOptional (bool, default false)
  RequiredRole (string?, 50)
  CustomFieldsConfig (TEXT, default "[]")              // JSON: [{name, type, label, required, options, min, max}]
  AssignedMachineIds (string?, 500)                    // Comma-separated
  RequiresMachineAssignment (bool, default false)
  DefaultMachineId (string?, 50)
  StageColor (string, 7, default "#007bff")
  StageIcon (string, 50, default "fas fa-cogs")
  Department (string?, 100)
  AllowParallelExecution (bool, default false)
  DefaultMaterialCost (decimal(10,2), default 0.00)
  DefaultDurationHours (double, default 1.0)
  IsBatchStage (bool, default false)
  IsActive (bool, default true)
  CreatedDate, LastModifiedDate (DateTime)
  CreatedBy, LastModifiedBy (string, 100)

  Nav: PartStageRequirements ? PartStageRequirement[], StageExecutions ? StageExecution[]

  Helper methods (carry from old project):
  GetCustomFields(), SetCustomFields(), GetAssignedMachineIds(), SetAssignedMachineIds()
  CanMachineExecuteStage(machineId), GetTotalEstimatedCost()
```

**NOTE**: `CustomFieldDefinition` record stays co-located in this file (same as old project).

#### PartStageRequirement.cs (from old project � Stage Duration Learning)

This model replaces the old `StageDefinition.cs` reference in the spec. There is NO separate `StageDefinition` model. `PartStageRequirement` IS the per-part stage configuration.

```
PartStageRequirement
  Id (int, PK)
  PartId (int, FK ? Part, required)
  ProductionStageId (int, FK ? ProductionStage, required)
  ExecutionOrder (int, required, range 1-100, default 1)
  IsRequired (bool, default true)
  IsActive (bool, default true)
  AllowParallelExecution (bool, default false)
  IsBlocking (bool, default true)

  // Timing & Cost Overrides
  EstimatedHours (double?)
  SetupTimeMinutes (int?)
  HourlyRateOverride (decimal(8,2)?)
  EstimatedCost (decimal(10,2), default 0)
  MaterialCost (decimal(10,2), default 0)

  // Machine Assignment
  AssignedMachineId (string?, 50)
  RequiresSpecificMachine (bool, default false)
  PreferredMachineIds (string?, 200)

  // Custom Field Values (per-part defaults for this stage's form)
  CustomFieldValues (TEXT, default "{}")

  // Process Config
  StageParameters (TEXT, default "{}")
  RequiredMaterials (TEXT, default "[]")
  RequiredTooling (string, 500, default "")
  QualityRequirements (TEXT, default "{}")

  // Notes
  SpecialInstructions (TEXT, default "")
  RequirementNotes (TEXT, default "")

  // Learning (EMA)
  ActualAverageDurationHours (double?)
  ActualSampleCount (int, default 0)
  LastActualDurationHours (double?)
  EstimateSource (string, 20, default "Manual")     // "Manual" | "Auto" | "Default"
  EstimateLastUpdated (DateTime?)

  // Audit
  CreatedDate, LastModifiedDate (DateTime)
  CreatedBy, LastModifiedBy (string, 100, required)

  Nav: Part ? Part, ProductionStage ? ProductionStage

  Helper methods (carry from old project):
  GetEffectiveEstimatedHours(), GetEffectiveHourlyRate(), CalculateTotalEstimatedCost()
  GetCustomFieldValues(), SetCustomFieldValues(), GetCustomFieldValue<T>(), SetCustomFieldValue()
  GetPreferredMachineIds(), SetPreferredMachineIds(), CanMachineExecute(), GetBestMachineId()
  GetDependencies()
```

#### Machine.cs (SLIM � rewritten)
```
Machine
  Id (int, PK)
  MachineId (string, 50, required, unique)           // "TI1", "EDM1"
  Name (string, 100, required)                        // "TruPrint 1000 #1"
  MachineType (string, 50, required, default "SLS")   // "SLS", "EDM", "CNC"
  MachineModel (string, 100)                          // "TruPrint 3000"
  SerialNumber (string?, 50)
  Location (string?, 100)
  Department (string?, 50)
  Status (MachineStatus, default Idle)
  IsActive (bool, default true)
  IsAvailableForScheduling (bool, default true)
  Priority (int, range 1-10, default 5)
  SupportedMaterials (string?, 1000)                  // Comma-separated
  CurrentMaterial (string?, 100)
  MaintenanceIntervalHours (double, default 500)
  HoursSinceLastMaintenance (double, default 0)
  LastMaintenanceDate (DateTime?)
  NextMaintenanceDate (DateTime?)
  TotalOperatingHours (double, default 0)

  // SLS-specific (only populated for SLS machines)
  BuildLengthMm (double, default 250)
  BuildWidthMm (double, default 250)
  BuildHeightMm (double, default 300)
  MaxLaserPowerWatts (double, default 400)

  // OPC UA
  OpcUaEndpointUrl (string?, 200)
  OpcUaEnabled (bool, default false)

  // Hourly rate for cost calculations
  HourlyRate (decimal(8,2), default 150.00)

  // Audit
  CreatedDate, LastModifiedDate (DateTime)
  CreatedBy, LastModifiedBy (string, 100, required)

  Nav: Components ? MachineComponent[], CurrentJob ? Job?
```

#### Material.cs
```
Material
  Id (int, PK)
  Name (string, 100, required)                        // "Ti-6Al-4V Grade 5"
  Category (string, 50, default "Metal Powder")       // "Metal Powder", "Wire", "Gas"
  Density (double?)                                    // g/cm�
  CostPerKg (decimal(10,2), default 0)
  Supplier (string?, 200)
  IsActive (bool, default true)
  CompatibleMaterials (string?, 500)                   // Comma-separated material IDs that can share machines
  Notes (string?, 1000)
  CreatedDate, LastModifiedDate (DateTime)
  CreatedBy, LastModifiedBy (string, 100)
```

#### Job.cs (REWRITTEN � ~30 stored properties)
```
Job
  Id (int, PK)
  PartId (int, FK ? Part)
  MachineId (string, 50)                               // FK ? Machine.MachineId
  WorkOrderLineId (int?, FK ? WorkOrderLine)

  // Scheduling
  ScheduledStart (DateTime)
  ScheduledEnd (DateTime)
  ActualStart (DateTime?)
  ActualEnd (DateTime?)

  // Production
  PartNumber (string, 50)                              // Denormalized for grid display
  Quantity (int)
  ProducedQuantity (int, default 0)
  DefectQuantity (int, default 0)
  EstimatedHours (double)
  SlsMaterial (string?, 100)

  // Stacking
  StackLevel (byte?, range 1-3)
  PartsPerBuild (int?)
  PlannedStackDurationHours (double?)

  // Workflow
  Status (JobStatus, default Draft)
  Priority (JobPriority, default Normal)
  Notes (string?)

  // Predecessor chain
  PredecessorJobId (int?, FK ? Job)
  UpstreamGapHours (double?)

  // Operator
  OperatorUserId (int?, FK ? User)
  LastStatusChangeUtc (DateTime?)

  // Audit
  CreatedDate, LastModifiedDate (DateTime)
  CreatedBy, LastModifiedBy (string, 100)

  Nav: Part ? Part, Machine ? Machine (via MachineId string match),
       PredecessorJob ? Job?, OperatorUser ? User?,
       WorkOrderLine ? WorkOrderLine?,
       Stages ? StageExecution[], Notes ? JobNote[]

  NotMapped: ScheduledDuration, DurationHours, IsOverdue
```

#### WorkOrder.cs + WorkOrderLine.cs
```
WorkOrder
  Id (int, PK)
  OrderNumber (string, 50, required)                   // "WO-2026-0001"
  CustomerName (string, 200, required)
  CustomerPO (string?, 100)                            // Customer purchase order number
  CustomerEmail (string?, 200)
  CustomerPhone (string?, 50)
  OrderDate (DateTime)
  DueDate (DateTime)
  Status (WorkOrderStatus, default Draft)
  Priority (JobPriority, default Normal)
  QuoteId (int?, FK ? Quote)                           // Linked quote if converted
  Notes (string?, TEXT)
  CreatedDate, LastModifiedDate (DateTime)
  CreatedBy, LastModifiedBy (string, 100)

  Nav: Lines ? WorkOrderLine[], Quote ? Quote?

WorkOrderLine
  Id (int, PK)
  WorkOrderId (int, FK ? WorkOrder, required)
  PartId (int, FK ? Part, required)
  Quantity (int, required, range 1-10000)
  ProducedQuantity (int, default 0)
  ShippedQuantity (int, default 0)
  Status (WorkOrderStatus, default Draft)
  Notes (string?, 500)

  Nav: WorkOrder ? WorkOrder, Part ? Part, Jobs ? Job[], PartInstances ? PartInstance[]
```

#### Quote.cs + QuoteLine.cs � already fully defined in �4.

#### BuildJob.cs (REWRITTEN � ~25 stored properties)
```
BuildJob
  BuildId (int, PK)
  PrinterName (string, 50)                             // Machine identifier
  ActualStartTime (DateTime)
  ActualEndTime (DateTime?)
  ScheduledStartTime (DateTime?)
  ScheduledEndTime (DateTime?)
  Status (BuildJobStatus, default Pending)
  UserId (int, FK ? User)
  Material (string?, 100)
  Notes (string?, TEXT)

  // Print results
  LaserRunTime (string?, 50)
  GasUsedLiters (float?)
  PowderUsedLiters (float?)
  EndReason (string?, 200)

  // Operator estimates
  OperatorEstimatedHours (decimal?)
  OperatorActualHours (decimal?)
  TotalPartsInBuild (int)

  // Link to scheduled job
  JobId (int?, FK ? Job)

  // Audit
  CreatedAt (DateTime)
  CompletedAt (DateTime?)

  Nav: User ? User, Job ? Job?, Parts ? BuildJobPart[], Delays ? DelayLog[]
```

#### BuildJobPart.cs
```
BuildJobPart
  Id (int, PK)
  BuildJobId (int, FK ? BuildJob.BuildId)
  PartId (int, FK ? Part)
  PartNumber (string, 50)                              // Denormalized
  Quantity (int, default 1)
  Notes (string?, 500)

  Nav: BuildJob ? BuildJob, Part ? Part
```

#### BuildPackage.cs + BuildPackagePart.cs
Already fully defined in old project � carry forward property list. Remove `StatusColor` and `PriorityDisplay` computed props (UI concern ? frontend). Keep `TotalPartCount`, `UniquePartCount`, `IsReadyToSchedule` as they're business logic.

#### BuildFileInfo.cs � already fully defined in �6.

#### StageExecution.cs (REWRITTEN from old ProductionStageExecution)
```
StageExecution
  Id (int, PK)
  JobId (int?, FK ? Job)
  ProductionStageId (int, FK ? ProductionStage, required)
  Status (StageExecutionStatus, default NotStarted)
  StartedAt (DateTime?)
  CompletedAt (DateTime?)

  // Time
  EstimatedHours (double?)
  ActualHours (double?)
  SetupHours (double?)

  // Cost
  EstimatedCost (decimal(10,2)?)
  ActualCost (decimal(10,2)?)
  MaterialCost (decimal(10,2)?)

  // Operator
  OperatorUserId (int?, FK ? User)
  OperatorName (string?, 100)

  // Custom Form Data (from CustomFieldsConfig)
  CustomFieldValues (TEXT, default "{}")               // JSON: filled form data

  // Quality
  QualityCheckRequired (bool, default true)
  QualityCheckPassed (bool?)
  QualityNotes (string?, 1000)

  // Notes
  Notes (string?, TEXT)
  Issues (string?, TEXT)

  // Audit
  CreatedDate, LastModifiedDate (DateTime)
  CreatedBy, LastModifiedBy (string?, 100)

  Nav: Job ? Job?, ProductionStage ? ProductionStage, Operator ? User?
```

**NOTE**: No more `PrototypeJobId`, `PrototypeJob`, `WorkflowTemplateId`, or `TimeLogs`. Those were dead weight.

#### PartInstance.cs + PartInstanceStageLog.cs � already fully defined in �3.

#### QCInspection.cs + QCChecklistItem.cs
Carry from old project. Changes:
- Remove `BuildCohortId` (dead concept)
- Add `PartInstanceId (int?, FK ? PartInstance)` for serial-number-level QC
- Remove `StatusColor` computed prop (UI concern)

#### JobNote.cs
```
JobNote
  Id (int, PK)
  JobId (int, FK ? Job, required)
  NoteText (string, TEXT, required)
  CreatedDate (DateTime)
  CreatedBy (string, 100, required)
  Nav: Job ? Job
```

#### DelayLog.cs
```
DelayLog
  Id (int, PK)
  BuildJobId (int?, FK ? BuildJob)
  JobId (int?, FK ? Job)
  StageExecutionId (int?, FK ? StageExecution)
  Reason (string, 200, required)
  ReasonCode (string?, 50)                             // Configurable reason codes from SystemSettings
  DelayMinutes (int, required)
  LoggedBy (string, 100, required)
  LoggedAt (DateTime, default UtcNow)
  Notes (string?, 500)

  Nav: BuildJob ? BuildJob?, Job ? Job?, StageExecution ? StageExecution?
```

#### OperatingShift.cs
```
OperatingShift
  Id (int, PK)
  Name (string, 100, required)                         // "Day Shift", "Night Shift"
  StartTime (TimeSpan, required)                       // 06:00
  EndTime (TimeSpan, required)                         // 18:00
  DaysOfWeek (string, 50, required)                    // "Mon,Tue,Wed,Thu,Fri"
  IsActive (bool, default true)
  CreatedDate (DateTime)
```

#### SystemSetting.cs
```
SystemSetting
  Id (int, PK)
  Key (string, 100, required, unique)                  // "CompanyName", "SerialNumberPrefix"
  Value (string, TEXT, required)
  Category (string, 50, default "General")             // "General", "Branding", "Debug", "Serial"
  Description (string?, 500)
  LastModifiedDate (DateTime)
  LastModifiedBy (string, 100)
```

#### MachineStateRecord.cs (telemetry � from old project)
```
MachineStateRecord
  Id (int, PK)
  MachineId (string, 50, required)
  Timestamp (DateTime, required)
  Status (string, 50)
  BuildProgress (double?)                              // 0-100%
  CurrentLayer (int?)
  TotalLayers (int?)
  BedTemperature (double?)
  ChamberTemperature (double?)
  LaserPower (double?)
  GasFlow (double?)
  OxygenLevel (double?)
  HumidityPercent (double?)
  IsConnected (bool, default false)
  RawDataJson (TEXT?)
```

#### MachineConnectionSettings.cs (OPC UA config � from old project)
```
MachineConnectionSettings
  Id (int, PK)
  MachineId (string, 50, required, unique)
  ProviderType (string, 50, required)                  // "Mock", "EOS", "Trumpf"
  EndpointUrl (string?, 200)
  IsEnabled (bool, default false)
  PollIntervalSeconds (int, default 5)
  ConfigJson (TEXT?)                                    // Provider-specific config
  LastModifiedDate (DateTime)
```

#### Maintenance Models � already fully defined in �5 + old project files. Carry forward as-is.

### TenantDbContext DbSet List (~35 DbSets)

```csharp
// Core Manufacturing
public DbSet<Part> Parts { get; set; }
public DbSet<Job> Jobs { get; set; }
public DbSet<ProductionStage> ProductionStages { get; set; }
public DbSet<PartStageRequirement> PartStageRequirements { get; set; }

// Work Orders & Quotes
public DbSet<WorkOrder> WorkOrders { get; set; }
public DbSet<WorkOrderLine> WorkOrderLines { get; set; }
public DbSet<Quote> Quotes { get; set; }
public DbSet<QuoteLine> QuoteLines { get; set; }

// Serial Tracking
public DbSet<PartInstance> PartInstances { get; set; }
public DbSet<PartInstanceStageLog> PartInstanceStageLogs { get; set; }

// Scheduling
public DbSet<Machine> Machines { get; set; }
public DbSet<OperatingShift> OperatingShifts { get; set; }

// Production Tracking
public DbSet<BuildJob> BuildJobs { get; set; }
public DbSet<BuildJobPart> BuildJobParts { get; set; }
public DbSet<StageExecution> StageExecutions { get; set; }
public DbSet<DelayLog> DelayLogs { get; set; }

// Build Planning
public DbSet<BuildPackage> BuildPackages { get; set; }
public DbSet<BuildPackagePart> BuildPackageParts { get; set; }
public DbSet<BuildFileInfo> BuildFileInfos { get; set; }

// Quality
public DbSet<QCInspection> QCInspections { get; set; }
public DbSet<QCChecklistItem> QCChecklistItems { get; set; }

// Notes & Logging
public DbSet<JobNote> JobNotes { get; set; }

// Infrastructure
public DbSet<User> Users { get; set; }
public DbSet<UserSettings> UserSettings { get; set; }
public DbSet<Material> Materials { get; set; }
public DbSet<SystemSetting> SystemSettings { get; set; }

// Machine Integration
public DbSet<MachineConnectionSettings> MachineConnectionSettings { get; set; }
public DbSet<MachineStateRecord> MachineStateRecords { get; set; }

// Maintenance
public DbSet<MachineComponent> MachineComponents { get; set; }
public DbSet<MaintenanceRule> MaintenanceRules { get; set; }
public DbSet<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; }
public DbSet<MaintenanceActionLog> MaintenanceActionLogs { get; set; }
```

### PlatformDbContext DbSet List (2 DbSets)

```csharp
public DbSet<Tenant> Tenants { get; set; }
public DbSet<PlatformUser> PlatformUsers { get; set; }
```

### Login Flow (Multi-Tenant Resolution)

```
1. User hits /Account/Login
2. AuthService.LoginAsync(username, password):
   a. First check platform.db ? PlatformUsers table
      - If found + password matches ? set claims: Role=SuperAdmin, IsPlatform=true
      - Redirect to /Platform/Tenants
   b. If not in platform.db ? scan tenant DBs:
      - For each active tenant in platform.db:
        - Open tenant DB ? check Users table for username
        - If found + password matches ? set claims: TenantCode, Role, UserId
        - Redirect to /
   c. If no match ? return "Invalid credentials"
3. TenantMiddleware reads TenantCode from claims ? sets ITenantContext
4. TenantDbContextFactory uses ITenantContext.TenantCode ? opens correct DB
```

**Performance note**: Scanning all tenant DBs on login is O(n) tenants. For <100 tenants this is fine. For scale: add a username?tenantCode lookup table in platform.db.

### Model Dependency Order (for Phase A creation)

Create files in this order to avoid forward-reference compilation errors:

```
1. Enums/ManufacturingEnums.cs         (no deps)
2. Platform/Tenant.cs                   (no deps)
3. Platform/PlatformUser.cs             (no deps)
4. SystemSetting.cs                     (no deps)
5. Material.cs                          (no deps)
6. OperatingShift.cs                    (no deps)
7. UserSettings.cs                      (no deps � User FK added later)
8. User.cs                              (nav ? UserSettings)
9. Machine.cs                           (no model deps)
10. ProductionStage.cs                  (no model deps � CustomFieldDefinition record in same file)
11. Part.cs                             (no model deps)
12. PartStageRequirement.cs             (deps: Part, ProductionStage)
13. WorkOrder.cs + WorkOrderLine.cs     (deps: Part)
14. Quote.cs + QuoteLine.cs             (deps: Part, WorkOrder)
15. Job.cs                              (deps: Part, Machine via string, User, WorkOrderLine)
16. StageExecution.cs                   (deps: Job, ProductionStage, User)
17. BuildJob.cs                         (deps: User, Job)
18. BuildJobPart.cs                     (deps: BuildJob, Part)
19. BuildPackage.cs + BuildPackagePart.cs (deps: Part, Job)
20. BuildFileInfo.cs                    (deps: BuildPackage)
21. PartInstance.cs                     (deps: Part, WorkOrderLine, ProductionStage)
22. PartInstanceStageLog.cs             (deps: PartInstance, ProductionStage)
23. QCInspection.cs + QCChecklistItem.cs (deps: Job, BuildJob, Part, User, PartInstance)
24. JobNote.cs                          (deps: Job)
25. DelayLog.cs                         (deps: BuildJob, Job, StageExecution)
26. MachineStateRecord.cs               (no model deps � uses MachineId string)
27. MachineConnectionSettings.cs        (no model deps)
28. Maintenance/MachineComponent.cs     (no model deps � uses MachineId string)
29. Maintenance/MaintenanceRule.cs      (deps: MachineComponent, ProductionStage)
30. Maintenance/MaintenanceWorkOrder.cs (deps: Machine, MachineComponent, MaintenanceRule, User)
31. Maintenance/MaintenanceActionLog.cs (deps: MaintenanceRule)
```

---

## 8. Navigation (FINAL � Dynamic from DB)

```
SIDEBAR NAVIGATION
??????????????????

?? Dashboard                         ? /

?? PRODUCTION ??????????????????????
?? Production Scheduler               ? /Scheduler
?? Work Orders                        ? /WorkOrders
?? Quotes                             ? /Quotes
?? Build Planning                     ? /Builds

?? SHOP FLOOR (dynamic from DB) ????
   (rendered from ProductionStages table, ordered by DisplayOrder)
   ?? SLS/LPBF Printing               ? /ShopFloor/Stage/sls-printing
   ? Depowdering                      ? /ShopFloor/Stage/depowdering
   ?? Heat Treatment                   ? /ShopFloor/Stage/heat-treatment
   ? Wire EDM                         ? /ShopFloor/Stage/wire-edm
   ?? CNC Machining                    ? /ShopFloor/Stage/cnc-machining
   ?? Laser Engraving                  ? /ShopFloor/Stage/laser-engraving
   ?? Surface Finishing                ? /ShopFloor/Stage/surface-finishing
   ? Quality Control                  ? /ShopFloor/Stage/qc
   ?? Shipping                         ? /ShopFloor/Stage/shipping
   (+ any custom stages the tenant adds)

?? VISIBILITY ??????????????????????
?? Part Tracker                       ? /Tracking
?? Analytics                          ? /Analytics
??? Machine Status                    ? /Machines

?? MAINTENANCE ?????????????????????
?? Maintenance Dashboard              ? /Maintenance
?? Maintenance Work Orders            ? /Maintenance/WorkOrders

?? ADMIN ???????????????????????????
?? Admin Panel                        ? /Admin

?? PLATFORM (super admin only) ?????
?? Tenants                            ? /Platform/Tenants
?? Platform Users                     ? /Platform/Users
```

**Operators see ONLY:** Dashboard + their assigned stage(s) + Part Tracker.
**Managers see:** Everything except Platform.
**Super Admin sees:** Everything including Platform.

---

## 9. Page Content Specs (What Each Page Shows)

### Dashboard (/)
- Active jobs by machine (Gantt mini-view)
- Machine utilization % (bar chart)
- On-time delivery rate (last 30/90 days)
- Parts in each stage (flow visualization pipeline)
- Active work orders (count + overdue alerts)
- OEE (Overall Equipment Effectiveness)
- Scrap/defect rate
- Upcoming maintenance alerts
- Cost per part trend (line chart)

### Scheduler (/Scheduler)
- Gantt chart: machines as rows, time as columns
- Drag-drop job placement
- Job creation form: select Part ? auto-fill stacking + duration
- Predecessor chain visualization
- Shift alignment (gray out non-working hours)
- Conflict detection (overlap highlighting)
- Quick filters: by machine, by material, by status, by priority

### Work Orders (/WorkOrders)
- List: WO number, customer, due date, status, priority, % complete
- Create: customer name, PO, due date, add lines (Part + qty)
- Detail view: all lines with stage progress per line
- Actions: Release, Cancel, Mark Complete
- Link to Part Tracker for each line

### Work Order Details (/WorkOrders/Details/{id})
- Header: customer, dates, status
- Line items table: Part, qty ordered/produced/shipped, status
- Per-line part tracker (pipeline visualization)
- Notes/attachments
- Linked jobs list
- Cost summary: quoted vs actual

### Quotes (/Quotes)
- List: quote number, customer, date, total, status
- Create: customer info, add lines (Part + qty + price)
- Auto-calculate cost from Part stages � rates
- Actions: Send, Accept ? Convert to Work Order, Reject, Expire

### Build Planning (/Builds)
- List of BuildPackages (build plates)
- Create: select machine, add parts (from WO lines)
- Stacking configuration per build
- Build file info panel (spoof data + debug form)
- Link to schedule: "Schedule this build" creates a Job
- Estimated vs actual powder usage
- Nesting preview (future: visual plate layout)

### Shop Floor Stage (/ShopFloor/Stage/{slug})
- **Queue**: Parts/batches waiting for this stage (sorted by priority/due date)
- **Active Work**: Currently in progress at this stage
- **Operator Form**: Start/complete actions + stage-specific fields
  - Built-in stages: rich form with stage-specific inputs
  - Custom stages: auto-rendered form from CustomFieldsConfig
- **Batch controls**: For batch stages (group parts, process together)
- **Serial number input**: Only at Laser Engraving stage (scan/enter serial)
- **Delay logging**: Record delays with reason codes
- **Notes**: Per-stage operator notes
- **History**: Recent completions at this stage

### Part Tracker (/Tracking)
- Search by: WO number, Part number, Serial number
- Pipeline visualization: stages as columns, parts as progress bars
- Click any WO line ? see each serial number's position (post-engraving)
- Click any serial ? full history: every stage, operator, duration, notes
- Filter by: stage, status, overdue, customer

### Analytics (/Analytics)
- Build time analysis: estimated vs actual (by part, by machine)
- Machine utilization heatmap (by day/week)
- OEE breakdown: Availability � Performance � Quality
- Stage throughput: parts per day per stage
- Cost analysis: per part, per WO, margins
- Learning curves: how estimates improve over time
- Scrap rate trends
- On-time delivery trends

### Machine Status (/Machines)
- Card per machine: status, current job, utilization %, live telemetry
- SignalR real-time updates
- Maintenance status: next PM due, hours until PM
- Quick actions: mark idle, start maintenance, view history

### Maintenance Dashboard (/Maintenance)
- Upcoming PM tasks (sorted by urgency)
- Overdue maintenance (red alerts)
- Active maintenance work orders
- Machine health summary
- Maintenance cost tracking

### Maintenance Work Orders (/Maintenance/WorkOrders)
- CRUD for maintenance work orders
- Assign technician, schedule, track completion
- Parts used, work performed, hours, cost
- Link to machine and component

### Admin (/Admin)
Hub page with links to sub-pages:

### Admin / Parts (/Admin/Parts)
- CRUD: Part number, name, material, manufacturing approach
- Stacking config: single/double/triple durations + parts per build
- Stage assignment: which stages this part requires + order + estimated hours
- Per-stage custom field defaults
- Batch stage config: total duration + parts per batch

### Admin / Production Stages (/Admin/ProductionStages)
- CRUD: name, slug, icon, color, department, display order
- Custom form builder: drag-and-drop field creation (text, number, dropdown, checkbox, date)
- Stage options: isBatchStage, requiresMachineAssignment, requiresSerialNumber
- Assign machines that can execute this stage
- Default duration, setup time, hourly rate

### Admin / Machines (/Admin/Machines)
- CRUD: machine ID, name, type, model, location, department
- Build volume specs (for SLS machines)
- Components list (for maintenance)
- Supported materials
- OPC UA connection settings
- Scheduling priority

### Admin / Materials (/Admin/Materials)
- CRUD: name, density, cost per kg, supplier
- Compatibility matrix: which materials can share a machine without full cleaning
- Powder management: lot tracking, sieve cycles, recycle percentage

### Admin / Users (/Admin/Users)
- CRUD: username, full name, email, role, department
- Role assignment (from configurable role list)
- Stage assignment: which shop floor stages this user can access
- Active/inactive toggle

### Admin / Settings (/Admin/Settings)
- Company branding: name, logo, primary color
- Default shift schedules
- Serial number format (prefix, auto-increment, etc.)
- Notification preferences
- Debug toggles (show build file debug form, etc.)
- Defect/delay reason codes (configurable lists)

---

## 10. PWA (iOS-First Progressive Web App)

### Requirements
- Installable on iPad/iPhone home screen
- Touch-friendly: large tap targets (min 44�44px), no hover-only interactions
- Responsive: sidebar collapses to bottom nav on mobile
- Offline shell: app shell loads even without network; data requires connection
- Service worker: caches static assets (CSS, JS, icons)
- Manifest: app name, icons, theme color, start URL

### Implementation
```json
// wwwroot/manifest.json
{
  "name": "OpCentrix MES",
  "short_name": "OpCentrix",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#111827",
  "theme_color": "#3B82F6",
  "icons": [
    { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icons/icon-512.png", "sizes": "512x512", "type": "image/png" }
  ]
}
```

### Touch-Friendly Design Rules
- All buttons min 44�44px (Apple HIG)
- Shop floor forms: large input fields, number inputs use `inputmode="numeric"`
- Swipe gestures: swipe right to complete stage, swipe left for delay
- Bottom nav bar on mobile (replaces sidebar)
- No context menus or right-click interactions

---

## 11. Execution Order (FINAL)

### Phase A: Scaffold + Multi-Tenant + Models (Session 1)

Each step below is scoped so Copilot can execute it in one shot and verify with `dotnet build`.

```
A1. Create project
    dotnet new webapp -n OpCentrix -o OpCentrix-v2
    cd OpCentrix-v2

A2. Add NuGet packages
    dotnet add package Microsoft.EntityFrameworkCore.Sqlite
    dotnet add package Microsoft.EntityFrameworkCore.Design
    dotnet add package Microsoft.AspNetCore.SignalR

A3. Create Models/Enums/ManufacturingEnums.cs
    All enums from �7A. Build + verify.

A4. Create Platform models
    Models/Platform/Tenant.cs (from �1)
    Models/Platform/PlatformUser.cs (from �1)
    Build + verify.

A5. Create Data/PlatformDbContext.cs
    2 DbSets: Tenants, PlatformUsers (from �7A PlatformDbContext list)
    Build + verify.

A6. Create leaf models (no FK dependencies)
    Models/SystemSetting.cs
    Models/Material.cs
    Models/OperatingShift.cs
    Models/UserSettings.cs
    Models/User.cs (with nav ? UserSettings)
    Models/Machine.cs
    Models/MachineStateRecord.cs
    Models/MachineConnectionSettings.cs
    All property specs in �7A. Build + verify.

A7. Create stage + part models
    Models/ProductionStage.cs (include CustomFieldDefinition record, add StageSlug/HasBuiltInPage/RequiresSerialNumber)
    Models/Part.cs (from old MasterPart.cs � rename class to Part, keep all props + computed + methods)
    Models/PartStageRequirement.cs (from old project � update nav prop Part type)
    Build + verify.

A8. Create work order + quote models
    Models/WorkOrder.cs (WorkOrder + WorkOrderLine classes)
    Models/Quote.cs (Quote + QuoteLine classes)
    Build + verify.

A9. Create job + execution models
    Models/Job.cs (REWRITTEN per �7A � ~30 stored props)
    Models/StageExecution.cs (REWRITTEN per �7A)
    Models/JobNote.cs
    Build + verify.

A10. Create build models
     Models/BuildJob.cs
     Models/BuildJobPart.cs
     Models/BuildPackage.cs (BuildPackage + BuildPackagePart classes)
     Models/BuildFileInfo.cs
     Build + verify.

A11. Create tracking + QC models
     Models/PartInstance.cs
     Models/PartInstanceStageLog.cs
     Models/QCInspection.cs (QCInspection + QCChecklistItem)
     Models/DelayLog.cs
     Build + verify.

A12. Create maintenance models
     Models/Maintenance/MachineComponent.cs
     Models/Maintenance/MaintenanceRule.cs
     Models/Maintenance/MaintenanceWorkOrder.cs
     Models/Maintenance/MaintenanceActionLog.cs
     Build + verify.

A13. Create Data/TenantDbContext.cs
     ~35 DbSets from �7A TenantDbContext list.
     OnModelCreating: one HasMany/WithOne per relationship, table names, indexes.
     Build + verify.

A14. Create Data/TenantDbContextFactory.cs
     Resolves tenant code ? opens correct DB file.
     Build + verify.

A15. Create Middleware/TenantMiddleware.cs
     Reads TenantCode from ClaimsPrincipal, sets ITenantContext.
     Build + verify.

A16. Create Services/Platform/ITenantContext.cs + TenantContext.cs
     Scoped per-request tenant info.
     Build + verify.

A17. Create Program.cs
     DI registrations, cookie auth, TenantMiddleware, SignalR, static files.
     Build + verify.

A18. Create migrations + verify full build
     dotnet ef migrations add PlatformInitial --context PlatformDbContext
     dotnet ef migrations add TenantInitial --context TenantDbContext
     dotnet build
```

**Checkpoint**: After A18, the project compiles with all models, both DbContexts, middleware, and Program.cs. No pages or services yet.

### Phase B: Services (Session 2)

Each step creates one interface + implementation pair. Build after each.

```
B1. Services/Platform/ITenantService.cs + TenantService.cs
    Tenant CRUD, create new tenant DB, apply migrations, seed default data.

B2. Services/Auth/IAuthService.cs + AuthService.cs
    Login (multi-tenant resolution per �7A login flow), logout, password hashing.

B3. Services/IPartService.cs + PartService.cs
    Part CRUD, stacking validation, stage requirement management.

B4. Services/IJobService.cs + JobService.cs
    Job CRUD, scheduling logic, overlap detection, stacking hydration from Part.

B5. Services/IWorkOrderService.cs + WorkOrderService.cs
    WO CRUD, status lifecycle, line management, fulfillment tracking.

B6. Services/IQuoteService.cs + QuoteService.cs
    Quote CRUD, auto-cost calculation, convert-to-WorkOrder.

B7. Services/IBuildService.cs + BuildService.cs
    BuildJob tracking, start/complete, delay logging.

B8. Services/IBuildPlanningService.cs + BuildPlanningService.cs
    BuildPackage CRUD, BuildFileInfo management, spoof data generator.

B9. Services/IStageService.cs + StageService.cs
    Stage progression, completion, batch processing, custom form data saving.

B10. Services/ISerialNumberService.cs + SerialNumberService.cs
     Serial number generation (format from SystemSettings), assignment at engraving, lookup.

B11. Services/IPartTrackerService.cs + PartTrackerService.cs
     "Where is my part?" queries � batch tracking (pre-engraving) + serial tracking (post-engraving).

B12. Services/ILearningService.cs + LearningService.cs
     EMA calculations: ?=0.3, update PartStageRequirement after stage completion,
     auto-switch EstimateSource to "Auto" after 3 samples.

B13. Services/IMaintenanceService.cs + MaintenanceService.cs
     PM rule evaluation, work order CRUD, action logging, scheduler blocking.

B14. Services/IAnalyticsService.cs + AnalyticsService.cs
     KPIs: OEE, utilization, on-time delivery, cost per part, margin calc.

B15. Services/IMaterialService.cs + MaterialService.cs
     Material CRUD, compatibility matrix.

B16. Services/IDataSeedingService.cs + DataSeedingService.cs
     Seeds: 9 ProductionStages (from �0 table), 5 Machines (from �0 table),
     default Materials, default OperatingShift, default SystemSettings.

B17. Machine providers
     Copy + clean from old project: IMachineProvider, MockMachineProvider,
     MachineProviderFactory, MachineSyncService.
     Copy: Hubs/MachineStateHub.cs, IMachineStateNotifier.cs, MachineStateNotifier.cs.

B18. Register all services in Program.cs DI container.
     Build + verify full compilation.
```

**Checkpoint**: After B18, all services compile. No pages yet but the entire backend is functional.

### Phase C: Pages + Navigation + PWA (Session 3)

```
C1. Pages/Shared/_Layout.cshtml
    Dynamic sidebar from DB (�8 navigation spec).
    Dark mode toggle. Mobile bottom nav. Touch-friendly.
    Pages/_ViewImports.cshtml, Pages/_ViewStart.cshtml.

C2. PWA files
    wwwroot/manifest.json (�10 spec).
    wwwroot/js/service-worker.js (cache static assets).
    wwwroot/icons/icon-192.png, icon-512.png (placeholder PNGs).
    Link manifest in _Layout.cshtml <head>.

C3. Pages/Account/Login.cshtml(.cs) + Logout.cshtml(.cs)
    Login form ? AuthService.LoginAsync. Cookie auth.

C4. Pages/Platform/ (3 pages � super admin only)
    Tenants.cshtml(.cs) � tenant list + create form.
    TenantSetup.cshtml(.cs) � configure new tenant.
    Users.cshtml(.cs) � platform user management.

C5. Pages/Index.cshtml(.cs) � Dashboard
    KPIs from AnalyticsService. See �9 Dashboard spec.

C6. Pages/Scheduler/Index.cshtml(.cs)
    Gantt chart. Job CRUD. See �9 Scheduler spec.
    Requires: wwwroot/js/scheduler.js.

C7. Pages/WorkOrders/Index.cshtml(.cs) + Details.cshtml(.cs)
    WO list, create, detail. See �9 spec.

C8. Pages/Quotes/Index.cshtml(.cs) + Details.cshtml(.cs)
    Quote list, create, convert-to-WO. See �9 spec.

C9. Pages/Builds/Index.cshtml(.cs)
    Build planning + file info + debug form. See �9 spec.

C10. Pages/ShopFloor/Stage.cshtml(.cs)
     Dynamic route: /ShopFloor/Stage/{slug}.
     Route resolution logic per �2.

C11. Shop Floor Partials (9 built-in + 1 generic)
     Pages/ShopFloor/Partials/_SLSPrinting.cshtml
     Pages/ShopFloor/Partials/_Depowdering.cshtml
     Pages/ShopFloor/Partials/_HeatTreatment.cshtml
     Pages/ShopFloor/Partials/_WireEDM.cshtml
     Pages/ShopFloor/Partials/_CNCMachining.cshtml
     Pages/ShopFloor/Partials/_LaserEngraving.cshtml (serial number input)
     Pages/ShopFloor/Partials/_SurfaceFinishing.cshtml
     Pages/ShopFloor/Partials/_QualityControl.cshtml
     Pages/ShopFloor/Partials/_Shipping.cshtml
     Pages/ShopFloor/Partials/_GenericStage.cshtml (fallback for custom stages)

C12. wwwroot/js/dynamic-form-renderer.js
     Reads CustomFieldsConfig JSON, renders form inputs dynamically.

C13. Pages/Tracking/Index.cshtml(.cs) � Part Tracker
     Search, pipeline visualization. See �9 spec.

C14. Pages/Analytics/Index.cshtml(.cs) � Analytics Dashboard
     Charts, KPIs. See �9 spec.

C15. Pages/Machines/Index.cshtml(.cs) � Machine Status
     Live cards, SignalR. See �9 spec.
     Requires: wwwroot/js/machine-state-client.js.

C16. Pages/Maintenance/ (3 pages)
     Index.cshtml(.cs) � Maintenance dashboard.
     WorkOrders.cshtml(.cs) � Maintenance WO CRUD.
     Rules.cshtml(.cs) � PM rule configuration.

C17. Pages/Admin/ (7 pages + 1 partial)
     Index.cshtml(.cs) � Admin hub.
     Parts.cshtml(.cs) + Shared/_PartForm.cshtml.
     ProductionStages.cshtml(.cs).
     Machines.cshtml(.cs).
     Materials.cshtml(.cs).
     Users.cshtml(.cs).
     Settings.cshtml(.cs).
     Requires: wwwroot/js/partForm.js.

C18. wwwroot/css/site.css
     Mobile-first, touch-friendly, dark mode, 44px tap targets.

C19. wwwroot/js/site.js
     Sidebar toggle, dark mode, PWA registration, shared utilities.

C20. Build + run + verify
     dotnet run ? all pages render, seed data loads, login works.
```

**Checkpoint**: After C20, the full app runs end-to-end with all pages, navigation, and PWA.

### Phase D: Tests + Retirement (Session 4)

```
D1. Create OpCentrix.Tests project (xUnit)
    dotnet new xunit -n OpCentrix.Tests
    Add project reference to OpCentrix-v2.

D2. Model unit tests
    Part validation, stacking config, computed props.
    Job scheduling overlap detection.

D3. Service unit tests
    LearningService EMA calculations.
    SerialNumberService format generation.
    QuoteService convert-to-WO logic.
    PartTrackerService query logic.

D4. Integration tests
    AuthService login flow (platform + tenant).
    DataSeedingService creates correct seed data.
    StageService progression logic.

D5. Rename old project ? OpCentrix-legacy
    Rename OpCentrix-v2 ? OpCentrix.
    Update solution file. Commit.
```

---

## 12. Design Rules (FINAL)

### Multi-Tenancy
- Every DB query is automatically scoped to the current tenant's DB
- No `TenantId` column needed � separate DB files provide isolation
- Super admin can impersonate any tenant
- New tenant = new DB file + seed data

### Page Models
- **Max 300 lines** per `.cshtml.cs`
- Call services, don't contain business logic
- No direct DbContext access

### Services
- **Every service has an interface**
- Single responsibility
- Services receive `TenantDbContext` via DI (already scoped to correct tenant)

### Models
- **No UI concerns** (no `GetStatusColor()`, no display helpers)
- **Enums over strings** for all status/priority/type fields
- **Telemetry** ? `MachineStateRecord`, not `Job`
- **QC data** ? `QCInspection`, not `Job`
- **Cost** ? calculated in `AnalyticsService`, not stored on `Job`

### Shop Floor
- ONE Razor page (`Stage.cshtml`) handles ALL stages
- Built-in stages get rich partial templates
- Custom stages get generic template with dynamic form rendering
- Navigation generated from DB, not hardcoded

### Mobile / PWA
- iOS-first: test on iPad Safari
- All touch targets ? 44�44px
- Bottom nav on mobile, sidebar on desktop
- Service worker caches app shell

---

## 13. Production Flow (FINAL � with Serial Numbers)

```
1. QUOTE created (pre-sales estimate)
   ??? Customer requests: Part � Qty, planner estimates cost
   ??? Quote accepted ? auto-converts to Work Order

2. WORK ORDER created (customer order ? part requirements)
   ??? Specifies: Part, Quantity, Due Date, Priority, Customer PO

3. BUILD PLANNING (planner groups parts into build plates)
   ??? Creates: BuildPackage (which parts go on which plate)
   ??? Build file info: layer count, height, powder estimate (spoof data for now)
   ??? Considers: Stacking (1x/2x/3x), machine capacity, due dates

4. SCHEDULING (planner schedules builds on machines)
   ??? Creates: Job per build per machine
   ??? Auto-calculates: Duration from Part stacking config
   ??? Handles: Shift alignment, conflicts, predecessors, maintenance windows

5. SLS/LPBF PRINTING (operator runs the build)
   ??? Tracks: Actual start/end, powder usage, build progress
   ??? SignalR: Live machine telemetry
   ??? Operator form: start build, log powder added, record delays
   ??? Output: Raw parts on build plate (BATCH tracking: "20 parts printed")

6. DEPOWDERING (operator removes excess powder)
   ??? Batch processing: multiple parts from a build
   ??? Operator form: batch qty, duration, powder recovered
   ??? Output: Clean parts

7. HEAT TREATMENT (oven process)
   ??? Batch processing: oven load
   ??? Operator form: batch qty, oven temp profile, duration
   ??? Output: Stress-relieved parts

8. WIRE EDM (cut parts from build plate)
   ??? Per-part or batch processing
   ??? Operator form: per-part duration, wire usage
   ??? Output: Individual parts separated from plate

9. CNC MACHINING (secondary ops � threading, facing)
   ??? Per-part processing
   ??? Operator form: operation type, duration, tool used
   ??? Output: Dimensionally finished parts

10. LASER ENGRAVING ? SERIAL NUMBER ASSIGNMENT ?
    ??? Each part gets unique serial number (engraved + recorded)
    ??? Creates PartInstance records
    ??? From this point: individual serial number tracking
    ??? Operator form: serial number entry/scan per part

11. SURFACE FINISHING (tumbling, blasting, coating)
    ??? Batch or individual processing
    ??? Operator form: finish type, duration, batch qty
    ??? Output: Finished parts (tracked by serial number)

12. QUALITY CONTROL (inspection + measurement)
    ??? Per-serial-number inspection
    ??? Operator form: checklist items, pass/fail, measurements, notes
    ??? Output: Accepted or rejected parts (by serial number)

13. SHIPPING (pack and ship to customer)
    ??? Operator form: packing list (serial numbers), carrier, tracking number
    ??? Links to: Work Order fulfillment (auto-updates WO line ProducedQty/ShippedQty)
    ??? Output: Shipped order, WO marked complete when all lines fulfilled
```

---

## 14. Flowcharts

- `C:\Users\Henry\OneDrive\Desktop\FlowCharts\OpCentrix-Core-MES-Flow.json`
- `C:\Users\Henry\OneDrive\Desktop\FlowCharts\OpCentrix-Final-Single-Model-Flow.json`

---

## 15. Implementation Phases (Full Roadmap � FINAL)

### PHASE 1: Fresh Project � Scaffold + Multi-Tenant + Models (Session 1)
Steps A1-A18 from �11. Model specs in �7A.

### PHASE 2: Fresh Project � Services (Session 2)
Steps B1-B18 from �11.

### PHASE 3: Fresh Project � Pages + Navigation + PWA (Session 3)
Steps C1-C20 from �11. Page content specs in �9.

### PHASE 4: Tests + Old Project Retirement (Session 4)
Steps D1-D5 from �11.

### PHASE 5: Slicer Integration (Future)
1. Define API endpoint: `POST /api/builds/{id}/build-file`
2. Research Materialise Magics / Netfabb export formats
3. Build import adapter
4. Replace debug spoof form with real data

### PHASE 6: Customer Portal (Future)
1. Separate login for customers (read-only)
2. View work orders, part tracker, shipping status
3. Download inspection reports

---

## 16. Quick Resume Instructions (FINAL)

If chat breaks or a new session starts:

1. **Read the MASTER PROGRESS TRACKER** at the top of this file � it shows exactly which step to resume.
2. **Check branch**: `New-Master-Parts`
3. **Strategy**: FRESH PROJECT BUILD with multi-tenancy baked in from day one.
4. **Check progress**: The tracker checkboxes are the source of truth. Also check if `OpCentrix-v2/` directory exists.
5. **If no OpCentrix-v2/ exists**: Start at Phase A, Step A1 (�11). Model specs in �7A.
6. **Old project**: Lives in `OpCentrix/` until Phase D renames it to `OpCentrix-legacy/`.
7. **After completing any step**: Mark it `[x]` in the tracker and update the Phase Status line.

### Architecture Summary (for quick reference)
- **Multi-tenant**: Separate SQLite DB per tenant, resolved from auth claims
- **Super admin**: platform.db with Tenant table, can impersonate any tenant
- **Dynamic stages**: One `/ShopFloor/Stage/{slug}` page, renders from DB config
- **Custom forms**: `ProductionStage.CustomFieldsConfig` JSON defines operator forms
- **Serial tracking**: `PartInstance` created at Laser Engraving, tracked individually after
- **Quotes ? Work Orders ? Jobs ? Stages ? Shipping**: Full lifecycle
- **Maintenance**: Rules, work orders, machine components, scheduler integration
- **PWA**: iOS-first, installable, touch-friendly, offline shell
- **Cost tracking**: Calculated in `AnalyticsService`, not on models

### Critical Rules for AI Assistants
- There is **ONE** `Part` model. No `MasterPart`.
- **No B&T, firearms, suppressors, ATF, caliber, baffle** references. Ever.
- `Job.cs` is **~30 stored properties** � telemetry ? `MachineStateRecord`, QC ? `QCInspection`, cost ? `AnalyticsService`.
- **Every service has an interface.** Every page model under 300 lines.
- **Enums over strings** for all status/priority/type fields.
- **No UI concerns on models** � no color helpers, no display methods.
- **Shop floor uses ONE page** (`Stage.cshtml`) with partials for built-in stages and generic renderer for custom stages.
- **Navigation is dynamic** � rendered from `ProductionStages` table in DB.
- **Multi-tenant from day one** � separate DB per tenant, `TenantMiddleware` resolves tenant.
- **PWA from day one** � manifest.json, service worker, touch-friendly CSS, mobile bottom nav.
- **Operators see only their assigned stages.** Managers see everything. Super admin sees platform pages.
- The fresh build spec is in �1-�12 + �7A (model specs). Follow it step by step.
- Model property specs are in �7A � ALWAYS reference �7A when creating models.
