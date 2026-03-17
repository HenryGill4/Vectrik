# Module 10: Cutting Tool & Fixture Management

## Status: [ ] Not Started
## Category: MES
## Phase: 2 — Operational Depth
## Priority: P2 - High

---

## Overview

Cutting Tool Management tracks every tool in the crib — inventory, usage per job,
predicted wear life, and job-specific tool kits. Fixture Management tracks physical
fixtures, their locations, and maintenance schedules. Together, these prevent
production stops from missing or worn-out tooling.

**ProShop Improvements**: Tool life management with usage-based wear tracking and
predictive replacement alerts, fixture location tracking, tool kit assembly for
job-specific packages, and a visual tool crib map.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `InventoryItem` with type=Tooling (from M06) | ✅ M06 | `Models/InventoryItem.cs` |
| Basic `Machine` model | ✅ Exists | `Models/Machine.cs` |

**Gap**: No dedicated tool model with wear tracking, no tool assembly kit, no fixture model, no tool crib location map, no presetter integration.

---

## What Needs to Be Built

### 1. Database Models (New)
- `CuttingTool` — detailed tool record (geometry, material, coating, manufacturer)
- `ToolInstance` — individual tool in stock with usage/wear tracking
- `ToolKit` — collection of tools required for a specific job/operation
- `Fixture` — workholding fixtures with location and maintenance tracking

### 2. Service Layer (New)
- `ToolManagementService` — tool lifecycle, usage tracking, replacement alerts

### 3. UI Components (New)
- **Tool Crib Dashboard** — stock levels, worn/expired tools, usage forecast
- **Tool Instance Detail** — usage history, wear status, replacement prediction
- **Tool Kit Builder** — create job-specific tool packages
- **Fixture Registry** — fixture inventory with location and status

---

## Implementation Steps

### Step 1 — Create CuttingTool Model
**New File**: `Models/CuttingTool.cs`
```csharp
public class CuttingTool
{
    public int Id { get; set; }
    public string ToolNumber { get; set; } = string.Empty;    // Internal tool ID
    public string Manufacturer { get; set; } = string.Empty;
    public string ManufacturerPartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ToolType ToolType { get; set; }
    public decimal Diameter { get; set; }                     // mm
    public decimal? FluteLength { get; set; }
    public decimal? OverallLength { get; set; }
    public int NumberOfFlutes { get; set; }
    public string? Material { get; set; }                     // HSS, Carbide, Ceramic
    public string? Coating { get; set; }                      // TiN, TiAlN, uncoated
    public decimal RatedLifeMinutes { get; set; }             // Manufacturer rated tool life
    public decimal? UnitCost { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ToolInstance> Instances { get; set; } = new List<ToolInstance>();
}

public enum ToolType
{
    EndMill, DrillBit, TapThread, ReamerBore, InsertableFace,
    TurningInsert, BoreTool, SlottingCutter, ThreadMill, Other
}
```

### Step 2 — Create ToolInstance Model
**New File**: `Models/ToolInstance.cs`
```csharp
public class ToolInstance
{
    public int Id { get; set; }
    public int CuttingToolId { get; set; }
    public CuttingTool CuttingTool { get; set; } = null!;
    public string SerialNumber { get; set; } = string.Empty;   // Or auto-generated
    public ToolInstanceStatus Status { get; set; } = ToolInstanceStatus.Available;
    public int? CurrentMachineId { get; set; }                 // Which machine it's loaded in
    public Machine? CurrentMachine { get; set; }
    public string? CurrentToolHolderPosition { get; set; }     // e.g., "T05"
    public decimal TotalUsageMinutes { get; set; }             // Accumulated usage
    public decimal RemainingLifePct => CuttingTool.RatedLifeMinutes > 0
        ? Math.Max(0, 100 - (TotalUsageMinutes / CuttingTool.RatedLifeMinutes * 100))
        : 100;
    public decimal? PresetterLengthOffset { get; set; }        // From presetter measurement
    public decimal? PresetterRadiusOffset { get; set; }
    public DateTime? LastPresetAt { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RetiredAt { get; set; }
    public ICollection<ToolUsageLog> UsageLogs { get; set; } = new List<ToolUsageLog>();
}

public enum ToolInstanceStatus { Available, InUse, NeedsRegrind, AtPresetter, Retired, Lost }
```

### Step 3 — Create ToolUsageLog Model
**New File**: `Models/ToolUsageLog.cs`
```csharp
public class ToolUsageLog
{
    public int Id { get; set; }
    public int ToolInstanceId { get; set; }
    public ToolInstance ToolInstance { get; set; } = null!;
    public int? JobId { get; set; }
    public Job? Job { get; set; }
    public int? StageExecutionId { get; set; }
    public decimal UsageMinutes { get; set; }
    public string? Notes { get; set; }
    public string LoggedByUserId { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 4 — Create ToolKit Model
**New File**: `Models/ToolKit.cs`
```csharp
public class ToolKit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;           // e.g., "Part #12345 Machining Kit"
    public int? PartId { get; set; }
    public Part? Part { get; set; }
    public int? ProductionStageId { get; set; }
    public ProductionStage? ProductionStage { get; set; }
    public string? Notes { get; set; }
    public ICollection<ToolKitItem> Items { get; set; } = new List<ToolKitItem>();
}

public class ToolKitItem
{
    public int Id { get; set; }
    public int ToolKitId { get; set; }
    public ToolKit ToolKit { get; set; } = null!;
    public int CuttingToolId { get; set; }
    public CuttingTool CuttingTool { get; set; } = null!;
    public int? ToolInstanceId { get; set; }                   // Specific instance assigned
    public ToolInstance? AssignedInstance { get; set; }
    public string? ToolHolderPosition { get; set; }            // e.g., "T01"
    public string? ProgrammedSpeed { get; set; }               // RPM from CNC program
    public string? ProgrammedFeed { get; set; }                // in/min or mm/min
    public string? Notes { get; set; }
}
```

### Step 5 — Create Fixture Model
**New File**: `Models/Fixture.cs`
```csharp
public class Fixture
{
    public int Id { get; set; }
    public string FixtureNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FixtureType FixtureType { get; set; }
    public int? AssociatedPartId { get; set; }
    public Part? AssociatedPart { get; set; }
    public FixtureStatus Status { get; set; } = FixtureStatus.Available;
    public int? CurrentLocationId { get; set; }
    public StockLocation? CurrentLocation { get; set; }
    public int? CurrentMachineId { get; set; }
    public Machine? CurrentMachine { get; set; }
    public DateTime? NextMaintenanceDue { get; set; }
    public string? MaintenanceNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum FixtureType { ViseJaw, PlateFixture, TombStone, Collet, Chuck, Custom }
public enum FixtureStatus { Available, InUse, InMaintenance, Retired }
```

### Step 6 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<CuttingTool> CuttingTools { get; set; }
public DbSet<ToolInstance> ToolInstances { get; set; }
public DbSet<ToolUsageLog> ToolUsageLogs { get; set; }
public DbSet<ToolKit> ToolKits { get; set; }
public DbSet<ToolKitItem> ToolKitItems { get; set; }
public DbSet<Fixture> Fixtures { get; set; }
```

### Step 7 — Create ToolManagementService
**New File**: `Services/ToolManagementService.cs`
**New File**: `Services/IToolManagementService.cs`

```csharp
public interface IToolManagementService
{
    // Tool catalog
    Task<List<CuttingTool>> GetAllToolsAsync(string tenantCode);
    Task<CuttingTool> CreateToolAsync(CuttingTool tool, string tenantCode);

    // Instances
    Task<List<ToolInstance>> GetInstancesAsync(int toolId, string tenantCode);
    Task LogUsageAsync(int instanceId, decimal minutes, int? jobId, string userId, string tenantCode);
    Task<List<ToolInstance>> GetNearEndOfLifeAsync(decimal thresholdPct, string tenantCode);
    Task RetireInstanceAsync(int instanceId, string tenantCode);
    Task UpdatePresetterDataAsync(int instanceId, decimal lengthOffset,
                                   decimal radiusOffset, string tenantCode);

    // Tool kits
    Task<List<ToolKit>> GetKitsForPartAsync(int partId, string tenantCode);
    Task<ToolKit> CreateKitAsync(ToolKit kit, string tenantCode);
    Task AssignInstanceToKitItemAsync(int kitItemId, int instanceId, string tenantCode);

    // Fixtures
    Task<List<Fixture>> GetAllFixturesAsync(string tenantCode);
    Task UpdateFixtureLocationAsync(int fixtureId, int? locationId, int? machineId, string tenantCode);
}
```

**Tool life alert logic**:
- After each `LogUsageAsync`: check `RemainingLifePct < 20%` → create maintenance alert or notification
- Check during job scheduling: are required tools available and have sufficient life?

### Step 8 — Tool Crib Dashboard
**New File**: `Components/Pages/ToolCrib/Dashboard.razor`
**Route**: `/toolcrib`

UI requirements:
- KPI row: Total Tool Types, Instances Available, Near End of Life (count), Tools in Use
- **Near End of Life** table: Tool#, Description, Instance#, Remaining Life %, Current Machine
  - Red: < 10%, Yellow: 10-25%, Green: > 25%
- **Usage Forecast** from open jobs: which tools are needed and when
- **Reorder Suggestions**: tools below reorder point
- Quick action: "Add Tool", "Log Usage"

### Step 9 — Tool Instance Detail
**New File**: `Components/Pages/ToolCrib/ToolDetail.razor`
**Route**: `/toolcrib/tools/{id:int}`

UI requirements:
- Tool header: number, description, type, manufacturer
- Instance list with status and life % progress bars
- Usage log history table
- Presetter data section (offsets, last preset date)
- "Retire Instance" button

### Step 10 — Tool Kit Builder
**New File**: `Components/Pages/ToolCrib/KitBuilder.razor`
**Route**: `/toolcrib/kits/{id:int}/edit`

UI requirements:
- Kit name, linked part, linked stage
- Tool items table: tool selector dropdown, position (T01-T99), programmed speed/feed
- For each item: "Assign Instance" button → pick from available instances
- Life check: show remaining life % of assigned instance with warning if < 25%
- Print kit list button (for setup operators)

### Step 11 — EF Core Migration
```bash
dotnet ef migrations add AddCuttingTools --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Cutting tools can be created with geometry, material, and rated life
- [ ] Tool instances track accumulated usage minutes
- [ ] Usage log records which job consumed which tool for how long
- [ ] Tools near end of life show warning in dashboard
- [ ] Tool kits can be built listing required tools per job/stage
- [ ] Specific instances can be assigned to kit items
- [ ] Presetter data (length/radius offsets) can be recorded per instance
- [ ] Fixtures have location tracking (machine or storage location)
- [ ] Fixture maintenance schedule shows upcoming due dates

---

## Dependencies

- **Module 04** (Shop Floor) — Tool usage logged during stage execution
- **Module 06** (Inventory) — Tooling items also tracked in inventory
- **Module 11** (Maintenance) — Fixture maintenance schedules
- **Module 08** (Parts/PDM) — Tool kits linked to part + stage

---

## Future Enhancements (Post-MVP)

- Mastercam tool export integration (import tool list from NC program)
- Presetter machine direct connection (import offset data automatically)
- Tool vending machine integration (Zoller, Kennametal)
- Visual tool crib map (drag tools to shelf/bin locations on a floor plan image)
