> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# Sprint 3: Work Orders + Quotes (Full Lifecycle)

> **Status**: NOT STARTED
> **Goal**: Complete quote→WO→release pipeline with line items and stage execution creation.
> **Depends on**: Sprint 2 (parts + stages configured)

---

## Tasks

```
[ ] 3.1  Quote creation — customer info + add line items (Part picker + qty)
[ ] 3.2  Quote Details — auto-calculate estimated cost from Part stage requirements × rates
[ ] 3.3  Quote Details — editable quoted price per part, auto-calc markup/margin
[ ] 3.4  Quote lifecycle — Draft → Sent → Accepted/Rejected/Expired status transitions
[ ] 3.5  Quote "Accept & Convert" — creates WorkOrder with lines matching quote lines
[ ] 3.6  WO creation — customer info, PO number, due date
[ ] 3.7  WO Details — add/remove line items (Part picker + qty)
[ ] 3.8  WO Details — "Release" action: creates StageExecution records for each line's required stages
[ ] 3.9  WO Details — pipeline visualization showing stage progress per line
[ ] 3.10 WO Details — status lifecycle (Draft → Released → InProgress → Complete → Cancelled)
[ ] 3.11 WO Index — % complete column (produced/ordered across all lines)
[ ] 3.12 WO Details — notes section
[ ] 3.13 Verify: create quote → accept → WO created → release → stage executions exist
```

---

## Acceptance Criteria

- Quotes auto-calculate cost: sum of (stage hours × hourly rate) per part × quantity
- "Accept & Convert" creates a WO with matching line items
- WO "Release" creates StageExecution rows for each part's required stages
- Pipeline visualization shows colored bars per stage (NotStarted/InProgress/Completed)
- WO status transitions are enforced (can't go from Complete back to Draft)
- Quote→WO link is visible on both pages

## Key Service Methods Needed

- `QuoteService.CalculateLineCostAsync(partId)` — sum stage costs
- `QuoteService.ConvertToWorkOrderAsync(quoteId)` — creates WO + lines
- `WorkOrderService.ReleaseWorkOrderAsync(woId)` — creates StageExecution records
- `WorkOrderService.GetCompletionPercentageAsync(woId)` — % complete

## Files to Touch

- `Components/Pages/Quotes/Index.razor` — creation form
- `Components/Pages/Quotes/Details.razor` — cost calc + convert button
- `Components/Pages/WorkOrders/Index.razor` — % complete column
- `Components/Pages/WorkOrders/Details.razor` — line management + release + pipeline
- `Services/QuoteService.cs` — cost calc + convert logic
- `Services/WorkOrderService.cs` — release logic
