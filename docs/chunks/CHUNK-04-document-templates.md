> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# CHUNK-04: Document Templates

> **Size**: M (Medium) — ~6-8 file edits, 2 new template data seeds
> **ROADMAP tasks**: H6.15, H6.16, H6.17, H6.18
> **Prerequisites**: H1-H5 complete

---

## Scope

Create default document templates for Quote PDFs and Work Order Travelers, then
wire "Print/Export" buttons on the respective detail pages. The
`IDocumentTemplateService` already exists — this chunk creates the default
templates and connects the UI.

---

## Files to Read First

| File | Why |
|------|-----|
| `Services/IDocumentTemplateService.cs` | Understand template API |
| `Services/DocumentTemplateService.cs` | Understand rendering/generation logic |
| `Models/DocumentTemplate.cs` | Template model structure |
| `Components/Pages/Quotes/Details.razor` | Wire print/export button here |
| `Components/Pages/WorkOrders/Details.razor` | Wire print/export button here |

---

## Tasks

### 1. Create default Quote PDF template (H6.15)
Seed a `DocumentTemplate` record for quotes:
- `EntityType = "Quote"`
- `Name = "Standard Quote"`
- `IsDefault = true`
- Template body: HTML-based template with placeholders for quote data
  (company name, customer, quote number, line items table, totals, terms)
- Add to `DataSeedingService` or create a separate template seed

### 2. Wire "Print/Export" button on Quote Details (H6.16)
**File**: `Components/Pages/Quotes/Details.razor`
- Inject `IDocumentTemplateService`
- Add a "Print Quote" / "Export PDF" button in the header area
- On click: call `DocumentTemplateService.RenderAsync("Quote", quoteId)`
- Display the rendered HTML in a print-friendly modal or new tab
- Use `window.print()` JS interop for actual printing

### 3. Create default Work Order Traveler template (H6.17)
Seed a `DocumentTemplate` record for work orders:
- `EntityType = "WorkOrder"`
- `Name = "Job Traveler"`
- `IsDefault = true`
- Template body: routing steps table, part info, QC requirements,
  sign-off lines per stage, barcode placeholder for work order number

### 4. Wire "Print Traveler" button on WO Details (H6.18)
**File**: `Components/Pages/WorkOrders/Details.razor`
- Same pattern as quote print
- Button labeled "Print Traveler"
- Renders the traveler template with WO + job routing data

---

## Implementation Notes

- If `IDocumentTemplateService` doesn't have a render method yet, create a simple
  one that loads the template, replaces `{{placeholders}}` with entity data, and
  returns HTML string
- For v1, HTML-to-print is sufficient. Actual PDF generation (wkhtmltopdf,
  PuppeteerSharp) can be added later
- The print modal should use `@media print` CSS to hide the modal chrome

---

## Verification

1. Build passes
2. Go to Quote Details → click "Print Quote" → see formatted quote document
3. Use browser print → document looks professional
4. Go to WO Details → click "Print Traveler" → see routing traveler
5. Traveler shows all stages with sign-off lines

---

## Files Modified (fill in after completion)

- `Services/DataSeedingService.cs` — Added `SeedDocumentTemplatesAsync` with "Standard Quote" and "Job Traveler" default templates
- `Components/Pages/Quotes/Details.razor` — Injected `IDocumentTemplateService` + `IJSRuntime`, added "Print Quote" button and `PrintQuote()` method
- `Components/Pages/WorkOrders/Details.razor` — Injected `IDocumentTemplateService` + `IJSRuntime`, added "Print Traveler" button and `PrintTraveler()` method with lines + routing tables
- `wwwroot/js/site.js` — Added `window.opcentrix.printHtml(html, title)` JS interop function
