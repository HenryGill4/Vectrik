# Module 08: Parts / Product Data Management (PDM)

## Status: [ ] Not Started
## Category: ERP
## Phase: 2 — Operational Depth
## Priority: P2 - High

---

## Overview

The Parts/PDM module is the single source of truth for all part data: drawings,
specifications, routings, revision history, material requirements, and 3D models.
Every other module references part data from here. ProShop's PDM is strong on
traceability but weak on CAD integration and file management UX.

**ProShop Improvements**: Automatic revision control with side-by-side comparison,
CAD thumbnail generation, direct SolidWorks/Fusion 360/Mastercam integration stubs,
smart search with part similarity matching, and streamlined file management (no
cumbersome checkout for renames).

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `Part` model (name, material, stacking, manufacturing approach) | ✅ Exists | `Models/Part.cs` |
| `PartStageRequirement` model (routing) | ✅ Exists | `Models/PartStageRequirement.cs` |
| `PartInstance` model (serial tracking) | ✅ Exists | `Models/PartInstance.cs` |
| `/admin/parts` CRUD page | ✅ Exists | `Components/Pages/Admin/Parts.razor` |
| `PartService` / `IPartService` | ✅ Exists | `Services/PartService.cs` |

**Gap**: No drawing file management, no revision control for part specs, no CAD file storage, no part number scheme, no customer-part linkage.

---

## What Needs to Be Built

### 1. Database Model Extensions
- Add `PartNumber`, `CustomerPartNumber`, `Revision`, `RevisionDate` to `Part`
- Add `PartDrawing` model — file attachments (PDFs, DXFs, step files, images)
- Add `PartRevisionHistory` — snapshot of part at each revision
- Add `PartNote` — engineering notes attached to a part

### 2. Service Layer (Enhance)
- Extend `PartService` with revision management, file handling, and similarity search
- `PartFileService` — file upload, retrieval, deletion

### 3. UI Components (New/Enhance)
- **Part List** — searchable with thumbnail preview and quick view
- **Part Detail** (`/parts/{id}`) — tabbed view with all part data
- **Routing Editor** — drag-to-reorder stage sequence
- **Drawing/File Manager** — upload, view, version drawings
- **Revision History** — timeline of part changes with diff view

---

## Implementation Steps

### Step 1 — Extend Part Model
**File**: `Models/Part.cs`
Add:
```csharp
public string PartNumber { get; set; } = string.Empty;           // Internal part number
public string? CustomerPartNumber { get; set; }                   // Customer's part number
public string? DrawingNumber { get; set; }
public string Revision { get; set; } = "A";                      // Current revision
public DateTime? RevisionDate { get; set; }
public string? Description { get; set; }
public string? CustomerName { get; set; }                        // Which customer owns this part
public decimal? EstimatedWeightKg { get; set; }
public string? RawMaterialSpec { get; set; }                     // e.g., "Ti-6Al-4V AMS 4928"
public decimal? RawMaterialWeightKg { get; set; }
public bool IsActive { get; set; } = true;
public bool IsCustomerProperty { get; set; } = false;            // Customer-supplied tooling/parts
public ICollection<PartDrawing> Drawings { get; set; } = new List<PartDrawing>();
public ICollection<PartRevisionHistory> RevisionHistory { get; set; } = new List<PartRevisionHistory>();
public ICollection<PartNote> Notes { get; set; } = new List<PartNote>();
```

### Step 2 — Create PartDrawing Model
**New File**: `Models/PartDrawing.cs`
```csharp
public class PartDrawing
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;        // relative path
    public DrawingFileType FileType { get; set; }              // PDF, DXF, STEP, IGES, Image
    public string? Description { get; set; }
    public string Revision { get; set; } = string.Empty;       // Drawing revision
    public bool IsPrimary { get; set; } = false;               // Primary drawing for quick view
    public bool IsControlled { get; set; } = true;             // Controlled document
    public long FileSizeBytes { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ObsoletedAt { get; set; }
}

public enum DrawingFileType { PDF, DXF, STEP, IGES, STL, Image, Mastercam, Other }
```

### Step 3 — Create PartRevisionHistory Model
**New File**: `Models/PartRevisionHistory.cs`
```csharp
public class PartRevisionHistory
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public string RevisionLabel { get; set; } = string.Empty;  // "A", "B", "C" or "1", "2"
    public string SnapshotJson { get; set; } = string.Empty;   // JSON of part + routing at that rev
    public string? ChangeDescription { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 4 — Create PartNote Model
**New File**: `Models/PartNote.cs`
```csharp
public class PartNote
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public NoteCategory Category { get; set; }    // Engineering, Manufacturing, Quality, General
    public string Body { get; set; } = string.Empty;
    public bool IsPinned { get; set; } = false;   // Pinned notes show at top
    public string AuthorUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum NoteCategory { Engineering, Manufacturing, Quality, Safety, General }
```

### Step 5 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<PartDrawing> PartDrawings { get; set; }
public DbSet<PartRevisionHistory> PartRevisionHistory { get; set; }
public DbSet<PartNote> PartNotes { get; set; }
```

### Step 6 — Extend PartService
**File**: `Services/PartService.cs`

Add methods:
```csharp
Task<List<Part>> SearchAsync(string query, string tenantCode);     // Part#, name, customer#
Task<Part?> GetByPartNumberAsync(string partNumber, string tenantCode);
Task SaveRevisionSnapshotAsync(int partId, string changeDesc, string userId, string tenantCode);
Task<List<PartRevisionHistory>> GetRevisionHistoryAsync(int partId, string tenantCode);

// File management
Task<PartDrawing> UploadDrawingAsync(int partId, IBrowserFile file, string revision,
                                     bool isPrimary, string userId, string tenantCode);
Task DeleteDrawingAsync(int drawingId, string tenantCode);
Task<List<PartDrawing>> GetDrawingsAsync(int partId, string tenantCode);

// Notes
Task<PartNote> AddNoteAsync(PartNote note, string tenantCode);
Task DeleteNoteAsync(int noteId, string tenantCode);
```

**Revision snapshot logic**: Before any `UpdateAsync`, serialize the current part + routing to JSON and save as `PartRevisionHistory` if the revision label has changed.

### Step 7 — Parts List Page
**New File**: `Components/Pages/Parts/Index.razor`
**Route**: `/parts`

UI requirements:
- Search bar (searches part number, name, customer part number, description)
- Table columns: Part#, Customer Part#, Name, Customer, Revision, Material, Status
- "New Part" button
- Filter: Active / Inactive / All
- Click row → `/parts/{id}`

> Note: The existing `/admin/parts` page is for admin-only CRUD. This new `/parts` page is the read+operational view for all authorized users.

### Step 8 — Part Detail Page
**New File**: `Components/Pages/Parts/Detail.razor`
**Route**: `/parts/{id:int}`

UI requirements with tabs:

**Overview Tab**:
- Part number, revision, description, customer, weight, raw material spec
- Primary drawing thumbnail (PDF embed or image)
- "Edit" button (Admin/Manager only)

**Routing Tab**:
- Ordered list of production stages with estimated hours per stage
- Drag-to-reorder (Admin only)
- Total estimated hours displayed

**Drawings Tab**:
- File list: filename, type badge, revision, uploaded date, size
- Upload button (drag-drop area)
- View/download button per file
- "Set as Primary" toggle
- Delete with confirmation

**History Tab**:
- Timeline of revisions: revision label, date, changed by, change description
- "View at this revision" link (shows snapshot JSON in readable format)

**Notes Tab**:
- Pinned notes at top (highlighted)
- All notes with category badge, author, timestamp
- "Add Note" button with category selector

**Production History Tab**:
- List of past jobs that made this part: Job#, WO#, Date, Qty, Outcome
- Links to job details for traceability

### Step 9 — Routing Editor Enhancement
**File**: `Components/Pages/Admin/Parts.razor` (existing admin form)

Enhance the Stages tab within the part edit form:
- Current: simple list of stage checkboxes
- Upgrade to: drag-to-reorder list with estimated hours input per stage
- Each stage row: [drag handle] [Stage name] [Est. hours input] [Setup hours] [Remove button]
- "Add Stage" dropdown to append more stages

Implement drag-and-drop using JavaScript interop:
- Add `dragSort.js` to `wwwroot/js/` with sortable list functionality
- Call from Blazor via `IJSRuntime.InvokeAsync<int[]>("dragSort.getOrder", elementId)`

### Step 10 — EF Core Migration
```bash
dotnet ef migrations add AddPartsPdm --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Part has a part number, customer part number, revision, and revision date
- [ ] Editing a part with a new revision label creates a `PartRevisionHistory` snapshot
- [ ] Drawings can be uploaded (PDF, DXF, STEP, images)
- [ ] Primary drawing is displayed as preview on part detail
- [ ] Routing stages can be reordered via drag-and-drop
- [ ] Part notes can be added with categories; pinned notes show at top
- [ ] Parts list is searchable by part number, name, and customer part number
- [ ] Production history tab shows all past jobs for the part

---

## Dependencies

- **Module 01** (Quoting) — Part selection in quote lines uses part number + routing
- **Module 02** (Work Orders) — Job routing spawned from part routing
- **Module 05** (Quality) — Inspection plans linked to part
- **Module 14** (Document Control) — Controlled drawings managed here

---

## Future Enhancements (Post-MVP)

- Mastercam `.mcam` file viewer thumbnail generation
- SolidWorks/Fusion 360 file metadata extraction
- Part similarity search (find parts with same material + weight class)
- STEP file 3D viewer in browser (Three.js)
- Automatic PDF thumbnail generation on upload
