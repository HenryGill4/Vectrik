> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# CHUNK-14: AS9102 FAIR Forms

> **Size**: M (Medium) — 3 new model files + 1 service extension + 1 template + 1 UI edit
> **ROADMAP tasks**: Stage 6F (6F.1 through 6F.9)
> **Prerequisites**: H6 (Chunks 01-05) complete, specifically CHUNK-01 (feature flags)
> **Detail plan**: ROADMAP.md Stage 6F section + AS9102 standard reference

---

## Scope

Implement AS9102 First Article Inspection Report (FAIR) support: three form models
matching the AS9102 standard (Form 1: Part Number Accountability, Form 2: Product
Accountability, Form 3: Characteristic Accountability), extend `IQualityService`
with FAIR generation methods, auto-populate from part routing + material data,
add a document template for FAIR PDF, and wire a "Generate FAIR" button on the
part detail page.

---

## Files to Read First

| File | Why |
|------|-----|
| `Models/Part.cs` | Part routing data used to populate FAIR |
| `Models/PartStageRequirement.cs` | Routing steps for Form 1 |
| `Models/Material.cs` | Material data for Form 2 |
| `Services/IQualityService.cs` | Extend with FAIR methods |
| `Services/QualityService.cs` | Add FAIR implementation |
| `Data/TenantDbContext.cs` | Add DbSets |
| `Components/Pages/Parts/Detail.razor` | Add "Generate FAIR" button |
| `Services/ITenantFeatureService.cs` | Gate behind feature flag |
| `Services/IDocumentTemplateService.cs` | Template pattern (if CHUNK-04 done) |

---

## Background: AS9102 Standard

AS9102 defines three forms for First Article Inspection:

- **Form 1 — Part Number Accountability**: Lists all part numbers, sub-assemblies,
  and their drawing references. Maps the part structure.
- **Form 2 — Product Accountability**: Lists materials, special processes, and
  functional testing. Proves the right materials and processes were used.
- **Form 3 — Characteristic Accountability**: Lists every dimension/characteristic
  from the drawing with nominal, tolerance, actual measurement, and pass/fail.
  This is the core measurement form.

---

## Tasks

### 1. Create FairForm1 Model (Part Number Accountability)
**New file**: `Models/FairForm1.cs`

```csharp
public class FairForm1
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;

    // Header fields
    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string? DrawingNumber { get; set; }
    public string? DrawingRevision { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string? CageCode { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string? FairNumber { get; set; }  // auto-generated

    // Detail lines — JSON array of sub-parts/assemblies
    // Each: { PartNumber, PartName, DrawingNumber, Quantity, FairRequired (bool) }
    public string DetailLinesJson { get; set; } = "[]";

    // Approval
    public string? PreparedBy { get; set; }
    public DateTime? PreparedDate { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? CustomerApprovedBy { get; set; }
    public DateTime? CustomerApprovedDate { get; set; }

    public FairStatus Status { get; set; } = FairStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### 2. Create FairForm2 Model (Product Accountability)
**New file**: `Models/FairForm2.cs`

```csharp
public class FairForm2
{
    public int Id { get; set; }
    public int FairForm1Id { get; set; }
    public FairForm1 Form1 { get; set; } = null!;

    // Detail lines — JSON array of material/process entries
    // Each: { DetailNumber, MaterialOrProcessName, Specification,
    //         Code (M=Material, S=Special Process, T=Test, O=Other),
    //         CertificateNumber, ReportNumber, Conforming (bool) }
    public string DetailLinesJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### 3. Create FairForm3 Model (Characteristic Accountability)
**New file**: `Models/FairForm3.cs`

```csharp
public class FairForm3
{
    public int Id { get; set; }
    public int FairForm1Id { get; set; }
    public FairForm1 Form1 { get; set; } = null!;

    // Detail lines — JSON array of characteristic measurements
    // Each: { CharacteristicNumber, ReferenceLocation (drawing zone/balloon),
    //         CharacteristicDesignator, Requirement (nominal + tolerance),
    //         ActualResults, Conforming (bool), InspectorInitials }
    public string DetailLinesJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### 4. Add FairStatus Enum
**File**: `Models/Enums/ManufacturingEnums.cs`

```csharp
public enum FairStatus { Draft, InReview, Approved, Rejected }
```

### 5. Register DbSets + Relationships
**File**: `Data/TenantDbContext.cs`

Add DbSets:
```csharp
public DbSet<FairForm1> FairForm1s { get; set; }
public DbSet<FairForm2> FairForm2s { get; set; }
public DbSet<FairForm3> FairForm3s { get; set; }
```

Relationships:
- FairForm1 → Part (many-to-one, restrict delete)
- FairForm2 → FairForm1 (one-to-one, cascade delete)
- FairForm3 → FairForm1 (one-to-one, cascade delete)

### 6. EF Migration
```bash
dotnet ef migrations add AddFairForms --context TenantDbContext --output-dir Data/Migrations/Tenant
```

### 7. Extend IQualityService with FAIR Methods
**File**: `Services/IQualityService.cs`

Add:
```csharp
// FAIR
Task<FairForm1?> GetFairFormAsync(int partId);
Task<FairForm1> GenerateFairAsync(int partId, string preparedBy);
Task UpdateFairAsync(FairForm1 form1);
Task<List<FairForm1>> GetAllFairFormsAsync();
```

### 8. Implement FAIR in QualityService
**File**: `Services/QualityService.cs`

`GenerateFairAsync` implementation:
1. Load Part with routing (`PartStageRequirements`) and material (`MaterialEntity`)
2. Auto-populate Form 1 header: PartNumber, PartName from Part; DrawingNumber
   from Part revision; Organization from tenant branding settings
3. Auto-populate Form 1 detail lines from BOM (if `PartBomItems` exist)
4. Auto-populate Form 2 detail lines:
   - Material entries from `Part.MaterialEntity` (Code = "M")
   - Special process entries from routing stages (heat treat, surface finishing)
     with Code = "S"
5. Create empty Form 3 with characteristic lines (to be filled by inspector)
   — or if SPC characteristics exist for this part, pre-populate from those
6. Set Status = Draft
7. Return the generated form for review/editing

### 9. Add "Generate FAIR" Button to Part Detail
**File**: `Components/Pages/Parts/Detail.razor`

- Add a "Quality" or "FAIR" tab (or add to existing Quality section)
- "Generate FAIR" button → calls `GenerateFairAsync`
- Display existing FAIR if one exists for this part (show status, dates)
- "View/Edit FAIR" link → opens a FAIR editor modal or inline form
- Gate behind `ITenantFeatureService.IsEnabled("quality.require_fair")` or
  `ITenantFeatureService.IsEnabled("fair")` — show nothing if feature disabled

### 10. FAIR Document Template (if CHUNK-04 is done)
If `IDocumentTemplateService` exists (from CHUNK-04), create a default FAIR PDF
template that renders Forms 1-3 in the standard AS9102 layout. If CHUNK-04 is
not yet done, add a TODO comment and skip this step.

---

## Verification

1. Build passes
2. Migration applies cleanly
3. "Generate FAIR" on a part with routing and material pre-populates Forms 1-2
4. Form 3 is created (empty or with SPC characteristics if available)
5. FAIR form data can be saved and retrieved
6. FAIR button is hidden when `quality.require_fair` feature is disabled
7. FAIR status can be updated (Draft → InReview → Approved)

---

## Files Modified (fill in after completion)

_To be filled in by the executing agent._
