# Vectrik Demo Polish Roadmap

Audit performed 2026-03-29 against fresh seed data on all 84 routes.

---

## BUGS FOUND DURING AUDIT

Issues discovered while testing seed data on every page. Fix these first.

| # | Page | Bug | Root Cause | Fix |
|---|------|-----|------------|-----|
| BUG-1 | **Quotes Index** | REV column rendered literal text `R@q.RevisionNumber` | Razor expression not wrapped in `@()` | **FIXED** — changed to `R@(q.RevisionNumber)` in `Components/Pages/Quotes/Index.razor:78` |
| BUG-2 | **On-Time Delivery** | Shows 0% OTD — all 4 completed WOs display as "LATE" with `Completed = 2026-03-29` (today) | OTD page uses `DateTime.UtcNow` (or WO `LastModifiedDate`) as the completion date instead of the actual last-job-completed date. WOs have no explicit `CompletedAt` timestamp. | Add `CompletedAt` field to WorkOrder model, set it when status transitions to Complete. OTD page should use `CompletedAt` instead of today's date. Alternatively, derive completion from `MAX(StageExecution.ActualEndAt)` across all jobs for the WO. |
| BUG-3 | **Capacity Dashboard** | All machines show 0% utilization / Idle | The capacity query filters by date range but completed stage executions' `ScheduledStartAt`/`ScheduledEndAt` from older builds fall outside the 7-day default window. Recent builds (compressed to -6d) should now show, but the query may also require `ActualStartAt`/`ActualEndAt` to be set within the window. | Check `Capacity.razor` query — ensure it includes both scheduled AND actual times in the date range filter. The recent completed builds now fall within the lookback window so this may already be partially fixed. |
| BUG-4 | **Scheduler KPI "Committed"** | Shows 5% — should reflect a more realistic percentage of demand covered by scheduled builds | The committed % is calculated as `(scheduled build parts) / (total WO demand)`. With 6 new scheduled builds this should be higher now, but the denominator includes all released WO demand. | Verify the committed % formula. It may need to include completed builds' produced quantities in the numerator, not just scheduled ones. |

---

## PRIORITY 0 — Seed Data Gaps (Demo Blockers)

These make the demo feel empty on key screens. Fix before any demo.

| # | Item | Current State | What's Needed |
|---|------|---------------|---------------|
| ~~S-1~~ | ~~**Gantt timeline looks empty**~~ | **FIXED** — Compressed recent builds to -6d through -2d on both machines, added 6 scheduled future builds (3 per machine). Gantt now shows dense back-to-back bars across the full viewport. | ~~Done~~ |
| S-2 | **On-Time Delivery shows 0%** | OTD page code bug — uses today as WO completion date (see BUG-2 above). Seed data due dates were adjusted but the page logic is the real issue. | Fix the OTD page to derive completion date from job/stage execution actual end times rather than `DateTime.UtcNow`. |
| S-3 | **Capacity Dashboard shows 0% utilization** | Machines show Idle / 0.0h — likely a query date range issue (see BUG-3). Recent builds are now within the 7-day window but the query may not pick them up. | Debug the capacity query in `Capacity.razor` — check whether it reads `ActualStartAt`/`ActualEndAt` or `ScheduledStartAt`/`ScheduledEndAt`. |
| S-4 | **Production Batches empty** | "No production batches found" | Seed 3-5 ProductionBatch records for completed builds that went through plate release |
| S-5 | **Certified Layouts empty** | "No certified layouts found" | Seed 4-6 CertifiedLayout records matching the master build plate programs (Tinman 56x, Handyman 64x, etc.) |
| S-6 | **Workflows empty** | "No workflows configured" | Seed 2-3 workflow definitions: WO Release Approval, Quote Acceptance, NCR Disposition |
| S-7 | **Maintenance Rules empty** | "All rules within limits" | Seed 3-4 maintenance rules: SLS laser hours, build plate resurfacing interval, EDM wire spool tracking |

---

## PRIORITY 1 — Features to Build Fully

Pages that either don't exist yet or are stubs that need complete implementation.

| # | Feature | Current State | Scope |
|---|---------|---------------|-------|
| F-1 | **Shipments Module** | Shipment model + DB exists, seed data populated, but no listing page or creation flow | Build `/shipments` index page (list all shipments with status/tracking), shipment detail page, "Create Shipment" flow from WO detail, shipment line editing, carrier + tracking number entry |
| F-2 | **Ship from Work Order** | WO detail shows "shipped" quantity but no button to initiate shipping | Add "Ship" action button on completed WO lines, modal to select carrier/tracking/qty, auto-create Shipment + ShipmentLine records, update WO line ShippedQuantity |
| F-3 | **Certificate of Conformance (CoC)** | `shipping.require_coc` setting exists but no generation logic | Build CoC document template, auto-generate from shipment data (parts, serials, material certs, inspection results), PDF output via document template system |
| F-4 | **Customer Entity / CRM** | Customers are freetext strings on WOs/Quotes — no deduplicated customer record | Create Customer model (name, contacts, addresses, PO history), link to WOs/Quotes, customer detail page with order history, contact management |
| F-5 | **Purchase Orders** | Inventory receiving exists but no PO creation or vendor management | Build PO create/edit pages, vendor model, PO approval workflow, receiving against PO, PO line items linked to inventory items |
| F-6 | **PDF Document Generation** | "Print Quote" / "Print Traveler" buttons exist, DocumentTemplate records seeded, but output is placeholder | Wire DocumentTemplate rendering into actual PDF generation (html-to-pdf via Playwright or wkhtmltopdf), support template variable substitution, print/download buttons |
| F-7 | **Certified Layout Editor** | CertifiedLayout model exists, admin page shows list but no visual editor | Build 2D plate layout visualization (parts positioned on 450x450mm grid), drag-and-drop part placement, link to build programs, revision tracking |
| F-8 | **Workflow Engine Execution** | WorkflowDefinition/Step/Instance models exist, admin page allows creation but no runtime execution | Build workflow trigger system (auto-trigger on WO status change, quote acceptance), approval step UI, notification to approvers, step completion tracking |

---

## PRIORITY 2 — Mobile & Tablet Responsiveness

The app is desktop-first. These items are needed for operators using tablets on the shop floor and managers checking status on phones.

| # | Area | Issue | Fix |
|---|------|-------|-----|
| M-1 | **All data tables** (WOs, Quotes, NCRs, Parts, Inventory Items, Programs) | Tables clip right-side columns on mobile (status, price, fulfillment hidden); horizontal scroll works but isn't discoverable | Add responsive card-view mode at <768px — each row renders as a stacked card showing key fields. Add a toggle icon (table/card). Hide low-priority columns at breakpoints. |
| M-2 | **Scheduler Gantt** | Gantt is unusable on phones (too narrow to see bars, no touch gestures) | Add touch gesture support: pinch-to-zoom, horizontal swipe to pan, tap-to-select bars. Consider a simplified "list view" for mobile that shows build queue as cards instead of timeline. |
| M-3 | **Tab strips** (Part detail has 9 tabs, WO detail has 3+, Scheduler has 4) | Tab bars overflow on mobile — text truncates, some tabs hidden off-screen | Make all tab strips horizontally scrollable with CSS `overflow-x: auto`. Add scroll indicators (fade edges). On <480px consider collapsing to a dropdown select. |
| M-4 | **Modal dialogs** (Reschedule, Schedule Wizard, Next Build Advisor, NCR form, CAPA form) | Modals use fixed widths that overflow on mobile | Add `max-width: 95vw; max-height: 90vh` to `.modal-content`. Use `overflow-y: auto` for body. Stack form fields vertically at mobile breakpoints. |
| M-5 | **CAPA Board kanban** | Cards are too narrow on mobile — "CorrectiveRyan Cole" text runs together, board doesn't scroll horizontally | At mobile breakpoint, switch kanban to vertical accordion (one column at a time) or make columns horizontally scrollable. Increase card min-width. |
| M-6 | **Quote detail financial cards** | Two-column card layout (Customer + Financials) doesn't stack on mobile | Add `flex-wrap: wrap` to the card container so cards stack to single column at <768px |
| M-7 | **Shop Floor stage views** | Already decent on mobile but "Claim & Start" button clipped on Available Work table | Ensure action buttons are visible — either pin to right or put them below the row on mobile |
| M-8 | **Nav sidebar section labels** | "Accepted (" truncates on Quotes tab strip; long nav section names clip | Add `text-overflow: ellipsis` and `white-space: nowrap` to tab buttons. Add title attributes for tooltip on truncated labels. |
| M-9 | **Form inputs on mobile** | Date pickers, select dropdowns, and text inputs don't use full width on mobile | Ensure all `.form-control` and `.form-group` elements go full-width (`width: 100%`) at mobile breakpoints |
| M-10 | **Print/Export buttons** | "Print Quote", "Print Traveler", "Export" buttons are desktop-oriented | On mobile, these should either trigger a share sheet or be hidden behind a "..." overflow menu |

---

## PRIORITY 3 — Nice-to-Have Polish

Not blockers but would make the demo significantly more impressive.

| # | Item | Description |
|---|------|-------------|
| P-1 | **Dashboard recent activity feed** | Add a "Recent Activity" card to the dashboard showing last 10 actions (build completed, WO shipped, NCR filed, quote sent) with timestamps and user names |
| P-2 | **Gantt drag-and-drop on tablet** | Extend the existing desktop drag-to-reschedule to work with touch events |
| P-3 | **Dark/light mode toggle** | Display settings page exists, add a working theme toggle that persists |
| P-4 | **Notification bell** | Add a notification dropdown in the header — show unread items (overdue WOs, NCRs pending, dispatch alerts) |
| P-5 | **Machine detail OEE widget** | Machine detail page should show a live OEE gauge for SLS machines (availability x performance x quality) |
| P-6 | **Quote → WO conversion wizard** | Currently `ConvertedWorkOrderId` is set manually — build a "Convert to WO" button on accepted quotes that auto-creates WO + lines |
| P-7 | **Part cost trending** | On part detail, show a sparkline of cost-per-part over time from completed job data |
| P-8 | **Barcode/QR scanning** | Shop floor barcode input exists but doesn't integrate with device camera — add camera-based scanning for mobile operators |
| P-9 | **Operator time tracking** | Stage executions track actual hours but there's no clock-in/out or time entry for operators |
| P-10 | **Email notifications** | System settings have notification flags but no email sending infrastructure |

---

## Implementation Notes

- **Seed data fixes (S-1 through S-7)** can all be done in `DataSeedingService.cs` — delete demo.db and restart to re-seed
- **S-1 is the highest-impact change** — a dense Gantt timeline is the single most impressive visual in the demo
- **Mobile table cards (M-1)** is the highest-impact responsive fix — it affects 6+ pages
- **F-1 (Shipments)** and **F-2 (Ship from WO)** are the most straightforward features to build since the data layer already exists
- **F-4 (Customer CRM)** is the largest scope item — consider deferring to post-demo and keeping freetext customer names for now
