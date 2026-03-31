# Vectrik Demo Script — 11am Meeting

## Context

**What is Vectrik?**
Vectrik is a Manufacturing Execution System (MES) purpose-built for additive manufacturing — specifically SLS (Selective Laser Sintering) metal 3D printing shops. It manages the entire production lifecycle from quoting and work orders through printing, post-processing, quality control, and shipping.

**What are we showing?**
A live production environment for a firearms suppressor manufacturer running two EOS M4 Onyx DMLS printers. The system tracks the full workflow: SLS printing > depowdering > heat treatment > wire EDM > CNC machining > laser engraving > surface finishing > quality control > packaging & shipping.

**Key stats:**
- 2 EOS M4 Onyx machines (6 lasers each, 1000W, 450mm build volume)
- 4 EMC suppressor part types (Pilate .22LR, Tinman 7.62mm, Handyman 9mm, Gargoyle 5.56mm)
- 16 active work orders across 6 customers
- 347 hours of scheduled production
- 1,840 parts outstanding
- 20 machines across 13 production stages

---

## Demo Flow (~15-20 minutes)

### 1. Dashboard Overview (1 min)

**Navigate:** Already on `/dashboard` at login

**What to show:**
- KPI cards at top: 13 active jobs, 12 work orders, 100% first pass yield
- Stage Pipeline visualization — the colorful horizontal bar showing work flowing through all 13 stages
- Quick Navigation buttons at the bottom

**What to say:**
> "This is the operational dashboard. At a glance, we can see 13 active jobs flowing through 12 work orders. The Stage Pipeline shows us exactly where work is at every stage — from SLS printing all the way through to packaging and shipping. You can see we have 12 items in SLS printing, 12 in depowdering, and work distributed across all downstream stages."

---

### 2. Work Orders (2 min)

**Navigate:** Click "Work Orders" in left nav

**What to show:**
- 16 work orders with different customers and priorities
- Fulfillment progress bars (some partially scheduled, some fully covered)
- RUSH and HIGH priority badges
- Click one WO row to show details

**What to say:**
> "Here are our active work orders. Notice the fulfillment bars — WO-00011 for Rugged Suppressors is RUSH priority, due April 5th, with 248 parts needed. The system shows 152 are already scheduled. WO-00014 for Dead Air Silencers has 160 parts fully scheduled. Each order tracks exact part counts, programs assigned, and delivery timelines."

---

### 3. Production Scheduler — The Main Event (5-7 min)

**Navigate:** Click "Production Scheduler" in left nav

#### 3a. Gantt View (2 min)

**What to show:**
- Two-week schedule across both EOS machines and all post-process machines
- The red "Now" line marking current time
- Build bars showing part counts
- Click a build bar to show the detail popup (program name, print time, cost, parts on plate)

**What to say:**
> "This is the production scheduler — the heart of the system. You're looking at a Gantt chart with both EOS M4 Onyx machines at the top, then all downstream post-processing machines. The red line is right now. Each bar is a scheduled build or operation."

**Click a build bar:**
> "Clicking a build gives us full details — this Pilate 96x run has 96 parts, 16 hours of print time, estimated cost of $3,200, uses 9.8 kg of Ti-6Al-4V powder across 2,333 layers. We can start the print, reschedule it, or delete it right from here."

#### 3b. Zoom Controls (30 sec)

**What to show:**
- Click + a few times to zoom in, showing more detail on bars
- Click - to zoom out for the bigger picture
- Click "Now" to re-center

**What to say:**
> "The Gantt supports zoom — zoom in to see individual builds in detail, zoom out for the two-week overview. The 'Now' button always brings you back to the current moment."

#### 3c. Orders Panel (30 sec)

**What to show:**
- The right-side Orders panel with Active/Overdue/This Week tabs
- Type a customer name in the search box (e.g., "Capitol")
- Show how it filters to just that customer's orders

**What to say:**
> "The orders panel on the right shows all active work orders alongside the schedule. I can search by customer — typing 'Capitol' instantly shows me just Capitol Armory's three orders with their program assignments and parts remaining."

#### 3d. SLS Pipeline Summary (30 sec)

**What to show:**
- Click the "SLS Pipeline" expander if collapsed
- Show the 16 active programs count

**What to say:**
> "The SLS Pipeline summary shows us 16 active programs feeding both machines. This gives schedulers a quick view of what's queued up."

---

### 4. Next Build Advisor — The Crown Jewel (3-4 min)

**Navigate:** Click "+ New" dropdown > "SLS Build"

#### Step 1: Machine Selection

**What to show:**
- The 3-step wizard (Machine > Program > Schedule)
- Machine dropdown defaulting to EOS M4 Onyx #1
- Outstanding Demand section: 1,740 parts, 26 builds needed, 4 part types with priorities
- The green "Recommended Next Build" card with SAFE CHANGEOVER badge

**What to say:**
> "This is our Next Build Advisor — it analyzes all outstanding demand and recommends what to build next. Right now it's looking at EOS M4 Onyx #1 and sees 1,740 parts outstanding across 26 builds needed. The system is recommending a Tinman 56x single-stack build because EMC-TIN-001 is RUSH priority, due in 4 days, and needs 10 builds. It also confirms this is a SAFE CHANGEOVER — the operator will be available during shift hours to remove the previous build from the cooldown chamber."

#### Step 2: Program Selection

**Click Next:**

**What to show:**
- Programs listed by part type
- The recommended program pre-selected with SAFE CO and READY badges
- Program details: 56 parts, 22.5h print, Ti-6Al-4V, 3,060 layers

**What to say:**
> "Step 2 shows all available programs for this machine. The advisor pre-selects the Tinman 56x single-stack program. We can see it produces 56 parts in a 22.5-hour print, uses Ti-6Al-4V Grade 5, and is marked READY with a safe changeover window."

**Close the wizard** (click X — do NOT schedule anything):
> "I won't actually schedule this now, but this gives you a sense of how the advisor guides operators to make data-driven scheduling decisions."

---

### 5. Demand Tab (1 min)

**Navigate:** Click "Demand" tab on scheduler

**What to show:**
- 12 active orders, 1840 parts outstanding
- Work orders listed with per-program demand breakdowns
- "Schedule Build" buttons
- Due dates and coverage status ("Covered" vs "X remaining")

**What to say:**
> "The Demand view gives planners a complete picture of outstanding demand. Each work order shows exactly which SLS programs are needed and how many builds. If demand is fully covered by existing scheduled builds, it shows 'Covered'. Otherwise it shows how many parts remain and how many builds are needed."

---

### 6. Dispatch Tab (1 min)

**Navigate:** Click "Dispatch" tab

**What to show:**
- Machine Capacity cards organized by department
- EOS machines showing over 100% utilization (red)
- Date range picker
- Queue counts and next job info

**What to say:**
> "The Dispatch view shows machine capacity across all departments. Notice the two EOS machines are at 133% and 106% — they're overcommitted for this week, which is exactly what you want with expensive additive machines. Each card shows queue depth and the next job in line."

---

### 7. Capacity Dashboard (1 min)

**Navigate:** Click "Capacity Dashboard" in left nav

**What to show:**
- Machine overview cards with utilization bars
- The Haas ST-20Y CNC lathes showing loaded programs
- "Assign Work" buttons

**What to say:**
> "The Capacity Dashboard gives managers a bird's-eye view of every machine in the shop. You can see which machines are loaded, which are idle, and assign work directly from here."

---

### 8. Shop Floor — Operator View (1 min)

**Navigate:** Click "Operator Queue" under Shop Floor

**What to show:**
- Welcome message with operator name
- "My Queue" section (empty — no work claimed yet)
- "Available Work" with 50 items and "Claim & Start" buttons

**What to say:**
> "This is what operators see on the shop floor. They log in, see their queue, and claim available work. Each item shows the part, stage, priority, due date, and assigned machine. One click to claim and start."

---

### 9. Quick Peeks (2 min)

**Parts Library** (30 sec):
> "The Parts Library manages our 4 EMC suppressor types. Each part has a full manufacturing process defined — 8 stages from SLS printing through shipping, with Ti-6Al-4V Grade 5 material."

**Quotes** (30 sec):
> "The quoting module tracks win rates, margins, and accuracy. We're at 100% win rate with an 85.8% average margin on $562K in won quotes."

**Analytics** (30 sec):
> "Analytics gives us production output, machine utilization, cost per part, and on-time delivery tracking. Everything in one place."

---

## Talking Points for Each Section

| Section | Key Message |
|---------|-------------|
| Dashboard | "Single pane of glass for the entire shop" |
| Work Orders | "Full traceability from customer PO to production floor" |
| Scheduler | "Visual scheduling purpose-built for additive manufacturing" |
| Build Advisor | "AI-driven recommendations that factor in demand, priority, changeover windows, and machine capacity" |
| Demand | "Never miss a delivery — full demand vs. capacity visibility" |
| Shop Floor | "Operators get exactly what they need, nothing more" |
| Quality | "NCR tracking, CAPA management, SPC — all integrated" |
| Parts Library | "Complete digital twin of every part's manufacturing process" |

---

## Things to AVOID During Demo

1. **Do NOT click "Start Print"** on any build — this will change job status and may affect the Gantt display
2. **Do NOT click "Delete"** on any build — this removes scheduled work and messes up the demo data
3. **Do NOT submit/save** new Work Orders or Quotes — draft then discard
4. **Do NOT click "Claim & Start"** on the Shop Floor — this changes stage execution status
5. **Avoid** the Production Batches page (empty — not a good look)
6. **Avoid** lingering on the Approvals page (empty queue)
7. **Avoid** clicking "Reschedule" unless you intend to show that flow specifically
8. If asked about OEE being 45% — explain demo data has future-dated schedules, actual utilization would be higher in production

---

## Q&A Prep

**Q: How does this compare to ProShop or Paperless Parts?**
> A: Vectrik is purpose-built for additive manufacturing. Unlike general-purpose MES systems, we understand build plates, powder management, changeover cycles, and multi-part nesting. The Next Build Advisor is unique — no other MES recommends what to print next based on demand, priority, and changeover safety.

**Q: Can this handle other manufacturing processes?**
> A: Yes — we already support CNC machining, wire EDM, laser engraving, heat treatment, and surface finishing as downstream stages. The Manufacturing Approaches system lets you define any routing, and the Shop Floor adapts to each stage automatically.

**Q: How does the changeover system work?**
> A: EOS machines have auto-changeover — the next build starts automatically, but an operator must remove the finished build from the cooldown chamber. The scheduler checks if that changeover falls within operator shift hours. "Safe changeover" means an operator will be there; otherwise the machine risks going down until the next shift.

**Q: Is this production-ready?**
> A: The core modules are functional and we're running 506 automated tests. The system is deployed on Azure with multi-tenant architecture. We're in active development with new features like dispatch optimization and learning-based scheduling coming next.

**Q: What about data security and multi-tenant?**
> A: Each tenant has an isolated SQLite database. Authentication uses cookie-based claims with role-based access (Admin, Manager, Operator, QC Inspector). The platform supports multiple tenants with separate feature flags per tenant.

**Q: How long to implement for a new customer?**
> A: The system seeds realistic demo data automatically. For production, you'd configure your machines, materials, manufacturing approaches, and production stages — typically a few days of setup. Parts and programs can be added as you go.

**Q: What's the pricing model?**
> A: [Defer to Henry on pricing specifics]

---

## Pre-Demo Checklist

- [ ] Verify https://www.vectrik.com loads (already logged in as admin)
- [ ] Browser is in dark mode (default)
- [ ] Close unnecessary browser tabs
- [ ] Screen sharing is ready
- [ ] Start on Dashboard page
- [ ] Have this demo script open in a separate window/tab for reference
