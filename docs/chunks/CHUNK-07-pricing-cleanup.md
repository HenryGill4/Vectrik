> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# CHUNK-07: PricingEngine Verification + Part System Cleanup

> **Size**: S (Small) — ~3-5 file edits
> **ROADMAP tasks**: PI.3 (verify), PI.6, PI.10, PI.11, PI.12, PI.13
> **Prerequisites**: CHUNK-06 complete

---

## Scope

Verify the PricingEngine uses PartStageRequirement data correctly (it likely
already does), backfill MaterialId in seed data, and clean up minor Part system
issues identified in the integration audit.

---

## Files to Read First

| File | Why |
|------|-----|
| `Services/PricingEngineService.cs` | Verify it uses FK + stage overrides |
| `Services/IPricingEngineService.cs` | Check interface |
| `Services/PartService.cs` | Check ValidatePartAsync is truly async |
| `Services/WorkOrderService.cs` | Check user capture on job generation |
| `Components/Pages/WorkOrders/Create.razor` | Check auth user capture |
| `Program.cs` | Find DataSeedingService call for MaterialId backfill |
| `Models/Part.cs` | Check RequiredStages Obsolete attribute |

---

## Tasks

### 1. Verify PricingEngine (PI.3)
Read `PricingEngineService.CalculatePartCostAsync` — confirm it:
- Uses `PartStageRequirement` (not hardcoded rates)
- Uses `HourlyRateOverride` with fallback to `ProductionStage.DefaultHourlyRate`
- Uses `MaterialEntity` FK for material cost (not string match)
- If already correct: mark PI.3 done. If not: fix it.

### 2. Backfill MaterialId in seed data (PI.6)
**File**: `Program.cs` or `Services/DataSeedingService.cs`
- Find where demo parts are seeded
- Ensure each seeded part has `MaterialId` set (not just `Material` string)
- If materials are seeded first, use their IDs

### 3. Verify Part.RequiredStages is obsolete (PI.10)
**File**: `Models/Part.cs`
- Confirm `RequiredStages` has `[Obsolete]` attribute
- Confirm it no longer has `[Required]`
- If not: add `[Obsolete("Use PartStageRequirements navigation property instead")]`

### 4. Verify ValidatePartAsync is truly async (PI.11)
**File**: `Services/PartService.cs`
- Confirm `ValidatePartAsync` uses `await` for the duplicate check
- Confirm it has the duplicate PartNumber check
- If not: fix it

### 5. Fix WO Create auth user capture (PI.12)
**File**: `Components/Pages/WorkOrders/Create.razor`
- Check if the "CreatedBy" field is set from the authenticated user
- If it's hardcoded as "System", fix it to use the auth user claim

### 6. Set Job.SlsMaterial from Part.Material (PI.13)
**File**: `Services/WorkOrderService.cs`
- In `GenerateJobsForLineAsync`, when creating a Job, check if
  `job.SlsMaterial` is being set from the part's material
- If not: add `job.SlsMaterial = part.Material;`

---

## Verification

1. Build passes
2. Seed data has MaterialId set on parts
3. PricingEngine uses stage-level cost overrides
4. Creating a WO captures the logged-in user name (not "System")
5. Job generation sets SlsMaterial from the part

---

## Files Modified (fill in after completion)

- `Services/PricingEngineService.cs` — **Verified correct** (PI.3): uses PartStageRequirement with GetEffectiveEstimatedHours/GetEffectiveHourlyRate, MaterialEntity FK for material cost, SystemSettings for labor/overhead
- `Services/DataSeedingService.cs` — **Fixed** (PI.6): Added `materialLookup` dictionary, set `MaterialId` on all 4 seed parts using FK lookup
- `Models/Part.cs` — **Verified correct** (PI.10): `RequiredStages` already has `[Obsolete]` attribute, no `[Required]`
- `Services/PartService.cs` — **Verified correct** (PI.11): `ValidatePartAsync` is truly async with `AnyAsync` duplicate check
- `Components/Pages/WorkOrders/Create.razor` — **Verified correct** (PI.12): Captures `_currentUser` from auth claims, sets `CreatedBy`/`LastModifiedBy`
- `Services/WorkOrderService.cs` — **Verified correct** (PI.13): `GenerateJobsForLineAsync` already sets `SlsMaterial = line.Part.Material`
