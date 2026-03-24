# Module 12: Purchasing & Vendor Management

## Status: [ ] Not Started
## Category: ERP
## Phase: 2 — Operational Depth
## Priority: P2 - High

---

## Overview

The Purchasing module covers the full procurement cycle from requisition to
receiving. Vendor Management maintains an approved vendor list, scorecards, and
performance history. Integration with Inventory (Module 06) means PO receipts
automatically update stock.

**ProShop Improvements**: Approved vendor lists, vendor scorecards (quality,
delivery, cost), automated RFQ distribution, PO approval workflows, receiving
inspection integration, and blanket PO management.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `InventoryTransaction` with type=Receipt | ✅ M06 | `Models/InventoryTransaction.cs` |
| `InventoryLot.PurchaseOrderLineId` reference | ✅ M06 | `Models/InventoryLot.cs` |

**Gap**: No Vendor model, no PurchaseOrder model, no approval workflow, no vendor scorecard.

---

## What Needs to Be Built

### 1. Database Models (New)
- `Vendor` — supplier record with contact info and approval status
- `VendorItem` — vendor-specific pricing per item
- `PurchaseOrder` — PO header with approval status
- `PurchaseOrderLine` — PO line items
- `VendorScorecard` — periodic vendor performance records

### 2. Service Layer (New)
- `PurchasingService` — PO lifecycle, approval workflow
- `VendorService` — vendor management, scorecard calculation

### 3. UI Components (New)
- **Vendor List** — with approval status and scorecard summary
- **Vendor Detail** — contact, approved items, scorecard history
- **Purchase Order List** — with status, approval workflow
- **PO Builder** — create PO from requisitions or manually
- **Receiving Dock** — receive PO lines (integrates with Module 06)

---

## Implementation Steps

### Step 1 — Create Vendor Model
**New File**: `Models/Vendor.cs`
```csharp
public class Vendor
{
    public int Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;      // Short code: "ACM-001"
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public VendorStatus Status { get; set; } = VendorStatus.Pending;  // Pending, Approved, Suspended
    public List<string> ApprovedCategories { get; set; } = new();    // Serialized list
    public string? PaymentTerms { get; set; }                   // Net30, Net45
    public int TypicalLeadTimeDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public ICollection<VendorScorecard> Scorecards { get; set; } = new List<VendorScorecard>();
}

public enum VendorStatus { Pending, Approved, Conditional, Suspended }
```

### Step 2 — Create VendorItem Model
**New File**: `Models/VendorItem.cs`
```csharp
public class VendorItem
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public string? VendorPartNumber { get; set; }              // Vendor's part number
    public decimal UnitCost { get; set; }
    public string? UnitOfMeasure { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal? MinOrderQuantity { get; set; }
    public DateTime? PriceEffectiveDate { get; set; }
    public bool IsPreferred { get; set; } = false;             // Preferred vendor for this item
}
```

### Step 3 — Create PurchaseOrder Model
**New File**: `Models/PurchaseOrder.cs`
```csharp
public class PurchaseOrder
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;       // Auto-generated: PO-2024-0001
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? ShipToLocationId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string? PaymentTerms { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public enum PurchaseOrderStatus
{
    Draft, PendingApproval, Approved, Sent, PartiallyReceived, Received, Closed, Cancelled
}
```

### Step 4 — Create PurchaseOrderLine Model
**New File**: `Models/PurchaseOrderLine.cs`
```csharp
public class PurchaseOrderLine
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public string? VendorPartNumber { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal => QuantityOrdered * UnitCost;
    public DateTime? RequestedDeliveryDate { get; set; }
    public int? RequiredForJobId { get; set; }                 // Demand traceability
    public string? Notes { get; set; }
}
```

### Step 5 — Create VendorScorecard Model
**New File**: `Models/VendorScorecard.cs`
```csharp
public class VendorScorecard
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public int PeriodYear { get; set; }
    public int PeriodQuarter { get; set; }
    public decimal OnTimeDeliveryPct { get; set; }           // % POs received on time
    public decimal QualityAcceptanceRatePct { get; set; }   // % lots accepted without NCR
    public decimal PricingCompliancePct { get; set; }       // % lines at or below PO price
    public decimal OverallScore { get; set; }               // Weighted composite score
    public string? Notes { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 6 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<Vendor> Vendors { get; set; }
public DbSet<VendorItem> VendorItems { get; set; }
public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
public DbSet<VendorScorecard> VendorScorecards { get; set; }
```

### Step 7 — Create PurchasingService
**New File**: `Services/PurchasingService.cs`
**New File**: `Services/IPurchasingService.cs`

```csharp
public interface IPurchasingService
{
    Task<List<PurchaseOrder>> GetAllAsync(PurchaseOrderStatus? status, string tenantCode);
    Task<PurchaseOrder?> GetByIdAsync(int id, string tenantCode);
    Task<PurchaseOrder> CreateAsync(PurchaseOrder po, string tenantCode);
    Task UpdateAsync(PurchaseOrder po, string tenantCode);
    Task SubmitForApprovalAsync(int id, string tenantCode);
    Task ApproveAsync(int id, string approverId, string tenantCode);
    Task RejectAsync(int id, string reason, string tenantCode);
    Task MarkSentAsync(int id, string tenantCode);
    Task<string> GeneratePoNumberAsync(string tenantCode);

    // Receiving — calls InventoryService.ReceiveStockAsync
    Task ReceiveLinesAsync(int poId, List<ReceiveLineRequest> lines, string userId, string tenantCode);
}

public record ReceiveLineRequest(int LineId, decimal QuantityReceived, string LotNumber, int LocationId);
```

**Approval workflow rule**: configurable in `SystemSettings`:
- `"po_approval_required"` = "true/false"
- `"po_approval_threshold"` = 500.00 (require approval for POs above this amount)

### Step 8 — Create VendorService
**New File**: `Services/VendorService.cs`
**New File**: `Services/IVendorService.cs`

```csharp
public interface IVendorService
{
    Task<List<Vendor>> GetAllAsync(string tenantCode, bool approvedOnly = false);
    Task<Vendor?> GetByIdAsync(int id, string tenantCode);
    Task<Vendor> CreateAsync(Vendor vendor, string tenantCode);
    Task UpdateAsync(Vendor vendor, string tenantCode);
    Task ApproveAsync(int id, string tenantCode);
    Task SuspendAsync(int id, string reason, string tenantCode);

    // Vendor items (pricing)
    Task<List<VendorItem>> GetVendorItemsAsync(int vendorId, string tenantCode);
    Task<List<Vendor>> GetApprovedVendorsForItemAsync(int inventoryItemId, string tenantCode);

    // Scorecards
    Task<VendorScorecard> GenerateScorecardAsync(int vendorId, int year, int quarter, string tenantCode);
    Task<List<VendorScorecard>> GetScorecardHistoryAsync(int vendorId, string tenantCode);
}
```

### Step 9 — Vendor List Page
**New File**: `Components/Pages/Purchasing/Vendors.razor`
**Route**: `/purchasing/vendors`

UI requirements:
- Status filter: All | Approved | Pending | Suspended
- Table: Vendor Code, Name, Contact, Lead Time, Status badge, Last Scorecard Score
- "New Vendor" button
- Click row → vendor detail

### Step 10 — Purchase Order List Page
**New File**: `Components/Pages/Purchasing/Orders.razor`
**Route**: `/purchasing/orders`

UI requirements:
- Status filter tabs: All | Draft | Pending Approval | Approved | Sent | Receiving | Closed
- Table: PO#, Vendor, Total Amount, Expected Delivery, Status, Approval Status
- "New PO" button
- Click row → PO detail with lines

**PO Detail page** (`/purchasing/orders/{id:int}`):
- Header: PO number, vendor, dates, status, total
- Lines table: item, qty ordered, qty received, unit cost, line total
- Approval section: "Submit for Approval" / "Approve" / "Reject" buttons (role-based)
- "Receive" button → receiving workflow for this PO

### Step 11 — Receiving Workflow
**File**: `Components/Pages/Inventory/Receive.razor` (from Module 06)

Update to support PO-linked receiving:
- "Receive against PO" mode: lookup PO, auto-populates lines with quantities and items
- Override quantity per line (partial receiving)
- Lot number entry with "Generate Lot #" auto-button
- Location assignment
- "Post Receipt" → calls `PurchasingService.ReceiveLinesAsync` → updates PO status + inventory

### Step 12 — EF Core Migration
```bash
dotnet ef migrations add AddPurchasing --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Vendors can be created with contact info and approval status
- [ ] PO can be created with vendor, lines, and total calculated
- [ ] PO approval workflow routes to approver when configured
- [ ] Approved PO can be marked "Sent" to vendor
- [ ] Receiving a PO creates inventory transactions via InventoryService
- [ ] Partially received PO status = "Partially Received"; fully received = "Received"
- [ ] Vendor scorecard calculates on-time delivery and quality acceptance
- [ ] Approved vendor list filters are enforced on PO creation

---

## Dependencies

- **Module 06** (Inventory) — PO receipt calls `InventoryService.ReceiveStockAsync`
- **Module 05** (Quality) — Incoming material NCR feeds vendor quality scorecard
- **Module 09** (Job Costing) — PO costs linked to job requirements

---

## Future Enhancements (Post-MVP)

- Automated RFQ distribution: send RFQ email to multiple vendors for competitive pricing
- EDI integration with vendor systems
- Blanket PO with call-off releases
- Vendor portal: vendors view and acknowledge POs online
