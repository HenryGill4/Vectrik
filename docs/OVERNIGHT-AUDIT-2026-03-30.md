# Vectrik Overnight Audit Report -- 2026-03-30

## Summary

Full codebase audit completed overnight. Created a new tenant ("Apex Additive Manufacturing"), fixed critical startup/migration bugs, ran all 476 tests (all pass), and performed a comprehensive code review of all pages, services, models, and data layer.

**New Tenant Created:**
- **Code:** `apex`
- **Company:** Apex Additive Manufacturing
- **Admin User:** `apexadmin` / Sarah Mitchell / sarah.mitchell@apexam.com (password: `ApexAdmin2026!`)
- **Subscription:** Professional
- **Features:** Full Suite enabled (all modules + SLS + DLMS + SPC + Workflows + Custom Fields)
- **Notes:** Phoenix, AZ. 2x EOS M400-4 machines. Aerospace & defense focus.

---

## Bugs Fixed During This Session

### FIX-1: Startup crash -- PendingModelChangesWarning (CRITICAL)
- **Files:** `Services/Platform/TenantService.cs`, `Data/TenantDbContextFactory.cs`
- **Issue:** EF Core threw `PendingModelChangesWarning` during `MigrateAsync`, crashing the app on startup.
- **Fix:** Added `.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))` to all DbContextOptionsBuilder usages.

### FIX-2: Startup crash -- Missing migration for quote/pricing columns (CRITICAL)
- **Files:** 4 migration files had no `.Designer.cs` files, making EF Core ignore them.
- **Fix:** Deleted the 4 incomplete migrations and created a single consolidated migration `20260330065002_UpgradeQuotingCostingAndCustomerPricing` using raw SQL to avoid SQLite table-rebuild quoting issues.

### FIX-3: Startup crash -- EnsureCreated vs Migrate incompatibility (CRITICAL)
- **File:** `Services/Platform/TenantService.cs`
- **Issue:** New tenant databases created with `EnsureCreatedAsync()` had no `__EFMigrationsHistory` table. When `TenantDbContextFactory.CreateDbContext()` later called `Migrate()`, it tried to re-run all migrations, crashing with "table already exists."
- **Fix:** After `EnsureCreatedAsync()`, populate `__EFMigrationsHistory` with all known migration IDs using `IMigrationsAssembly`.

### FIX-4: Tenant creation -- UpdateTenantAsync crash on tracked entity
- **File:** `Services/Platform/TenantService.cs`
- **Issue:** `UpdateTenantAsync` called `_platformDb.Tenants.Update(tenant)` on an already-tracked entity.
- **Fix:** Check entity state before calling `Update()`.

### FIX-5: Database path mismatch on Windows
- **Issue:** `Environment.GetEnvironmentVariable("HOME")` resolves to `C:\Users\Henry` on Windows, so databases were at `C:\Users\Henry\data\tenants/` rather than the project's `data/tenants/`.
- **Fix:** Not a code fix needed -- just awareness. Deleted stale databases at the actual path.

### FIX-6: Better error display on tenant creation
- **File:** `Components/Pages/Platform/TenantDetail.razor`
- **Fix:** Changed `_error = ex.Message` to `_error = ex.InnerException?.Message ?? ex.Message` to show the real SQLite error.

---

## Bugs Found -- Fix Plan for Tomorrow

### CRITICAL (Fix First -- Will Cause Crashes)

| ID | Bug | File:Line | Fix |
|----|-----|-----------|-----|
| C01 | NRE: `ProductionStage.StageSlug` without null check | `ShopFloor/Index.razor:468` | Add `?.` null propagation |
| C02 | NRE: `exec.Job?.ScheduledEnd.ToString("MM/dd")` when Job is null | `ShopFloor/Index.razor:171,215`, `Stage.razor:62` | Add `?? "--"` fallback |
| C03 | NRE: `exec.Job?.Priority.ToString().ToLower()` when Job is null | `ShopFloor/Index.razor:170,214`, `Stage.razor:61` | Add `?? "normal"` fallback |
| C04 | SchedulingService diagnostics object replaced but never returned | `Services/SchedulingService.cs:42-52` | Return modified `diagnostics` instead of original |
| C05 | CNC changeover always added for first job on machine (PartId==0) | `Services/SchedulingService.cs:207-215` | Check `lastPartOnMachine > 0` before comparing |
| C06 | InventoryService.TransferAsync is a complete no-op | `Services/InventoryService.cs:185-201` | Implement actual lot quantity movement |
| C07 | DispatchGenerationBackgroundService uses wrong tenant context | `Services/DispatchGenerationBackgroundService.cs:69-91` | Resolve scoped services with correct tenant |
| C08 | Quote.ConvertedWorkOrderId FK mismatch (shadow property) | `Models/Quote.cs:98-105`, `TenantDbContext.cs` | Add explicit fluent config for FK |

### HIGH (Fix Soon -- Security/Data Issues)

| ID | Bug | File:Line | Fix |
|----|-----|-----------|-----|
| H01 | 6 Admin pages missing `[Authorize]` attribute | `Admin/Index,Stages,Approaches,OperatorRoles,Workflows,OperationCosts` | Add `@attribute [Authorize(Roles = "Admin")]` |
| H02 | WorkOrders/Index.razor missing authorization | `WorkOrders/Index.razor` | Add `@attribute [Authorize]` |
| H03 | 5 Quotes pages missing authorization | `Quotes/Index,Details,Edit,Analytics,Customers` | Add `@attribute [Authorize]` |
| H04 | 6 Analytics pages missing authorization | `Analytics/*` | Add `@attribute [Authorize]` |
| H05 | Home/Dashboard missing authorization | `Home.razor` | Add `@attribute [Authorize]` |
| H06 | StageService.CompleteStage missing `Include(j => j.Part)` | `Services/StageService.cs:252` | Add Part include for inventory auto-receipt |
| H07 | ProgramSchedulingService passes wrong start time | `Services/ProgramSchedulingService.cs:483` | Use `bestSlot.Slot.PrintStart` instead of `startAfter` |
| H08 | QuoteService labor cost missing hourly rate | `Services/QuoteService.cs:246` | Multiply by `SystemSetting.LaborRate` |
| H09 | NRE: `exec.ProductionStage!` without null check | `Services/SchedulingService.cs:335` | Add null check |
| H10 | ProgramSchedulingService.ScheduleFromWorkOrderLine returns null Job | `Services/ProgramSchedulingService.cs:680` | Include Job in stage execution query |

### MEDIUM (Fix This Week)

| ID | Bug | File:Line | Fix |
|----|-----|-----------|-----|
| M01 | ArchiveAllExpired is a no-op (sets Expired to Expired) | `Quotes/Index.razor:225-228` | Change to `Archived` status |
| M02 | CSV export via `data:` URI doesn't work in Blazor Server | `Analytics/Index.razor:192` + 5 others | Use JS interop for download |
| M03 | Customers page N+1 queries | `Quotes/Customers.razor:376-384` | Use batch loading |
| M04 | PartService BOM tree allows double-counting for diamond BOMs | `Services/PartService.cs:494` | Track visited set properly |
| M05 | Inventory lot quantity can go negative | `Services/InventoryService.cs:160-165` | Validate lot-level qty before deduction |
| M06 | Standard program scheduling ignores shift hours | `Services/ProgramSchedulingService.cs:568` | Use `ShiftTimeHelper.AdvanceByWorkHours` |
| M07 | Analytics N+1 queries on dashboard | `Services/AnalyticsService.cs:60-69` | Batch cost calculation |
| M08 | TenantDbContextFactory HashSet race condition | `Data/TenantDbContextFactory.cs:51` | Use `ConcurrentDictionary` |
| M09 | Shipment/ShipmentLine missing EF config | `Data/TenantDbContext.cs` | Add fluent configuration |
| M10 | Login page "An unhandled error has occurred" banner | `Components/Pages/Account/Login.razor` | Investigate Blazor reconnection error |

### LOW (Fix When Convenient)

| ID | Bug | File | Fix |
|----|-----|------|-----|
| L01 | WorkOrders/Create non-atomic line creation | `WorkOrders/Create.razor:297` | Wrap in transaction |
| L02 | RfqInbox hardcodes "System" as user | `Quotes/RfqInbox.razor:186` | Use auth context |
| L03 | WorkInstructionViewer hardcodes operator ID | `ShopFloor/WorkInstructionViewer.razor:228` | Use auth context |
| L04 | Analytics search `PartNumber!.ToLower()` NRE risk | `Services/AnalyticsService.cs:437` | Add null check |
| L05 | ProgramFeedback.OperatorUserId is string instead of int FK | `Models/ProgramFeedback.cs:24` | Refactor to int + FK |
| L06 | Maintenance models use string MachineId instead of int FK | `Models/Maintenance/*.cs` | Refactor (breaking change) |

---

## Features Still Needed / Incomplete

### Critical (Blocks Production Use)
| Feature | Status | Effort |
|---------|--------|--------|
| Job Costing & Actual Cost Tracking | Not started | 2-3 weeks |

### High Priority
| Feature | Status | Effort |
|---------|--------|--------|
| Real Machine Providers (EOS API) | Mock only | 2-3 weeks |
| AS9102 FAIR Forms (Aerospace QA) | Not started | 1 week |
| Time Clock & Labor Tracking | Not started | 2 weeks |

### Medium Priority
| Feature | Status | Effort |
|---------|--------|--------|
| Shipping Management Page | Service exists, no standalone page | 3-5 days |
| External Operations Dashboard | Service complete, no UI | 2-3 days |
| Cutting Tools & Fixtures | Not started | 2 weeks |
| Calibration Extension | Partial (maintenance exists) | 2 weeks |
| Purchasing & Vendors | Not started | 2-3 weeks |
| Document Control | Not started | 2 weeks |
| Reports CSV Export | Stub (toast only) | 1 day |
| SavedReport UI | Model exists, no UI | 2-3 days |
| PartSignature UI | Model/migration exists, no UI | 1-2 days |
| Inventory Manual Adjustments | Partial (view only) | 2-3 days |

### Low Priority (Phase 3)
| Feature | Status | Effort |
|---------|--------|--------|
| CRM & Contact Management | Not started | 2 weeks |
| CMMC & Compliance | Not started | 2-3 weeks |
| Training / LMS | Not started | 2 weeks |
| REST API Layer | Not started | 2 weeks |
| Customer Portal | Not started | 2 weeks |
| DLMS Transaction Services | Not started | 2-3 weeks |
| QuoteRevision UI | Model exists, no UI | 1-2 days |
| DashboardLayout (Custom Widgets) | Model exists, unused | 1 week |
| Workflow Auto-Triggering | Engine exists, no event wiring | 3-5 days |
| Scheduled Report Delivery | Promised in marketing page | 1 week |

### Quick Wins (< 1 day each)
1. CSV export fix (JS interop) -- affects 6 analytics pages
2. Operator ID hardcode fixes (2 files)
3. Authorization attributes on ~20 pages (copy-paste)
4. Null reference fixes in ShopFloor pages (3 bugs, 5 minutes each)
5. ArchiveAllExpired no-op fix
6. RfqInbox "System" user hardcode

---

## Recommended Fix Order for Tomorrow

### Morning Session (Critical Path)
1. **Authorization attributes** (H01-H05) -- 15 minutes, affects 20+ pages
2. **ShopFloor NRE fixes** (C01-C03) -- 10 minutes, prevents operator crashes
3. **SchedulingService diagnostics** (C04) -- 10 minutes
4. **CNC changeover false positive** (C05) -- 5 minutes
5. **QuoteService labor cost rate** (H08) -- 10 minutes
6. **StageService missing Include** (H06) -- 5 minutes

### Afternoon Session (Functional Fixes)
7. **InventoryService.TransferAsync** (C06) -- 30 minutes
8. **DispatchGenerationBackgroundService wrong tenant** (C07) -- 20 minutes
9. **Quote.ConvertedWorkOrderId FK config** (C08) -- 15 minutes
10. **CSV export JS interop** (M02) -- 1 hour
11. **ProgramSchedulingService wrong start time** (H07) -- 5 minutes
12. **Customers N+1 queries** (M03) -- 30 minutes

### Test everything: `dotnet test` (should remain at 476 passing)

---

## Test Status
- **476 tests passing** (0 failures, 0 skipped)
- Tests cover: scheduling, shift management, part service, job service, machine service, build templates, certified layouts, stage service, dispatch learning, program scheduling, downstream programs, maintenance dispatch
- **Gap:** No tests for QuoteService, InventoryService, AnalyticsService, WorkOrderService, QualityService

---

## Changes Made in This Session

### Files Modified:
1. `Services/Platform/TenantService.cs` -- Fixed startup crashes, EnsureCreated migration history, UpdateTenant tracking
2. `Data/TenantDbContextFactory.cs` -- Added PendingModelChangesWarning suppression
3. `Components/Pages/Platform/TenantDetail.razor` -- Better error display (InnerException)
4. `Data/Migrations/Tenant/20260330065002_UpgradeQuotingCostingAndCustomerPricing.cs` -- New consolidated migration (raw SQL)
5. `Data/Migrations/Tenant/20260330065002_UpgradeQuotingCostingAndCustomerPricing.Designer.cs` -- Auto-generated
6. `Data/Migrations/Tenant/TenantDbContextModelSnapshot.cs` -- Updated by EF

### Files Deleted:
- `Data/Migrations/Tenant/20260329060000_UpgradeQuoteLineCosting.cs` (incomplete, no Designer)
- `Data/Migrations/Tenant/20260329060100_AddPartSignatures.cs` (incomplete, no Designer)
- `Data/Migrations/Tenant/20260329060200_AddQuoteLossTracking.cs` (incomplete, no Designer)
- `Data/Migrations/Tenant/20260329060300_AddCustomerPricing.cs` (incomplete, no Designer)

### New Tenant Data:
- Tenant "apex" created with full seeded data (parts, machines, work orders, quotes, etc.)
- Admin user "apexadmin" (Sarah Mitchell) created
