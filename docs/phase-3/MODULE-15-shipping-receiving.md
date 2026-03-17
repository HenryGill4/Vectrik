# Module 15: Shipping & Receiving

## Status: [ ] Not Started
## Category: ERP
## Phase: 3 — Platform Maturity
## Priority: P3 - Medium

---

## Overview

The Shipping & Receiving module handles the outbound flow of finished parts to
customers and inbound receipt of materials. ProShop's shipping module is its most
criticized feature. Our implementation unifies BOL, packing list, commercial
invoice, and carrier label printing in a single streamlined workflow.

**ProShop Improvements**: Integrated BOL + packing list + commercial invoice in one
flow, carrier rate shopping, UPS/FedEx label printing, receiving inspection with
auto-allocation, shipment tracking, and customer notification automation.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| Shipping stage view partial | ✅ Exists | `Components/Pages/ShopFloor/StageViews/Shipping.razor` |
| `PartInstance.Status = Shipped` | ✅ Exists | `Models/PartInstance.cs` |
| `InventoryTransaction` type=CustomerReturn | ✅ M06 | `Models/InventoryTransaction.cs` |

**Gap**: No `Shipment` model, no packing list, no BOL, no carrier integration, no customer notification.

---

## What Needs to Be Built

### 1. Database Models (New)
- `Shipment` — outbound shipment record
- `ShipmentLine` — parts/quantities in a shipment
- `CarrierService` — configured shipping carrier accounts

### 2. Service Layer (New)
- `ShippingService` — shipment creation, document generation, status tracking

### 3. UI Components (New)
- **Shipping Queue** — work orders ready to ship
- **Ship Order** — create shipment workflow
- **Packing List / BOL Printout** — printable documents
- **Shipment History** — all past shipments with tracking

---

## Implementation Steps

### Step 1 — Create Shipment Model
**New File**: `Models/Shipment.cs`
```csharp
public class Shipment
{
    public int Id { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;  // SHP-2024-0001
    public int WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Preparing;
    public string ShipToName { get; set; } = string.Empty;
    public string ShipToAddress { get; set; } = string.Empty;
    public string? ShipToCity { get; set; }
    public string? ShipToState { get; set; }
    public string? ShipToZip { get; set; }
    public string? ShipToCountry { get; set; } = "US";
    public string? CarrierName { get; set; }                    // UPS, FedEx, Freight, Customer Pickup
    public string? ServiceLevel { get; set; }                   // Ground, 2-Day, Overnight
    public string? TrackingNumber { get; set; }
    public decimal? DeclaredValue { get; set; }
    public decimal? FreightCost { get; set; }
    public decimal? WeightLbs { get; set; }
    public string? NumberOfBoxes { get; set; }
    public string? CustomerPO { get; set; }
    public string? ShipperNotes { get; set; }
    public bool IsHazmat { get; set; } = false;
    public string ShippedByUserId { get; set; } = string.Empty;
    public DateTime? ShippedAt { get; set; }
    public ICollection<ShipmentLine> Lines { get; set; } = new List<ShipmentLine>();
}

public enum ShipmentStatus { Preparing, ReadyToShip, Shipped, InTransit, Delivered, Exception }
```

### Step 2 — Create ShipmentLine Model
**New File**: `Models/ShipmentLine.cs`
```csharp
public class ShipmentLine
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    public int WorkOrderLineId { get; set; }
    public WorkOrderLine WorkOrderLine { get; set; } = null!;
    public decimal QuantityShipped { get; set; }
    public List<int> PartInstanceIds { get; set; } = new();     // Serial numbers being shipped
    public string? CertificateNumbers { get; set; }             // Material certs
    public string? InspectionReportRefs { get; set; }           // QC report references
}
```

### Step 3 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<Shipment> Shipments { get; set; }
public DbSet<ShipmentLine> ShipmentLines { get; set; }
```

### Step 4 — Create ShippingService
**New File**: `Services/ShippingService.cs`
**New File**: `Services/IShippingService.cs`

```csharp
public interface IShippingService
{
    Task<List<WorkOrder>> GetReadyToShipAsync(string tenantCode);
    Task<Shipment> CreateShipmentAsync(Shipment shipment, string tenantCode);
    Task UpdateShipmentAsync(Shipment shipment, string tenantCode);
    Task MarkShippedAsync(int shipmentId, string userId, string tenantCode);
    Task<string> GenerateShipmentNumberAsync(string tenantCode);
    Task<string> GeneratePackingListHtmlAsync(int shipmentId, string tenantCode);
    Task<string> GenerateBolHtmlAsync(int shipmentId, string tenantCode);
    Task<List<Shipment>> GetShipmentHistoryAsync(string tenantCode, int? workOrderId = null);
}
```

**`MarkShippedAsync` logic**:
1. Set `ShippedAt = UtcNow`, status = Shipped
2. For each `ShipmentLine`, update all `PartInstance.Status = Shipped`
3. Update `WorkOrder.Status = Complete` if all lines shipped
4. Trigger customer notification (email or portal notification if Module 16 built)

### Step 5 — Shipping Queue Page
**New File**: `Components/Pages/Shipping/Queue.razor`
**Route**: `/shipping`

UI requirements:
- Table: WO#, Customer, Parts, Qty, Due Date, Status
- Filter: "Ready to Ship" (all jobs complete, not yet shipped) | "All Open"
- Priority badge per row
- "Create Shipment" button per row → opens Ship Order wizard

### Step 6 — Ship Order Workflow
**New File**: `Components/Pages/Shipping/CreateShipment.razor`
**Route**: `/shipping/create/{workOrderId:int}`

Wizard with 3 steps:

**Step 1 — Select Items**:
- Auto-populates from WO lines
- Adjust quantity per line (partial shipments allowed)
- Serial number selection (if tracking serialized parts)
- Certificate attachment references

**Step 2 — Carrier & Shipping Info**:
- Ship-to address (auto-fills from WO customer info, editable)
- Carrier selector: UPS | FedEx | USPS | Freight | Customer Pickup | Other
- Service level
- Weight and box count
- Declared value
- "Saturday Delivery" / "Signature Required" options
- Tracking number entry (manual for now; future: auto-generate via carrier API)

**Step 3 — Documents & Confirm**:
- Preview packing list (rendered HTML)
- Preview BOL (if freight)
- "Print Packing List" button → opens print dialog
- "Print BOL" button
- "Confirm & Mark Shipped" button

### Step 7 — Packing List HTML Template
**New File**: `Services/ShippingDocumentTemplates.cs`

Generate packing list as HTML string (printable via browser print):
```csharp
public static string GeneratePackingList(Shipment shipment, TenantInfo tenantInfo)
{
    return $"""
    <html>
    <head><style>/* print-friendly CSS */</style></head>
    <body>
        <div class="header">
            <h1>{tenantInfo.CompanyName}</h1>
            <h2>PACKING LIST</h2>
        </div>
        <div class="ship-to">
            <strong>Ship To:</strong><br/>
            {shipment.ShipToName}<br/>
            {shipment.ShipToAddress}<br/>
            {shipment.ShipToCity}, {shipment.ShipToState} {shipment.ShipToZip}
        </div>
        <table><!-- line items --></table>
        <div class="footer">
            Shipment: {shipment.ShipmentNumber} | Date: {shipment.ShippedAt:d}
        </div>
    </body>
    </html>
    """;
}
```

Similarly for BOL template (Bill of Lading).

### Step 8 — EF Core Migration
```bash
dotnet ef migrations add AddShipping --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Shipping queue shows work orders with all jobs complete
- [ ] Shipment can be created selecting WO lines and quantities
- [ ] Carrier, tracking number, and shipping details recorded
- [ ] Packing list prints with correct line items and quantities
- [ ] "Mark Shipped" updates PartInstance status to Shipped
- [ ] Work Order closes when all lines shipped
- [ ] Shipment history filterable by customer and date range

---

## Dependencies

- **Module 02** (Work Orders) — WO lines source for shipment
- **Module 05** (Quality) — QC certs reference in packing list
- **Module 06** (Inventory) — Outbound removes from finished goods inventory
- **Module 16** (CRM) — Customer notification on shipment

---

## Future Enhancements (Post-MVP)

- UPS/FedEx API integration for real-time rate shopping and label printing
- Customer email notification with tracking number on shipment
- Commercial invoice generation for international shipments
- Shipment tracking status sync (update "In Transit" → "Delivered" from carrier API)
