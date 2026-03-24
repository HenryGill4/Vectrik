# Module 03: Visual Work Instructions

## Status: [ ] Not Started
## Category: MES
## Phase: 1 — Core Production Engine
## Priority: P1 - Critical

---

## Overview

Visual Work Instructions (VWI) are step-by-step, media-rich instructions embedded
directly in work orders and shop floor views. ProShop claims up to 90% error
reduction and 50% faster operator onboarding from this feature. Our implementation
goes further with operator feedback loops, version control with visual diffs, and
an AR-ready data model.

**This module does not exist in the current foundation and must be built from scratch.**

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `ProductionStage` with custom form fields | ✅ Exists | `Models/ProductionStage.cs` |
| `StageExecution` (links job to stage) | ✅ Exists | `Models/StageExecution.cs` |
| Dynamic form renderer (JS) | ✅ Exists | `wwwroot/js/dynamic-form-renderer.js` |
| Shop floor stage views | ✅ Partial | `Components/Pages/ShopFloor/` |

**Gap**: No `WorkInstruction` model, no step editor, no media attachment, no version control for instructions, no operator feedback mechanism.

---

## What Needs to Be Built

### 1. Database Models (New)
- `WorkInstruction` — linked to Part + Stage combination
- `WorkInstructionStep` — ordered steps within an instruction
- `WorkInstructionMedia` — images/videos attached to steps
- `WorkInstructionRevision` — version snapshot for audit history
- `OperatorFeedback` — operator flags on confusing steps

### 2. Service Layer (New)
- `WorkInstructionService` — full CRUD + versioning
- Integration into `StageExecution` display (fetch instructions when operator starts stage)

### 3. UI Components (New)
- **Instruction Editor** (`/admin/work-instructions/{id}/edit`) — rich step editor for admins
- **Instruction Viewer** (`/shopfloor/instructions/{id}`) — clean operator-facing view
- **Feedback Modal** — "Flag this step" with reason selection
- **Revision Diff View** — side-by-side comparison of instruction versions

---

## Implementation Steps

### Step 1 — Create WorkInstruction Model
**New File**: `Models/WorkInstruction.cs`
```csharp
public class WorkInstruction
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public int ProductionStageId { get; set; }
    public ProductionStage ProductionStage { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<WorkInstructionStep> Steps { get; set; } = new List<WorkInstructionStep>();
    public ICollection<WorkInstructionRevision> Revisions { get; set; } = new List<WorkInstructionRevision>();
}
```

### Step 2 — Create WorkInstructionStep Model
**New File**: `Models/WorkInstructionStep.cs`
```csharp
public class WorkInstructionStep
{
    public int Id { get; set; }
    public int WorkInstructionId { get; set; }
    public WorkInstruction WorkInstruction { get; set; } = null!;
    public int StepOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;          // Markdown/HTML content
    public string? WarningText { get; set; }                  // Safety warning (shown prominently)
    public string? TipText { get; set; }                      // Pro tip for operators
    public bool RequiresOperatorSignoff { get; set; } = false;
    public ICollection<WorkInstructionMedia> Media { get; set; } = new List<WorkInstructionMedia>();
    public ICollection<OperatorFeedback> Feedback { get; set; } = new List<OperatorFeedback>();
}
```

### Step 3 — Create WorkInstructionMedia Model
**New File**: `Models/WorkInstructionMedia.cs`
```csharp
public class WorkInstructionMedia
{
    public int Id { get; set; }
    public int WorkInstructionStepId { get; set; }
    public WorkInstructionStep Step { get; set; } = null!;
    public MediaType MediaType { get; set; }          // Image, Video, PDF, Model3D
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;     // relative path under wwwroot/uploads/
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public enum MediaType { Image, Video, PDF, Model3D }
```

### Step 4 — Create WorkInstructionRevision Model
**New File**: `Models/WorkInstructionRevision.cs`
```csharp
public class WorkInstructionRevision
{
    public int Id { get; set; }
    public int WorkInstructionId { get; set; }
    public WorkInstruction WorkInstruction { get; set; } = null!;
    public int RevisionNumber { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;  // JSON snapshot of all steps
    public string? ChangeNotes { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 5 — Create OperatorFeedback Model
**New File**: `Models/OperatorFeedback.cs`
```csharp
public class OperatorFeedback
{
    public int Id { get; set; }
    public int WorkInstructionStepId { get; set; }
    public WorkInstructionStep Step { get; set; } = null!;
    public string OperatorUserId { get; set; } = string.Empty;
    public FeedbackType FeedbackType { get; set; }
    public string? Comment { get; set; }
    public FeedbackStatus Status { get; set; } = FeedbackStatus.New;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public enum FeedbackType { Confusing, IncorrectInfo, SafetyConcern, Suggestion, Typo }
public enum FeedbackStatus { New, Acknowledged, Resolved, WontFix }
```

### Step 6 — Register DbSets
**File**: `Data/TenantDbContext.cs`
Add:
```csharp
public DbSet<WorkInstruction> WorkInstructions { get; set; }
public DbSet<WorkInstructionStep> WorkInstructionSteps { get; set; }
public DbSet<WorkInstructionMedia> WorkInstructionMedia { get; set; }
public DbSet<WorkInstructionRevision> WorkInstructionRevisions { get; set; }
public DbSet<OperatorFeedback> OperatorFeedback { get; set; }
```

### Step 7 — Create WorkInstructionService
**New File**: `Services/WorkInstructionService.cs`
**New File**: `Services/IWorkInstructionService.cs`

Interface:
```csharp
public interface IWorkInstructionService
{
    Task<List<WorkInstruction>> GetAllAsync(string tenantCode);
    Task<WorkInstruction?> GetByPartAndStageAsync(int partId, int stageId, string tenantCode);
    Task<WorkInstruction?> GetByIdAsync(int id, string tenantCode);
    Task<WorkInstruction> CreateAsync(WorkInstruction instruction, string tenantCode);
    Task UpdateAsync(WorkInstruction instruction, string tenantCode);  // auto-snapshot revision
    Task DeleteAsync(int id, string tenantCode);
    Task<WorkInstructionStep> AddStepAsync(int instructionId, WorkInstructionStep step, string tenantCode);
    Task UpdateStepAsync(WorkInstructionStep step, string tenantCode);
    Task DeleteStepAsync(int stepId, string tenantCode);
    Task ReorderStepsAsync(int instructionId, List<int> orderedStepIds, string tenantCode);
    Task<string> UploadMediaAsync(int stepId, IBrowserFile file, string tenantCode);
    Task DeleteMediaAsync(int mediaId, string tenantCode);
    Task SubmitFeedbackAsync(OperatorFeedback feedback, string tenantCode);
    Task<List<OperatorFeedback>> GetPendingFeedbackAsync(string tenantCode);
    Task<List<WorkInstructionRevision>> GetRevisionsAsync(int instructionId, string tenantCode);
}
```

Implementation notes:
- `UpdateAsync` automatically creates a `WorkInstructionRevision` snapshot before saving
- `UploadMediaAsync` saves file to `wwwroot/uploads/instructions/{tenantCode}/` and returns relative URL
- Steps returned ordered by `StepOrder`

### Step 8 — Register Service in DI
**File**: `Program.cs`
```csharp
builder.Services.AddScoped<IWorkInstructionService, WorkInstructionService>();
```

### Step 9 — Admin Instruction Editor
**New File**: `Components/Pages/Admin/WorkInstructions/Index.razor`
**Route**: `/admin/work-instructions`

UI requirements:
- List of all work instructions with Part name, Stage name, Rev #, Last Updated
- "New Instruction" button
- Filter by Part or Stage
- Click row → `/admin/work-instructions/{id}/edit`

**New File**: `Components/Pages/Admin/WorkInstructions/Edit.razor`
**Route**: `/admin/work-instructions/{id:int}/edit`

UI requirements:
- Header: Part selector, Stage selector, Title, Description
- **Steps Editor Panel**:
  - Numbered step list with drag-to-reorder handles
  - Each step: Title input, Body textarea (supports Markdown), Warning field (shows in yellow), Tip field (shows in blue)
  - "Requires Signoff" toggle per step
  - **Media Upload**: drag-drop or file picker, shows thumbnail gallery
  - "Add Step" / "Delete Step" per row
- "Save & Create Revision" button — saves and auto-increments revision
- "Preview as Operator" link — opens viewer in new tab
- Revision history sidebar showing previous versions

### Step 10 — Operator Instruction Viewer
**New File**: `Components/Pages/ShopFloor/WorkInstructionViewer.razor`
**Route**: `/shopfloor/instructions/{id:int}`

UI requirements:
- Clean, large-text design for shop floor tablet use
- One step at a time with "Previous" / "Next" navigation
- Step number indicator ("Step 3 of 7")
- Large images with tap-to-zoom
- Video embed with controls
- Warning section: yellow banner with ⚠️ icon
- Tip section: blue info banner
- "Signoff" button (appears when `RequiresOperatorSignoff = true`) — records operator + timestamp
- "Flag this step" button → opens feedback modal

**New File**: `Components/Pages/ShopFloor/FeedbackModal.razor`
UI requirements:
- Feedback type radio buttons (Confusing, Incorrect Info, Safety Concern, Suggestion, Typo)
- Comment textarea
- Submit button

### Step 11 — Integrate Viewer into Shop Floor Stage Views
**File**: `Components/Pages/ShopFloor/StageViews/*.razor` (each stage partial)

Add to the top of each stage view before the operator data entry form:
```razor
@if (workInstruction != null)
{
    <div class="work-instruction-panel">
        <div class="wi-header">
            <span>📋 Work Instructions — Rev @workInstruction.RevisionNumber</span>
            <a href="/shopfloor/instructions/@workInstruction.Id" target="_blank">
                Open Full View
            </a>
        </div>
        <!-- Quick step summary inline -->
    </div>
}
```

Inject `IWorkInstructionService` in each stage partial and load the instruction for the current `PartId` + `StageId` combination.

### Step 12 — Pending Feedback Admin View
**New File**: `Components/Pages/Admin/WorkInstructions/FeedbackReview.razor`
**Route**: `/admin/work-instructions/feedback`

UI requirements:
- Table: Step reference, Feedback type, Operator, Comment, Submitted, Status
- "Acknowledge" / "Resolve" / "Won't Fix" action buttons
- Filter by Status (New | Acknowledged | Resolved)

### Step 13 — EF Core Migration
```bash
dotnet ef migrations add AddWorkInstructions --context TenantDbContext
dotnet ef database update
```

---

## File Upload Configuration

**File**: `Program.cs`
Add Blazor file upload configuration:
```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Increase file upload limit for instruction media
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});
```

Add static file serving for uploads:
```csharp
app.UseStaticFiles(); // already exists — uploads folder auto-served
```

Create uploads directory structure in `wwwroot/uploads/instructions/` (add `.gitkeep`).

---

## Acceptance Criteria

- [ ] Admin can create a work instruction linked to a Part + Stage
- [ ] Admin can add, reorder, and delete steps
- [ ] Steps support text, warning, and tip fields
- [ ] Images can be uploaded to steps and display as thumbnails
- [ ] "Save" creates a revision snapshot automatically
- [ ] Operator view shows instructions one step at a time
- [ ] Steps requiring signoff record operator + timestamp on signoff
- [ ] Operator can flag a step with feedback type and comment
- [ ] Admin can view and resolve pending operator feedback
- [ ] Instructions are automatically fetched and previewed in shop floor stage views

---

## Dependencies

- **Module 04** (Shop Floor) — Instructions displayed during stage execution
- **Module 08** (Parts/PDM) — Part selection in instruction editor
- **Module 15** (Document Control) — Future: instructions as controlled documents

---

## Future Enhancements (Post-MVP)

- 3D model viewer (GLTF/OBJ) embedded in steps
- AR-ready data model (step coordinates for headset overlay)
- Video recording directly from tablet camera
- Version diff view showing changed steps highlighted
- Tribal knowledge attachments (operator tips linked to specific parts/operations)
