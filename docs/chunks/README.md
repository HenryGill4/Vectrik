> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# Work Chunks — Agent Execution Guide

> **For AI agents**: Each chunk in this folder is a self-contained work unit
> designed to be completed in a single session. Follow these rules:
>
> 1. Read `ROADMAP.md` header to confirm current phase and state
> 2. Read `QUEUE.md` (this folder) to find the next chunk to execute
> 3. Read the chunk file — it contains everything you need
> 4. Execute all tasks in the chunk
> 5. Mark the chunk complete in `QUEUE.md`
> 6. Run a build to verify

---

## Chunk File Format

Each chunk file contains:

| Section | Purpose |
|---------|---------|
| **Scope** | What this chunk accomplishes, estimated size |
| **Prerequisites** | What chunks must be complete first |
| **Files to Read** | Exact files to read before making changes |
| **Tasks** | Numbered, ordered steps with acceptance criteria |
| **Files Modified** | Summary of what was touched (filled in after completion) |
| **Verification** | How to confirm the chunk is done (build, specific behaviors) |

## Sizing Guide

| Size | Meaning | Typical scope |
|------|---------|---------------|
| **S** | Small — 1-3 files, straightforward edits | Wiring a service, adding a component |
| **M** | Medium — 4-8 files, some new logic | New feature wiring, service method + UI |
| **L** | Large — 8-15 files, new patterns | New module, model + service + UI |

All chunks are sized to complete within one agent session without rushing.

---

## File Reference

| Document | Purpose |
|----------|---------|
| `QUEUE.md` | Ordered execution list — find next chunk here |
| `CHUNK-XX-*.md` | Individual work chunks |
| `../../ROADMAP.md` | Master project roadmap (high-level) |
| `../MASTER_CONTEXT.md` | Architecture patterns, model/service/route registries |
