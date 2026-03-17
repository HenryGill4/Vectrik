# Module 06: Inventory Control & Material Planning

## Status: [ ] Not Started
## Category: ERP
## Phase: 1 — Core Production Engine
## Priority: P1 - Critical

---

## Overview

Inventory Control tracks raw materials, consumables, and finished goods with
real-time stock visibility, lot/serial traceability, multi-location warehouse
support, and intelligent reorder point management. Material Planning forecasts
demand from live job requirements and automatically generates purchase suggestions.

**ProShop Improvements**: Barcode/QR scanning for receiving and consumption,
full lot/serial genealogy trees, multi-location warehouse support, and intelligent
reorder points using consumption velocity — not just static min/max.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `Material` model (name, description, density) | ✅ Exists | `Models/Material.cs` |
| `MaterialService` / `IMaterialService` (stub) | ✅ Exists | `Services/MaterialService.cs` |
| `/admin/materials` management page | ✅ Exists | `Components/Pages/Admin/Materials.razor` |

**Gap**: No inventory quantity tracking, no stock locations, no receiving workflow, no lot/serial tracking, no reorder automation, no barcode scanning.

---

## What Needs to Be Built

### 1. Database Models (New)
- `InventoryItem` — stockable item (material, consumable, component)
- `StockLocation` — physical storage locations (warehouse, shelf, bin)
- `InventoryLot` — lot/heat number tracking
- `InventoryTransaction` — all stock movements (receive, consume, adjust, transfer)
- `ReorderRule` — reorder point configuration per item
- `MaterialRequest` — request raw material for a job

### 2. Service Layer (New)
- `InventoryService` — stock transactions, reservations, availability checks
- `MaterialPlanningService` — demand vs. supply analysis, reorder suggestions

### 3. UI Components (New)
- **Inventory Dashboard** — stock levels, low-stock alerts, pending receipts
- **Stock Ledger** — transaction history per item
- **Receiving Workflow** — receive PO items with lot number entry
- **Material Request** — operators request material for a job
- **Reorder Management** — configure reorder rules, review auto-suggestions

---

## Implementation Steps

### Step 1 — Create InventoryItem Model
**New File**: `Models/InventoryItem.cs`
```csharp
public class InventoryItem
{
    public int Id { get; set; }
    public string ItemNumber { get; set; } = string.Empty;   // Internal part/item #
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public InventoryItemType ItemType { get; set; }          // RawMaterial, Consumable, Tooling, FinishedGood
    public int? MaterialId { get; set; }                     // Link to existing Material model
    public Material? Material { get; set; }
    public string UnitOfMeasure { get; set; } = "each";     // each, kg, ft, lb, gal
    public decimal CurrentStockQty { get; set; }             // Calculated from transactions
    public decimal ReservedQty { get; set; }                 // Reserved for open jobs
    public decimal AvailableQty => CurrentStockQty - ReservedQty;
    public decimal ReorderPoint { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal? UnitCost { get; set; }
    public bool TrackLots { get; set; } = false;
    public bool TrackSerials { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<InventoryLot> Lots { get; set; } = new List<InventoryLot>();
}

public enum InventoryItemType { RawMaterial, Consumable, CuttingTool, Fixture, FinishedGood, WIP }
```

### Step 2 — Create StockLocation Model
**New File**: `Models/StockLocation.cs`
```csharp
public class StockLocation
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;         // e.g., "WH1-A3-B2"
    public string Name { get; set; } = string.Empty;         // e.g., "Warehouse 1 - Aisle 3 - Bin 2"
    public LocationType LocationType { get; set; }           // Warehouse, ShopFloor, Quarantine, Shipping
    public string? ParentLocationCode { get; set; }          // For hierarchical locations
    public bool IsActive { get; set; } = true;
}

public enum LocationType { Warehouse, ShopFloor, Quarantine, Receiving, Shipping, CuttingToolCrib }
```

### Step 3 — Create InventoryLot Model
**New File**: `Models/InventoryLot.cs`
```csharp
public class InventoryLot
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public string LotNumber { get; set; } = string.Empty;    // Supplier lot/heat number
    public string? CertificateNumber { get; set; }           // Material cert reference
    public decimal ReceivedQty { get; set; }
    public decimal CurrentQty { get; set; }
    public int? StockLocationId { get; set; }
    public StockLocation? Location { get; set; }
    public int? PurchaseOrderLineId { get; set; }            // Traceability to PO
    public DateTime ReceivedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public LotStatus Status { get; set; } = LotStatus.Available;
    public string? InspectionStatus { get; set; }
}

public enum LotStatus { Quarantine, Available, Depleted, Rejected }
```

### Step 4 — Create InventoryTransaction Model
**New File**: `Models/InventoryTransaction.cs`
```csharp
public class InventoryTransaction
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public TransactionType TransactionType { get; set; }
    public decimal Quantity { get; set; }                    // Positive = in, Negative = out
    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }
    public int? JobId { get; set; }                          // Consumption for job
    public int? PurchaseOrderLineId { get; set; }            // Receipt from PO
    public string PerformedByUserId { get; set; } = string.Empty;
    public string? Reference { get; set; }                   // PO#, Job#, Adjustment reason
    public string? Notes { get; set; }
    public DateTime TransactedAt { get; set; } = DateTime.UtcNow;
}

public enum TransactionType
{
    Receipt,           // Received from PO
    JobConsumption,    // Used on a job
    JobReturn,         // Returned from job (unused material)
    Adjustment,        // Manual inventory adjustment
    Transfer,          // Move between locations
    Scrap,             // Scrapped/disposed
    CustomerReturn,    // Customer returned finished goods
    CycleCount         // Cycle count correction
}
```

### Step 5 — Create MaterialRequest Model
**New File**: `Models/MaterialRequest.cs`
```csharp
public class MaterialRequest
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job Job { get; set; } = null!;
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public decimal QuantityRequested { get; set; }
    public decimal? QuantityIssued { get; set; }
    public int? LotId { get; set; }
    public InventoryLot? IssuedFromLot { get; set; }
    public MaterialRequestStatus Status { get; set; } = MaterialRequestStatus.Pending;
    public string RequestedByUserId { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FulfilledAt { get; set; }
}

public enum MaterialRequestStatus { Pending, PartiallyFulfilled, Fulfilled, Cancelled }
```

### Step 6 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<InventoryItem> InventoryItems { get; set; }
public DbSet<StockLocation> StockLocations { get; set; }
public DbSet<InventoryLot> InventoryLots { get; set; }
public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
public DbSet<MaterialRequest> MaterialRequests { get; set; }
```

### Step 7 — Create InventoryService
**New File**: `Services/InventoryService.cs`
**New File**: `Services/IInventoryService.cs`

```csharp
public interface IInventoryService
{
    // Stock queries
    Task<List<InventoryItem>> GetAllItemsAsync(string tenantCode, InventoryItemType? type = null);
    Task<InventoryItem?> GetItemByIdAsync(int id, string tenantCode);
    Task<decimal> GetAvailableQtyAsync(int itemId, string tenantCode);
    Task<List<InventoryItem>> GetLowStockItemsAsync(string tenantCode);

    // Transactions
    Task ReceiveStockAsync(int itemId, decimal qty, string lotNumber, int locationId,
                           string userId, string reference, string tenantCode);
    Task ConsumeForJobAsync(int itemId, decimal qty, int jobId, int? lotId,
                            string userId, string tenantCode);
    Task TransferAsync(int itemId, decimal qty, int fromLocationId, int toLocationId,
                       string userId, string tenantCode);
    Task AdjustAsync(int itemId, decimal newQty, string reason, string userId, string tenantCode);
    Task<List<InventoryTransaction>> GetTransactionHistoryAsync(int itemId, string tenantCode);

    // Reservations
    Task ReserveForJobAsync(int itemId, decimal qty, int jobId, string tenantCode);
    Task ReleaseReservationAsync(int jobId, string tenantCode);

    // Material requests
    Task<MaterialRequest> CreateRequestAsync(MaterialRequest request, string tenantCode);
    Task FulfillRequestAsync(int requestId, decimal qty, int? lotId, string userId, string tenantCode);
    Task<List<MaterialRequest>> GetPendingRequestsAsync(string tenantCode);
}
```

**Key business rules**:
- `ReceiveStockAsync`: creates `InventoryTransaction` (type=Receipt), updates `InventoryItem.CurrentStockQty`
- `ConsumeForJobAsync`: creates transaction (type=JobConsumption), reduces qty, updates lot qty
- `AdjustAsync`: records cycle count transaction, sets new qty as difference
- All transactions are immutable — corrections are new transactions

### Step 8 — Create MaterialPlanningService
**New File**: `Services/MaterialPlanningService.cs`
**New File**: `Services/IMaterialPlanningService.cs`

```csharp
public interface IMaterialPlanningService
{
    Task<List<MaterialRequirement>> GetRequirementsFromOpenJobsAsync(string tenantCode);
    Task<List<ReorderSuggestion>> GetReorderSuggestionsAsync(string tenantCode);
    Task<MaterialAvailabilityReport> CheckJobMaterialAvailabilityAsync(int jobId, string tenantCode);
}

public record MaterialRequirement(int ItemId, string ItemName, decimal RequiredQty,
    decimal AvailableQty, decimal ShortfallQty, DateTime RequiredByDate);

public record ReorderSuggestion(int ItemId, string ItemName, decimal CurrentQty,
    decimal ReorderPoint, decimal SuggestedOrderQty, string Reason);
```

Logic:
- Aggregate material requirements from all open/scheduled jobs
- Compare against `AvailableQty` (accounting for reservations)
- Flag shortfalls with the earliest `MustLeaveByDate` from associated jobs
- Reorder suggestions trigger when `CurrentStockQty ≤ ReorderPoint`
- Velocity-based reorder: also suggest if consumption rate is accelerating

### Step 9 — Inventory Dashboard Page
**New File**: `Components/Pages/Inventory/Dashboard.razor`
**Route**: `/inventory`

UI requirements:
- KPI row: Total SKUs, Total Stock Value, Low Stock Items (count), Pending Receipts
- **Low Stock Alert Panel**: items below reorder point with "Create PO" quick action
- **Recent Transactions** table: last 20 transactions across all items
- **Material Demand vs. Supply** table: requirements from open jobs vs. available
- Quick action buttons: "Receive Stock", "Create Material Request"

### Step 10 — Inventory Items List
**New File**: `Components/Pages/Inventory/Items.razor`
**Route**: `/inventory/items`

UI requirements:
- Searchable, filterable table: Item#, Name, Type, UOM, On Hand, Reserved, Available, Unit Cost, Location
- Type filter tabs: All | Raw Material | Consumable | Tooling
- Color-coded availability: green (above reorder), yellow (near reorder), red (below reorder)
- Click item → item detail page with transaction history
- "New Item" button

### Step 11 — Receiving Workflow Page
**New File**: `Components/Pages/Inventory/Receive.razor`
**Route**: `/inventory/receive`

UI requirements:
- PO# lookup (or receive without PO via "Blind Receipt")
- Item line items with quantity received input
- Lot number entry (with barcode scan support via camera)
- Location assignment per line
- Certificate number input
- "Post Receipt" button → creates all transactions

**Barcode scanning support**:
- Add `BarcodeScanner.js` to `wwwroot/js/` that uses browser camera API
- JS interop method: `scanBarcode()` → returns decoded string
- Use in receiving page for lot number scanning

### Step 12 — Stock Ledger Page
**New File**: `Components/Pages/Inventory/Ledger.razor`
**Route**: `/inventory/items/{id:int}/ledger`

UI requirements:
- Item header: name, current qty, reserved qty, available qty
- Lot breakdown table: lot#, qty on hand, location, received date
- Transaction history: chronological list of all movements
- Running balance column

### Step 13 — Nav Menu Update
**File**: `Components/Layout/NavMenu.razor`
Add Inventory section (visible to Manager+ roles):
- Inventory → `/inventory`
- Items → `/inventory/items`
- Receive → `/inventory/receive`

### Step 14 — EF Core Migration
```bash
dotnet ef migrations add AddInventoryControl --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Inventory items can be created with type, UOM, and reorder settings
- [ ] Stock can be received with lot number and location assignment
- [ ] Receiving creates an `InventoryTransaction` and updates `CurrentStockQty`
- [ ] Material can be consumed for a job, reducing available qty
- [ ] Manual adjustments (cycle counts) are logged with reason
- [ ] Lot traceability shows which jobs consumed from which lot
- [ ] Low stock dashboard shows items below reorder point
- [ ] Material planning shows open job requirements vs. available stock
- [ ] Shortfalls show earliest required-by date for urgency ranking
- [ ] Reorder suggestions auto-calculate based on reorder point

---

## Dependencies

- **Module 12** (Purchasing) — PO receipts update inventory
- **Module 04** (Shop Floor) — Material consumption at stage execution
- **Module 07** (Analytics) — Stock value and turnover KPIs
- **Module 10** (Cutting Tools) — Tooling items tracked in inventory

---

## Future Enhancements (Post-MVP)

- RFID-based location tracking (real-time stock location without manual transfer)
- Vendor-managed inventory (VMI) portal for suppliers to see stock levels
- ABC analysis for inventory classification
- Cycle count scheduling with assignment to stock counter users
- Integration with receiving inspection (Module 05 QMS)
