# Module 11: Calibration & Preventative Maintenance (CMMS)

## Status: [ ] Not Started
## Category: QMS
## Phase: 2 — Operational Depth
## Priority: P2 - High

---

## Overview

The CMMS (Computerized Maintenance Management System) covers both equipment
calibration and preventive/corrective maintenance. Calibration manages gages and
inspection equipment with certificate tracking and automatic expiry alerts.
Preventive Maintenance manages machine health with rules-based scheduling.

**ProShop Improvements**: IoT integration hooks for machine health monitoring
(vibration, temperature, power draw), usage-based maintenance scheduling (not
just calendar-based), operator-submitted maintenance requests with photo
attachments, and calibration certificate management.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `MachineComponent` model | ✅ Exists | `Models/Maintenance/MachineComponent.cs` |
| `MaintenanceRule` model (time/build/date triggers) | ✅ Exists | `Models/Maintenance/MaintenanceRule.cs` |
| `MaintenanceWorkOrder` model | ✅ Exists | `Models/Maintenance/MaintenanceWorkOrder.cs` |
| `MaintenanceActionLog` model | ✅ Exists | `Models/Maintenance/MaintenanceActionLog.cs` |
| `MaintenanceTriggerType`, `MaintenanceSeverity` enums | ✅ Exists | `Models/Enums/ManufacturingEnums.cs` |
| `MaintenanceService` / `IMaintenanceService` (partial) | ✅ Exists | `Services/MaintenanceService.cs` |
| `/maintenance` dashboard page | ✅ Exists | `Components/Pages/Maintenance/` |

**Gap**: No calibration management (separate from machine maintenance), no operator request submission with photos, no certificate attachment, no IoT hook model.

---

## What Needs to Be Built

### 1. Database Models (New)
- `GageEquipment` — calibrated measurement equipment registry
- `CalibrationRecord` — calibration event with certificate file
- `MaintenanceRequest` — operator-submitted ad-hoc maintenance request

### 2. Service Layer (Enhance)
- Complete `MaintenanceService` with full workflow
- `CalibrationService` — gage management, expiry alerts, certificate storage

### 3. UI Components (New/Enhance)
- **Maintenance Dashboard** (complete existing stub)
- **Maintenance Work Order Detail** — full workflow view
- **Operator Request Form** — mobile-friendly photo upload
- **Calibration Registry** — gage list with expiry status
- **Calibration Record Detail** — certificate attachment and history

---

## Implementation Steps

### Step 1 — Create GageEquipment Model
**New File**: `Models/GageEquipment.cs`
```csharp
public class GageEquipment
{
    public int Id { get; set; }
    public string GageId { get; set; } = string.Empty;         // e.g., "GAGE-001"
    public string Name { get; set; } = string.Empty;           // e.g., "Digital Micrometer 0-1\""
    public string Manufacturer { get; set; } = string.Empty;
    public string? ModelNumber { get; set; }
    public string? SerialNumber { get; set; }
    public GageType GageType { get; set; }
    public string? CalibrationStandard { get; set; }           // e.g., "ANSI/MSA Z540"
    public int CalibrationIntervalDays { get; set; } = 365;
    public DateTime? LastCalibratedAt { get; set; }
    public DateTime? NextCalibrationDue { get; set; }
    public CalibrationStatus Status { get; set; } = CalibrationStatus.DueForCalibration;
    public string? CurrentLocationId { get; set; }
    public string? AssignedToUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<CalibrationRecord> CalibrationHistory { get; set; } = new List<CalibrationRecord>();
}

public enum GageType
{
    Micrometer, Caliper, HeightGage, DialIndicator, BoreGage, CMM,
    SurfaceRoughness, HardnessTester, TorqueWrench, Thermometer, Other
}

public enum CalibrationStatus { Current, DueSoon, Overdue, OutOfService, DueForCalibration }
```

### Step 2 — Create CalibrationRecord Model
**New File**: `Models/CalibrationRecord.cs`
```csharp
public class CalibrationRecord
{
    public int Id { get; set; }
    public int GageEquipmentId { get; set; }
    public GageEquipment GageEquipment { get; set; } = null!;
    public DateTime CalibratedAt { get; set; }
    public DateTime NextDueAt { get; set; }
    public string? CalibratedByLab { get; set; }               // External lab name
    public string? CertificateNumber { get; set; }
    public string? CertificateFileUrl { get; set; }            // Path to uploaded cert
    public CalibrationOutcome Outcome { get; set; }            // Pass, Fail, AsFound
    public string? Notes { get; set; }
    public string? PerformedByUserId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public enum CalibrationOutcome { Pass, Fail, AsFoundAdjusted, OutOfTolerance }
```

### Step 3 — Create MaintenanceRequest Model
**New File**: `Models/MaintenanceRequest.cs`
```csharp
public class MaintenanceRequest
{
    public int Id { get; set; }
    public int MachineId { get; set; }
    public Machine Machine { get; set; } = null!;
    public string SubmittedByUserId { get; set; } = string.Empty;
    public MaintenanceRequestType RequestType { get; set; }    // Corrective, Urgent, Observation
    public string Description { get; set; } = string.Empty;
    public string? PhotoFileUrl { get; set; }                  // Operator-uploaded photo
    public RequestStatus Status { get; set; } = RequestStatus.New;
    public int? ConvertedToWorkOrderId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
}

public enum MaintenanceRequestType { Corrective, Urgent, Observation, PartRequest }
public enum RequestStatus { New, Acknowledged, WorkOrderCreated, Resolved, Declined }
```

### Step 4 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<GageEquipment> GageEquipment { get; set; }
public DbSet<CalibrationRecord> CalibrationRecords { get; set; }
public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
```

### Step 5 — Complete MaintenanceService
**File**: `Services/MaintenanceService.cs`

Implement all methods:
```csharp
// Maintenance work orders
Task<List<MaintenanceWorkOrder>> GetOpenWorkOrdersAsync(string tenantCode);
Task<MaintenanceWorkOrder?> GetWorkOrderByIdAsync(int id, string tenantCode);
Task<MaintenanceWorkOrder> CreateWorkOrderAsync(MaintenanceWorkOrder wo, string tenantCode);
Task UpdateWorkOrderAsync(MaintenanceWorkOrder wo, string tenantCode);
Task CompleteWorkOrderAsync(int id, string notes, string technicianId, string tenantCode);
Task AssignTechnicianAsync(int workOrderId, string technicianId, string tenantCode);

// Maintenance requests
Task<MaintenanceRequest> SubmitRequestAsync(MaintenanceRequest request, string tenantCode);
Task<List<MaintenanceRequest>> GetPendingRequestsAsync(string tenantCode);
Task AcknowledgeRequestAsync(int requestId, string tenantCode);
Task<MaintenanceWorkOrder> ConvertRequestToWorkOrderAsync(int requestId, string tenantCode);

// Scheduled maintenance
Task<List<MaintenanceWorkOrder>> GetDueMaintenanceAsync(string tenantCode);
Task CheckAndGenerateScheduledWorkOrdersAsync(string tenantCode);  // Called by background service

// Machine components
Task<List<MachineComponent>> GetComponentsAsync(int machineId, string tenantCode);
Task LogUsageAsync(int machineId, decimal hoursRun, int? buildsCompleted, string tenantCode);
```

**Auto-generation logic** in `CheckAndGenerateScheduledWorkOrdersAsync`:
- For each active `MaintenanceRule`:
  - `DateInterval`: if `LastServiceDate + IntervalDays <= today` → create WO
  - `HoursRun`: if `Component.TotalHoursRun - Component.LastServiceHours >= Rule.HoursThreshold` → create WO
  - `BuildsCompleted`: if builds since last service >= threshold → create WO
- Avoid duplicate WO creation: check for existing open WO with same rule

### Step 6 — Create CalibrationService
**New File**: `Services/CalibrationService.cs`
**New File**: `Services/ICalibrationService.cs`

```csharp
public interface ICalibrationService
{
    Task<List<GageEquipment>> GetAllGagesAsync(string tenantCode);
    Task<List<GageEquipment>> GetOverdueOrDueSoonAsync(int daysForecast, string tenantCode);
    Task<GageEquipment> CreateGageAsync(GageEquipment gage, string tenantCode);
    Task UpdateGageAsync(GageEquipment gage, string tenantCode);
    Task<CalibrationRecord> RecordCalibrationAsync(CalibrationRecord record, IBrowserFile? cert, string tenantCode);
    Task<List<CalibrationRecord>> GetHistoryAsync(int gageId, string tenantCode);
    Task UpdateCalibrationStatusesAsync(string tenantCode);  // Recalculate Due/Overdue status
}
```

**Status update logic** in `UpdateCalibrationStatusesAsync`:
- For each gage: if `NextCalibrationDue < today` → `Overdue`
- If `NextCalibrationDue < today + 30` → `DueSoon`
- Else → `Current`

### Step 7 — Maintenance Dashboard (Complete Stub)
**File**: `Components/Pages/Maintenance/Dashboard.razor`

UI requirements:
- KPI row: Open Work Orders, Overdue PM, Gages Due for Calibration, Pending Requests
- **Overdue Maintenance** table: Machine, Component, Rule name, Days Overdue, Severity
- **Work Order Pipeline**: count by status (Open, Assigned, In Progress, Waiting for Parts)
- **Upcoming PM** table: next 30 days of scheduled maintenance
- **Operator Requests** table: new unacknowledged requests with machine and description
- Quick action: "Submit Request" button (accessible to all operators)

### Step 8 — Maintenance Work Order Detail
**File**: `Components/Pages/Maintenance/WorkOrders.razor` (complete existing)

Add work order detail modal/page:
- Header: machine, component, type, severity badge, status, priority
- Description, notes from technician
- "Assign to Me" button
- Start / Complete workflow buttons
- Time entry: actual hours, labor type
- Parts used: list with inventory item references (links to Module 06)
- Photo attachments section
- Action log history at bottom

### Step 9 — Operator Maintenance Request Form
**New File**: `Components/Pages/Maintenance/SubmitRequest.razor`
**Route**: `/maintenance/request`

UI requirements (mobile-optimized for shop floor tablet):
- Machine selector (large dropdown or list)
- Request type selector: Corrective | Urgent | Observation
- Description textarea (large touch-friendly)
- **Photo Upload**: "Take Photo" button using camera API or file picker
- Submit button
- Confirmation screen with request number

### Step 10 — Calibration Registry Page
**New File**: `Components/Pages/Maintenance/Calibration.razor`
**Route**: `/maintenance/calibration`

UI requirements:
- Status filter tabs: All | Current | Due Soon | Overdue | Out of Service
- Table: Gage ID, Name, Type, Location, Last Calibrated, Next Due, Status badge
- Status badges: green (Current), yellow (Due Soon ≤30 days), red (Overdue)
- "Record Calibration" button per row
- "New Gage" button

**Calibration Record Modal**:
- Date picker for calibration date
- Interval input (auto-calculates next due)
- Lab name, certificate number
- Outcome radio buttons
- Certificate file upload
- Notes

### Step 11 — Wire Background Service
**File**: `Services/MachineSyncService.cs` (existing background service)

Add to the existing periodic sync logic:
```csharp
// Periodically check and generate scheduled maintenance work orders
await _maintenanceService.CheckAndGenerateScheduledWorkOrdersAsync(tenant.Code);
// Periodically update calibration statuses
await _calibrationService.UpdateCalibrationStatusesAsync(tenant.Code);
```

Schedule: run `CheckAndGenerateScheduledWorkOrdersAsync` once per day at a configured time.

### Step 12 — EF Core Migration
```bash
dotnet ef migrations add AddCalibrationAndMaintenanceEnhancements --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Gages can be registered with calibration interval and status tracking
- [ ] Calibration record can be created with certificate file upload
- [ ] Gage status auto-updates to Due Soon / Overdue based on date
- [ ] Operator can submit maintenance request with description and photo
- [ ] Request can be acknowledged and converted to a maintenance work order
- [ ] Scheduled PM rules auto-generate work orders when trigger conditions are met
- [ ] PM work orders appear in maintenance dashboard with due date
- [ ] Completing a work order records technician, actual hours, and action log
- [ ] Overdue calibrations show on quality dashboard as alerts

---

## Dependencies

- **Module 04** (Shop Floor) — Machine usage hours update maintenance counters
- **Module 05** (Quality) — Gage calibration status checked during inspection
- **Module 06** (Inventory) — Parts used in maintenance consumed from inventory
- **Module 09** (Job Costing) — Maintenance labor costs tracked

---

## Future Enhancements (Post-MVP)

- IoT sensor integration: OPC-UA/MTConnect for real-time vibration, temperature, power readings
- Predictive maintenance: ML model on sensor data to predict failure before it occurs
- Integration with external calibration lab scheduling systems
- QR code on each gage: scan to view calibration status and history instantly
