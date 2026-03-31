# Overnight QA & Demo Prep — 2026-03-31

## Pages Tested & Results

### All Pages Functional via SPA Navigation
Every nav menu item was clicked and verified. All load correctly via in-app navigation:

| Section | Pages Tested | Status |
|---------|-------------|--------|
| **Dashboard** | KPI cards, Stage Pipeline, Quick Nav | OK — 13 jobs, 12 active WOs |
| **Production Scheduler** | Schedule, Demand, Dispatch, Data | OK — all 4 tabs, 347h scheduled |
| **Capacity Dashboard** | Machine capacity cards | OK — 20 machines visible |
| **Work Orders** | Table, Released/InProgress/Complete tabs | OK — 16 work orders |
| **Quotes** | List, analytics, customers | OK — 5 quotes, $562K pipeline |
| **RFQ Inbox** | RFQ list with View/Quote/Decline | OK — 3 RFQs |
| **SLS Builds / Programs** | Program list, Machine Setup | OK — 16 active programs |
| **Production Batches** | Batch list | OK — empty (expected, no plate releases yet) |
| **Operation Costs** | Stage cost profiles | OK |
| **Approvals** | Approval queue | OK — empty (expected) |
| **Workflows** | Workflow config | OK — 5 active workflows |
| **Parts Library** | Part list, detail, edit | OK — 4 EMC suppressor parts |
| **Work Instructions** | Instruction list | OK |
| **Approaches** | Manufacturing approaches | OK |
| **Shop Floor** | Operator Queue, Setup Queue | OK — 12+ available work items |
| **Stage Pages** | SLS, Depowder, Wire EDM, CNC, etc. | OK — all stage views load |
| **Part Tracker** | Tracking view | OK |
| **Analytics** | Dashboard, OTD, Quality, OEE, Cost, Profit | OK — charts render |
| **Machines** | Machine list, detail | OK — 20 machines |
| **Maintenance** | Alerts, Work Orders, Rules | OK |
| **Inventory** | Dashboard, Items, Receive | OK — 11 SKUs, $116K value |
| **Quality** | Dashboard, NCR, CAPA, SPC | OK — 3 NCRs |
| **Admin** | Users, Roles, Shifts, Settings, etc. | OK |

### Known Issue: Root URL for Logged-In Users
- Navigating to `vectrik.com/` while logged in may show "Not Found"
- **Workaround**: Always start from the login page or use nav links
- Login at `/account/login` correctly redirects to `/dashboard`

---

## Demo Data — Verified on Live Site

### Work Orders (16 total: 9 Released, 3 InProgress, 4 Complete)

**New RUSH orders (due in 5-7 days):**
| Order | Customer | Priority | Due | Parts | Fulfillment |
|-------|----------|----------|-----|-------|-------------|
| WO-00011 | Rugged Suppressors | RUSH | Apr 5 | Pilate 192, Tinman 56 | 152 scheduled, 96 outstanding |
| WO-00012 | SilencerCo | RUSH | Apr 7 | Handyman 128 | 128 scheduled |

**New HIGH priority (due in 10-14 days):**
| Order | Customer | Priority | Due | Parts | Fulfillment |
|-------|----------|----------|-----|-------|-------------|
| WO-00013 | Capitol Armory | HIGH | Apr 10 | Gargoyle 144, Tinman 112 | 128 scheduled, 128 outstanding |
| WO-00014 | Dead Air Silencers | HIGH | Apr 14 | Handyman 64, Pilate 96 | 160 scheduled |

**New NORMAL priority (due in 18-21 days):**
| Order | Customer | Priority | Due | Parts | Fulfillment |
|-------|----------|----------|-----|-------|-------------|
| WO-00015 | Silencer Shop | NORMAL | Apr 18 | Tinman 168, Gargoyle 72 | 128 scheduled, 112 outstanding |
| WO-00016 | Thunder Beast Arms | NORMAL | Apr 21 | Pilate 288, Handyman 64 | 96 scheduled, 256 outstanding |

### Gantt Schedule — Verified
- **EOS M4 Onyx #1**: 6 builds queued (Pilate, Tinman, Gargoyle mix)
- **EOS M4 Onyx #2**: 6 builds queued (Handyman, Pilate, Tinman mix)
- **347 hours total scheduled** across both machines
- **Downstream pipeline**: 12 items each in Depowder, Wire EDM, CNC Turning, Laser Engraving, Sandblasting, QC, Packaging
- Full production flow: SLS → Depowder → Wire EDM → CNC → Laser → Sandblast → QC → Package

### Existing Data (pre-enhancement)
- 10 original work orders (4 complete, 3 in-progress, 3 released)
- 4 EMC suppressor parts: Tinman, Handyman, Gargoyle, Pilate
- 20 machines across SLS, CNC, EDM, Finishing
- 5 quotes ($562K won value, 85.8% margin)
- 3 RFQs (New, Reviewed, Quoted)
- 3 NCRs (quality records)
- 11 inventory SKUs ($116K value)
- 5 workflow definitions

---

## Bugs Found & Fixed

| Bug | Severity | Status | Commit |
|-----|----------|--------|--------|
| Root URL `/` shows "Not Found" for auth users | Medium | Fixed — redirect to `/dashboard` | f8559ad |
| Demo seed ran multiple times (duplicate WOs) | Medium | Fixed — cleanup + V3 marker | 0a02271 |

---

## Suggested Demo Flow (11am)

### Opening (2 min)
1. **Login** → `vectrik.com/account/login` → `admin / Vectrik2026!`
2. **Dashboard** → Overview KPIs (13 active jobs, 12 WOs), Stage Pipeline showing work flowing through all stages

### Production Scheduling (5 min)
3. **Scheduler → Schedule tab** → Gantt chart with both EOS M4 machines loaded, 347h scheduled
4. **Scheduler → Demand tab** → 12 active orders, 6192 parts outstanding, RUSH/HIGH/NORMAL priorities
5. **Scheduler → Dispatch tab** → Machine capacity heatmap with utilization bars
6. **Scheduler → Data tab** → Stage execution table with filtering

### Capacity & Work Orders (3 min)
7. **Capacity Dashboard** → Machine cards showing load, "Assign Work" buttons
8. **Work Orders** → Click WO-00011 (Rugged Suppressors, RUSH) to show detail
9. **Work Orders → Kanban** → Visual board view

### Engineering (3 min)
10. **Parts Library** → Click EMC-TIN-001 → 8 production stages, process routing
11. **Machine Programs** → Build plate programs, machine setup pending banner
12. **SLS Builds** → 16 active programs across the pipeline

### Shop Floor (3 min)
13. **Shop Floor → Operator Queue** → "Claim & Start" workflow for available work
14. **Shop Floor → SLS Printing** → Stage queue view
15. **Shop Floor → CNC Machining** → Part-level stage view

### Business Intelligence (2 min)
16. **Analytics** → KPI dashboard, production output, machine utilization
17. **Quality → NCR Management** → Non-conformance tracking
18. **Quotes** → Pipeline with margins, win rate

### Closing (2 min)
19. **Inventory** → Stock dashboard, recent transactions
20. **Workflows** → Configurable approval workflows

**Tips:**
- Navigate via nav links — don't type URLs directly
- Gantt supports scroll zoom and drag-to-reschedule
- Dark mode is default; toggle via moon icon in bottom-left
- All data is for EMC suppressors in Ti-6Al-4V on EOS M4 Onyx DMLS printers
