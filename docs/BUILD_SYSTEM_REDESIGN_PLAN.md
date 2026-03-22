# Build System Redesign Plan

> **Status:** Ready for Implementation  
> **Branch:** claude/fix-scheduling-routing-JHUOI  
> **Prerequisite:** All MANUFACTURING_PROCESS_REDESIGN_PLAN.md Phases A-F COMPLETE ✅  

---

## Problem Statement

The current build system conflates the **build file definition** (what gets loaded onto the SLS printer) with the **build run** (a scheduled print job). This creates unnecessary complexity:

1. **BuildPackage** acts as both a build definition AND a run instance (Draft→Sliced→Ready→Scheduled→Printing→PostPrint→Completed)
2. **BuildTemplate** exists as a "reusable config" but is disconnected from the actual slicer output
3. **BuildFileInfo** (slicer metadata) lives on BuildPackage instead of the template
4. The Draft→Sliced→Ready pipeline is redundant — operators import a finished build file, not draft one
5. No proper version control on build files
6. Scheduling doesn't optimize for auto-changeover or operator shift alignment

## User Requirements (from Q&A)

1. **Builds = actual slicer output files** loaded onto the printer; operators are prompted to run them when scheduled
2. **Mixed-part builds** needed for multi-WO/multi-part demand fulfillment
3. **Streamline models** — eliminate redundancy between Template and Package
4. **Auto-changeover is critical** — if two builds finish and nobody removes plates, the machine goes down. Avoid at all costs.
5. **Weekend coverage** — schedule builds to keep machines running over weekends; align end times with operator shift starts
6. **Version control** on build files; "Slice" status is redundant (the whole build IS the slice info)
7. **Suggestion system** — mix of smart suggestions and manual build file selection to meet demand

---

## Design: Terminology & Model Mapping

| Old Concept | New Concept | Description |
|---|---|---|
| BuildTemplate | **Build File** (stays `BuildTemplate` in code) | A reusable, versioned slicer output file that can be loaded onto an SLS printer. Contains parts, duration, layer count, parameters. |
| BuildPackage | **Build Run** (stays `BuildPackage` in code) | A scheduled instance of a Build File on a specific machine at a specific time. Simplified status: Ready → Scheduled → Printing → PostPrint → Completed. |
| BuildFileInfo | Merged into **Build File** (moved to `BuildTemplate`) | Slicer metadata (layer count, build height, powder estimate, file name) belongs on the template. |
| BuildPackageRevision | Stays on Build File | Version history tracks changes to the build file definition. Renamed `BuildTemplateRevision` in code. |

### Key Design Decisions

1. **BuildTemplate IS the build file.** It has all slicer metadata, version control, and part definitions. Status flow: Draft → Certified (ready to use). Archived when superseded.
2. **BuildPackage IS a build run.** It references a BuildTemplate (FK) and a Machine. No more Draft/Sliced/Ready on the run — that's the template's lifecycle. Run status: Ready → Scheduled → Printing → PostPrint → Completed → Cancelled.
3. **BuildFileInfo fields move to BuildTemplate.** `FileName`, `LayerCount`, `BuildHeightMm`, `EstimatedPrintTimeHours`, `EstimatedPowderKg`, `PartPositionsJson`, `SlicerSoftware`, `SlicerVersion`.
4. **Auto-changeover scheduling** — scheduling logic actively avoids gaps that would leave plates unattended. Prefer back-to-back builds with changeover timing that aligns with shift starts.
5. **Version control** — `BuildTemplateRevision` tracks snapshots each time a certified template is modified and recertified.

---

## Phase 1: Model Changes

### 1A. Merge BuildFileInfo fields into BuildTemplate

Add to `BuildTemplate`:
```
FileName              string?   (the .sli/.cls file name)
LayerCount            int?
BuildHeightMm         double?
EstimatedPowderKg     double?
PartPositionsJson     string?   (JSON: part positions on plate)
SlicerSoftware        string?
SlicerVersion         string?
```

The existing `EstimatedDurationHours` field already covers `EstimatedPrintTimeHours`.

### 1B. Add BuildTemplateId FK to BuildPackage

```
BuildTemplateId       int?      FK → BuildTemplate
```

This links every run to its source build file. `SourceBuildPackageId` (copy tracking) is retained for the "re-run" use case but `BuildTemplateId` is the canonical source.

### 1C. Simplify BuildPackageStatus enum

**Current:** Draft, Sliced, Ready, Scheduled, Printing, PostPrint, Completed, Cancelled  
**New:** Ready, Scheduled, Printing, PostPrint, Completed, Cancelled

Remove `Draft` and `Sliced` — those are template-level concerns. A BuildPackage (run) is created in `Ready` state from a certified template.

> **Migration safety:** Existing Draft/Sliced packages will be migrated to Ready status via SQL in the migration.

### 1D. Add BuildTemplateRevision model

Rename/replace `BuildPackageRevision`:
```csharp
public class BuildTemplateRevision
{
    public int Id { get; set; }
    public int BuildTemplateId { get; set; }
    public int RevisionNumber { get; set; }
    public string ChangedBy { get; set; }
    public DateTime ChangedDate { get; set; }
    public string? ChangeNotes { get; set; }
    public string? PartsSnapshotJson { get; set; }
    public string? ParametersSnapshotJson { get; set; }
    public string? SlicerMetadataSnapshotJson { get; set; }
}
```

### 1E. EF Migration

Single migration for all model changes. Data migration:
- Existing BuildFileInfo records → copy fields to their BuildTemplate (via BuildPackage.SourceBuildPackageId chain)
- Existing Draft/Sliced BuildPackages → set status to Ready
- Existing BuildPackageRevisions → migrate to BuildTemplateRevisions where possible

---

## Phase 2: Service Layer Updates

### 2A. BuildTemplateService (Build File Library)

Enhanced to be the primary build management service:
- **Create/Edit:** Set parts, slicer metadata, parameters, duration
- **Certify:** Validates all required fields (parts, duration, slicer info) then marks Certified
- **Version tracking:** On recertification, auto-creates a BuildTemplateRevision snapshot
- **InstantiateAsync:** Creates a BuildPackage (run) in Ready status from a certified template

### 2B. BuildPlanningService (Build Run Lifecycle)

Simplified — no longer handles build file definition:
- Remove CreatePackageAsync Draft flow (replaced by template instantiation)
- Keep: CreateScheduledCopyAsync (re-run a build), UpdatePackageAsync (status transitions only)
- Keep: CreateBuildStageExecutionsAsync, CreatePartStageExecutionsAsync (these create the manufacturing jobs)
- Remove: SaveBuildFileInfoAsync, GenerateSpoofBuildFileAsync (slicer data lives on template now)

### 2C. BuildSchedulingService (Smart Scheduling)

Enhanced scheduling with auto-changeover optimization:
- **FindEarliestBuildSlotAsync:** Already shift-aware and collision-detecting. Enhance with:
  - Prefer slots where build end time + changeover aligns with next operator shift start
  - Prefer back-to-back builds to maximize machine utilization
  - Weekend scheduling: fill SLS machines over weekends when operators won't be present (machine prints autonomously)
- **Auto-changeover awareness:** 
  - When scheduling, check if the machine has `AutoChangeoverEnabled` and `BuildPlateCapacity > 1`
  - If capacity allows, schedule the next build to start exactly at the previous build's end + changeover time
  - Alert if a build would end during a gap where no operators are on shift (risk of machine going down)
- **ScheduleBuildAsync:** Takes a BuildTemplateId + MachineId, instantiates the run, schedules it

### 2D. BuildSuggestionService (Demand Matching)

Already well-designed. Minor updates:
- `GenerateTemplateSuggestions` — works with certified templates (unchanged)
- `GenerateMixedBuildSuggestions` — matches partial-plate parts that could share a build (unchanged)
- Suggestions now directly reference templates instead of creating Draft packages

---

## Phase 3: UI Changes

### 3A. Build File Library (new page or enhanced `/builds`)

Two-section layout:
1. **Build File Library** — grid/cards of certified build files with:
   - Name, parts list, material, duration, stack level, use count
   - Actions: View/Edit, Certify, Create Run, Archive
   - Filter by: material, part, status, certified/needs-recert
2. **Active Runs** — current pipeline of scheduled/printing/post-print runs
   - Status badges, machine assignment, Gantt integration
   - Actions: Schedule, Start Print, Post-Print, Release Plate

### 3B. BuildsView.razor (Scheduler Tab)

Simplified:
- Remove Draft/Sliced sections (those are in the Build File Library)
- Focus on: Ready to Schedule → Scheduled → Printing → Post-Print
- "New Build Run" button → shows certified template picker → creates run
- Keep: machine Gantt timeline, changeover warnings, per-machine pipelines

### 3C. Template Management UI

Build File detail page:
- Parts on plate (add/remove/edit quantities)
- Slicer metadata (file name, layers, height, powder estimate)
- Build parameters (laser power, scan speed, layer thickness — JSON editor or structured form)
- Version history with diff view
- Certification status and history
- "Create Run" action → picks machine, creates BuildPackage

---

## Phase 4: Seed Data

### 4A. SeedBuildTemplatesAsync

Add seed build templates for each seeded part (if additive approach):
- Single-stack, double-stack, triple-stack variants
- Certified status with realistic slicer metadata
- Linked to correct material

### 4B. SeedBuildPackagesAsync (Demo Runs)

Optional: Create a few demo build runs in various states for the demo database.

---

## Phase 5: Scheduling Enhancements (Auto-Changeover Optimization)

### 5A. Changeover-Aware Slot Finding

When `FindEarliestBuildSlotAsync` looks for a slot:
1. Check machine's `BuildPlateCapacity` and `AutoChangeoverEnabled`
2. If auto-changeover is enabled with capacity 2:
   - Prefer scheduling where build B starts exactly when build A ends + changeover time
   - The machine autonomously switches plates — no operator intervention needed
3. If NOT auto-changeover:
   - Schedule builds so end times align with operator shift starts
   - Avoid builds ending during nights/weekends when no operators are available

### 5B. Weekend Coverage

- SLS machines print autonomously — schedule builds to cover full weekends
- Calculate: "If build starts Friday at X, it ends Saturday at Y; auto-changeover starts build 2 which ends Sunday at Z; operators arrive Monday at 6am and can unload"
- Flag if a build would end and require manual intervention during an unstaffed period

### 5C. Changeover Risk Warnings

ScheduleDiagnostics enhanced:
- "Changeover Warning: Build ends at 3:00 AM (no operators on shift until 6:00 AM)"
- "Capacity Risk: Both plate slots will be full at 2:00 PM Saturday — next operator availability is Monday 6:00 AM"
- "Suggestion: Delay build start by 2 hours to align changeover with Monday morning shift"

---

## Implementation Order

| Step | Phase | Description | Risk |
|------|-------|-------------|------|
| 1 | 1A | Add slicer fields to BuildTemplate model | Low |
| 2 | 1B | Add BuildTemplateId FK to BuildPackage | Low |
| 3 | 1C | Simplify BuildPackageStatus enum | Medium (references throughout) |
| 4 | 1D | Create BuildTemplateRevision model | Low |
| 5 | 1E | EF Migration (all model changes) | Medium |
| 6 | 2A | Update BuildTemplateService | Low |
| 7 | 2B | Simplify BuildPlanningService | Medium (900+ lines) |
| 8 | 2C | Enhance BuildSchedulingService | Medium |
| 9 | 2D | Update BuildSuggestionService | Low |
| 10 | 3A-C | UI updates | Medium |
| 11 | 4A-B | Seed data | Low |
| 12 | 5A-C | Scheduling enhancements | High (core algorithm) |

**Total estimated changes:** ~15-20 files, ~500-800 lines modified

---

## Backward Compatibility

- Existing `BuildPackageRevision` table kept but deprecated (new data goes to `BuildTemplateRevision`)
- Existing `BuildFileInfo` table kept but deprecated (new data lives on `BuildTemplate`)
- Migration updates existing records to new structure
- `SourceBuildPackageId` on BuildPackage retained for copy/re-run tracking
- No FK removal in this phase — only additions

---

## Test Strategy

- Update existing BuildPlanningService tests for simplified status flow
- Add BuildTemplateService tests for slicer metadata and version control
- Add scheduling tests for auto-changeover optimization
- Verify all 308+ existing tests continue to pass after each phase
