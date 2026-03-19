# CHUNK-10: Build Plate UI + SLS Printing Enhancement

> **Size**: L (Large) — ~8-12 file edits
> **ROADMAP tasks**: BP.17, BP.18, BP.19, BP.20, BP.21, BP.22, BP.23, BP.24, BP.25
> **Prerequisites**: CHUNK-09 complete

---

## Scope

Update the Builds UI to show revision history and the SLS Printing shop floor
partial to display build-level context (all parts, WO references, progress).
Add the Part Separation confirmation UI after EDM completion.

---

## Files to Read First

| File | Why |
|------|-----|
| `Components/Pages/Builds/Index.razor` | Build management page |
| `Components/Pages/ShopFloor/Partials/SLSPrinting.razor` | SLS stage partial |
| `Services/IBuildPlanningService.cs` | Updated service from CHUNK-09 |
| `Models/BuildPackageRevision.cs` | Revision model from CHUNK-08 |
| `Components/Pages/ShopFloor/Index.razor` | Shop floor for stage completion flow |
| `Components/Pages/ShopFloor/Partials/GenericStage.razor` | Understand partial pattern |

---

## Tasks

### 1. Add revision history to Builds page (BP.17)
**File**: `Components/Pages/Builds/Index.razor`
- Add an expandable "Revision History" section per build package
- Show: revision number, date, who changed, notes
- Load via `BuildPlanningService.GetRevisionsAsync(buildPackageId)` (add this
  method if it doesn't exist)

### 2. Update SLSPrinting.razor for build-level context (BP.18, BP.19, BP.20, BP.21)
**File**: `Components/Pages/ShopFloor/Partials/SLSPrinting.razor`
- Check if the current StageExecution has a BuildPackageId
- If yes (build-level execution):
  - Load the BuildPackage with all its parts
  - Show a "Build Contents" table: Part#, Part Name, Qty, WO#, Customer
  - Show build-level info from BuildFileInfo: machine, material, est. print time,
    layer count, build height, powder estimate
  - Add build progress section (layer count vs total, % complete input)
- If no (single-part mode): show existing single-part view (backward compatible)

### 3. Build-level stage advancement (BP.22)
**File**: `Components/Pages/ShopFloor/Index.razor` (or stage completion logic)
- When completing an SLS printing stage that has a BuildPackageId:
  - Advance the build to the next build-level stage (Depowdering)
  - Do NOT advance individual parts yet
  - Show a toast: "Build advanced to Depowdering"

### 4. Part Separation UI after EDM (BP.23, BP.24, BP.25)
Create a new component or modal for part separation:
**New File**: `Components/Shared/PartSeparationDialog.razor`

Triggered when the Wire EDM build-level stage completes:
- Show list of all parts from the build package
- Each part has:
  - Checkbox: "Separated OK" (default checked)
  - Checkbox: "Damaged/Scrap" → if checked, show reason input
  - Serial number input (auto-generated or manual)
- "Confirm Separation" button:
  - For OK parts: create PartInstance records, trigger `CreatePartStageExecutionsAsync`
  - For damaged parts: create NCR, mark as failed
- Link PartInstance records back to BuildPackageId

---

## Verification

1. Build passes
2. Open a build package → see revision history timeline
3. Start an SLS print stage from shop floor → SLSPrinting.razor shows all build
   parts with WO references and build-level info
4. Complete SLS stage → build advances to Depowdering (not individual parts)
5. Complete Depowdering → build advances to EDM
6. Complete EDM → Part Separation dialog appears
7. Confirm separation → individual part jobs created for downstream stages
8. Mark a part as damaged → NCR created automatically

---

## Files Modified (fill in after completion)

- `Services/IBuildPlanningService.cs` — Added `GetRevisionsAsync` method
- `Services/BuildPlanningService.cs` — Implemented `GetRevisionsAsync`; added WO line include to `GetPackageByIdAsync`
- `Components/Pages/Builds/Index.razor` — Added expandable revision history section per package with toggle and lazy-load
- `Components/Pages/ShopFloor/Partials/SLSPrinting.razor` — Full rewrite: build-level context (build contents table, slice file data, progress bar) when BuildPackageId present; backward-compatible single-part mode
- `Components/Shared/PartSeparationDialog.razor` — New component: part separation confirmation after EDM with OK/damaged tracking, serial numbers, damage reason
- `Models/PartSeparationResult.cs` — New model classes: PartSeparationResult, SeparatedPart, DamagedPart
- `Components/Pages/ShopFloor/Index.razor` — Injected build planning/serial/quality/numbering services; wired PartSeparationDialog on Wire EDM completion; auto-creates PartInstances for OK parts and NCRs for damaged parts; build-level stage toast messages
