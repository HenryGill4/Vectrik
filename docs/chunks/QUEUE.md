# Work Queue — Execution Order

> **For AI agents**: Find the first unchecked `[ ]` chunk — that's your assignment.
> After completing a chunk, mark it `[x]` and note the date.

---

## Phase 1A — H6: Cross-Cutting Wiring

| # | Chunk | Size | Status | Prereqs |
|---|-------|------|--------|---------|
| 01 | [Feature Flag Gating](CHUNK-01-feature-flags.md) | M | [x] | H1-H5 |
| 02 | [Custom Fields Integration](CHUNK-02-custom-fields.md) | M | [x] | H1-H5 |
| 03 | [Number Sequences](CHUNK-03-number-sequences.md) | S | [x] | H1-H5 |
| 04 | [Document Templates](CHUNK-04-document-templates.md) | M | [x] | H1-H5 |
| 05 | [Workflow Engine Wiring](CHUNK-05-workflow-engine.md) | L | [x] | H1-H5 |

## Phase 1D — Part System & Build Plate

| # | Chunk | Size | Status | Prereqs |
|---|-------|------|--------|---------|
| 06 | [Part Edit Redesign (BOM + Routing in-memory)](CHUNK-06-part-edit-redesign.md) | M | [x] | — |
| 07 | [PricingEngine Verification + Cleanup](CHUNK-07-pricing-cleanup.md) | S | [x] | 06 |
| 08 | [Build Plate Models + Migration](CHUNK-08-build-plate-models.md) | M | [x] | 07 |
| 09 | [Build Plate Execution Engine](CHUNK-09-build-plate-execution.md) | L | [x] | 08 |
| 10 | [Build Plate UI + SLS Printing](CHUNK-10-build-plate-ui.md) | L | [x] | 09 |
| 11 | [Build Plate Scheduling + Cost](CHUNK-11-build-plate-scheduling.md) | M | [x] | 10 |

## Phase 1B — Visual Work Instructions

| # | Chunk | Size | Status | Prereqs |
|---|-------|------|--------|---------|
| 12 | [Work Instructions Models + Service](CHUNK-12-work-instructions-backend.md) | M | [x] | H6 |
| 13 | [Work Instructions UI](CHUNK-13-work-instructions-ui.md) | M | [x] | 12 |

## Phase 1C — FAIR Forms

| # | Chunk | Size | Status | Prereqs |
|---|-------|------|--------|---------|
| 14 | [AS9102 FAIR Models + Service + UI](CHUNK-14-fair-forms.md) | M | [ ] | H6 |

---

## Notes

- **Chunks 01–13 are COMPLETE** — do NOT re-read those chunk files unless
  debugging a specific feature they built. They are historical records only.
- **Phase 2+ chunks** will be created when Phase 1C is complete. The module plan
  files in `docs/phase-2/MODULE-XX-*.md` have the detail for those.
- See `ROADMAP.md` for the full stage map and project status.
