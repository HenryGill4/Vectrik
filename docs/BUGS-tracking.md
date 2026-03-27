# Vectrik Bug & Issue Tracker

Last tested: 2026-03-27 (full site walkthrough as admin user)

## Critical Bugs (App Crashes / Data Loss Risk)

### BUG-001: /shopfloor operator landing page crashes with 500 error
- **Page**: `/shopfloor` (ShopFloor/Index.razor)
- **Severity**: CRITICAL
- **Reproduction**: Log in as admin user → navigate to Shop Floor
- **Error**: "An error occurred while processing your request." (500 server error)
- **Root Cause**: Likely the `OnAfterRenderAsync` or `LoadData()` method fails for non-operator users. The page tries to call `GetCurrentExecutionForOperatorAsync()` and `GetOperatorQueueAsync()` with the admin's UserId, which may throw or return unexpected results. The `_currentUiConfig` property tries to access `_currentExecution?.ProductionStage?.GetUiConfig()` which may NPE if the execution has no stage loaded.
- **Status**: [ ] Not fixed

## UI Bugs (Visual / Display Issues)

### BUG-002: Machine Detail OEE KPI shows raw C# code
- **Page**: `/machines/{id}` (Machines/Detail.razor)
- **Severity**: MEDIUM
- **Reproduction**: Navigate to any machine detail page → look at OEE (30D) KPI card
- **Symptom**: Shows `0.ToString("F1")%` as literal text instead of `0.0%`
- **Root Cause**: Razor template has `.ToString("F1")` in an expression that's being rendered as markup instead of code. Likely a missing `@()` wrapper or incorrect Razor syntax.
- **Status**: [ ] Not fixed

## Minor Issues / Polish

### ISSUE-003: Scheduler tab views (Demand, Floor, Data) don't visually change
- **Page**: `/scheduler` — Demand, Floor, Data tabs
- **Severity**: LOW
- **Note**: Clicking Demand/Floor/Data tabs highlights them but the Gantt view remains the same. May be by design (all share the Gantt viewport) or the tab content components aren't rendering their distinct views.
- **Status**: [ ] Needs investigation

### ISSUE-004: WebSocket 1006 disconnections on Azure B1
- **Context**: Blazor Server SignalR circuit drops during idle periods
- **Severity**: LOW (auto-reconnects)
- **Note**: Normal for Azure B1 App Service Plan. The branded reconnect modal handles this gracefully. Could be improved by configuring keep-alive intervals.
- **Status**: [ ] Known limitation

### ISSUE-005: Each page navigation creates new WebSocket connection
- **Context**: Console shows ~37 WebSocket connections created during testing session
- **Severity**: LOW
- **Note**: Every `forceLoad: true` navigation (login redirect) and some enhanced navigations create new circuits. This is expected Blazor Server behavior but could be optimized with connection reuse.
- **Status**: [ ] Known limitation

## Pages Tested (Full Results)

| Page | Route | Status | Notes |
|------|-------|--------|-------|
| Login | /account/login | ✅ OK | Branded animation, all users login correctly |
| Dashboard | /dashboard | ✅ OK | KPIs, pipeline, quick nav all loading |
| Parts Library | /parts | ✅ OK | 3 parts, tabs, search, edit/view buttons |
| Part Detail | /parts/1 | ✅ OK | Overview, process, tabs all working |
| Part Edit | /parts/1/edit | ✅ OK | Form loads, all tabs present |
| Work Orders | /workorders | ✅ OK | 10 WOs, filters, fulfillment bars |
| WO Detail | /workorders/7 | ✅ OK | Header, lines, routing, sign-offs |
| New Work Order | /workorders/new | ✅ OK | Form loads, line items section |
| Scheduler Gantt | /scheduler | ✅ OK | Gantt renders, builds shown, KPIs correct |
| Scheduler Wizard | + New → SLS Build | ✅ OK | All 3 steps work (machine, program, schedule) |
| Auto-Schedule | Auto-Schedule button | ✅ OK | Runs and updates KPIs |
| Shop Floor Landing | /shopfloor | 🔴 BUG | 500 error (BUG-001) |
| Stage: SLS Printing | /shopfloor/stage/sls-printing | ✅ OK | Queue/Active/History, batch select |
| Stage: CNC Machining | /shopfloor/stage/cnc-machining | ✅ OK | Queue (11), History (14) |
| Quotes | /quotes | ✅ OK | Empty state with CTA |
| New Quote | /quotes/new | ✅ OK | Form loads |
| Analytics | /analytics | ✅ OK | KPIs, charts rendering |
| Analytics OTD | /analytics/on-time-delivery | ✅ OK | |
| Analytics OEE | /analytics/oee | ✅ OK | |
| Machines | /machines | ✅ OK | 19 machines, filters, cards |
| Machine Detail | /machines/1 | 🟡 BUG | OEE KPI shows raw code (BUG-002) |
| Inventory | /inventory | ✅ OK | Module gate works correctly |
| Quality | /quality | ✅ OK | Module gate works correctly |
| Part Tracker | /tracking | ✅ OK | Search interface loads |
| Machine Programs | /programs | ✅ OK | 27 programs, KPIs, setup alert |
| Search | /search | ✅ OK | |
| Reports | /reports | ✅ OK | |
| Maintenance | /maintenance | ✅ OK | |
| Capacity Dashboard | /scheduler/capacity | ✅ OK | |
| Admin: Stages | /admin/stages | ✅ OK | All stages, edit modal with 5 tabs |
| Admin: Users | /admin/users | ✅ OK | 6 users, permissions section |
| Admin: Branding | /admin/branding | ✅ OK | Icon color picker visible |
| Admin: Settings | /admin/settings | ✅ OK | 56 settings across categories |
| Admin: Features | /admin/features | ✅ OK | Feature flag toggles |
| Admin: Materials | /admin/materials | ✅ OK | |
| Admin: Numbering | /admin/numbering | ✅ OK | |
| Admin: Shifts | /admin/shifts | ✅ OK | |
| Admin: Work Instructions | /admin/work-instructions | ✅ OK | |
| Admin: Approaches | /admin/manufacturing-approaches | ✅ OK | |
| Admin: Custom Fields | /admin/custom-fields | ✅ OK | |
| Admin: Operation Costs | /admin/operation-costs | ✅ OK | |

## Summary
- **Total pages tested**: 38
- **Passing**: 35
- **Critical bugs**: 1 (shop floor landing)
- **UI bugs**: 1 (machine OEE display)
- **Minor issues**: 3 (scheduler tabs, WebSocket drops, connection count)
