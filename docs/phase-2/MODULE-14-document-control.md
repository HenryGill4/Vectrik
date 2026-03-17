# Module 14: Document Control

## Status: [ ] Not Started
## Category: QMS
## Phase: 2 — Operational Depth
## Priority: P2 - High

---

## Overview

Document Control manages all quality system documents — procedures, work
instructions, forms, specs, and certificates. Every document has a controlled
revision history, approval workflow, access restrictions, and read acknowledgment
tracking. This is a core QMS requirement for AS9100, ISO 9001, ISO 13485, and
NADCAP certifications.

**ProShop Improvements**: Streamlined check-in/check-out without cumbersome rename
workflows, inline document renaming, bulk operations, automatic watermarking,
electronic signature capture, distribution tracking, and template library.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `PartDrawing` file upload model (from M08) | ✅ M08 | `Models/PartDrawing.cs` |
| `WorkInstruction` models (from M03) | ✅ M03 | `Models/WorkInstruction.cs` |
| File upload infrastructure (`wwwroot/uploads/`) | ✅ Exists | `wwwroot/uploads/` |

**Gap**: No general-purpose `ControlledDocument` model, no approval workflow, no read acknowledgment, no electronic signature, no template library.

---

## What Needs to Be Built

### 1. Database Models (New)
- `ControlledDocument` — master document record with revision and approval state
- `DocumentRevision` — each file version as a distinct record
- `DocumentApproval` — approval workflow record (who approved + signature data)
- `DocumentReadRecord` — tracks who acknowledged a document
- `DocumentCategory` — organizational classification

### 2. Service Layer (New)
- `DocumentControlService` — full document lifecycle management

### 3. UI Components (New)
- **Document Library** — searchable, filterable document browser
- **Document Detail** — revision history, approvals, reader list
- **Document Editor** (metadata edit, file replace, new revision)
- **Approval Workflow** — review and sign-off interface
- **My Documents** — documents requiring my acknowledgment

---

## Implementation Steps

### Step 1 — Create DocumentCategory Model
**New File**: `Models/DocumentCategory.cs`
```csharp
public class DocumentCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // e.g., "Quality Procedures"
    public string? Code { get; set; }                        // e.g., "QP"
    public int? ParentCategoryId { get; set; }
    public DocumentCategory? ParentCategory { get; set; }
    public int RequiredReviewIntervalDays { get; set; } = 365;  // Annual review
}
```

### Step 2 — Create ControlledDocument Model
**New File**: `Models/ControlledDocument.cs`
```csharp
public class ControlledDocument
{
    public int Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;  // e.g., "QP-001"
    public string Title { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public DocumentCategory? Category { get; set; }
    public string CurrentRevision { get; set; } = "A";
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public string? Description { get; set; }
    public string? Scope { get; set; }
    public bool RequiresAcknowledgment { get; set; } = false;    // All affected users must read
    public string? AcknowledgmentRoles { get; set; }             // JSON list of roles required
    public string? RelatedStandard { get; set; }                 // "AS9100", "ISO9001", etc.
    public DateTime? NextReviewDate { get; set; }
    public string OwnerId { get; set; } = string.Empty;          // Document owner user
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DocumentRevision> Revisions { get; set; } = new List<DocumentRevision>();
}

public enum DocumentStatus { Draft, PendingApproval, Released, Obsolete }
```

### Step 3 — Create DocumentRevision Model
**New File**: `Models/DocumentRevision.cs`
```csharp
public class DocumentRevision
{
    public int Id { get; set; }
    public int ControlledDocumentId { get; set; }
    public ControlledDocument Document { get; set; } = null!;
    public string RevisionLabel { get; set; } = string.Empty;    // "A", "B", "1.0", "1.1"
    public string? ChangeDescription { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;         // pdf, docx, xlsx
    public long FileSizeBytes { get; set; }
    public bool IsCurrentRevision { get; set; } = false;
    public RevisionStatus Status { get; set; } = RevisionStatus.Draft;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAt { get; set; }
    public DateTime? ObsoletedAt { get; set; }
    public ICollection<DocumentApproval> Approvals { get; set; } = new List<DocumentApproval>();
    public ICollection<DocumentReadRecord> ReadRecords { get; set; } = new List<DocumentReadRecord>();
}

public enum RevisionStatus { Draft, InReview, Approved, Released, Obsolete }
```

### Step 4 — Create DocumentApproval Model
**New File**: `Models/DocumentApproval.cs`
```csharp
public class DocumentApproval
{
    public int Id { get; set; }
    public int DocumentRevisionId { get; set; }
    public DocumentRevision Revision { get; set; } = null!;
    public string ApproverUserId { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverRole { get; set; } = string.Empty;    // e.g., "Quality Manager"
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public string? Comments { get; set; }
    public string? SignatureData { get; set; }                   // Base64 canvas signature or typed name
    public DateTime? DecidedAt { get; set; }
    public int ApprovalOrder { get; set; }                       // Sequential or parallel approval
}

public enum ApprovalDecision { Pending, Approved, Rejected, Abstained }
```

### Step 5 — Create DocumentReadRecord Model
**New File**: `Models/DocumentReadRecord.cs`
```csharp
public class DocumentReadRecord
{
    public int Id { get; set; }
    public int DocumentRevisionId { get; set; }
    public DocumentRevision Revision { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }                       // Audit trail
}
```

### Step 6 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<DocumentCategory> DocumentCategories { get; set; }
public DbSet<ControlledDocument> ControlledDocuments { get; set; }
public DbSet<DocumentRevision> DocumentRevisions { get; set; }
public DbSet<DocumentApproval> DocumentApprovals { get; set; }
public DbSet<DocumentReadRecord> DocumentReadRecords { get; set; }
```

### Step 7 — Create DocumentControlService
**New File**: `Services/DocumentControlService.cs`
**New File**: `Services/IDocumentControlService.cs`

```csharp
public interface IDocumentControlService
{
    // Document management
    Task<List<ControlledDocument>> GetAllAsync(string tenantCode, DocumentStatus? status = null);
    Task<ControlledDocument?> GetByIdAsync(int id, string tenantCode);
    Task<ControlledDocument> CreateAsync(ControlledDocument document, string tenantCode);
    Task UpdateMetadataAsync(ControlledDocument document, string tenantCode);
    Task<string> GenerateDocumentNumberAsync(string categoryCode, string tenantCode);  // QP-001

    // Revisions
    Task<DocumentRevision> AddRevisionAsync(int documentId, IBrowserFile file,
                                             string revisionLabel, string changeDesc, string userId, string tenantCode);
    Task<DocumentRevision?> GetCurrentRevisionAsync(int documentId, string tenantCode);

    // Approval workflow
    Task SubmitForApprovalAsync(int revisionId, List<string> approverUserIds, string tenantCode);
    Task ApproveRevisionAsync(int revisionId, string approverId, string signatureData,
                               string? comments, string tenantCode);
    Task RejectRevisionAsync(int revisionId, string approverId, string reason, string tenantCode);
    Task ReleaseRevisionAsync(int revisionId, string tenantCode);  // After all approvals received

    // Acknowledgment
    Task AcknowledgeAsync(int revisionId, string userId, string tenantCode);
    Task<List<string>> GetPendingAcknowledgersAsync(int revisionId, string tenantCode);
    Task<List<ControlledDocument>> GetDocumentsRequiringAcknowledgmentAsync(string userId, string tenantCode);
}
```

**Release logic**: When `ReleaseRevisionAsync` is called:
1. Set revision status = Released, set `IsCurrentRevision = true`
2. Obsolete the previously current revision
3. Set document status = Released
4. If `RequiresAcknowledgment = true` → create notification for all affected roles

### Step 8 — Document Library Page
**New File**: `Components/Pages/Documents/Library.razor`
**Route**: `/documents`

UI requirements:
- Left panel: Category tree browser
- Main panel: document list filterable by category, status, search text
- Table: Doc#, Title, Category, Revision, Status badge, Owner, Last Modified
- Status badges: Draft (grey), In Review (blue), Released (green), Obsolete (orange)
- "New Document" button (Manager+ only)
- "My Documents to Acknowledge" alert banner when pending acknowledgments exist

### Step 9 — Document Detail Page
**New File**: `Components/Pages/Documents/Detail.razor`
**Route**: `/documents/{id:int}`

UI requirements with tabs:

**Current Revision Tab**:
- Embedded PDF viewer (use `<iframe>` for PDF URLs)
- Document metadata: number, title, category, owner, related standard
- **Acknowledge** button (shown when user hasn't acknowledged and doc requires it)
  - Shows modal with "I have read and understood this document" checkbox + signature pad

**Revisions Tab**:
- Timeline: all revisions with labels, dates, status, change description
- "Download" button per revision
- Current revision highlighted

**Approvals Tab**:
- Approval workflow status: who approved, when, with comments
- "Approve" / "Reject" buttons (shown to authorized approvers only)

**Readers Tab**:
- Who has acknowledged this revision (name, date)
- Who still needs to acknowledge (pending list)

### Step 10 — My Acknowledgments Page
**New File**: `Components/Pages/Documents/MyDocuments.razor`
**Route**: `/documents/my-acknowledgments`

UI requirements:
- List of documents requiring my acknowledgment
- Table: Doc#, Title, Revision, Released Date, Days Since Release
- "Read & Acknowledge" button per row
- Overdue acknowledgments highlighted (> 30 days since release)

### Step 11 — Signature Pad Component
**New File**: `Components/Shared/SignaturePad.razor`

Implementation:
- HTML5 Canvas element for drawing signature with mouse/touch
- "Clear" button to reset
- "Type Name Instead" toggle — renders typed full name as signature text
- Returns base64 PNG data string on submit
- Uses JavaScript: `wwwroot/js/signature-pad.js`

**signature-pad.js**: minimal canvas drawing implementation:
```javascript
window.signaturePad = {
    init: function(canvasId) { /* setup mouse/touch events */ },
    clear: function(canvasId) { /* clear canvas */ },
    getDataUrl: function(canvasId) { return canvas.toDataURL('image/png'); },
    isEmpty: function(canvasId) { /* check if any marks */ }
};
```

### Step 12 — EF Core Migration
```bash
dotnet ef migrations add AddDocumentControl --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Documents can be created with categories, numbers, and file uploads
- [ ] New revision added requires change description and file upload
- [ ] Approval workflow routes to specified approvers
- [ ] Approver can approve/reject with signature and comments
- [ ] Released documents show "Released" status badge
- [ ] Documents requiring acknowledgment show for assigned roles
- [ ] Acknowledgment records user + timestamp for audit
- [ ] Obsolete revisions are preserved but marked as obsolete
- [ ] Document library browsable by category with status filters
- [ ] PDF documents render in embedded viewer on detail page

---

## Dependencies

- **Module 03** (Visual Work Instructions) — Work instructions can be linked as controlled documents
- **Module 05** (Quality) — Inspection plans managed as controlled documents
- **Module 08** (Parts/PDM) — Part drawings optionally stored in document control
- **Module 17** (Compliance) — Documents linked to compliance requirements

---

## Future Enhancements (Post-MVP)

- Document templates library (pre-formatted Word/Excel templates for common forms)
- Automatic PDF watermarking with "CONTROLLED COPY" / "UNCONTROLLED IF PRINTED"
- QR code on printed documents linking back to the current controlled revision
- Bulk import of existing document files with metadata mapping
