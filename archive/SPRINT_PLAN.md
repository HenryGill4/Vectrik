> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# OpCentrix — Full MES Build Sprint Plan

> **DEPRECATED**: This plan has been superseded by **`ROADMAP.md`** (project root).
> Retained for historical reference only. Do not use for planning.

> **Created**: 2026-03-13
> **Status**: SUPERSEDED — See `ROADMAP.md`
> **Starting Point**: Phases A-C complete (models, services, placeholder pages)

---

## Sprint Order

| Sprint | Focus | Status | File |
|--------|-------|--------|------|
| 1 | Login Flow + Seeding + Nav | COMPLETE | `sprints/SPRINT-01.md` |
| 2 | Admin Pages (Full CRUD) | COMPLETE | `sprints/SPRINT-02.md` |
| 3 | Work Orders + Quotes (Full Lifecycle) | NOT STARTED | `sprints/SPRINT-03.md` |
| 4 | Shop Floor (Operator Workflow) | NOT STARTED | `sprints/SPRINT-04.md` |
| 5 | Scheduler (Gantt + Job Management) | NOT STARTED | `sprints/SPRINT-05.md` |
| 6 | Part Tracker (Visibility) | NOT STARTED | `sprints/SPRINT-06.md` |
| 7 | Build Planning | NOT STARTED | `sprints/SPRINT-07.md` |
| 8 | Maintenance System | NOT STARTED | `sprints/SPRINT-08.md` |
| 9 | Analytics + Dashboard | NOT STARTED | `sprints/SPRINT-09.md` |
| 10 | Machine Integration + SignalR | NOT STARTED | `sprints/SPRINT-10.md` |
| 11 | PWA + Mobile Polish | NOT STARTED | `sprints/SPRINT-11.md` |

---

## Why This Order

1. **Sprint 1** — Can't do anything without login working end-to-end
2. **Sprint 2** — Admins configure parts, stages, machines, users before production starts
3. **Sprint 3** — Work orders are the entry point for all production work
4. **Sprint 4** — Shop floor is the core MES loop (operators process parts through stages)
5. **Sprint 5** — Scheduler plans production (depends on parts + WOs existing)
6. **Sprint 6** — Part tracker gives visibility into what Sprint 4 produces
7. **Sprint 7** — Build planning is SLS-specific scheduling layer
8. **Sprint 8** — Maintenance runs parallel to production
9. **Sprint 9** — Analytics summarizes everything from Sprints 3-8
10. **Sprint 10** — Machine integration is enhancement (mock data works without it)
11. **Sprint 11** — Mobile polish is final layer

## Resume Instructions

1. Read this file to find current sprint
2. Open `sprints/SPRINT-XX.md` for the active sprint
3. Find the first unchecked `[ ]` task — that's where to resume
4. After completing a task, mark it `[x]`
5. When all tasks in a sprint are done, update status here to `COMPLETE`
