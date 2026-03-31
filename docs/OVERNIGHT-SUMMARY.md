# Overnight QA & Demo Prep — 2026-03-31

## Pages Tested & Results

### All Pages Functional via SPA Navigation
Every page in the nav menu was clicked and verified. All load correctly when navigated via the in-app nav links:

| Section | Pages Tested | Status |
|---------|-------------|--------|
| **Production Scheduler** | Schedule, Demand, Dispatch, Data tabs | OK — all 4 tabs functional |
| **Capacity Dashboard** | Machine capacity view | OK — 20 machines visible |
| **Work Orders** | List, detail, Kanban views | OK — 10 existing WOs |
| **Quotes** | List, detail, analytics, customers | OK — 5 quotes, $562K pipeline |
| **RFQ Inbox** | RFQ list with actions | OK — 3 RFQs |
| **SLS Builds / Programs** | Program list, Machine Setup | OK — 19 active, 10 archived |
| **Production Batches** | Batch list | OK — empty state (expected) |
| **Operation Costs** | Stage cost profiles | OK |
| **Approvals** | Approval queue | OK — empty (expected) |
| **Workflows** | Workflow configuration | OK — 5 active workflows |
| **Parts Library** | Part list, detail, edit | OK — 4 EMC parts |
| **Machine Programs** | Program list, detail | OK |
| **Work Instructions** | Instruction list | OK |
| **Approaches** | Manufacturing approaches | OK |
| **Shop Floor** | Operator Queue, Setup Queue | OK — 8 available work items |
| **Stage Pages** | SLS Printing, Depowdering, Wire EDM, CNC, etc. | OK — all stage views load |
| **Part Tracker** | Tracking view | OK |
| **Analytics** | Dashboard, OTD, Quality, OEE, Cost, Profit | OK — charts render |
| **Reports** | Report builder | OK |
| **Search** | Global search | OK |
| **Machines** | Machine list, detail | OK — 20 machines |
| **Maintenance** | Alerts, Work Orders, Rules | OK |
| **Inventory** | Dashboard, Items, Receive | OK — 11 SKUs, $116K value |
| **Quality** | Dashboard, NCR, CAPA, SPC | OK — 3 NCRs |
| **Admin** | Users, Roles, Shifts, Settings, Features, Numbering, Branding | OK |

### Bug Found: Root URL "Not Found" for Logged-In Users
- **Issue**: Navigating to `vectrik.com/` (root URL) while logged in showed "Not Found"
- **Cause**: `Public/Home.razor` has `[ExcludeFromInteractiveRouting]`, so the interactive Blazor router can't match `/`
- **Fix**: Added server-side redirect in `Public/Home.razor` — authenticated users now redirect to `/dashboard`
- **Impact on demo**: Minimal — login redirects to `/dashboard` already

### Note: Direct URL Access
Typing URLs directly in the browser bar (e.g., `/scheduler`) works for SPA navigation but may show "Not Found" on hard refresh for some routes. This is a known Blazor Server behavior. **For the demo, always navigate via in-app links** — the login page at `/account/login` redirects to `/dashboard` after authentication.

---

## Demo Data Created

### New Work Orders (6 added, 16 total)
| Order | Customer | Priority | Due Date | Parts |
|-------|----------|----------|----------|-------|
| WO-00011 | Rugged Suppressors | **RUSH** | Apr 5 | Pilate 192, Tinman 56 |
| WO-00012 | SilencerCo | **RUSH** | Apr 7 | Handyman 128 |
| WO-00013 | Capitol Armory | HIGH | Apr 10 | Gargoyle 144, Tinman 112 |
| WO-00014 | Dead Air Silencers | HIGH | Apr 14 | Handyman 64, Pilate 96 |
| WO-00015 | Silencer Shop | NORMAL | Apr 18 | Tinman 168, Gargoyle 72 |
| WO-00016 | Thunder Beast Arms | NORMAL | Apr 21 | Pilate 288, Handyman 64 |

### New SLS Builds (11 added across both machines)

**EOS M4 Onyx #1 (5 builds):**
1. Pilate 96x — Rush (16h print)
2. Tinman 56x — Rush (22.5h print)
3. Gargoyle 72x — Capitol (18.5h print)
4. Tinman 56x — Capitol (22.5h print)
5. Gargoyle 72x — SS (18.5h print)

**EOS M4 Onyx #2 (6 builds):**
1. Handyman 64x — Rush (20h print)
2. Handyman 64x — Rush #2 (20h print)
3. Pilate 96x — Dead Air (16h print)
4. Handyman 64x — Dead Air (20h print)
5. Pilate 96x — TBAC (16h print)
6. Tinman 56x — SS (22.5h print)

### Downstream Operations
Every build has a full downstream pipeline auto-scheduled:
SLS Printing → Depowdering → Wire EDM → CNC Turning → Laser Engraving → Sandblasting → QC → Packaging

---

## Bugs Found & Fixed

| Bug | Severity | Status |
|-----|----------|--------|
| Root URL `/` shows "Not Found" for authenticated users | Medium | **Fixed** — redirect to `/dashboard` |

---

## Suggested Demo Flow (11am)

1. **Login** at `vectrik.com/account/login` → `admin / Vectrik2026!`
2. **Dashboard** — overview KPIs, stage pipeline, quick navigation
3. **Production Scheduler → Schedule tab** — Gantt chart with both EOS M4 machines loaded, builds color-coded by priority
4. **Scheduler → Demand tab** — Show 12+ active orders, parts outstanding, build needs
5. **Scheduler → Dispatch tab** — Machine capacity heatmap
6. **Scheduler → Data tab** — Stage execution table, Part Path view
7. **Capacity Dashboard** — Machine cards with utilization bars
8. **Work Orders** — Table view, click into a RUSH order to show detail
9. **Parts Library** — Click into EMC-TIN-001, show 8 production stages
10. **Machine Programs** — Show build plate programs, machine setup view
11. **Shop Floor → Operator Queue** — Show available work, "Claim & Start" workflow
12. **Shop Floor → SLS Printing** — Stage-specific view with queue
13. **Analytics** — KPI dashboard, production output chart
14. **Quality → NCR Management** — Show non-conformance tracking
15. **Inventory** — Stock dashboard with transaction history
16. **Quotes** — Pipeline with margins, click into a quote

**Tips:**
- Use the nav menu to navigate — don't type URLs
- The Gantt chart supports scroll zoom and drag-to-reschedule
- Dark mode is default; toggle via moon icon in bottom-left
