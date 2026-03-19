# CHUNK-13: Work Instructions — UI Pages

> **Size**: M (Medium) — 5 new page files + 2-3 edits
> **ROADMAP tasks**: Stage 5 steps 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.11
> **Prerequisites**: CHUNK-12 complete (models + service exist)
> **Detail plan**: `docs/phase-1/MODULE-03-visual-work-instructions.md`

---

## Scope

Build all UI pages for Visual Work Instructions: admin list + editor, operator
step-by-step viewer, feedback modal, feedback review page, shop floor integration,
and navigation links.

---

## Files to Read First

| File | Why |
|------|-----|
| `docs/phase-1/MODULE-03-visual-work-instructions.md` | Full UI specs |
| `Services/IWorkInstructionService.cs` | Available methods (from CHUNK-12) |
| `Components/Pages/Admin/Stages.razor` | Admin CRUD page pattern reference |
| `Components/Pages/ShopFloor/Index.razor` | Shop floor layout pattern |
| `Components/Pages/ShopFloor/Partials/GenericStage.razor` | Stage partial pattern |
| `Components/Shared/ConfirmDialog.razor` | Shared dialog pattern |
| `Components/Shared/AppModal.razor` | Shared modal pattern |
| `Components/Layout/NavMenu.razor` | Where to add nav links |
| `wwwroot/css/site.css` | CSS variable conventions |

---

## Tasks

### 1. Admin Instruction List Page
**New file**: `Components/Pages/Admin/WorkInstructions/Index.razor`
**Route**: `/admin/work-instructions`

- Table listing all work instructions: Part name, Stage name, Title, Rev #,
  Active status, Last Updated
- "New Instruction" button → inline create form or modal (Part dropdown, Stage
  dropdown, Title)
- Filter by Part or Stage (dropdowns)
- Click row → navigate to edit page
- Delete with `ConfirmDialog`
- Gate behind `FeatureGate Feature="work-instructions"` (if CHUNK-01 is done;
  otherwise add a TODO comment)

### 2. Admin Instruction Editor Page
**New file**: `Components/Pages/Admin/WorkInstructions/Edit.razor`
**Route**: `/admin/work-instructions/{Id:int}/edit`

- Header section: Part (read-only or dropdown), Stage (read-only or dropdown),
  Title input, Description textarea
- **Steps editor**: Numbered list with:
  - Title input, Body textarea, Warning text input, Tip text input
  - "Requires Signoff" toggle
  - Media gallery: upload button + thumbnail grid, delete per media item
  - "Add Step" button at bottom
  - "Delete Step" per step with ConfirmDialog
  - Drag-to-reorder handles (can use simple ↑↓ buttons if drag-drop is complex)
- "Save" button → calls `UpdateAsync` (auto-creates revision)
- "Preview as Operator" link → `/shopfloor/instructions/{Id}` in new tab
- Revision history panel: collapsible list showing rev #, date, who, change notes

### 3. Operator Instruction Viewer
**New file**: `Components/Pages/ShopFloor/WorkInstructionViewer.razor`
**Route**: `/shopfloor/instructions/{Id:int}`

- Clean, large-text design for tablet/shop floor use
- **One step at a time** navigation: "Previous" / "Next" buttons, step counter
  ("Step 3 of 7")
- Large image display with click-to-expand
- Video embed if present
- Warning section: yellow banner with ⚠️ icon (when `WarningText` present)
- Tip section: blue info banner (when `TipText` present)
- "Signoff" button when `RequiresOperatorSignoff` is true — records operator +
  timestamp (add a `StepSignoff` tracking mechanism, can be JSON on the step or
  a simple log)
- "🚩 Flag this step" button → opens feedback modal

### 4. Feedback Modal Component
**New file**: `Components/Pages/ShopFloor/FeedbackModal.razor`
(or `Components/Shared/InstructionFeedbackModal.razor`)

- Feedback type radio: Confusing, Incorrect Info, Safety Concern, Suggestion, Typo
- Comment textarea
- Submit button → calls `IWorkInstructionService.SubmitFeedbackAsync`
- Toast confirmation on success

### 5. Feedback Review Admin Page
**New file**: `Components/Pages/Admin/WorkInstructions/FeedbackReview.razor`
**Route**: `/admin/work-instructions/feedback`

- Table: Instruction title, Step #, Feedback type, Operator name, Comment,
  Submitted date, Status
- Filter by status: New | Acknowledged | Resolved | Won't Fix
- Action buttons per row: Acknowledge, Resolve, Won't Fix
- Click instruction link → navigates to editor for that instruction

### 6. Integrate into Shop Floor Stage Views
**Files**: All files in `Components/Pages/ShopFloor/Partials/*.razor`

At the top of each stage partial (before operator data entry), add a work
instruction banner:
- Inject `IWorkInstructionService`
- On init: load instruction via `GetByPartAndStageAsync(partId, stageId)`
- If instruction exists, show:
  ```
  📋 Work Instructions — Rev {N}    [Open Full View →]
  ```
  With a link to `/shopfloor/instructions/{Id}`
- If no instruction, show nothing (no empty state needed — instructions are optional)

This applies to all 10 partials: SLSPrinting, Depowdering, WireEDM, HeatTreat,
SurfaceFinishing, CNCMachining, QualityControl, Shipping, PostProcessing,
GenericStage.

### 7. Wire "View Work Instructions" Button on Stage.razor
**File**: `Components/Pages/ShopFloor/Stage.razor`

The H3.7 task added a disabled "View Instructions" placeholder button. Now:
- Enable the button
- Link it to `/shopfloor/instructions/{Id}` when an instruction exists for
  the current part + stage
- Keep it disabled/hidden when no instruction exists

### 8. Add Navigation Links
**File**: `Components/Layout/NavMenu.razor`

- Under the Admin section: add "Work Instructions" link → `/admin/work-instructions`
- Under Admin → Work Instructions: add "Feedback Review" sub-link → `/admin/work-instructions/feedback`
- The shop floor link is contextual (from stage execution), not a nav item

---

## CSS Notes

Use existing CSS variables (`--accent`, `--bg-card`, `--border-color`, etc.).
Key new styles needed:
- `.wi-step-card` — card for each step in the viewer
- `.wi-warning` — yellow warning banner (use `--warning-color` if defined, else `#f59e0b`)
- `.wi-tip` — blue info banner
- `.wi-media-grid` — thumbnail grid in editor
- `.wi-step-counter` — step number indicator

Add styles to `wwwroot/css/site.css` following the existing section comment pattern.

---

## Verification

1. Build passes
2. Admin can create a work instruction linked to a Part + Stage
3. Admin can add/edit/delete/reorder steps
4. Media can be uploaded and displays as thumbnails in editor
5. "Save" creates a revision visible in history panel
6. Operator viewer shows one step at a time with Previous/Next
7. Warning and Tip sections display when present
8. "Flag this step" submits feedback successfully
9. Admin feedback review page shows pending feedback with action buttons
10. Shop floor stage partials show instruction banner when instruction exists
11. Nav links appear in admin section

---

## Files Modified (fill in after completion)

- `Components/Pages/Admin/WorkInstructions/Index.razor` — NEW: Admin list page with filters, create modal, delete
- `Components/Pages/Admin/WorkInstructions/Edit.razor` — NEW: Admin editor with step management, media upload, revision history
- `Components/Pages/ShopFloor/WorkInstructionViewer.razor` — NEW: Operator step-by-step viewer with signoff, feedback
- `Components/Pages/Admin/WorkInstructions/FeedbackReview.razor` — NEW: Feedback review page with status actions
- `Components/Shared/WorkInstructionBanner.razor` — NEW: Shared banner for stage partials
- `Components/Pages/ShopFloor/Partials/SLSPrinting.razor` — Added WorkInstructionBanner
- `Components/Pages/ShopFloor/Partials/Depowdering.razor` — Added WorkInstructionBanner
- `Components/Pages/ShopFloor/Partials/WireEDM.razor` — Added WorkInstructionBanner
- `Components/Pages/ShopFloor/Partials/HeatTreatment.razor` — Added WorkInstructionBanner
- `Components/Pages/ShopFloor/Partials/SurfaceFinishing.razor` — Added WorkInstructionBanner
- `Components/Pages/ShopFloor/Partials/CNCMachining.razor` — Added WorkInstructionBanner
- `Components/Pages/ShopFloor/Partials/QualityControl.razor` — Added WorkInstructionBanner
- `Components/Pages/ShopFloor/Partials/Shipping.razor` — Added WorkInstructionBanner
- `Components/Pages/ShopFloor/Partials/LaserEngraving.razor` — Added WorkInstructionBanner
- `Components/Pages/ShopFloor/Partials/GenericStage.razor` — Added WorkInstructionBanner
- `Components/Layout/NavMenu.razor` — Added Work Instructions nav link
- `wwwroot/css/site.css` — Added .wi-step-card, .wi-step-counter, .wi-warning, .wi-tip, .wi-media-grid, .wi-media-item styles
