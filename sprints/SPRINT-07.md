# Sprint 7: Build Planning

> **Status**: NOT STARTED
> **Goal**: Plan SLS build plates with stacking, build file info, and schedule linking.
> **Depends on**: Sprint 2 (parts configured with stacking), Sprint 5 (scheduler)

---

## Tasks

```
[ ] 7.1  BuildPackage list — show all packages with status, machine, part count
[ ] 7.2  BuildPackage creation — select SLS machine, name the build
[ ] 7.3  Add parts to package — from WO lines, select part + qty + stacking level
[ ] 7.4  Stacking config per build — show available stack levels from Part config
[ ] 7.5  Build file info panel — display layer count, height, estimated time, powder usage
[ ] 7.6  Debug spoof form — manual entry for build file fields (hidden via SystemSetting)
[ ] 7.7  "Generate Spoof Data" button — auto-fills realistic build file values
[ ] 7.8  "Schedule Build" action — creates a Job linked to the BuildPackage
[ ] 7.9  Powder tracking — estimated vs actual usage
[ ] 7.10 BuildPackage status lifecycle — Draft → Ready → Scheduled → InProgress → Completed
[ ] 7.11 Verify: create build package → add 3 parts → generate spoof data → schedule → runs on Gantt
```

---

## Acceptance Criteria

- Build packages group parts onto build plates
- Parts show available stacking levels from Part config
- Build file info displays slicer data (spoof for now)
- "Schedule Build" creates a real Job on the scheduler
- Powder usage is tracked
- Status transitions are enforced

## Files to Touch

- `Components/Pages/Builds/Index.razor` — major rewrite
- `Services/BuildPlanningService.cs` — package CRUD + spoof data
- `Services/JobService.cs` — create job from build package
