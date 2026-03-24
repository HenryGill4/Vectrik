> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# CHUNK-12: Work Instructions — Models + Service

> **Size**: M (Medium) — 5 new files + 2 edits
> **ROADMAP tasks**: Stage 5 steps 5.1, 5.2, 5.3, 5.10
> **Prerequisites**: H6 (Chunks 01-05) complete
> **Detail plan**: `docs/phase-1/MODULE-03-visual-work-instructions.md`

---

## Scope

Create the backend foundation for Visual Work Instructions: 5 entity models,
DbSets + relationships, EF migration, the `IWorkInstructionService` interface
and `WorkInstructionService` implementation, and DI registration.

No UI pages in this chunk — those are in CHUNK-13.

---

## Files to Read First

| File | Why |
|------|-----|
| `docs/phase-1/MODULE-03-visual-work-instructions.md` | Full model specs, interface definition |
| `Data/TenantDbContext.cs` | Add DbSets + relationships |
| `Models/Enums/ManufacturingEnums.cs` | Add MediaType, FeedbackType, FeedbackStatus enums |
| `Models/Part.cs` | FK target for WorkInstruction.PartId |
| `Models/ProductionStage.cs` | FK target for WorkInstruction.ProductionStageId |
| `Services/IPartService.cs` | Pattern reference for interface style |
| `Program.cs` | DI registration location |

---

## Tasks

### 1. Add Enums
**File**: `Models/Enums/ManufacturingEnums.cs`

Add these enums (follow the existing file's style):
```csharp
public enum MediaType { Image, Video, PDF, Model3D }
public enum FeedbackType { Confusing, IncorrectInfo, SafetyConcern, Suggestion, Typo }
public enum FeedbackStatus { New, Acknowledged, Resolved, WontFix }
```

### 2. Create Models (5 new files)

**`Models/WorkInstruction.cs`**
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

**`Models/WorkInstructionStep.cs`**
```csharp
public class WorkInstructionStep
{
    public int Id { get; set; }
    public int WorkInstructionId { get; set; }
    public WorkInstruction WorkInstruction { get; set; } = null!;
    public int StepOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? WarningText { get; set; }
    public string? TipText { get; set; }
    public bool RequiresOperatorSignoff { get; set; } = false;
    public ICollection<WorkInstructionMedia> Media { get; set; } = new List<WorkInstructionMedia>();
    public ICollection<OperatorFeedback> Feedback { get; set; } = new List<OperatorFeedback>();
}
```

**`Models/WorkInstructionMedia.cs`**
```csharp
public class WorkInstructionMedia
{
    public int Id { get; set; }
    public int WorkInstructionStepId { get; set; }
    public WorkInstructionStep Step { get; set; } = null!;
    public MediaType MediaType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
```

**`Models/WorkInstructionRevision.cs`**
```csharp
public class WorkInstructionRevision
{
    public int Id { get; set; }
    public int WorkInstructionId { get; set; }
    public WorkInstruction WorkInstruction { get; set; } = null!;
    public int RevisionNumber { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string? ChangeNotes { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**`Models/OperatorFeedback.cs`**
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
```

### 3. Register DbSets + Relationships
**File**: `Data/TenantDbContext.cs`

Add DbSets:
```csharp
public DbSet<WorkInstruction> WorkInstructions { get; set; }
public DbSet<WorkInstructionStep> WorkInstructionSteps { get; set; }
public DbSet<WorkInstructionMedia> WorkInstructionMedia { get; set; }
public DbSet<WorkInstructionRevision> WorkInstructionRevisions { get; set; }
public DbSet<OperatorFeedback> OperatorFeedback { get; set; }
```

In `OnModelCreating`, add relationship configuration:
- WorkInstruction → Part (many-to-one, restrict delete)
- WorkInstruction → ProductionStage (many-to-one, restrict delete)
- WorkInstruction → Steps (one-to-many, cascade delete)
- WorkInstructionStep → Media (one-to-many, cascade delete)
- WorkInstructionStep → Feedback (one-to-many, cascade delete)
- WorkInstruction → Revisions (one-to-many, cascade delete)
- Unique index on (PartId, ProductionStageId) — one instruction per part+stage

### 4. Create IWorkInstructionService Interface
**New file**: `Services/IWorkInstructionService.cs`

See `docs/phase-1/MODULE-03-visual-work-instructions.md` Step 7 for the full
interface definition. Key methods:
- CRUD for instructions
- `GetByPartAndStageAsync(int partId, int stageId)` — used by shop floor
- Step CRUD + reorder
- Media upload/delete
- Feedback submit + review list
- Revision history

### 5. Create WorkInstructionService Implementation
**New file**: `Services/WorkInstructionService.cs`

Implementation notes:
- Inject `TenantDbContext` (follows service layer pattern)
- `UpdateAsync` auto-creates a `WorkInstructionRevision` (JSON snapshot of steps)
  before applying changes, increments `RevisionNumber`
- `UploadMediaAsync` saves to `wwwroot/uploads/instructions/{tenantCode}/` and
  returns relative URL. Validate file type (images: jpg/png/gif/webp, video:
  mp4/webm, PDF). Max 50MB.
- Steps always returned ordered by `StepOrder`
- Use `.Include(w => w.Steps).ThenInclude(s => s.Media)` for full loads

### 6. Register in DI
**File**: `Program.cs`
```csharp
builder.Services.AddScoped<IWorkInstructionService, WorkInstructionService>();
```

### 7. Create uploads directory
**Path**: `wwwroot/uploads/instructions/.gitkeep`

### 8. EF Migration
```bash
dotnet ef migrations add AddWorkInstructions --context TenantDbContext --output-dir Data/Migrations/Tenant
```

---

## Verification

1. Build passes — no compilation errors
2. Migration applies cleanly to a fresh tenant DB
3. Verify DbSets appear in TenantDbContext
4. Verify DI registration doesn't throw at startup

---

## Files Modified (fill in after completion)

- `Models/Enums/ManufacturingEnums.cs` — Added MediaType, FeedbackType, FeedbackStatus enums
- `Models/WorkInstruction.cs` — New: WorkInstruction entity with Part/Stage FKs, revision tracking
- `Models/WorkInstructionStep.cs` — New: ordered steps with body, warning, tip, signoff flag
- `Models/WorkInstructionMedia.cs` — New: media attachments per step (image/video/PDF)
- `Models/WorkInstructionRevision.cs` — New: JSON snapshot revisions for audit trail
- `Models/OperatorFeedback.cs` — New: operator step feedback with type and status
- `Data/TenantDbContext.cs` — Added 5 DbSets + relationship config (Restrict on Part/Stage FKs, Cascade on children, unique index on PartId+ProductionStageId)
- `Services/IWorkInstructionService.cs` — New: interface with CRUD, steps, media, feedback, revisions
- `Services/WorkInstructionService.cs` — New: full implementation with auto-revision snapshots, file upload with type validation, step reordering
- `Program.cs` — Added DI registration for IWorkInstructionService
- `wwwroot/uploads/instructions/.gitkeep` — New: uploads directory placeholder
- `Data/Migrations/Tenant/20260319014159_AddWorkInstructions.cs` — New: EF migration
