# Module 01: Estimating & Quoting

## Status: [ ] Not Started
## Category: ERP
## Phase: 1 — Core Production Engine
## Priority: P1 - Critical

---

## Overview

The Estimating & Quoting module is the entry point of the entire production workflow.
A customer RFQ flows into a quote, which converts to a Work Order with all BOMs,
routing, and QC specs auto-filled. This module must surpass ProShop by adding
AI-assisted pricing intelligence, a customer-facing RFQ portal, and predictive
margin analytics.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `Quote` model (Id, WorkOrderId, Status, Lines) | ✅ Exists | `Models/Quote.cs` |
| `QuoteLine` model (PartId, Qty, UnitPrice, Markup) | ✅ Exists | `Models/QuoteLine.cs` |
| `QuoteStatus` enum (Draft, Sent, Accepted, Rejected, Expired) | ✅ Exists | `Models/Enums/ManufacturingEnums.cs` |
| `QuoteService` / `IQuoteService` (stub) | ✅ Exists | `Services/QuoteService.cs` |
| `/quotes` page (index + details UI stub) | ✅ Exists | `Components/Pages/Quotes/` |

**Gap**: Service methods are stubs; no pricing logic, no quote-to-WO conversion, no margin tracking, no RFQ portal.

---

## What Needs to Be Built

### 1. Database Model Extensions
- Add `EstimatedHours`, `EstimatedMaterialCost`, `EstimatedOverhead`, `TargetMarginPct`, `ActualCost` to `Quote`
- Add `LaborMinutes`, `MaterialCostEach`, `SetupMinutes`, `OutsideProcessCost` to `QuoteLine`
- Add `QuoteRevision` model for version history
- Add `RfqRequest` model for customer portal submissions

### 2. Service Layer
- Complete `QuoteService` with full CRUD + business logic
- `QuoteToWorkOrderConverter` — converts accepted quote to WO with auto-fill
- `PricingEngineService` — calculates costs from routing + materials + overhead rates
- `MarginAnalyticsService` — tracks estimated vs. actual margin per quote

### 3. UI Components
- **Quote List** (`/quotes`) — filterable by status, customer, date range
- **Quote Builder** (`/quotes/new`, `/quotes/{id}/edit`) — visual BOM assembly
  - Line item editor with quantity breaks
  - Real-time cost/margin preview panel
  - Profit guard: warn when margin drops below threshold
- **Quote Details** (`/quotes/{id}`) — view summary, revision history, convert button
- **Quote Revision Diff** — side-by-side comparison of revisions
- **RFQ Portal** (`/portal/rfq`) — public-facing customer submission form

---

## Implementation Steps

### Step 1 — Extend Database Models
**File**: `Models/Quote.cs`
Add the following properties:
```csharp
public decimal EstimatedLaborCost { get; set; }
public decimal EstimatedMaterialCost { get; set; }
public decimal EstimatedOverheadCost { get; set; }
public decimal TargetMarginPct { get; set; } = 30m;
public decimal ActualCostFinal { get; set; }
public string? CustomerReference { get; set; }
public string? Notes { get; set; }
public int RevisionNumber { get; set; } = 1;
public DateTime? SentAt { get; set; }
public DateTime? AcceptedAt { get; set; }
public DateTime? ExpiresAt { get; set; }
```

**File**: `Models/QuoteLine.cs`
Add:
```csharp
public decimal LaborMinutes { get; set; }
public decimal SetupMinutes { get; set; }
public decimal MaterialCostEach { get; set; }
public decimal OutsideProcessCostEach { get; set; }
public string? Notes { get; set; }
```

**New File**: `Models/QuoteRevision.cs`
```csharp
public class QuoteRevision
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;
    public int RevisionNumber { get; set; }
    public string SnapshotJson { get; set; } = string.Empty; // JSON of quote at that revision
    public string? ChangeNotes { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**New File**: `Models/RfqRequest.cs`
```csharp
public class RfqRequest
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string PartDescription { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? DrawingFileUrl { get; set; }
    public string? MaterialRequested { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public string? AdditionalNotes { get; set; }
    public RfqStatus Status { get; set; } = RfqStatus.New;
    public int? ConvertedToQuoteId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public enum RfqStatus { New, InReview, QuoteCreated, Declined }
```

**File**: `Data/TenantDbContext.cs`
Add DbSets:
```csharp
public DbSet<QuoteRevision> QuoteRevisions { get; set; }
public DbSet<RfqRequest> RfqRequests { get; set; }
```

### Step 2 — Complete QuoteService
**File**: `Services/QuoteService.cs`

Implement:
- `GetAllAsync(QuoteStatus? filter)` — list quotes with optional status filter
- `GetByIdAsync(int id)` — include lines, revisions, part references
- `CreateAsync(Quote quote)` — save new quote, create rev 1 snapshot
- `UpdateAsync(Quote quote)` — save changes, auto-increment revision, snapshot old
- `DeleteAsync(int id)` — soft-delete (set status to Cancelled)
- `AddLineAsync(int quoteId, QuoteLine line)` — add line item
- `RemoveLineAsync(int lineId)` — remove line
- `UpdateLineAsync(QuoteLine line)` — update pricing
- `CalculateTotalsAsync(int quoteId)` — recalculate all cost/margin fields
- `SendToCustomerAsync(int quoteId)` — set status to Sent, record SentAt
- `AcceptAsync(int quoteId)` — set status to Accepted, record AcceptedAt
- `RejectAsync(int quoteId)` — set status to Rejected
- `ConvertToWorkOrderAsync(int quoteId)` — create WO from quote (see Module 02)

### Step 3 — Pricing Engine Service
**New File**: `Services/PricingEngineService.cs`

```csharp
public interface IPricingEngineService
{
    Task<QuoteLineCostBreakdown> CalculateLineCostAsync(int partId, int quantity, string tenantCode);
    Task<decimal> GetMaterialCostAsync(int materialId, decimal weightKg, string tenantCode);
    Task<decimal> GetLaborCostAsync(int partId, decimal laborMinutes, string tenantCode);
    Task<decimal> GetOverheadRateAsync(string tenantCode);
}
```

Logic:
- Pull `PartStageRequirement` durations × machine hourly rate = labor cost
- Pull material from `Material` model with price-per-kg × estimated weight
- Overhead rate from `SystemSetting` key `"overhead_rate_pct"`
- Return `QuoteLineCostBreakdown` record with each cost component

### Step 4 — Quote List Page
**File**: `Components/Pages/Quotes/Index.razor`

UI requirements:
- Table with columns: Quote #, Customer, Part(s), Qty, Est. Value, Margin %, Status, Created, Actions
- Status filter tabs (All | Draft | Sent | Accepted | Rejected)
- "New Quote" button → `/quotes/new`
- Row click → `/quotes/{id}`
- Status badges: Draft (grey), Sent (blue), Accepted (green), Rejected (red), Expired (orange)

### Step 5 — Quote Builder Page
**File**: `Components/Pages/Quotes/Edit.razor` (new or update existing)

UI requirements:
- Customer info section (name, email, company, their PO reference)
- **Quote Lines** section:
  - Add line: select Part from dropdown, enter Qty, see auto-calculated cost
  - Each line shows: Part name, Qty, Material cost, Labor cost, Overhead, Unit price, Margin %
  - Quantity breaks: option to add break pricing at different qty tiers
  - Inline edit of markup percentage per line
- **Summary Panel** (right sidebar or bottom):
  - Total estimated cost, total quoted price, blended margin %
  - Margin guard: red warning when below `TargetMarginPct`
- **Profit Guard Threshold**: configurable in SystemSettings, warn visually
- Expiry date picker
- Internal notes textarea
- "Save Draft" / "Send to Customer" / "Convert to Work Order" action buttons

### Step 6 — Quote Details Page
**File**: `Components/Pages/Quotes/Details.razor` (update existing)

UI requirements:
- Read-only view of all quote data
- Revision history timeline (Rev 1 → Rev 2 → Rev 3 with dates and change notes)
- "Edit", "Send", "Accept", "Reject" action buttons (based on current status)
- "Convert to Work Order" button (only when Accepted)
- Estimated vs. Actual cost comparison (shown after WO completion)

### Step 7 — RFQ Portal Page
**File**: `Components/Pages/Portal/RfqSubmit.razor` (new)
**Route**: `/portal/rfq` (accessible without authentication — configure in `Program.cs`)

UI requirements:
- Public-facing form: company name, contact email, part description, quantity, required date
- File upload for drawing (store to `wwwroot/uploads/rfq/`)
- Material selection dropdown
- Additional notes
- Submit button → save `RfqRequest`, send confirmation email (log to console initially)

**File**: `Components/Pages/Quotes/RfqInbox.razor` (new, admin-only)
**Route**: `/quotes/rfq-inbox`

UI requirements:
- List incoming RFQ requests
- "Create Quote" button per row → pre-populates quote builder from RFQ data
- Status management (New, In Review, Declined)

### Step 8 — EF Core Migration
```bash
dotnet ef migrations add AddQuoteEnhancements --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Quote can be created with line items and cost auto-calculation
- [ ] Margin % displayed in real-time on quote builder
- [ ] Profit guard warning appears when margin < threshold
- [ ] Quote can be revised with revision history tracked
- [ ] "Send to Customer" transitions status correctly
- [ ] "Accept" + "Convert to Work Order" creates WO with lines auto-filled
- [ ] RFQ portal form submits without authentication
- [ ] RFQ inbox shows new submissions to admins
- [ ] All quote statuses render with correct badges
- [ ] Quote list is filterable by status

---

## Dependencies

- **Module 02** (Work Order Management) — `ConvertToWorkOrderAsync` integration
- **Module 05** (Quality Systems) — QC specs auto-fill on WO conversion
- **Module 08** (Parts/PDM) — Part selection with routing data for cost estimation
- **Module 16** (CRM) — Customer lookup for quote creation

---

## Future Enhancements (Post-MVP)

- AI-assisted quote pricing using historical job profitability data
- Win-rate analytics per customer/part-type/industry
- Automated quote follow-up reminders
- Customer portal with quote approval workflow
