# Scheduler Overhaul Plan

## Overview

Six phases, execute in order A → B → D → C → E → F. Each phase ships fully complete and tested before the next begins.

**Goal:** Transform the scheduler into a work-order-driven command center with certified build templates, demand-driven suggestions, infinite-scroll Gantt, and drag-and-drop scheduling.

### Key Decisions

| # | Decision | Impact |
|---|----------|--------|
| 1 | **Certified builds are reusable templates** with slicer-verified data | `BuildTemplate` + `BuildTemplatePart` entities |
| 2 | **Templates created manually AND from completed builds** | Both paths into `IBuildTemplateService` |
| 3 | **Part version changes invalidate templates** — must re-slice and re-certify | `PartVersionHash` + `NeedsRecertification` flag, hook in `PartService.UpdatePartAsync` |
| 4 | **Both single-part and multi-part templates** supported | `BuildTemplatePart` is 1..N per template |
| 5 | **Work Orders view is the primary scheduling entry point** | Redesigned with certified build cards, quick-schedule, suggestions |
| 6 | **Drag-and-drop conflict resolution: confirmation dialog** | "This will push Build X back 18h. [Move & Shift] [Cancel]" |
| 7 | **Infinite horizontal scroll replaces discrete zoom** | Continuous `pixelsPerHour`, lazy data loading, virtual rendering |
| 8 | **Future: machine API for real slicer data** | Not built now; `EstimatedDurationHours` field and template structure support it |

### Execution Order

```
Phase A (Templates: Data + Service)        ← START HERE
  ↓
Phase B (WO Command Center)                ← depends on A
  ↓
Phase D (Infinite Scroll + Fluid Zoom)     ← independent of B/C, do before E
  ↓
Phase C (Build Suggestions)                ← depends on A+B for UI
  ↓
Phase E (Drag-and-Drop)                    ← depends on D (shares viewport)
  ↓
Phase F (Integration + Polish)             ← depends on all above
```

---

## Phase A — Build Templates (Certified Builds): Data + Service

**Goal:** Create the `BuildTemplate` and `BuildTemplatePart` entities, `IBuildTemplateService` with full CRUD, instantiation, certification, and part-version invalidation. Migration. DI registration. Tests. No UI yet.

### A.1 Create Model — `Models/BuildTemplate.cs`

```csharp
public class BuildTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public BuildTemplateStatus Status { get; set; } = BuildTemplateStatus.Draft;

    // Material constraint — all parts on this template must share the same material
    public int? MaterialId { get; set; }
    public virtual Material? Material { get; set; }

    // Build configuration
    public int StackLevel { get; set; } = 1;

    [Range(0.1, 500)]
    public double EstimatedDurationHours { get; set; }

    // JSON — laser power, layer thickness, scan speed, etc.
    public string? BuildParameters { get; set; }

    // Certification
    [MaxLength(100)]
    public string? CertifiedBy { get; set; }
    public DateTime? CertifiedDate { get; set; }

    // Usage tracking
    public int UseCount { get; set; }
    public DateTime? LastUsedDate { get; set; }

    // Source — if created from a completed build
    public int? SourceBuildPackageId { get; set; }
    public virtual BuildPackage? SourceBuildPackage { get; set; }

    // Part version tracking — invalidated when any part on the template is updated
    [MaxLength(200)]
    public string? PartVersionHash { get; set; }
    public bool NeedsRecertification { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual ICollection<BuildTemplatePart> Parts { get; set; } = new List<BuildTemplatePart>();

    // Computed
    [NotMapped]
    public int TotalPartCount => Parts?.Sum(p => p.Quantity) ?? 0;

    [NotMapped]
    public int UniquePartCount => Parts?.Select(p => p.PartId).Distinct().Count() ?? 0;

    [NotMapped]
    public bool IsCertified => Status == BuildTemplateStatus.Certified && !NeedsRecertification;
}
```

### A.2 Create Enum — `BuildTemplateStatus`

Add to `Models/Enums/ManufacturingEnums.cs`:

```csharp
public enum BuildTemplateStatus
{
    Draft,
    Certified,
    Archived
}
```

### A.3 Create Model — `Models/BuildTemplatePart.cs`

```csharp
public class BuildTemplatePart
{
    public int Id { get; set; }

    [Required]
    public int BuildTemplateId { get; set; }

    [Required]
    public int PartId { get; set; }

    [Required, Range(1, 500)]
    public int Quantity { get; set; } = 1;

    public int StackLevel { get; set; } = 1;

    [MaxLength(500)]
    public string? PositionNotes { get; set; }

    // Navigation
    public virtual BuildTemplate BuildTemplate { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
}
```

### A.4 Update `TenantDbContext.cs`

Add DbSets:
```csharp
public DbSet<BuildTemplate> BuildTemplates { get; set; }
public DbSet<BuildTemplatePart> BuildTemplateParts { get; set; }
```

In `OnModelCreating`:
```csharp
modelBuilder.Entity<BuildTemplate>()
    .HasMany(t => t.Parts)
    .WithOne(p => p.BuildTemplate)
    .HasForeignKey(p => p.BuildTemplateId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<BuildTemplate>()
    .HasOne(t => t.SourceBuildPackage)
    .WithMany()
    .HasForeignKey(t => t.SourceBuildPackageId)
    .OnDelete(DeleteBehavior.SetNull);

modelBuilder.Entity<BuildTemplate>()
    .HasIndex(t => t.Status);

modelBuilder.Entity<BuildTemplatePart>()
    .HasIndex(p => new { p.BuildTemplateId, p.PartId });
```

### A.5 Create Interface — `Services/IBuildTemplateService.cs`

```csharp
public interface IBuildTemplateService
{
    // CRUD
    Task<List<BuildTemplate>> GetAllAsync(BuildTemplateStatus? statusFilter = null);
    Task<BuildTemplate?> GetByIdAsync(int id);
    Task<BuildTemplate> CreateAsync(BuildTemplate template);
    Task<BuildTemplate> UpdateAsync(BuildTemplate template);
    Task ArchiveAsync(int templateId);

    // Template parts
    Task<BuildTemplatePart> AddPartAsync(int templateId, int partId, int quantity, int stackLevel = 1, string? positionNotes = null);
    Task<BuildTemplatePart> UpdatePartAsync(int templatePartId, int quantity, int stackLevel, string? positionNotes = null);
    Task RemovePartAsync(int templatePartId);

    // Certification
    Task<BuildTemplate> CertifyAsync(int templateId, string certifiedBy);
    Task<BuildTemplate> RecertifyAsync(int templateId, string certifiedBy);

    // Instantiation — create a BuildPackage from a template, linked to a WO line
    Task<BuildPackage> InstantiateAsync(int templateId, string machineId, string createdBy, int? workOrderLineId = null);

    // Create template from a completed build
    Task<BuildTemplate> CreateFromBuildPackageAsync(int buildPackageId, string createdBy);

    // Part lookup
    Task<List<BuildTemplate>> GetTemplatesForPartAsync(int partId, bool certifiedOnly = true);
    Task<List<BuildTemplate>> GetTemplatesNeedingRecertificationAsync();

    // Part version invalidation — called when a Part is updated
    Task InvalidateTemplatesForPartAsync(int partId);

    // Part version hash computation
    string ComputePartVersionHash(IEnumerable<Part> parts);
}
```

### A.6 Implement — `Services/BuildTemplateService.cs`

Key implementation details:

**`CertifyAsync`:**
1. Verify template has at least one part
2. Verify `EstimatedDurationHours > 0`
3. Compute `PartVersionHash` from current part versions (use `Part.LastModifiedDate` ticks concatenated + hashed)
4. Set `Status = Certified`, `CertifiedBy`, `CertifiedDate`, `NeedsRecertification = false`
5. Save

**`RecertifyAsync`:**
1. Same validation as `CertifyAsync`
2. Recompute `PartVersionHash`
3. Set `NeedsRecertification = false`, update `CertifiedBy`, `CertifiedDate`

**`InstantiateAsync`:**
1. Load template with parts
2. Verify `IsCertified` (certified and not needing recertification)
3. Create new `BuildPackage`:
   - `Name = template.Name + " — " + DateTime.UtcNow.ToString("MMdd-HHmm")`
   - `MachineId = machineId`
   - `Status = BuildPackageStatus.Sliced` (slicer data is pre-verified from template)
   - `IsSlicerDataEntered = true`
   - `EstimatedDurationHours = template.EstimatedDurationHours`
   - `Material = template.Material?.Name`
   - `BuildParameters = template.BuildParameters`
   - `CreatedBy = createdBy`
4. For each `BuildTemplatePart`:
   - Create `BuildPackagePart` with same `PartId`, `Quantity`, `StackLevel`
   - If `workOrderLineId` provided, set `WorkOrderLineId` on the first matching part
5. Increment `template.UseCount`, set `template.LastUsedDate`
6. Save all, return the new `BuildPackage`

**`CreateFromBuildPackageAsync`:**
1. Load completed `BuildPackage` with parts
2. Verify `Status == Completed`
3. Create `BuildTemplate`:
   - Copy `EstimatedDurationHours`, `Material`, `BuildParameters`
   - `SourceBuildPackageId = buildPackageId`
   - `Status = Draft` (user must explicitly certify)
   - `Name = "Template from " + buildPackage.Name`
4. Copy each `BuildPackagePart` → `BuildTemplatePart`
5. Save, return template

**`InvalidateTemplatesForPartAsync`:**
1. Query all `BuildTemplates` where `Status == Certified` and `Parts.Any(p => p.PartId == partId)`
2. Set `NeedsRecertification = true` on each
3. Save

**`ComputePartVersionHash`:**
1. Concatenate `$"{part.Id}:{part.LastModifiedDate.Ticks}"` for each part, sorted by Id
2. SHA256 hash → hex string (first 32 chars)

### A.7 Hook into `PartService.UpdatePartAsync`

After saving the part update, call:
```csharp
await _buildTemplateService.InvalidateTemplatesForPartAsync(part.Id);
```

This requires injecting `IBuildTemplateService` into `PartService` (or using a domain event pattern — injecting is simpler).

### A.8 Register in DI — `Program.cs`

```csharp
builder.Services.AddScoped<IBuildTemplateService, BuildTemplateService>();
```

### A.9 Migration — `AddBuildTemplates`

- Create `BuildTemplates` table
- Create `BuildTemplateParts` table
- FKs: `BuildTemplatePart.BuildTemplateId → BuildTemplates.Id`, `BuildTemplatePart.PartId → Parts.Id`
- FK: `BuildTemplate.SourceBuildPackageId → BuildPackages.Id` (SET NULL on delete)
- FK: `BuildTemplate.MaterialId → Materials.Id` (SET NULL on delete)
- Index: `BuildTemplates.Status`
- Index: `BuildTemplateParts(BuildTemplateId, PartId)`

### A.10 Tests

- `BuildTemplateServiceTests.cs`:
  - Create template, add parts, certify → verify status and hash
  - Instantiate certified template → verify BuildPackage created with correct data
  - Instantiate template needing recertification → should throw
  - Create from completed build → verify template mirrors build
  - Invalidate on part update → verify `NeedsRecertification = true`
  - Recertify → verify hash updated, flag cleared
  - Archive → verify status
- Verify existing tests still pass

### A.11 Deliverables Checklist

```
[x] BuildTemplate model created
[x] BuildTemplatePart model created
[x] BuildTemplateStatus enum added
[x] TenantDbContext updated (DbSets, OnModelCreating)
[x] IBuildTemplateService interface created
[x] BuildTemplateService implemented
[x] PartService.UpdatePartAsync hooks InvalidateTemplatesForPartAsync
[x] DI registration in Program.cs
[x] Migration created and runs
[x] Tests written and passing
[x] Full build passes
```

---

## Phase B — Work Orders Command Center

**Goal:** Redesign the scheduler's Work Orders view as the primary scheduling entry point. Integrate certified build lookup, quick-schedule from template, "New Build" redirect flow, and "Certify as Template" on the Builds page.

### B.1 Redesign Scheduler Work Orders View — `Components/Pages/Scheduler/Index.razor`

Replace the existing `_viewMode == "orders"` section (lines ~510–688) with the new command center layout.

**Data to load (add to `LoadData`):**
- `_buildTemplates` — all certified templates (for matching to WO lines)
- `_demandSummary` — computed outstanding demand per WO line (reuse existing `GetOutstanding` logic)

**New fields in `@code`:**
```csharp
private List<BuildTemplate> _buildTemplates = new();
private bool _showQuickScheduleModal;
private BuildTemplate? _selectedTemplate;
private WorkOrderLine? _quickScheduleWoLine;
private WorkOrder? _quickScheduleWo;
private string _quickScheduleMachineId = "";
private DateTime? _quickScheduleStartAfter;
```

**Load templates in `LoadData`:**
```csharp
_buildTemplates = await BuildTemplateService.GetAllAsync(BuildTemplateStatus.Certified);
```

**New layout structure for `_viewMode == "orders"`:**

1. **Stats row** — same as current but add: "Templates Available", "Needing Re-cert"
2. **Per Work Order cards** (sorted by due date, overdue first):
   - WO header: order number, customer, due date with urgency badge, priority
   - Per line item:
     - Type badge (🖨️ SLS / ⚙️ CNC)
     - Part number + name
     - Progress: ordered / produced / in-builds / outstanding
     - Progress bar (same as current)
     - **For SLS lines with outstanding demand:**
       - Query `_buildTemplates.Where(t => t.Parts.Any(p => p.PartId == line.PartId) && t.IsCertified)`
       - If matches found: show certified build cards with "📅 Schedule This Build" button
       - If templates exist but need recertification: show ⚠️ badge
       - If no matches: show "🆕 New Build" button
       - Always show "🖨️ Create Build" fallback (current behavior)
     - **For CNC lines:** keep current "⚡ Auto-Schedule Job" / "⚙️ Add Job" buttons

3. **Quick Schedule Modal** (new):
   - Triggered by "📅 Schedule This Build" on a certified template card
   - Shows: template name, parts, duration, use count
   - Machine picker (SLS machines only)
   - Optional: start after datetime
   - "📅 Schedule" button → calls `BuildTemplateService.InstantiateAsync` then `BuildScheduling.ScheduleBuildAsync`
   - On success: toast, close modal, reload data
   - WO line automatically linked via `workOrderLineId`

### B.2 "New Build" Redirect Flow

When user clicks "🆕 New Build" on an SLS line with no certified template:

1. Navigate to: `/builds?newForPart={partId}&woLineId={lineId}&returnTo=/scheduler?view=orders`
2. `Builds/Index.razor` reads query params:
   - `newForPart` → auto-opens create build modal with that part pre-selected
   - `woLineId` → pre-links the WO line to the build
   - `returnTo` → after build is saved (Draft created), shows banner: "🔗 Return to Work Orders" linking back
3. User completes build setup (slicer data entry, etc.)
4. On the Builds page, completed builds show "✅ Certify as Template" button

### B.3 "Certify as Template" on Builds/Index.razor

Add to completed build cards/detail:
- Button: "✅ Certify as Reusable Template"
- On click: calls `BuildTemplateService.CreateFromBuildPackageAsync`
- Then shows sub-modal: "Template created in Draft. Enter name and certify?"
  - Name input (pre-filled)
  - "Certify Now" button → calls `CertifyAsync`
  - "Save as Draft" button → saves without certifying
- Toast on success

### B.4 Update `Builds/Index.razor` for Query Params

Read `[SupplyParameterFromQuery]` for:
- `newForPart` (int?) — auto-open create modal
- `woLineId` (int?) — pre-link WO line
- `returnTo` (string?) — show return banner

### B.5 Inject `IBuildTemplateService` in Scheduler

Add to `@inject` block:
```razor
@inject IBuildTemplateService BuildTemplateService
```

### B.6 Deliverables Checklist

```
[x] Scheduler WO view redesigned with certified build cards
[x] Quick Schedule modal implemented
[x] "New Build" redirect flow with query params
[x] Builds/Index.razor reads query params, auto-opens create modal
[x] "Certify as Template" button on completed builds
[x] Certification sub-modal on Builds page
[x] Return banner on Builds page
[x] IBuildTemplateService injected in Scheduler
[x] Full build passes
[ ] End-to-end flow tested: WO → template → schedule
```

---

## Phase D — Infinite Scroll + Fluid Zoom

**Goal:** Replace the discrete zoom system with continuous zoom and infinite horizontal scrolling. JS module for viewport management, Blazor integration, applied to all three Gantt views (Machines, SLS Builds, Part Path).

> **NOTE:** Phase D before Phase C because Phase E (drag-and-drop) depends on the viewport system from Phase D.

### D.1 Create JS Module — `wwwroot/js/gantt-viewport.js`

**Exports:**

```javascript
export function initGanttViewport(dotNetRef, containerEl, options)
// options: { initialPixelsPerHour, minPxPerHour, maxPxPerHour, dataBufferHours }

export function dispose()
export function scrollToTime(isoDateTimeString)
export function setZoom(pixelsPerHour)
export function getViewport() // returns { startIso, endIso, pixelsPerHour }
```

**Core behavior:**

- `containerEl` is the `.gantt-container` div
- Tracks `pixelsPerHour` (continuous float, default ~6.0 for 2-week view)
- Renders a wide inner div whose width = `totalHoursInView * pixelsPerHour`
- Horizontal scroll is native CSS `overflow-x: auto` on the container
- On scroll: compute visible time range from `scrollLeft` + `clientWidth` → call `dotNetRef.invokeMethodAsync('OnViewportChanged', startIso, endIso, pixelsPerHour)` debounced at 200ms
- On Ctrl+wheel / trackpad pinch: adjust `pixelsPerHour` by multiplier (1.15× per step), anchored at cursor position. Recompute inner width, adjust `scrollLeft` to keep cursor time fixed. Call `OnViewportChanged`.
- Minimum `pixelsPerHour`: 0.5 (~3 month view at 1200px width)
- Maximum `pixelsPerHour`: 120 (~10-hour view at 1200px width)

**Infinite scroll:**
- Initial data covers `[now - 7d, now + 21d]` (28 day buffer)
- When scroll position approaches edge (within 20% of buffer): Blazor fetches more data, extends the range
- No hard boundaries — user can scroll indefinitely in either direction

### D.2 Update Scheduler Gantt Rendering

**Replace `_ganttSlots` system:**
- Remove `_ganttSlots` list, `BuildGanttSlots()`, slot-based grid
- Gantt bars are positioned absolutely: `left = (barStart - viewportStart).TotalHours * pixelsPerHour` px, `width = durationHours * pixelsPerHour` px
- Machine label column is fixed (CSS `position: sticky; left: 0`)
- Time header is generated from viewport range (computed in Blazor, not from slots)

**New `@code` fields:**
```csharp
private double _pixelsPerHour = 6.0;
private DateTime _viewportStart;
private DateTime _viewportEnd;
private DateTime _dataRangeStart;
private DateTime _dataRangeEnd;
```

**`[JSInvokable] OnViewportChanged`:**
```csharp
[JSInvokable]
public async Task OnViewportChanged(string startIso, string endIso, double pixelsPerHour)
{
    _viewportStart = DateTime.Parse(startIso, null, System.Globalization.DateTimeStyles.RoundtripKind);
    _viewportEnd = DateTime.Parse(endIso, null, System.Globalization.DateTimeStyles.RoundtripKind);
    _pixelsPerHour = pixelsPerHour;

    // Check if we need more data
    var buffer = TimeSpan.FromDays(1);
    if (_viewportStart < _dataRangeStart + buffer || _viewportEnd > _dataRangeEnd - buffer)
    {
        await ExtendDataRange();
    }

    StateHasChanged();
}
```

**`ExtendDataRange`:**
- Compute new range: `[min(viewportStart - 7d, dataRangeStart), max(viewportEnd + 7d, dataRangeEnd)]`
- Fetch executions and timelines for the new range
- Merge with existing data (don't re-fetch what we already have)

**Bar positioning helper:**
```csharp
private double GetBarLeftPx(DateTime start) =>
    (start - _dataRangeStart).TotalHours * _pixelsPerHour;

private double GetBarWidthPx(DateTime start, DateTime end) =>
    Math.Max(2, (end - start).TotalHours * _pixelsPerHour);
```

**Time header rendering:**
- Compute tick marks based on `_pixelsPerHour`:
  - High zoom (>60 px/hr): show 15-min ticks
  - Medium zoom (10-60): show hourly ticks with day headers
  - Low zoom (2-10): show day ticks
  - Very low zoom (<2): show week ticks with month headers
- Render as absolutely positioned elements

### D.3 Update Controls Bar

- **Remove** all zoom level buttons ("30m", "1h", "2h", etc.)
- **Remove** `_zoomLevel` field and all `GetZoomConfig()` / `SetZoom()` methods
- **Keep** "Now" button — calls JS `scrollToTime(DateTime.UtcNow)`
- **Keep** ◀ ▶ nav buttons — scroll by 50% of viewport width via JS
- **Convert** date inputs to viewport indicators:
  - Show `_viewportStart` and `_viewportEnd` as read-only formatted dates
  - Clicking opens date picker → jumps viewport to that date via JS `scrollToTime`

### D.4 CSS Updates — `wwwroot/css/site.css`

- `.gantt-container`: `overflow-x: auto; position: relative;`
- `.gantt-inner`: `position: relative; min-height: 100%;` width set dynamically
- `.gantt-resource-label`: `position: sticky; left: 0; z-index: 2; background: var(--bg-secondary);`
- `.gantt-bar`: `position: absolute; top: 4px; bottom: 4px;` (left and width set inline)
- `.gantt-time-header`: `position: sticky; top: 0; z-index: 3;`
- `.gantt-today-line`: `position: absolute;` left computed from viewport
- Remove all grid-based gantt styles (column span, etc.)

### D.5 Apply to All Gantt Views

1. **Machines view** (`_viewMode == "gantt"`)
2. **SLS Builds view** (`_viewMode == "builds"`)
3. **Part Path view** (`_viewMode == "path"`)

Each uses the same `gantt-viewport.js` module and shared positioning helpers. The viewport state (`_pixelsPerHour`, `_viewportStart`, `_viewportEnd`) is shared across all views (single scheduler page state).

### D.6 Cleanup

Remove from `@code`:
- `_ganttSlots` field
- `_zoomLevel` field
- `BuildGanttSlots()` method
- `GetZoomConfig()` method
- `SetZoom()` method
- `IsDayLevel` property
- `GetColumnMinWidth()` method
- `GetGanttContainerStyle()` method
- `GetGanttPosition()` / `GetGanttWidth()` (replaced by px-based helpers)
- `FormatSlotTime()` method
- `GetDateGroups()` method
- `ZoomBtnClass()` method
- `JsZoomIn()` / `JsZoomOut()` / `StepZoom()` methods

Update `wwwroot/js/site.js`:
- Remove `opcentrix.initSchedulerZoom` function (replaced by `gantt-viewport.js`)

### D.7 Deliverables Checklist

```
[x] gantt-viewport.js module created
[x] Horizontal scroll working (infinite in both directions)
[x] Scroll-wheel zoom working (continuous, cursor-anchored)
[x] Lazy data loading on scroll (ExtendDataRange)
[x] Bar positioning converted to absolute px (all three views)
[x] Time header rendering (adaptive tick marks based on zoom)
[x] Sticky machine labels
[x] Today line positioned correctly
[x] "Now" button scrolls to current time
[x] ◀ ▶ buttons scroll by half viewport
[x] Date inputs show viewport range, editable to jump
[x] All discrete zoom buttons and code removed
[x] CSS updated for absolute positioning
[x] Old slot-based code removed
[x] All three Gantt views working (Machines, SLS Builds, Part Path)
[x] Full build passes
[ ] Scroll + zoom feels smooth at 60fps
```

---

## Phase C — Demand-Driven Build Suggestions

**Goal:** Build suggestion engine that analyzes outstanding WO demand, matches against certified templates, suggests single-part and mixed builds. Integrated into the WO command center from Phase B.

### C.1 Create Interface — `Services/IBuildSuggestionService.cs`

```csharp
public interface IBuildSuggestionService
{
    /// <summary>
    /// Generate build suggestions based on outstanding WO demand.
    /// Checks certified templates, suggests full/partial plates, mixed builds.
    /// </summary>
    Task<BuildSuggestionResult> GetSuggestionsAsync();
}

public record BuildSuggestionResult(
    List<TemplateSuggestion> TemplateSuggestions,
    List<MixedBuildSuggestion> MixedBuildSuggestions);

public record TemplateSuggestion(
    int BuildTemplateId,
    string TemplateName,
    int PartId,
    string PartNumber,
    int SuggestedQuantity,
    double EstimatedDurationHours,
    int UseCount,
    List<WorkOrderReference> FulfillsWorkOrders,
    string Rationale);

public record WorkOrderReference(
    int WorkOrderId,
    string OrderNumber,
    int WorkOrderLineId,
    int QuantityFulfilled,
    DateTime DueDate);

public record MixedBuildSuggestion(
    List<MixedBuildLine> Parts,
    string? MatchingTemplateName,
    int? MatchingTemplateId,
    double EstimatedDurationHours,
    List<WorkOrderReference> FulfillsWorkOrders,
    string Rationale);

public record MixedBuildLine(
    int PartId,
    string PartNumber,
    int SuggestedQuantity,
    string MaterialName);
```

### C.2 Implement — `Services/BuildSuggestionService.cs`

**`GetSuggestionsAsync`:**

1. **Get outstanding demand:**
   - Query `WorkOrderLines` where `WorkOrder.Status` is `Released` or `InProgress`
   - For each line: `outstanding = Quantity - ProducedQuantity - inBuildQty`
   - Only include lines where `outstanding > 0` and part is additive (`ManufacturingApproach.RequiresBuildPlate`)

2. **Match against certified templates (single-part suggestions):**
   - For each part with demand: `GetTemplatesForPartAsync(partId, certifiedOnly: true)`
   - For each matching template: calculate how many runs needed → `ceil(outstanding / template.TotalPartCount)`
   - Create `TemplateSuggestion` with `FulfillsWorkOrders` listing which WOs benefit
   - Sort by: WO priority desc, then due date asc, then plate utilization desc

3. **Mixed-build detection:**
   - Find parts with `outstanding < PlannedPartsPerBuildSingle` (partial plate demand)
   - Group by `MaterialId` (must match for mixing)
   - Within each material group: find combinations whose total ≤ max plate capacity
   - Check if a certified multi-part template already matches the combination
   - Prefer combinations where WO due dates align (within 7 days)
   - Create `MixedBuildSuggestion` with rationale like "Combine SUP-TUBE (40) + SUP-BAFFLE (20) — both due 07/15, Nylon 12, 78% plate fill"

4. **Deduplication:** Don't suggest a single-part build AND a mixed build for the same demand — prefer whichever has higher plate utilization

### C.3 Integrate into WO Command Center

Update the `_viewMode == "orders"` section from Phase B:

**Load suggestions in `LoadData`:**
```csharp
private BuildSuggestionResult? _suggestions;
// In LoadData:
_suggestions = await BuildSuggestionService.GetSuggestionsAsync();
```

**Render suggestions section** (above WO cards):
```
💡 BUILD SUGGESTIONS

[Template suggestion card]
  "SUP-TUBE Single Stack 76pc — 18.5h"
  "Fulfills WO-0047 (76pc). Used 12 times."
  [📅 Schedule from Template] [Dismiss]

[Mixed build suggestion card]
  "🔀 Mixed Build: SUP-TUBE (40) + SUP-BAFFLE (20)"
  "Both due 07/15 · Nylon 12 · Est 16h · 78% plate fill"
  "Fulfills WO-0047 (40pc) + WO-0048 (20pc)"
  [Create Mixed Build] [Dismiss]
```

**"Schedule from Template":** opens same Quick Schedule modal from Phase B

**"Create Mixed Build":**
1. Creates a new `BuildPackage` in Draft with both parts pre-added at suggested quantities
2. Links each part to its respective WO line
3. Navigates to `/builds/{id}` for slicer data entry
4. User can optionally certify it as a template after completion

**"Dismiss":** hide the suggestion card (session-scoped `HashSet<string>` — dismissed suggestion keys)

### C.4 Register in DI

```csharp
builder.Services.AddScoped<IBuildSuggestionService, BuildSuggestionService>();
```

### C.5 Deliverables Checklist

```
[x] IBuildSuggestionService interface created
[x] BuildSuggestionService implemented
[x] Single-part template suggestions working
[x] Mixed-build detection working (same material, aligned due dates)
[x] Suggestions integrated into WO command center
[x] "Schedule from Template" action working
[x] "Create Mixed Build" action working
[x] "Dismiss" hides suggestions for session
[x] DI registration
[x] Full build passes
[x] Suggestions appear correctly for test WO data
```

---

## Phase E — Drag-and-Drop Scheduling

**Goal:** Enable drag-and-drop on Gantt bars to reschedule builds and stage executions. Drag from sidebar/cards onto timelines. Confirmation dialog on conflicts.

### E.1 Create JS Module — `wwwroot/js/gantt-dragdrop.js`

**Exports:**

```javascript
export function initDragDrop(dotNetRef, containerEl)
export function dispose()
export function setDraggable(elements) // called after Blazor re-render
```

**Implementation approach — HTML5 Drag API:**

- On `dragstart`: store source data (`{ type, id }`) in `dataTransfer`; create ghost clone at 50% opacity
- On `dragover` on `.gantt-track` rows: compute time from cursor X position using `pixelsPerHour`; show vertical time indicator line; highlight the row
- On `dragleave`: remove highlights
- On `drop`: compute `targetMachineId` from the row's `data-machine-id`, `targetTime` from cursor position; call `dotNetRef.invokeMethodAsync('OnDragDrop', sourceType, sourceId, targetMachineId, targetTimeIso)`
- On `dragend`: cleanup ghost, indicators

**Data attributes on draggable elements:**
```html
<div class="gantt-bar" draggable="true"
     data-drag-type="build" data-drag-id="42" data-machine-id="3">
```

**Data attributes on drop targets:**
```html
<div class="gantt-track" data-machine-id="3" data-drop-target="true">
```

### E.2 Blazor Drag Handler — `[JSInvokable] OnDragDrop`

```csharp
[JSInvokable]
public async Task OnDragDrop(string sourceType, int sourceId, int targetMachineId, string targetTimeIso)
{
    var targetTime = DateTime.Parse(targetTimeIso, null, DateTimeStyles.RoundtripKind);

    switch (sourceType)
    {
        case "build":
            await HandleBuildDrop(sourceId, targetMachineId, targetTime);
            break;
        case "exec":
            await HandleExecDrop(sourceId, targetMachineId, targetTime);
            break;
        case "ready-build":
            await HandleReadyBuildDrop(sourceId, targetMachineId, targetTime);
            break;
        case "unscheduled":
            await HandleUnscheduledDrop(sourceId, targetMachineId, targetTime);
            break;
        case "template":
            await HandleTemplateDrop(sourceId, targetMachineId, targetTime);
            break;
    }

    await LoadData();
    StateHasChanged();
}
```

### E.3 Conflict Detection + Confirmation Modal

**`HandleBuildDrop` / `HandleExecDrop`:**

1. Load the existing timeline for `targetMachineId`
2. Compute the bar's new time range: `[targetTime, targetTime + duration]`
3. Check for overlaps with existing bars
4. **If no conflict:** directly reschedule (update DB, toast success)
5. **If conflict:** populate conflict modal and show it:

```csharp
private bool _showConflictModal;
private DragConflictInfo? _conflictInfo;

record DragConflictInfo(
    string SourceDescription,
    DateTime NewStart,
    DateTime NewEnd,
    List<ConflictingItem> Conflicts,
    Func<Task> ConfirmAction);

record ConflictingItem(
    string Name,
    DateTime CurrentStart,
    DateTime CurrentEnd,
    DateTime ShiftedStart,
    DateTime ShiftedEnd);
```

**Conflict modal UI:**
```
⚠️ Schedule Conflict

Moving "Build-SUP-001" to M4-1 at 07/12 08:00 → 07/13 02:30

This will affect:
  • Build-SUP-002: shifts from 07/12 08:00 → 07/13 08:30 (pushed 18.5h)
  • Build-SUP-003: shifts from 07/13 14:00 → 07/14 14:30 (pushed 18.5h)

[Move & Shift All]  [Cancel]
```

**"Move & Shift All":**
1. Update the dragged item's schedule
2. For each conflicting item: shift forward by the overlap amount + changeover buffer
3. Cascade: if shifted items now conflict with further items, shift those too
4. Save all in one transaction
5. Toast: "Rescheduled Build-SUP-001 + shifted 2 builds"

### E.4 Add `draggable` Attributes to Gantt Bars

Update all three Gantt views to add HTML5 drag attributes:

**Machines view — build-level bars:**
```html
<div class="gantt-bar" draggable="true"
     data-drag-type="build" data-drag-id="@first.BuildPackageId"
     data-machine-id="@machine.Id">
```

**Machines view — individual execution bars:**
```html
<div class="gantt-bar" draggable="true"
     data-drag-type="exec" data-drag-id="@exec.Id"
     data-machine-id="@machine.Id">
```

**SLS Builds view — build timeline bars:**
```html
<div class="gantt-bar" draggable="true"
     data-drag-type="build" data-drag-id="@entry.BuildPackageId"
     data-machine-id="@machine.Id">
```

**Ready Build cards:**
```html
<div class="builds-ready-card" draggable="true"
     data-drag-type="ready-build" data-drag-id="@build.Id">
```

**Unscheduled sidebar items:**
```html
<div class="sched-sidebar-item" draggable="true"
     data-drag-type="unscheduled" data-drag-id="@exec.Id">
```

### E.5 Drop Target Attributes

**Machine rows (all Gantt views):**
```html
<div class="gantt-track" data-machine-id="@machine.Id" data-drop-target="true">
```

### E.6 CSS for Drag Feedback — `wwwroot/css/site.css`

```css
/* Drag ghost */
.gantt-bar[draggable="true"] { cursor: grab; }
.gantt-bar.dragging { opacity: 0.4; }

/* Drop target highlight */
.gantt-track.drag-over {
    outline: 2px dashed var(--accent);
    outline-offset: -2px;
    background: rgba(var(--accent-rgb), 0.05);
}

/* Time indicator (positioned by JS) */
.gantt-drop-indicator {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 2px;
    background: var(--accent);
    z-index: 10;
    pointer-events: none;
}

/* Conflict flash */
.gantt-bar.conflict-flash {
    animation: conflict-pulse 0.6s ease-in-out 2;
}
@keyframes conflict-pulse {
    0%, 100% { box-shadow: none; }
    50% { box-shadow: 0 0 0 3px var(--danger); }
}
```

### E.7 JS Integration with Viewport Module

The drag-drop module needs to know the current `pixelsPerHour` and `dataRangeStart` to convert cursor X → time. It reads these from the viewport module:

```javascript
// In gantt-dragdrop.js
import { getViewport } from './gantt-viewport.js';

function cursorToTime(clientX, trackEl) {
    const { startIso, pixelsPerHour } = getViewport();
    const trackRect = trackEl.getBoundingClientRect();
    const scrollLeft = trackEl.closest('.gantt-container').scrollLeft;
    const xInTrack = clientX - trackRect.left + scrollLeft;
    const hoursOffset = xInTrack / pixelsPerHour;
    const start = new Date(startIso);
    return new Date(start.getTime() + hoursOffset * 3600000);
}
```

### E.8 Deliverables Checklist

```
[ ] gantt-dragdrop.js module created
[ ] Drag attributes added to all bar types (build, exec, ready-build, unscheduled)
[ ] Drop target attributes on machine rows
[ ] JS: ghost element, time indicator, row highlight
[ ] Blazor OnDragDrop handler implemented
[ ] Build drag → reschedule (time + machine)
[ ] Exec drag → reschedule (time + machine)
[ ] Ready build card drag → schedule on machine
[ ] Unscheduled sidebar drag → schedule on machine
[ ] Conflict detection working
[ ] Conflict confirmation modal with "Move & Shift All"
[ ] Cascade shift logic (push conflicting builds forward)
[ ] CSS drag feedback styles
[ ] Full build passes
[ ] Drag-and-drop feels responsive and intuitive
```

---

## Phase F — Integration, Polish, Refactor

**Goal:** Connect all pieces. Drag certified build cards from WO view to timeline. Refactor remaining views. Test coverage. Final polish.

### F.1 Template Card Drag from WO View → Timeline

In the WO command center, certified build template cards need a drag affordance:

```html
<div class="wo-template-card" draggable="true"
     data-drag-type="template" data-drag-id="@template.Id"
     data-wo-line-id="@line.Id">
```

**`HandleTemplateDrop`:**
1. `InstantiateAsync(templateId, machineId, userName, workOrderLineId)`
2. `ScheduleBuildAsync(newPackageId, targetMachineId, targetTime)`
3. Toast: "Build created from template and scheduled on M4-1 at 07/12 08:00"

This requires the WO view and the SLS Builds Gantt to be visible simultaneously, OR:
- Alternative: dragging a template card switches to the SLS Builds view with the build "held" — drop it on a machine row
- Simpler alternative: the "📅 Schedule This Build" modal from Phase B is the primary path; drag-to-timeline is a power-user shortcut if both panels are visible

**Decision: implement the modal path as primary (Phase B), drag as enhancement if layout permits.**

### F.2 Refactor Part Path View

- Convert to absolute positioning (done in Phase D)
- Add drag support for stage execution bars (same as Machines view)
- Ensure stage color legend still works

### F.3 Refactor Stages View

- Ensure queue counts update correctly after drag-and-drop rescheduling
- No drag support needed (stage queue is not a timeline)

### F.4 Refactor Table View

- Add visual indicator for draggable items (grip icon)
- "Drag to Gantt" is complex — defer to future. Table stays as click-to-reschedule.

### F.5 Navigation Flow Polish

- Test: WO → "New Build" → Build setup → slicer data → certify → return to WO → schedule
- Test: WO → "Schedule from Template" → quick modal → build on timeline
- Test: Suggestion → "Create Mixed Build" → navigate to Builds → setup → return
- Toast notifications on every action
- Error handling: if scheduling fails, show clear error in modal (not just generic toast)

### F.6 Template Management

Add to Builds/Index.razor (or new tab):
- Section: "📋 Build Templates" showing all templates with status badges
- Filter by: status (Draft/Certified/Archived), part, material
- Actions: Edit, Certify, Re-certify, Archive
- ⚠️ badge on templates needing recertification with "Re-Certify" button
- Click-to-expand: show template parts, slicer data, usage history

### F.7 Test Coverage

**Service tests:**
- `BuildTemplateServiceTests` (from Phase A — verify still passing)
- `BuildSuggestionServiceTests`:
  - No demand → no suggestions
  - Single part demand with matching template → template suggestion
  - Single part demand without template → no template suggestion (but user can create)
  - Multi-part demand with aligned dates + same material → mixed suggestion
  - Multi-part demand with different materials → no mixed suggestion
  - Already-fulfilled demand → no suggestions
- Integration test: `InstantiateAsync` → verify BuildPackage created correctly
- Integration test: `InvalidateTemplatesForPartAsync` → verify flag set

**Manual smoke test checklist:**
```
[ ] Open scheduler → WO view → see released WOs with demand
[ ] SLS line shows certified templates (or "New Build" if none)
[ ] Click "Schedule This Build" → modal → pick machine → schedule → build appears on timeline
[ ] Click "New Build" → navigates to Builds page → create build → certify → return → schedule
[ ] Build suggestion cards appear when demand exists
[ ] "Create Mixed Build" from suggestion → build created with multiple parts
[ ] Scroll Gantt horizontally → more data loads seamlessly
[ ] Zoom with scroll wheel → smooth, anchored at cursor
[ ] Drag a build bar to different time → confirms if conflict → reschedules
[ ] Drag a build bar to different machine → reschedules on new machine
[ ] Drag Ready Build card onto SLS timeline → schedules
[ ] Drag unscheduled sidebar item onto machine → schedules
[ ] Part update → template shows ⚠️ needs recertification
[ ] Re-certify template → ⚠️ cleared, template usable again
```

### F.8 Performance Check

- Verify scroll + zoom stays smooth with 50+ bars on screen
- Verify lazy loading doesn't cause visible flicker
- Verify drag-drop responsiveness (no lag between drag and visual feedback)
- If performance issues: profile and optimize (virtual rendering, DOM reduction)

### F.9 Deliverables Checklist

```
[ ] Template drag from WO view (if layout permits, else modal-only)
[ ] Part Path view fully converted + drag support
[ ] Stages view queue counts update after rescheduling
[ ] Navigation flow polished (all redirect paths tested)
[ ] Template management section on Builds page
[ ] Toast/error handling on all actions
[ ] BuildSuggestionService tests written and passing
[ ] Manual smoke test checklist completed
[ ] Performance verified (smooth at 50+ bars)
[ ] Full build passes
[ ] All existing tests still pass
```

---

## Files Changed Summary

| File | Phase | Change |
|------|-------|--------|
| `Models/BuildTemplate.cs` | A | **NEW** — reusable build configuration |
| `Models/BuildTemplatePart.cs` | A | **NEW** — parts in a template |
| `Models/Enums/ManufacturingEnums.cs` | A | Add `BuildTemplateStatus` enum |
| `Data/TenantDbContext.cs` | A | Add DbSets, relationships, indexes |
| `Services/IBuildTemplateService.cs` | A | **NEW** — template CRUD + instantiation |
| `Services/BuildTemplateService.cs` | A | **NEW** — implementation |
| `Services/PartService.cs` | A | Hook `InvalidateTemplatesForPartAsync` |
| `Services/IBuildSuggestionService.cs` | C | **NEW** — demand-driven suggestions |
| `Services/BuildSuggestionService.cs` | C | **NEW** — implementation |
| `Program.cs` | A, C | Register new services |
| `Components/Pages/Scheduler/Index.razor` | B, C, D, E, F | Major redesign: WO command center, viewport, drag-drop |
| `Components/Pages/Builds/Index.razor` | B, F | Query params, certify-as-template, template management |
| `wwwroot/js/gantt-viewport.js` | D | **NEW** — infinite scroll + fluid zoom |
| `wwwroot/js/gantt-dragdrop.js` | E | **NEW** — drag-and-drop scheduling |
| `wwwroot/css/site.css` | D, E | Gantt absolute positioning, drag feedback styles |
| `wwwroot/js/site.js` | D | Remove old `initSchedulerZoom` |
| `Data/Migrations/*.cs` | A | `AddBuildTemplates` migration |
| `Tests/BuildTemplateServiceTests.cs` | A | **NEW** — template service tests |
| `Tests/BuildSuggestionServiceTests.cs` | C | **NEW** — suggestion service tests |

---

## Execution Checklist

```
[x] Phase A: Build Templates — Data + Service
    [x] Models (BuildTemplate, BuildTemplatePart, enum)
    [x] TenantDbContext updated
    [x] IBuildTemplateService + implementation
    [x] PartService hook for invalidation
    [x] DI registration
    [x] Migration
    [x] Tests
    [x] Build passes

[x] Phase B: Work Orders Command Center
    [x] WO view redesigned with certified build cards
    [x] Quick Schedule modal
    [x] "New Build" redirect flow
    [x] Builds/Index.razor query param support
    [x] "Certify as Template" on completed builds
    [x] Build passes

[x] Phase D: Infinite Scroll + Fluid Zoom
    [x] gantt-viewport.js module
    [x] Blazor integration (OnViewportChanged, ExtendDataRange)
    [x] Bar positioning converted to absolute px
    [x] Time header rendering (adaptive)
    [x] Sticky labels, today line
    [x] Controls updated (Now, ◀ ▶, date inputs)
    [x] Old zoom system removed
    [x] All three Gantt views converted
    [x] Build passes

[ ] Phase C: Build Suggestions
    [x] IBuildSuggestionService + implementation
    [x] Single-part template suggestions
    [x] Mixed-build detection
    [x] Integrated into WO command center
    [x] Action buttons (Schedule, Create Mixed Build, Dismiss)
    [x] DI registration
    [x] Tests
    [x] Build passes

[ ] Phase E: Drag-and-Drop
    [ ] gantt-dragdrop.js module
    [ ] Drag attributes on all bar types
    [ ] Drop targets on machine rows
    [ ] Visual feedback (ghost, indicator, highlight)
    [ ] Blazor OnDragDrop handler
    [ ] All drag sources working (build, exec, ready, unscheduled)
    [ ] Conflict detection + confirmation modal
    [ ] Cascade shift logic
    [ ] Build passes

[ ] Phase F: Integration + Polish
    [ ] Template drag from WO (if applicable)
    [ ] Part Path + Stages views adapted
    [ ] Template management section
    [ ] Navigation flow polished
    [ ] All smoke tests passing
    [ ] Performance verified
    [ ] Full build passes
    [ ] All existing tests pass
```
