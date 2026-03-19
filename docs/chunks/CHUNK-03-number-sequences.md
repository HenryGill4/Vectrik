# CHUNK-03: Number Sequences

> **Size**: S (Small) — ~4-6 file edits
> **ROADMAP tasks**: H6.11, H6.12, H6.13, H6.14
> **Prerequisites**: H1-H5 complete

---

## Scope

Wire `INumberSequenceService.NextAsync("EntityType")` into the creation flows
for Quotes, Work Orders, NCRs, and Jobs. The service already exists — this is
wiring the auto-generated numbers into the create forms and services.

---

## Files to Read First

| File | Why |
|------|-----|
| `Services/INumberSequenceService.cs` | Understand `NextAsync(entityType)` API |
| `Services/NumberSequenceService.cs` | Understand how sequences are generated |
| `Components/Pages/Admin/Numbering.razor` | See configured sequences |
| `Components/Pages/Quotes/Edit.razor` | Quote creation — wire QuoteNumber |
| `Components/Pages/WorkOrders/Create.razor` | WO creation — wire OrderNumber |
| `Components/Pages/Quality/Ncr.razor` | NCR creation — wire NcrNumber |
| `Services/WorkOrderService.cs` | Job generation — wire JobNumber |

---

## Tasks

### 1. Wire quote auto-numbering (H6.11)
**File**: `Components/Pages/Quotes/Edit.razor`
- Inject `INumberSequenceService`
- On new quote creation, if QuoteNumber is empty:
  ```csharp
  if (string.IsNullOrEmpty(_quote.QuoteNumber))
      _quote.QuoteNumber = await NumberSequenceService.NextAsync("Quote");
  ```
- Keep the field editable (user can override) but pre-fill it
- Generate the number when the user first navigates to the create page, not on save
  (so they see it immediately)

### 2. Wire work order auto-numbering (H6.12)
**File**: `Components/Pages/WorkOrders/Create.razor`
- Same pattern as quotes
- Pre-fill `_workOrder.OrderNumber` with `NextAsync("WorkOrder")`

### 3. Wire NCR auto-numbering (H6.13)
**File**: `Components/Pages/Quality/Ncr.razor`
- In the NCR creation form, pre-fill the NCR number
- `_ncr.NcrNumber = await NumberSequenceService.NextAsync("NCR");`

### 4. Wire job auto-numbering (H6.14)
**File**: `Services/WorkOrderService.cs` (or `Services/JobService.cs`)
- Find the method that generates jobs (likely `GenerateJobsForLineAsync`)
- When creating a new Job, set:
  ```csharp
  job.JobNumber = await _numberSequenceService.NextAsync("Job");
  ```
- Inject `INumberSequenceService` into the service constructor

---

## Verification

1. Build passes
2. Configure number sequences in Admin → Numbering for Quote, WorkOrder, NCR, Job
3. Create a new quote → number is auto-filled (e.g., "Q-00001")
4. Create a new work order → number is auto-filled (e.g., "WO-00001")
5. Create a new NCR → number is auto-filled
6. Release a work order → generated jobs have auto-numbered JobNumbers

---

## Files Modified (fill in after completion)

- `Services/QuoteService.cs` — Injected `INumberSequenceService`, replaced `GenerateQuoteNumberAsync` to delegate to `_numberSeq.NextAsync("Quote")`
- `Services/WorkOrderService.cs` — Injected `INumberSequenceService`, replaced `GenerateOrderNumberAsync` to delegate to `_numberSeq.NextAsync("WorkOrder")`, wired `JobNumber = await _numberSeq.NextAsync("Job")` into `GenerateJobsForLineAsync`
- `Services/NumberSequenceService.cs` — Added "Job" and "CAPA" entries to `_entityMap`
- `Models/Job.cs` — Added `JobNumber` property (string?, MaxLength 50)
- `Services/QualityService.cs` — Already wired (NCR and CAPA use `_numberSeq.NextAsync`) — no changes needed
