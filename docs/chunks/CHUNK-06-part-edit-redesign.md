# CHUNK-06: Part Edit Redesign (BOM + Routing In-Memory)

> **Size**: M (Medium) — ~4 file edits
> **ROADMAP tasks**: PI.4 (verify), PI.19 (BOM via service), Part Edit architecture fix
> **Prerequisites**: None (can run in parallel with H6 chunks)

---

## Scope

Fix the Part Edit page so that new parts can have routing stages and BOM items
added **before saving**. Currently, the Routing and BOM tabs show "save the part
first" placeholders when creating a new part. This chunk:

1. Removes the TenantDbContext injection from Edit.razor (architecture violation)
2. Adds BOM CRUD methods to IPartService/PartService
3. Enables in-memory routing and BOM editing during part creation
4. Saves part + routing + BOM all at once on "Create Part"

---

## Files to Read First

| File | Why |
|------|-----|
| `Components/Pages/Parts/Edit.razor` | The page to redesign (full read) |
| `Services/IPartService.cs` | Check current interface — needs BOM methods |
| `Services/PartService.cs` | Check current implementation |
| `Services/IMachineService.cs` | Replace DbContext machine queries |
| `Models/PartBomItem.cs` | BOM item model |
| `Models/PartStageRequirement.cs` | Routing model |

---

## Tasks

### 1. Add BOM methods to IPartService
**File**: `Services/IPartService.cs`
Add:
```csharp
// BOM
Task<List<PartBomItem>> GetBomItemsAsync(int partId);
Task<PartBomItem> AddBomItemAsync(PartBomItem item);
Task<PartBomItem> UpdateBomItemAsync(PartBomItem item);
Task RemoveBomItemAsync(int itemId);
```

### 2. Implement BOM methods in PartService
**File**: `Services/PartService.cs`
Implement the 4 BOM methods using `_db.PartBomItems` with Include for Material
and InventoryItem navigation properties.

### 3. Redesign Edit.razor
**File**: `Components/Pages/Parts/Edit.razor`

Changes:
- **Remove** `@inject Opcentrix_V3.Data.TenantDbContext Db`
- **Remove** `@using Microsoft.EntityFrameworkCore`
- **Add** `@inject IMachineService MachineService`
- **Initialize** `_stageReqs` and `_bomItems` as empty lists when `_isNew`
- **Load machines** via `MachineService.GetAllMachinesAsync(true)` in `OnInitializedAsync`
  instead of the `OnAfterRenderAsync` hack with direct Db query
- **Remove** the "save first" placeholder cards from Routing and BOM tabs
- **Show full editors** for Routing and BOM regardless of `_isNew`

### 4. Make routing CRUD in-memory for new parts
In `AddStageReq()`:
- If `_isNew`: add to `_stageReqs` list in memory (populate `ProductionStage` nav property from `_allStages`)
- If editing: persist immediately via `PartService.AddStageRequirementAsync()` (existing behavior)

Same pattern for `RemoveStageReq()`, `UpdateReqField()`, `UpdateReqMachine()`,
`UpdateReqRequired()` — skip the service call when `_isNew`.

Change `RemoveStageReq` to accept the `PartStageRequirement` object (not int id)
so it can remove from the in-memory list.

### 5. Make BOM CRUD in-memory for new parts
Same pattern as routing:
- `AddBomItem()`: in-memory when `_isNew`, persist when editing
- `RemoveBomItem()`: in-memory when `_isNew`, persist when editing
- Replace inline `SaveBomItem` with `UpdateBomNotes` that skips DB when `_isNew`

### 6. Update Save() to persist everything on create
In `Save()`, after `PartService.CreatePartAsync(_part)`:
```csharp
// Persist in-memory routing
foreach (var req in _stageReqs)
{
    req.PartId = _part.Id;
    await PartService.AddStageRequirementAsync(req);
}
// Persist in-memory BOM
foreach (var item in _bomItems.Where(b => b.IsActive))
{
    item.PartId = _part.Id;
    await PartService.AddBomItemAsync(item);
}
```

---

## Verification

1. Build passes
2. Create a new part → Routing tab shows full editor (no "save first" card)
3. Add 3 routing stages to new part → they appear in the table with cost summary
4. Add 2 BOM items to new part → they appear in the table
5. Click "Create Part" → part created with all routing stages and BOM items saved
6. Edit existing part → routing and BOM still work as before (no regression)
7. No `TenantDbContext` or `Microsoft.EntityFrameworkCore` references in Edit.razor

---

## Files Modified (fill in after completion)

- `Services/IPartService.cs` — Added `GetBomItemsAsync`, `AddBomItemAsync`, `UpdateBomItemAsync`, `RemoveBomItemAsync`
- `Services/PartService.cs` — Implemented 4 BOM methods using `_db.PartBomItems`
- `Components/Pages/Parts/Edit.razor` — Removed `TenantDbContext` + `Microsoft.EntityFrameworkCore`, added `IMachineService`, initialized empty `_stageReqs`/`_bomItems` for new parts, loaded machines via `MachineService.GetAllMachinesAsync(true)`, removed "save first" placeholders, made routing/BOM CRUD in-memory for new parts, persists all on Create
