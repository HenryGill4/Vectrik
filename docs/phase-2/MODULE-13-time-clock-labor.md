# Module 13: Time Clock & Labor Tracking

## Status: [ ] Not Started
## Category: MES
## Phase: 2 — Operational Depth
## Priority: P2 - High

---

## Overview

Time Clock & Labor Tracking captures operator time at the job/operation level for
accurate job costing and payroll integration. OEE dashboards give management
visibility into how efficiently labor is deployed. Skill-based assignment
recommendations ensure the right operator is on the right job.

**ProShop Improvements**: Tablet kiosk mode with barcode scan-on/scan-off, mobile
support, OEE per machine and operator, automatic overtime calculation, labor
utilization reporting, and skill-based operator assignment recommendations.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `StageExecution.ActualStartAt`, `ActualEndAt` | ✅ M04 | `Models/StageExecution.cs` |
| `StageExecution.AssignedOperatorId` | ✅ M04 | `Models/StageExecution.cs` |
| `User.Role`, `User.Department` | ✅ Exists | `Models/User.cs` |
| `User.AssignedStageIds` (stages operator is qualified for) | ✅ Exists | `Models/User.cs` |

**Gap**: No dedicated `TimeEntry` model (only stage-level timestamps), no kiosk mode, no OEE calculation, no skill matrix model, no payroll export.

---

## What Needs to Be Built

### 1. Database Models (New)
- `TimeEntry` — discrete clock-in/clock-out records per operator per job/stage
- `OperatorSkill` — certified skill levels per operator
- `ShiftDefinition` — shift times and days for payroll/OEE calculation

### 2. Service Layer (New)
- `TimeClockService` — clock-in/out, break management, time entry queries
- `LaborAnalyticsService` — utilization, OEE by operator, overtime calculation

### 3. UI Components (New)
- **Kiosk Mode** (`/kiosk`) — large-button tablet interface for clock-in/out
- **Time Entry List** — manager view of all time entries
- **Labor Utilization Dashboard** — per-operator and per-machine metrics
- **Skill Matrix** — operator skills and certifications grid

---

## Implementation Steps

### Step 1 — Create TimeEntry Model
**New File**: `Models/TimeEntry.cs`
```csharp
public class TimeEntry
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public TimeEntryType EntryType { get; set; }           // ProductiveWork, Setup, Rework, Break, Meeting, Indirect
    public int? JobId { get; set; }
    public Job? Job { get; set; }
    public int? StageExecutionId { get; set; }
    public StageExecution? StageExecution { get; set; }
    public int? MachineId { get; set; }
    public Machine? Machine { get; set; }
    public DateTime ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }
    public decimal? TotalMinutes { get; set; }              // Calculated on clock-out
    public bool IsOvertime { get; set; } = false;           // Flagged by OT calculation
    public bool IsApproved { get; set; } = false;           // Supervisor approval
    public string? ApprovedByUserId { get; set; }
    public string? Notes { get; set; }
    public TimeEntrySource Source { get; set; } = TimeEntrySource.Kiosk;
}

public enum TimeEntryType { ProductiveWork, Setup, Rework, Break, Meeting, Indirect, Training }
public enum TimeEntrySource { Kiosk, Barcode, Manual, StageCompletion }
```

### Step 2 — Create OperatorSkill Model
**New File**: `Models/OperatorSkill.cs`
```csharp
public class OperatorSkill
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public int ProductionStageId { get; set; }
    public ProductionStage ProductionStage { get; set; } = null!;
    public SkillLevel Level { get; set; }                  // Trainee, Competent, Proficient, Expert
    public DateTime? CertifiedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? CertifiedByUserId { get; set; }
    public string? Notes { get; set; }
}

public enum SkillLevel { Trainee, Competent, Proficient, Expert }
```

### Step 3 — Create ShiftDefinition Model
**New File**: `Models/ShiftDefinition.cs`
```csharp
public class ShiftDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // "Day Shift", "Night Shift"
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal ScheduledHours { get; set; }
    public bool[] ActiveDays { get; set; } = new bool[7];   // Mon-Sun
    public decimal OvertimeAfterHours { get; set; } = 8.0m; // OT kicks in after X hours
    public bool IsDefault { get; set; } = false;
}
```

### Step 4 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<TimeEntry> TimeEntries { get; set; }
public DbSet<OperatorSkill> OperatorSkills { get; set; }
public DbSet<ShiftDefinition> ShiftDefinitions { get; set; }
```

### Step 5 — Create TimeClockService
**New File**: `Services/TimeClockService.cs`
**New File**: `Services/ITimeClockService.cs`

```csharp
public interface ITimeClockService
{
    // Clock operations
    Task<TimeEntry> ClockInAsync(string userId, TimeEntryType type, int? jobId,
                                  int? stageExecutionId, int? machineId, string tenantCode);
    Task<TimeEntry> ClockOutAsync(string userId, string? notes, string tenantCode);
    Task<TimeEntry?> GetCurrentEntryAsync(string userId, string tenantCode);
    Task<bool> IsCurrentlyClockedInAsync(string userId, string tenantCode);

    // Queries
    Task<List<TimeEntry>> GetEntriesForUserAsync(string userId, DateTime from, DateTime to, string tenantCode);
    Task<List<TimeEntry>> GetEntriesForJobAsync(int jobId, string tenantCode);
    Task<decimal> GetTotalHoursForJobAsync(int jobId, string tenantCode);

    // Approval
    Task<List<TimeEntry>> GetPendingApprovalAsync(string supervisorUserId, string tenantCode);
    Task ApproveEntryAsync(int entryId, string approverId, string tenantCode);
    Task<List<TimeEntry>> GetDailyEntriesAsync(DateTime date, string tenantCode);  // for manager view
}
```

**Business rules**:
- `ClockInAsync`: check if user is already clocked in → throw if so (prevent double entry)
- `ClockOutAsync`: set `ClockOutAt`, calculate `TotalMinutes`, flag overtime if hours > shift OT threshold
- Source = `Barcode` when triggered via barcode scan JS interop

### Step 6 — Create LaborAnalyticsService
**New File**: `Services/LaborAnalyticsService.cs`
**New File**: `Services/ILaborAnalyticsService.cs`

```csharp
public interface ILaborAnalyticsService
{
    Task<List<OperatorUtilizationData>> GetUtilizationAsync(DateTime from, DateTime to, string tenantCode);
    Task<decimal> GetOverallLaborEfficiencyAsync(DateTime from, DateTime to, string tenantCode);
    Task<List<OperatorSkillGap>> GetSkillGapsAsync(string tenantCode);
    Task<List<TimeEntry>> GetOvertimeEntriesAsync(DateTime from, DateTime to, string tenantCode);
}

public record OperatorUtilizationData(
    string UserId,
    string OperatorName,
    decimal TotalScheduledHours,
    decimal TotalProductiveHours,
    decimal UtilizationPct,
    decimal OvertimeHours
);
```

### Step 7 — Kiosk Mode Page
**New File**: `Components/Pages/ShopFloor/Kiosk.razor`
**Route**: `/kiosk`

UI requirements (tablet-optimized — large touch targets, minimal clutter):
- **Clock-In Screen** (when not clocked in):
  - Operator name dropdown (or barcode scan badge)
  - Job selector: searchable dropdown or barcode scan job ticket
  - Activity type: large buttons (Work | Setup | Indirect | Break)
  - Machine selector (optional)
  - Large green "CLOCK IN" button
- **Clock-Out Screen** (when clocked in):
  - Shows current job, elapsed time (live timer)
  - Optional notes textarea
  - Large red "CLOCK OUT" button
  - Quick switch: "Switch to Different Job" (clocks out + in atomically)
- **Dark mode**: always use dark mode on kiosk (better for bright shop floors)
- **Auto-refresh**: keep page alive on tablet (prevent timeout/sleep)

Barcode scanning support:
- Button "Scan Badge" → activates camera barcode scanner via `BarcodeScanner.js`
- Button "Scan Job Ticket" → scans job barcode, auto-fills job field

### Step 8 — Time Entries Manager View
**New File**: `Components/Pages/Labor/TimeEntries.razor`
**Route**: `/labor/entries`

UI requirements:
- Date range filter + operator filter
- Table: Operator, Job#, Stage, Type, Clock In, Clock Out, Total Hours, Overtime flag, Approved
- Approve button per row (supervisor role)
- "Approve All" for a date
- Export to CSV for payroll

### Step 9 — Labor Utilization Dashboard
**New File**: `Components/Pages/Analytics/LaborDashboard.razor`
**Route**: `/analytics/labor`

UI requirements:
- Date range selector
- Bar chart: utilization % per operator (last 2 weeks)
- Table: Operator, Scheduled Hrs, Productive Hrs, Utilization %, Overtime Hrs, Efficiency %
- OT alerts: operators with high overtime flagged in orange
- Bottom section: Skill gaps table (stages where no qualified operators available)

### Step 10 — Skill Matrix Admin Page
**New File**: `Components/Pages/Admin/SkillMatrix.razor`
**Route**: `/admin/skills`

UI requirements:
- Grid layout: rows = operators, columns = production stages
- Each cell: skill level badge (Trainee/Competent/Proficient/Expert) or empty (not qualified)
- Click cell → edit skill record (level, cert date, expiry)
- Color coding: red = expired, orange = expiring soon, green = current
- "Add Skill" button per operator row

### Step 11 — Integrate Time Entries with Job Costing
**File**: `Services/TimeClockService.cs`

On `ClockOutAsync`, after saving time entry:
```csharp
// Notify job costing service of new labor entry
if (entry.JobId.HasValue)
{
    var laborRate = await GetLaborRateForUserAsync(entry.UserId, tenantCode);
    await _jobCostingService.RecordLaborCostAsync(
        entry.JobId.Value,
        entry.StageExecutionId,
        (entry.TotalMinutes ?? 0) / 60,
        entry.UserId,
        tenantCode
    );
}
```

### Step 12 — EF Core Migration
```bash
dotnet ef migrations add AddTimeClock --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Operator can clock in on kiosk with job selection
- [ ] Clock-in from kiosk shows live elapsed timer
- [ ] Clock-out records time entry with total minutes calculated
- [ ] Overtime flagged when daily hours exceed configured threshold
- [ ] Manager can view all entries for a date and approve
- [ ] Time entries feed into job costing automatically on clock-out
- [ ] Skill matrix shows all operators' qualifications per stage
- [ ] Skill certifications have expiry dates and show expiry alerts
- [ ] Labor utilization dashboard shows productive vs. scheduled hours
- [ ] Barcode scan-in works on kiosk page via camera

---

## Dependencies

- **Module 04** (Shop Floor) — Kiosk ties to stage execution clock-in
- **Module 09** (Job Costing) — Labor costs recorded from time entries
- **Module 07** (Analytics) — Labor utilization feeds workforce KPIs
- **Module 18** (Training/LMS) — Skills managed and referenced here

---

## Future Enhancements (Post-MVP)

- Payroll export format support (ADP, Paychex, Gusto CSV formats)
- Biometric time clock integration (fingerprint reader via USB)
- Automatic clock-out detection based on machine state (machine stops → prompt to clock out)
- Photo capture on clock-in for identity verification
