# Opcentrix MES — Master Context & Progress Tracker

> **For Copilot**: This is your primary navigation file. Every time you start a
> session on this project, read this file first to understand what exists, what's
> in progress, and which module plan to follow next. Each module plan is a
> self-contained implementation guide. Follow them in phase order.

---

## Project Identity

| Field | Value |
|-------|-------|
| **Project Name** | Opcentrix V3 |
| **Type** | Manufacturing Execution System (MES) / ERP / QMS |
| **Framework** | ASP.NET Core Blazor Server (.NET 10.0) |
| **Database** | SQLite + Entity Framework Core 10.0 (multi-tenant) |
| **Real-time** | SignalR |
| **Auth** | Cookie-based, multi-tier (Platform + Tenant + Portal) |
| **Architecture** | Multi-tenant SaaS — separate SQLite DB per tenant |
| **Target Market** | Precision CNC & additive manufacturing shops (5–200 employees) |
| **Competitive Baseline** | Surpass ProShop ERP in features, UX, and API-openness |

---

## Repository Structure

```
Opcentrix-V3/
├── Components/           # Blazor Razor components & pages
│   ├── Pages/            # Feature pages organized by domain
│   ├── Layout/           # MainLayout, NavMenu, ReconnectModal
│   └── Shared/           # Reusable shared components
├── Models/               # EF Core entity models
│   ├── Platform/         # Multi-tenant platform models
│   ├── Enums/            # All manufacturing enums
│   └── [domain models]   # Feature-specific models
├── Services/             # Business logic layer (DI services)
├── Data/                 # DbContext files (Platform + Tenant)
├── Hubs/                 # SignalR hubs
├── Middleware/           # TenantMiddleware
├── wwwroot/              # Static assets (CSS, JS, uploads, icons)
├── sprints/              # Original sprint plans (superseded by docs/)
├── docs/                 # NEW: Full module implementation plans
│   ├── MASTER_CONTEXT.md (this file)
│   ├── phase-1/          # Core Production Engine (Months 1–6)
│   ├── phase-2/          # Operational Depth (Months 7–12)
│   └── phase-3/          # Platform Maturity (Months 13–18)
├── Program.cs            # DI setup, middleware, seeding
├── appsettings.json
└── Opcentrix-V3.csproj   # .NET 10.0 project
```

---

## Foundation Already Built ✅

These items are fully implemented and do NOT need to be rebuilt:

| Feature | Status | Key Files |
|---------|--------|-----------|
| Multi-tenant architecture | ✅ Complete | `Data/PlatformDbContext.cs`, `Data/TenantDbContext.cs`, `Middleware/TenantMiddleware.cs` |
| Authentication (login/logout) | ✅ Complete | `Components/Pages/Account/Login.razor`, `Services/Auth/AuthService.cs` |
| Platform super-admin | ✅ Complete | `Components/Pages/Platform/` |
| Tenant management | ✅ Complete | `Services/Platform/TenantService.cs` |
| Admin: Parts CRUD | ✅ Complete | `Components/Pages/Admin/Parts.razor`, `Services/PartService.cs` |
| Admin: Stages CRUD | ✅ Complete | `Components/Pages/Admin/Stages.razor`, `Services/StageService.cs` |
| Admin: Machines CRUD | ✅ Complete | `Components/Pages/Admin/Machines.razor` |
| Admin: Users CRUD | ✅ Complete | `Components/Pages/Admin/Users.razor` |
| Admin: Materials CRUD | ✅ Complete | `Components/Pages/Admin/Materials.razor` |
| Admin: Settings | ✅ Complete | `Components/Pages/Admin/Settings.razor` |
| Role-based navigation | ✅ Complete | `Components/Layout/NavMenu.razor` |
| Dark mode + responsive CSS | ✅ Complete | `wwwroot/css/site.css` |
| PWA support | ✅ Complete | `wwwroot/manifest.json`, `wwwroot/js/service-worker.js` |
| Real-time machine state (SignalR) | ✅ Complete | `Hubs/MachineStateHub.cs`, `Services/MachineSyncService.cs` |
| Maintenance rules/components | ✅ Complete | `Models/Maintenance/`, `Services/MaintenanceService.cs` |
| 31 EF Core entity models | ✅ Scaffolded | `Models/` |
| 37 service files | ✅ Scaffolded | `Services/` |
| Data seeding (demo tenant + admin) | ✅ Complete | `Program.cs` |

**Demo Credentials**:
- Super Admin: `superadmin` / `admin123` → `/platform/tenants`
- Demo tenant code: `demo`

---

## Module Implementation Plan

### Phase 1: Core Production Engine (Months 1–6)
*Goal: A shop can run core daily operations entirely in the system after Phase 1.*

| # | Module | Category | Priority | Status | Plan File |
|---|--------|----------|----------|--------|-----------|
| 01 | [Estimating & Quoting](#) | ERP | P1 | [ ] Not Started | [MODULE-01-estimating-quoting.md](phase-1/MODULE-01-estimating-quoting.md) |
| 02 | [Sales & Work Order Management](#) | ERP | P1 | [ ] Not Started | [MODULE-02-work-order-management.md](phase-1/MODULE-02-work-order-management.md) |
| 03 | [Visual Work Instructions](#) | MES | P1 | [ ] Not Started | [MODULE-03-visual-work-instructions.md](phase-1/MODULE-03-visual-work-instructions.md) |
| 04 | [Shop Floor Management & Scheduling](#) | MES | P1 | [ ] Not Started | [MODULE-04-shop-floor-scheduling.md](phase-1/MODULE-04-shop-floor-scheduling.md) |
| 05 | [Quality Systems & Inspection (QMS)](#) | QMS | P1 | [ ] Not Started | [MODULE-05-quality-systems.md](phase-1/MODULE-05-quality-systems.md) |
| 06 | [Inventory Control & Material Planning](#) | ERP | P1 | [ ] Not Started | [MODULE-06-inventory-control.md](phase-1/MODULE-06-inventory-control.md) |
| 07 | [Reporting & Analytics](#) | ERP | P1 | [ ] Not Started | [MODULE-07-reporting-analytics.md](phase-1/MODULE-07-reporting-analytics.md) |

### Phase 2: Operational Depth (Months 7–12)
*Goal: Full ERP/MES/QMS platform — financially transparent, deeply traceable.*

| # | Module | Category | Priority | Status | Plan File |
|---|--------|----------|----------|--------|-----------|
| 08 | [Parts / Product Data Management (PDM)](#) | ERP | P2 | [ ] Not Started | [MODULE-08-parts-pdm.md](phase-2/MODULE-08-parts-pdm.md) |
| 09 | [Job Costing & Financial Data](#) | ERP | P2 | [ ] Not Started | [MODULE-09-job-costing.md](phase-2/MODULE-09-job-costing.md) |
| 10 | [Cutting Tool & Fixture Management](#) | MES | P2 | [ ] Not Started | [MODULE-10-cutting-tools-fixtures.md](phase-2/MODULE-10-cutting-tools-fixtures.md) |
| 11 | [Calibration & Preventative Maintenance (CMMS)](#) | QMS | P2 | [ ] Not Started | [MODULE-11-calibration-maintenance.md](phase-2/MODULE-11-calibration-maintenance.md) |
| 12 | [Purchasing & Vendor Management](#) | ERP | P2 | [ ] Not Started | [MODULE-12-purchasing-vendor.md](phase-2/MODULE-12-purchasing-vendor.md) |
| 13 | [Time Clock & Labor Tracking](#) | MES | P2 | [ ] Not Started | [MODULE-13-time-clock-labor.md](phase-2/MODULE-13-time-clock-labor.md) |
| 14 | [Document Control](#) | QMS | P2 | [ ] Not Started | [MODULE-14-document-control.md](phase-2/MODULE-14-document-control.md) |

### Phase 3: Platform Maturity (Months 13–18)
*Goal: Customer-facing tools, compliance frameworks, and organizational learning.*

| # | Module | Category | Priority | Status | Plan File |
|---|--------|----------|----------|--------|-----------|
| 15 | [Shipping & Receiving](#) | ERP | P3 | [ ] Not Started | [MODULE-15-shipping-receiving.md](phase-3/MODULE-15-shipping-receiving.md) |
| 16 | [CRM & Contact Management](#) | ERP | P3 | [ ] Not Started | [MODULE-16-crm-contact.md](phase-3/MODULE-16-crm-contact.md) |
| 17 | [CMMC & Cybersecurity Compliance](#) | QMS | P3 | [ ] Not Started | [MODULE-17-cmmc-compliance.md](phase-3/MODULE-17-cmmc-compliance.md) |
| 18 | [User Training & Knowledge Management (LMS)](#) | QMS | P3 | [ ] Not Started | [MODULE-18-training-lms.md](phase-3/MODULE-18-training-lms.md) |

---

## Cross-Module Dependency Map

When implementing, respect these build-order dependencies:

```
Module 08 (Parts/PDM)
    └─► Module 01 (Quoting)
    └─► Module 02 (Work Orders)
    └─► Module 03 (Work Instructions)
    └─► Module 05 (Quality)

Module 02 (Work Orders)
    └─► Module 04 (Shop Floor) ← triggers stage executions
    └─► Module 06 (Inventory) ← material reservations
    └─► Module 09 (Job Costing) ← budget tracking
    └─► Module 15 (Shipping) ← shipment creation

Module 04 (Shop Floor)
    └─► Module 09 (Job Costing) ← labor cost entries
    └─► Module 10 (Tools) ← tool usage logging
    └─► Module 13 (Time Clock) ← clock in/out per stage

Module 05 (Quality)
    └─► Module 11 (Maintenance) ← gage calibration status
    └─► Module 12 (Purchasing) ← vendor quality scorecards
    └─► Module 14 (Documents) ← controlled inspection plans

Module 06 (Inventory)
    └─► Module 12 (Purchasing) ← PO receipts update stock
    └─► Module 10 (Tools) ← tooling inventory items

Module 09 (Job Costing)
    └─► Module 07 (Analytics) ← financial KPIs

Module 16 (CRM)
    └─► Module 01 (Quoting) ← customer on quotes
    └─► Module 15 (Shipping) ← shipment notifications
```

**Recommended implementation order within Phase 1**:
`08 → 01 → 02 → 04 → 06 → 05 → 07 → 03`

*(Parts/PDM first so routing data is available for quoting and WO creation)*

---

## Global Architecture Principles

These apply to every module. Copilot must follow these for every file created:

### 1. Multi-Tenant Pattern
Every service method signature includes `string tenantCode`:
```csharp
public async Task<List<Thing>> GetAllAsync(string tenantCode)
{
    using var db = _dbContextFactory.CreateDbContext(tenantCode);
    return await db.Things.ToListAsync();
}
```
Never use a global DbContext — always create via `TenantDbContextFactory`.

### 2. Service Registration
All services registered in `Program.cs` as `Scoped`:
```csharp
builder.Services.AddScoped<INewService, NewService>();
```

### 3. Interface Pattern
Every service must have a matching interface file:
- `Services/INewService.cs` — interface definition
- `Services/NewService.cs` — implementation

### 4. Blazor Component Pattern
Every new page follows this pattern:
```razor
@page "/route/{param}"
@attribute [Authorize(Roles = "Manager,Admin")]
@inject IServiceName _service
@inject ITenantContext _tenant

<PageTitle>Page Name</PageTitle>

@if (isLoading)
{
    <div class="loading-spinner">Loading...</div>
}
else
{
    <!-- content -->
}

@code {
    private bool isLoading = true;
    private List<Thing> items = new();

    protected override async Task OnInitializedAsync()
    {
        items = await _service.GetAllAsync(_tenant.TenantCode);
        isLoading = false;
    }
}
```

### 5. EF Core Migrations
After every module's database changes:
```bash
dotnet ef migrations add Add[ModuleName] --context TenantDbContext
dotnet ef database update
```
Or for platform DB changes:
```bash
dotnet ef migrations add Add[FeatureName] --context PlatformDbContext
dotnet ef database update
```

### 6. Enum Additions
Add new enums to the existing file (do not create new enum files unless for a
completely distinct domain):
- `Models/Enums/ManufacturingEnums.cs` — all manufacturing domain enums

### 7. CSS Standards
New component styles go in `wwwroot/css/site.css` using existing CSS variable tokens:
```css
/* Use these variables — do not hardcode colors */
--color-primary, --color-secondary, --color-success, --color-danger,
--color-warning, --color-bg, --color-surface, --color-text
```

### 8. File Uploads
All uploaded files stored under `wwwroot/uploads/{category}/{tenantCode}/`.
Max file size: 50MB (configured in `Program.cs` Kestrel limits).
Return relative URL for storage (not absolute path).

### 9. SignalR for Real-Time Updates
Use existing `MachineStateNotifier` pattern for any real-time broadcasts.
Do not create new SignalR hubs — extend the existing hub with new methods.

### 10. NavMenu Updates
After adding a new module's pages, add navigation links to `Components/Layout/NavMenu.razor`.
Group links by domain section. Use role-based visibility:
```razor
@if (_authService.IsInRole("Manager") || _authService.IsInRole("Admin"))
{
    <NavLink href="/newmodule">New Module</NavLink>
}
```

---

## New Database Models Summary (All 18 Modules)

This is the complete list of new models to be added across all phases.
Track which have been added to `TenantDbContext.cs`:

### Phase 1 Models
| Model | Module | DbSet Added? |
|-------|--------|-------------|
| `QuoteRevision` | M01 | [ ] |
| `RfqRequest` | M01 | [ ] |
| `WorkOrderComment` | M02 | [ ] |
| `WorkInstruction` | M03 | [ ] |
| `WorkInstructionStep` | M03 | [ ] |
| `WorkInstructionMedia` | M03 | [ ] |
| `WorkInstructionRevision` | M03 | [ ] |
| `OperatorFeedback` | M03 | [ ] |
| `InspectionPlan` | M05 | [ ] |
| `InspectionPlanCharacteristic` | M05 | [ ] |
| `InspectionMeasurement` | M05 | [ ] |
| `NonConformanceReport` | M05 | [ ] |
| `CorrectiveAction` | M05 | [ ] |
| `SpcDataPoint` | M05 | [ ] |
| `InventoryItem` | M06 | [ ] |
| `StockLocation` | M06 | [ ] |
| `InventoryLot` | M06 | [ ] |
| `InventoryTransaction` | M06 | [ ] |
| `MaterialRequest` | M06 | [ ] |
| `DashboardLayout` | M07 | [ ] |
| `SavedReport` | M07 | [ ] |

### Phase 2 Models
| Model | Module | DbSet Added? |
|-------|--------|-------------|
| `PartDrawing` | M08 | [ ] |
| `PartRevisionHistory` | M08 | [ ] |
| `PartNote` | M08 | [ ] |
| `CostEntry` | M09 | [ ] |
| `OverheadRate` | M09 | [ ] |
| `LaborRate` | M09 | [ ] |
| `CuttingTool` | M10 | [ ] |
| `ToolInstance` | M10 | [ ] |
| `ToolUsageLog` | M10 | [ ] |
| `ToolKit` | M10 | [ ] |
| `ToolKitItem` | M10 | [ ] |
| `Fixture` | M10 | [ ] |
| `GageEquipment` | M11 | [ ] |
| `CalibrationRecord` | M11 | [ ] |
| `MaintenanceRequest` | M11 | [ ] |
| `Vendor` | M12 | [ ] |
| `VendorItem` | M12 | [ ] |
| `PurchaseOrder` | M12 | [ ] |
| `PurchaseOrderLine` | M12 | [ ] |
| `VendorScorecard` | M12 | [ ] |
| `TimeEntry` | M13 | [ ] |
| `OperatorSkill` | M13 | [ ] |
| `ShiftDefinition` | M13 | [ ] |
| `DocumentCategory` | M14 | [ ] |
| `ControlledDocument` | M14 | [ ] |
| `DocumentRevision` | M14 | [ ] |
| `DocumentApproval` | M14 | [ ] |
| `DocumentReadRecord` | M14 | [ ] |

### Phase 3 Models
| Model | Module | DbSet Added? |
|-------|--------|-------------|
| `Shipment` | M15 | [ ] |
| `ShipmentLine` | M15 | [ ] |
| `Customer` | M16 | [ ] |
| `Contact` | M16 | [ ] |
| `CustomerActivity` | M16 | [ ] |
| `Opportunity` | M16 | [ ] |
| `ComplianceFramework` | M17 | [ ] |
| `ComplianceControl` | M17 | [ ] |
| `ComplianceSelfAssessment` | M17 | [ ] |
| `AuditAccessLog` | M17 | **PlatformDbContext** |
| `TrainingCourse` | M18 | [ ] |
| `TrainingLesson` | M18 | [ ] |
| `TrainingEnrollment` | M18 | [ ] |
| `TrainingCompletion` | M18 | [ ] |
| `KnowledgeArticle` | M18 | [ ] |

---

## New Services Summary

Track service registration in `Program.cs`:

### Phase 1 Services
| Service | Module | Registered? |
|---------|--------|------------|
| `IPricingEngineService` | M01 | [ ] |
| `IWorkInstructionService` | M03 | [ ] |
| `IOeeService` | M04 | [ ] |
| `IQualityService` | M05 | [ ] |
| `ISpcService` | M05 | [ ] |
| `IInventoryService` | M06 | [ ] |
| `IMaterialPlanningService` | M06 | [ ] |
| `IReportingService` | M07 | [ ] |

### Phase 2 Services
| Service | Module | Registered? |
|---------|--------|------------|
| `IPartFileService` | M08 | [ ] |
| `IJobCostingService` | M09 | [ ] |
| `IProfitabilityService` | M09 | [ ] |
| `IToolManagementService` | M10 | [ ] |
| `ICalibrationService` | M11 | [ ] |
| `IPurchasingService` | M12 | [ ] |
| `IVendorService` | M12 | [ ] |
| `ITimeClockService` | M13 | [ ] |
| `ILaborAnalyticsService` | M13 | [ ] |
| `IDocumentControlService` | M14 | [ ] |

### Phase 3 Services
| Service | Module | Registered? |
|---------|--------|------------|
| `IShippingService` | M15 | [ ] |
| `ICrmService` | M16 | [ ] |
| `IComplianceService` | M17 | [ ] |
| `IAuditLogService` | M17 | [ ] |
| `ITrainingService` | M18 | [ ] |
| `IKnowledgeBaseService` | M18 | [ ] |

---

## New Pages / Routes Summary

All new routes to add to the application:

### Phase 1 Routes
```
/quotes                         Quote list
/quotes/new                     New quote form
/quotes/{id}/edit               Edit quote
/quotes/{id}                    Quote detail
/quotes/rfq-inbox               RFQ inbox (admin)
/portal/rfq                     Public RFQ submission (no auth)
/workorders                     Work order list
/workorders/new                 New work order
/workorders/{id}                WO detail
/workorders/{woId}/jobs/{jobId} Job detail
/admin/work-instructions        Work instruction list
/admin/work-instructions/{id}/edit  Instruction editor
/admin/work-instructions/feedback   Feedback review
/shopfloor/instructions/{id}    Operator instruction viewer
/shopfloor                      Operator queue (kiosk entry)
/scheduler                      Gantt scheduler
/scheduler/capacity             Capacity dashboard
/quality                        Quality dashboard
/quality/ncr                    NCR management
/quality/capa                   CAPA board
/quality/spc                    SPC charts
/inventory                      Inventory dashboard
/inventory/items                Item list
/inventory/items/{id}/ledger    Stock ledger
/inventory/receive              Receiving workflow
/analytics                      Main analytics dashboard
/analytics/on-time-delivery     OTD report
/analytics/quality              Quality metrics report
/analytics/oee                  OEE dashboard
/analytics/cost                 Cost analysis
/search                         Universal search
```

### Phase 2 Routes
```
/parts                          Parts list (operational view)
/parts/{id}                     Part detail with tabs
/analytics/labor                Labor utilization dashboard
/labor/entries                  Time entries manager view
/kiosk                          Operator time clock kiosk
/admin/skills                   Skill matrix
/admin/rates                    Labor/overhead rates
/toolcrib                       Tool crib dashboard
/toolcrib/tools/{id}            Tool instance detail
/toolcrib/kits/{id}/edit        Tool kit builder
/maintenance                    Maintenance dashboard (complete)
/maintenance/workorders         Work order management
/maintenance/rules              Rule definitions
/maintenance/calibration        Calibration registry
/maintenance/request            Operator request form
/purchasing/orders              PO list
/purchasing/orders/{id}         PO detail
/purchasing/vendors             Vendor list
/documents                      Document library
/documents/{id}                 Document detail
/documents/my-acknowledgments   My pending reads
/admin/training                 Training admin
```

### Phase 3 Routes
```
/shipping                       Shipping queue
/shipping/create/{woId}         Create shipment wizard
/crm/customers                  Customer list
/crm/customers/{id}             Customer detail
/crm/pipeline                   Sales pipeline
/portal/login                   Customer portal login
/portal/dashboard               Customer portal home
/portal/orders                  Customer portal order status
/portal/certs                   Customer portal cert downloads
/compliance                     Compliance dashboard
/compliance/assess/{frameworkId} Control assessment
/admin/audit-log                Audit log viewer
/admin/security                 Security settings
/training/my                    My training dashboard
/training/courses/{id}          Course viewer
/knowledge                      Knowledge base
/knowledge/write                Write article
```

---

## Competitive Differentiators vs. ProShop

These features we are building that ProShop does NOT have:

| Differentiator | Where Built | Status |
|---------------|-------------|--------|
| API-first REST architecture | Architecture (all modules) | [ ] |
| Customer self-service portal | Module 16 (CRM) | [ ] |
| SPC control charts with Cp/Cpk | Module 05 (Quality) | [ ] |
| Operator tribal knowledge capture | Module 18 (Training/LMS) | [ ] |
| Visual instruction operator feedback loop | Module 03 (Work Instructions) | [ ] |
| Finite-capacity constraint-based scheduler | Module 04 (Shop Floor) | [ ] |
| Barcode/camera scan for inventory receiving | Module 06 (Inventory) | [ ] |
| Integrated BOL + packing list + label in one flow | Module 15 (Shipping) | [ ] |
| Multi-framework compliance engine (CMMC+AS9100+ISO) | Module 17 (Compliance) | [ ] |
| Complete audit access log | Module 17 (Compliance) | [ ] |
| Role-based LMS with in-app contextual help | Module 18 (Training/LMS) | [ ] |
| Tool lifecycle wear tracking with predictive alerts | Module 10 (Tools) | [ ] |
| Vendor scorecards (quality + delivery + cost) | Module 12 (Purchasing) | [ ] |
| Activity-based overhead allocation | Module 09 (Job Costing) | [ ] |

---

## How Copilot Should Use This Document

1. **Before starting work**: Read `MASTER_CONTEXT.md` (this file) to understand
   current state and which module to work on next.

2. **To implement a module**: Open the corresponding plan file in `docs/phase-N/`.
   Follow the steps in order. Each step specifies exact file paths and code patterns.

3. **After completing a step**: Check the checkbox in the module plan file:
   `- [ ] Step description` → `- [x] Step description`

4. **After completing a full module**: Update the status in this file's module table:
   `[ ] Not Started` → `[~] In Progress` → `[x] Complete`

5. **For database changes**: Always run EF migrations as specified at the end of
   each module's implementation steps.

6. **For cross-module work**: Check the dependency map to ensure prerequisite
   modules are complete before starting dependent ones.

7. **When in doubt about patterns**: Look at existing implementations in the
   foundation (Admin pages, MaintenanceService, etc.) and match the style.

---

## Quick Reference: Key Existing Files

| File | Purpose |
|------|---------|
| `Program.cs` | DI registration, middleware, auth, seeding |
| `Data/TenantDbContext.cs` | All tenant entity DbSets — ADD NEW ONES HERE |
| `Data/PlatformDbContext.cs` | Platform-level entities |
| `Models/Enums/ManufacturingEnums.cs` | All enums — ADD NEW ENUMS HERE |
| `Components/Layout/NavMenu.razor` | Navigation — ADD NEW LINKS HERE |
| `Components/Layout/MainLayout.razor` | Master layout |
| `wwwroot/css/site.css` | All styles — ADD NEW STYLES HERE |
| `OPCENTRIX_ARCHITECTURE_DECISIONS.md` | Original architecture decisions reference |

---

*Last Updated: March 2026*
*18 modules planned across 3 phases covering 100% of ProShop ERP feature parity + key improvements*
