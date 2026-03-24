# Module 17: CMMC & Cybersecurity Compliance

## Status: [ ] Not Started
## Category: QMS
## Phase: 3 — Platform Maturity
## Priority: P3 - Medium

---

## Overview

The Compliance Framework Engine supports CMMC (Cybersecurity Maturity Model
Certification), ITAR, AS9100, ISO 9001, ISO 13485, NADCAP, and custom regulatory
frameworks. It provides gap analysis tools, audit trail management, and a
compliance dashboard with remediation tracking.

**ProShop Improvements**: Flexible multi-framework engine (not just CMMC/ITAR),
complete data access audit trail, IP restriction support, MFA enforcement, role-based
access with granular permissions, compliance gap analysis dashboard, and
remediation task tracking.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| Cookie authentication with claims | ✅ Exists | `Program.cs` |
| Role-based authorization (`[Authorize(Roles="...")]`) | ✅ Exists | Multiple pages |
| `User.Role` enum (Operator through SuperAdmin) | ✅ Exists | `Models/User.cs` |
| Multi-tenant isolation (separate DBs) | ✅ Exists | `Data/TenantDbContext.cs` |

**Gap**: No compliance framework model, no audit access log, no MFA, no IP restrictions, no CMMC control tracking, no gap analysis tooling.

---

## What Needs to Be Built

### 1. Database Models (New)
- `ComplianceFramework` — registered framework (CMMC, AS9100, etc.)
- `ComplianceControl` — individual control/requirement
- `ComplianceSelfAssessment` — tenant's assessment of each control
- `AuditAccessLog` — every significant data access logged for audit

### 2. Service Layer (New)
- `ComplianceService` — framework management, gap analysis
- `AuditLogService` — records all security-relevant events

### 3. UI Components (New)
- **Compliance Dashboard** — framework status and gap analysis
- **Control Assessment** — answer each control requirement
- **Audit Log Viewer** — security event history
- **Access Control Admin** — user roles, IP restrictions, MFA setup

---

## Implementation Steps

### Step 1 — Create ComplianceFramework Model
**New File**: `Models/ComplianceFramework.cs`
```csharp
public class ComplianceFramework
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;          // "CMMC Level 2", "AS9100D"
    public string Version { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public ICollection<ComplianceControl> Controls { get; set; } = new List<ComplianceControl>();
}
```

### Step 2 — Create ComplianceControl Model
**New File**: `Models/ComplianceControl.cs`
```csharp
public class ComplianceControl
{
    public int Id { get; set; }
    public int FrameworkId { get; set; }
    public ComplianceFramework Framework { get; set; } = null!;
    public string ControlId { get; set; } = string.Empty;     // "CMMC-AC.1.001"
    public string Domain { get; set; } = string.Empty;        // "Access Control"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ControlSeverity Severity { get; set; }             // Low, Medium, High, Critical
    public string? GuidanceNotes { get; set; }
    public bool IsApplicable { get; set; } = true;
    public ICollection<ComplianceSelfAssessment> Assessments { get; set; } = new List<ComplianceSelfAssessment>();
}

public enum ControlSeverity { Low, Medium, High, Critical }
```

### Step 3 — Create ComplianceSelfAssessment Model
**New File**: `Models/ComplianceSelfAssessment.cs`
```csharp
public class ComplianceSelfAssessment
{
    public int Id { get; set; }
    public int ControlId { get; set; }
    public ComplianceControl Control { get; set; } = null!;
    public AssessmentStatus Status { get; set; } = AssessmentStatus.NotAssessed;
    public string? ImplementationNotes { get; set; }
    public string? EvidenceReference { get; set; }             // Link to document or procedure
    public int? LinkedDocumentId { get; set; }                 // ControlledDocument reference
    public string? RemediationPlan { get; set; }
    public DateTime? RemediationDueDate { get; set; }
    public string? AssessedByUserId { get; set; }
    public DateTime? AssessedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

public enum AssessmentStatus { NotAssessed, Compliant, PartiallyCompliant, NonCompliant, NotApplicable }
```

### Step 4 — Create AuditAccessLog Model
**New File**: `Models/AuditAccessLog.cs`
```csharp
public class AuditAccessLog
{
    public long Id { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string EventType { get; set; } = string.Empty;     // Login, Logout, DataAccess, DataModify, Export
    public string? EntityType { get; set; }                    // "WorkOrder", "Part", etc.
    public string? EntityId { get; set; }
    public string? Action { get; set; }                        // "View", "Edit", "Delete", "Export"
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool WasSuccessful { get; set; } = true;
    public string? FailureReason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

> Note: `AuditAccessLog` should be stored in the **platform.db** or a separate audit db,
> not tenant db, to prevent tampering by tenant admins.

### Step 5 — Register DbSets
**File**: `Data/TenantDbContext.cs`
```csharp
public DbSet<ComplianceFramework> ComplianceFrameworks { get; set; }
public DbSet<ComplianceControl> ComplianceControls { get; set; }
public DbSet<ComplianceSelfAssessment> ComplianceSelfAssessments { get; set; }
```

**File**: `Data/PlatformDbContext.cs`
```csharp
public DbSet<AuditAccessLog> AuditAccessLogs { get; set; }
```

### Step 6 — Create AuditLogService
**New File**: `Services/AuditLogService.cs`
**New File**: `Services/IAuditLogService.cs`

```csharp
public interface IAuditLogService
{
    Task LogAsync(string tenantCode, string? userId, string eventType, string? entityType,
                  string? entityId, string? action, bool success, string? ipAddress);
    Task<List<AuditAccessLog>> QueryAsync(string tenantCode, DateTime from, DateTime to,
                                           string? userId = null, string? eventType = null);
}
```

Wire into key application events:
- Login/logout in `AuthService`
- Document access in `DocumentControlService`
- Data export in `ReportingService`
- Admin CRUD operations via service interceptor pattern

### Step 7 — Create ComplianceService
**New File**: `Services/ComplianceService.cs`
**New File**: `Services/IComplianceService.cs`

```csharp
public interface IComplianceService
{
    Task<List<ComplianceFramework>> GetFrameworksAsync(string tenantCode);
    Task<ComplianceGapSummary> GetGapSummaryAsync(int frameworkId, string tenantCode);
    Task<List<ComplianceSelfAssessment>> GetAssessmentsAsync(int frameworkId, string tenantCode);
    Task SaveAssessmentAsync(ComplianceSelfAssessment assessment, string tenantCode);
    Task SeedFrameworkAsync(string frameworkName, string tenantCode);  // Load pre-built control sets
}

public record ComplianceGapSummary(
    int TotalControls,
    int Compliant,
    int PartiallyCompliant,
    int NonCompliant,
    int NotAssessed,
    decimal CompliancePct
);
```

**Pre-seed framework data**: Include a static seed class with CMMC Level 1 and Level 2 control definitions (NIST SP 800-171 domains). Store as a C# embedded resource JSON file.

### Step 8 — Compliance Dashboard Page
**New File**: `Components/Pages/Compliance/Dashboard.razor`
**Route**: `/compliance`

UI requirements:
- Framework selector (tabs: CMMC L2 | AS9100 | ISO 9001 | Custom)
- **Gap Analysis Donut Chart**: Compliant | Partial | Non-Compliant | Not Assessed
- **Compliance % score** (large number, green/yellow/red)
- **Controls by Domain** bar chart: compliance rate per domain
- **Remediation Items**: non-compliant controls sorted by severity
- "Start Assessment" button if not yet assessed

### Step 9 — Control Assessment Page
**New File**: `Components/Pages/Compliance/Assessment.razor`
**Route**: `/compliance/assess/{frameworkId:int}`

UI requirements:
- Sidebar: domain list with completion indicators
- Main panel: controls list for selected domain
- Each control: title, description, guidance text
- Assessment input: radio buttons (Compliant / Partial / Non-Compliant / N/A)
- Notes field + evidence reference field
- Link to controlled document (Module 14 integration)
- Remediation plan + due date (when Partial/Non-Compliant selected)
- "Save" per control (auto-saves with debounce)
- Progress bar per domain showing assessment completion

### Step 10 — Audit Log Viewer
**New File**: `Components/Pages/Admin/AuditLog.razor`
**Route**: `/admin/audit-log`

UI requirements (Admin/SuperAdmin only):
- Date range filter + user filter + event type filter
- Table: Timestamp, User, Event Type, Entity, Action, IP Address, Success/Fail
- Export to CSV (for compliance evidence)
- Failed login attempts highlighted in red

### Step 11 — Security Settings Page
**New File**: `Components/Pages/Admin/SecuritySettings.razor`
**Route**: `/admin/security`

UI requirements:
- **Session Settings**: session timeout duration
- **Password Policy**: minimum length, complexity requirements, expiry days
- **IP Allowlist**: add/remove allowed IP ranges for admin access
- **MFA Settings**: enable MFA requirement per role (future implementation hook)
- **Data Retention**: configure how long audit logs are retained

Store settings in `SystemSetting` key-value table under category "Security".

### Step 12 — EF Core Migrations
```bash
# Tenant DB migration
dotnet ef migrations add AddComplianceFramework --context TenantDbContext
# Platform DB migration
dotnet ef migrations add AddAuditLog --context PlatformDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] CMMC Level 2 framework pre-loaded with control definitions
- [ ] Each control can be assessed as Compliant/Partial/Non-Compliant
- [ ] Gap analysis chart shows compliance percentage by domain
- [ ] Non-compliant controls show remediation plan and due date
- [ ] Audit access log records login, logout, and data export events
- [ ] Audit log is queryable by date, user, and event type
- [ ] Audit log exportable as CSV for compliance evidence
- [ ] Security settings configurable for session timeout and IP restrictions

---

## Dependencies

- **Module 14** (Document Control) — Evidence documents linked to controls
- **Module 05** (Quality) — Quality procedures linked to AS9100 controls

---

## Future Enhancements (Post-MVP)

- CMMC C3PAO assessment export format
- ITAR data classification tagging on records (mark CUI data)
- IP restriction enforcement at middleware level
- MFA via TOTP (authenticator app) integration
- Automated CMMC evidence package generation (zip all linked docs + audit logs)
