# Full Scheduler System Review — March 2026

> Tested by Claude (Opus 4.6), stepping through every scheduler feature as an employee managing two EOS M4 Onyx DMLS printers with six active work orders. Previous feedback in `FEEDBACK-scheduler-wizard.md` is incorporated; P0/P1 items from that doc have been fixed.

---

## Executive Summary

The scheduler is feature-rich and demonstrates deep domain knowledge of SLS manufacturing. The six views (Production/Gantt, Demand, Programs, Floor, Dispatch, Data) cover different decision-making needs well. The Next Build Advisor wizard is the standout feature — it automates the hard work of slot-finding and changeover analysis.

However, the system suffers from **information fragmentation**: a production manager must jump between 6+ views to answer basic questions like "can I hit my deadlines?" The Demand view only shows 1 of 6 active work orders. The Gantt shows builds but not downstream bottlenecks. The Floor view shows bottlenecks but not the schedule. No single view answers: **"What do I need to do today, and what's at risk?"**

---

## Critical Issues (P0-P1)

### 1. Demand View Only Shows 1 Work Order (P1)
The Demand view says "6 ACTIVE ORDERS" and "420 PARTS OUTSTANDING" but only renders WO-00011 (Apex Industries). The other 5 active work orders are invisible. A production manager using this view to prioritize would miss 5 orders entirely.

**Root cause**: Likely a filter or rendering bug — the view may only show orders with unscheduled demand, or there's a pagination issue.

**Fix**: Show all active work orders. Group by urgency: Overdue > Due This Week > Due Next Week > Later. Each should show fulfillment progress.

### 2. Build Bar Tooltip Cannot Be Dismissed (P1)
Clicking a scheduled build bar on the Gantt opens a tooltip/popover showing build details. The X button on this popover does not close it. Clicking elsewhere doesn't close it either. The tooltip persists until the page is navigated away.

**Root cause**: The X button's click handler fires but the Blazor component state doesn't update, or there's a z-index/event propagation issue.

**Fix**: Add a proper close mechanism — click-outside-to-close, Escape key, and ensure the X button works.

### 3. Work Order Create Fails Silently (P1)
Creating a new work order with all fields filled shows a generic EF Core error ("An error occurred while saving the entity changes") with no field-level validation feedback. The error is displayed but doesn't identify which field or constraint failed.

**Root cause**: The `Save()` method catches the EF exception but only shows the outer message. Inner exception likely reveals a missing required field (e.g., `CreatedBy` not being set from the auth context, or a foreign key constraint).

**Fix**:
- Show field-level validation errors before attempting save
- Log and display the inner exception message in development
- Ensure `CreatedBy`/`LastModifiedBy` are set from the authenticated user, not from "System"

---

## UX Issues (P2)

### 4. KPI Strip Shows 0% Utilization After Auto-Schedule
After auto-scheduling 6 programs (954h of work), the KPI strip still shows "0% UTILIZATION" and "0 IN PROGRESS". This is because utilization is calculated from currently-running work, not from scheduled work. But from a manager's perspective, seeing 954h scheduled with 0% utilization is confusing — it looks like nothing is happening.

**Fix**: Show two metrics: "Current Utilization" (active right now) and "Scheduled Utilization" (capacity committed for the visible time range). Or show utilization as a percentage of available hours in the date range.

### 5. Gantt Machine Labels Are Truncated
Machine names like "EOS M4 Onyx #1" are truncated to "EOS M4 Onyx ..." in the Gantt resource labels. The machine name is critical — operators need to know which physical machine to walk to. Similarly "Incineris Depo..." truncates the depowdering station name.

**Fix**: Either widen the label column, use a two-line layout (machine name on top, type/status below), or show full name on hover.

### 6. No "Today" Focus in Any View
When opening the scheduler, the Gantt shows the current week (Mar 25-29) but there's no clear indication of what needs attention TODAY. The "0 DUE TODAY" KPI is good but there's no drill-down. The Floor view shows queue sizes but not today's specific tasks.

**Fix**: Add a "Today's Actions" summary at the top of the Gantt or as a first-class view: what builds are currently printing, what changeovers are needed today, what downstream work is ready to start.

### 7. Dispatch View Is the Best View but Buried
The Dispatch view is the most actionable view — it shows machine availability, recommended next builds, changeover warnings, and "Schedule This Build" buttons. But it's the 5th tab. A production manager's daily workflow starts with dispatch decisions.

**Fix**: Consider making Dispatch the default view, or merge its "Recommended Next Builds" into the Production/Gantt view as a sidebar.

### 8. No Connection Between Gantt Builds and Work Orders
The Gantt shows builds like "Bracket 96x Dou..." and "Manifold 24x Build" but doesn't show which work orders they fulfill. Clicking a build shows parts and timing but not "this fulfills WO-00011 for Apex Industries (due Apr 9, RUSH)". The customer context is completely missing from the Gantt.

**Fix**: Add work order references to the build tooltip and/or color-code builds by customer or due-date urgency.

### 9. Downstream Stages Not Visible on Gantt After Auto-Schedule
After auto-scheduling 6 SLS programs, the post-process machines (depowdering, heat treatment, wire EDM, CNC) show bars but the FINISHING, QUALITY, and SHIPPING rows are empty. The scheduler auto-schedules downstream stages for SLS builds, but the Gantt doesn't clearly show the full production chain.

**Fix**: Show downstream stage executions on the Gantt timeline, even if they're estimated. This helps identify cascade bottlenecks early.

### 10. Gantt Scale Too Zoomed Out by Default
At 6.0 px/hr, one day is about 144px — build bars of 18-27 hours are very small and text is truncated. Short operations (<4h) on post-process machines are barely visible colored slivers. The user has to zoom in to read any build details.

**Fix**: Default to a higher pixels-per-hour (e.g., 10-12 px/hr) for the SLS section, or auto-fit zoom to show the next 3-5 days at readable size.

---

## Architecture Issues (P2-P3)

### 11. Index.razor Is a God Component (~640 lines)
`Scheduler/Index.razor` manages all state for 6 views, 3 modals, auto-refresh, viewport calculations, and data loading. It injects 12 services. Every view change triggers a full data reload. This makes the component:
- Hard to maintain (any change could break another view)
- Slow to re-render (all views share the same render cycle)
- Memory-heavy (all data for all views is loaded simultaneously)

**Fix**: Extract shared state into a `SchedulerStateService` (scoped per circuit). Each view loads only its own data on activation. The Index component becomes a thin shell routing between views.

### 12. Full Data Reload on Every Tab Switch
Switching between views (gantt → demand → programs) triggers `LoadData()` which re-queries everything from the database: executions, machines, stages, programs, work orders, shifts, and bottleneck reports. This is ~10 queries per tab switch.

**Fix**: Cache data in the state service with staleness checks. Only reload data that's specific to the new view. Use `_tabLastLoaded` dictionary (which exists but is underused) to skip redundant loads.

### 13. Auto-Schedule Creates Duplicate Keys
PR #16 and #17 both fixed duplicate key crashes in auto-schedule. The underlying issue is that auto-scheduling multiple programs in sequence can create stage executions with conflicting keys when programs share parts across work orders.

**Fix**: The auto-schedule loop should batch-validate uniqueness before committing, or use a transaction with conflict resolution.

### 14. Depowdering Shows as 342h Bottleneck — Is This Real?
The Floor view flags Depowdering as a bottleneck with 342.1 hours queued against 24h daily capacity. But depowdering is a build-level operation (one per build, not one per part). With 6-8 builds queued, even at 4h per depowder cycle, that's only 24-32h. The 342h number seems inflated — it may be counting part-level executions instead of build-level ones.

**Fix**: Verify the bottleneck calculation distinguishes build-level vs part-level stages. A build with 144 parts should count as 1 depowder operation, not 144.

---

## Feature Gaps (P3)

### 15. No Drag-and-Drop Rescheduling Feedback
The Gantt has JS interop for drag-and-drop bar movement, but there's no visual feedback during the drag (no ghost bar, no snap-to guides, no validity indication). If a user drags a build to an invalid time, there's no preview of the conflict.

### 16. No What-If Scenario Mode
There's no way to explore schedule changes without committing them. A manager wants to ask "what if I prioritize WO-00011 over WO-00010?" without actually rescheduling.

### 17. No Operator Assignment Visibility
The scheduler checks operator availability for changeovers but doesn't show who is assigned to what. There's no way to see "John is on shift 6AM-6PM on the SLS machines, Sarah handles CNC."

### 18. No Material/Powder Tracking Integration
The build tooltip shows "Powder: 11.2 kg" but there's no integration with inventory to verify powder is available. Scheduling a build when powder stock is low is a real-world problem.

### 19. No Print Queue Reordering
The machine queue badges (e.g., "4 QUEUED") indicate pending builds but there's no way to reorder the queue from the Gantt. Dragging within the same machine lane should reorder the queue.

---

## What Works Well

1. **Next Build Advisor** — The 2-step wizard (Select Program → Review & Schedule) is well-designed. Machine status, demand ranking by priority/due-date, part breakdown, changeover safety analysis, cost estimate, and downstream auto-scheduling are all excellent.

2. **Auto-Schedule** — One-click scheduling of 6 programs with intelligent machine assignment and changeover analysis is a killer feature.

3. **Changeover Conflict Detection** — The hazard stripes on bars with changeover conflicts, and the warning text in Dispatch, are clear and actionable.

4. **Floor View Bottleneck Detection** — Flagging depowdering as a bottleneck with hours-queued vs daily-capacity is exactly the kind of operational insight managers need.

5. **Programs Pipeline** — The Draft → Ready → Scheduled → Printing → Post-Print pipeline with counts is a clean status tracker.

6. **Data View with CSV Export** — The table of all stage executions with filtering and CSV export is essential for reporting.

7. **Dispatch View Machine Availability** — Showing next available slot, queue count, and recommended builds per machine is the fastest path to action.

8. **Build Bar Tooltip Detail** — Parts count, print time, start/end, changeover status, and powder weight in one click is very informative.

---

## Refactoring Priority

Based on impact to daily usability:

| # | Change | Impact | Effort |
|---|--------|--------|--------|
| 1 | Fix Demand view to show all work orders | High | Low |
| 2 | Fix tooltip dismiss on Gantt | High | Low |
| 3 | Add work order context to build tooltips | High | Medium |
| 4 | Make Dispatch the default or merge into Gantt | High | Medium |
| 5 | Fix KPI utilization to include scheduled work | Medium | Low |
| 6 | Widen Gantt machine labels / show on hover | Medium | Low |
| 7 | Add "Today's Actions" summary | Medium | Medium |
| 8 | Extract SchedulerStateService from Index.razor | Medium | High |
| 9 | Optimize tab-switch data loading | Medium | Medium |
| 10 | Verify bottleneck calculations | Medium | Medium |
