# Machine Scheduling Rules System

## Problem
Builds can currently be scheduled over weekends/holidays when no operator is available for changeover, causing the cooldown chamber to overflow and machines to go DOWN. The scheduling engine flags this as downtime but doesn't **block** it.

## Solution
A per-machine rule system with hard-block enforcement. Three rule types from day one, plus a shared blackout calendar.

---

## Data Model

### New Entities

#### 1. `MachineSchedulingRule` (per-machine rules)
```
Models/MachineSchedulingRule.cs
```
| Property | Type | Description |
|----------|------|-------------|
| Id | int | PK |
| MachineId | int | FK → Machine |
| RuleType | SchedulingRuleType | enum (see below) |
| Name | string (max 200) | Human-readable label |
| Description | string? (max 500) | Optional explanation |
| IsEnabled | bool | Toggle on/off without deleting |
| **RequireOperatorForChangeover params:** | | |
| — (no extra fields, uses shift data) | | Blocks if changeover falls outside all operator shift windows |
| **MaxConsecutiveBuilds params:** | | |
| MaxConsecutiveBuilds | int? | Max builds before forced break |
| MinBreakHours | double? | Minimum downtime between run and next build |
| **BlackoutPeriod params:** | | |
| — (uses MachineBlackoutAssignment) | | Links to shared BlackoutPeriod records |
| CreatedDate | DateTime | Audit |
| LastModifiedDate | DateTime | Audit |
| CreatedBy | string | Audit |
| LastModifiedBy | string | Audit |

#### 2. `BlackoutPeriod` (shared company-wide calendar)
```
Models/BlackoutPeriod.cs
```
| Property | Type | Description |
|----------|------|-------------|
| Id | int | PK |
| Name | string (max 200) | e.g. "Easter Weekend 2026" |
| StartDate | DateTime | Blackout start (inclusive) |
| EndDate | DateTime | Blackout end (inclusive) |
| Reason | string? (max 500) | Why this blackout exists |
| IsRecurringAnnually | bool | Auto-repeat each year |
| IsActive | bool | Soft toggle |
| CreatedDate | DateTime | Audit |
| CreatedBy | string | Audit |

#### 3. `MachineBlackoutAssignment` (many-to-many: Machine ↔ BlackoutPeriod)
```
Models/MachineBlackoutAssignment.cs
```
| Property | Type | Description |
|----------|------|-------------|
| MachineId | int | FK → Machine |
| BlackoutPeriodId | int | FK → BlackoutPeriod |

### New Enum
```csharp
// In ManufacturingEnums.cs
public enum SchedulingRuleType
{
    RequireOperatorForChangeover,  // Block if no operator shift covers changeover window
    MaxConsecutiveBuilds,          // Limit back-to-back builds without maintenance/inspection gap
    BlackoutPeriod                 // Block scheduling during specific date ranges
}
```

### Navigation Properties
- `Machine` gets: `ICollection<MachineSchedulingRule> SchedulingRules`
- `Machine` gets: `ICollection<MachineBlackoutAssignment> BlackoutAssignments`
- `BlackoutPeriod` gets: `ICollection<MachineBlackoutAssignment> MachineAssignments`

---

## Scheduling Enforcement (Hard Block)

### Where: `ProgramSchedulingService.FindEarliestSlotAsync()`

After the current slot-finding logic (line ~1084), add a **rule validation pass** before returning the slot:

```
1. Load all enabled MachineSchedulingRules for this machine
2. For each rule, evaluate:

   RequireOperatorForChangeover:
   - If changeover window (candidateEnd → changeoverEnd) falls outside ALL shift windows → SKIP this slot
   - Advance candidateStart to next shift start + build duration and re-check
   - Keep advancing until a valid slot is found or a max search horizon (14 days) is hit

   MaxConsecutiveBuilds:
   - Count consecutive builds on this machine (from blocks list)
   - If count >= MaxConsecutiveBuilds, insert a forced gap of MinBreakHours before this slot

   BlackoutPeriod:
   - Check if the build window (candidateStart → changeoverEnd) overlaps any active blackout for this machine
   - If yes → advance candidateStart past the blackout end and re-find

3. Return the first slot that satisfies ALL rules
```

### New Return Fields on `ProgramScheduleSlot`
Add optional fields:
- `List<string>? BlockedReasons` — why earlier slots were skipped (for UI display)
- `DateTime? OriginalEarliestStart` — what the slot would have been without rules (shows the cost of rules)

### New Service: `ISchedulingRuleService` / `SchedulingRuleService`
```
Services/ISchedulingRuleService.cs
Services/SchedulingRuleService.cs
```
- CRUD for MachineSchedulingRule
- CRUD for BlackoutPeriod
- `GetMachineBlackoutsAsync(machineId)` — returns active blackouts for a machine
- `ValidateSlotAgainstRulesAsync(machineId, slot)` — returns pass/fail + reasons
- `GetEnabledRulesForMachineAsync(machineId)` — returns active rules

---

## UI: Machine Detail Page — New "Rules" Tab

### Tab Addition
Add a 6th tab to `Detail.razor` tab bar:
```razor
<button class="tab @(_tab == "rules" ? "active" : "")" @onclick="SelectRulesTab">
    Rules (@(_schedulingRules?.Count(r => r.IsEnabled) ?? 0))
</button>
```

### Rules Tab Content (3 sections)

#### Section 1: Scheduling Rules
Card listing all `MachineSchedulingRule` records for this machine. Each rule shows:
- Toggle switch (IsEnabled)
- Rule type badge
- Name and description
- Type-specific parameters (e.g., MaxConsecutiveBuilds count, MinBreakHours)
- Edit/Delete buttons

"+ Add Rule" button opens an inline form:
- Rule type dropdown (the 3 types)
- Dynamic parameter fields based on type
- Name auto-generated from type but editable

#### Section 2: Blackout Calendar
Card showing assigned blackout periods for this machine:
- Table: Name, Start, End, Recurring?, Active toggle
- "Manage Blackout Calendar" link → opens a modal to CRUD shared BlackoutPeriod records
- Checkboxes to assign/unassign blackout periods to this machine

#### Section 3: Rule Summary
Read-only summary card:
- "Next changeover requires operator: ✅ Enforced" / "❌ Not configured"
- "Max consecutive builds: 5 (2hr break)"  / "Not configured"
- "Active blackouts: 2 upcoming"

---

## Migration & Seed Data

### EF Core Migration
```bash
dotnet ef migrations add AddSchedulingRules --context TenantDbContext --output-dir Data/Migrations/Tenant
```

### Seed Data (DataSeedingService)
For the two EOS M4 machines, auto-create:
- `RequireOperatorForChangeover` rule (enabled by default) — **this is the fix for the weekend problem**
- `MaxConsecutiveBuilds` rule (disabled, MaxConsecutiveBuilds=10, MinBreakHours=2) — template for user
- No blackout periods seeded (user-configured)

### DbContext Registration
- Add `DbSet<MachineSchedulingRule>`, `DbSet<BlackoutPeriod>`, `DbSet<MachineBlackoutAssignment>`
- Configure composite key on `MachineBlackoutAssignment`
- Configure FK relationships in `OnModelCreating`

---

## Files to Create/Modify

### New Files (8)
1. `Models/MachineSchedulingRule.cs` — Entity
2. `Models/BlackoutPeriod.cs` — Entity
3. `Models/MachineBlackoutAssignment.cs` — Join entity
4. `Services/ISchedulingRuleService.cs` — Interface
5. `Services/SchedulingRuleService.cs` — Implementation
6. `Components/Pages/Machines/Modals/BlackoutCalendarModal.razor` — Shared blackout CRUD modal
7. `Data/Migrations/Tenant/[timestamp]_AddSchedulingRules.cs` — Migration (auto-generated)
8. `Data/Migrations/Tenant/[timestamp]_AddSchedulingRulesDesigner.cs` — Designer (auto-generated)

### Modified Files (7)
1. `Models/Enums/ManufacturingEnums.cs` — Add `SchedulingRuleType` enum
2. `Models/Machine.cs` — Add navigation properties
3. `Data/TenantDbContext.cs` — Add DbSets + OnModelCreating config
4. `Services/ProgramSchedulingService.cs` — Enforce rules in `FindEarliestSlotAsync()`
5. `Components/Pages/Machines/Detail.razor` — Add Rules tab + UI
6. `Services/DataSeedingService.cs` — Seed default rules for M4 machines
7. `Program.cs` — Register `ISchedulingRuleService`
8. `wwwroot/css/site.css` — Styles for rules tab (`.machine-rule-*`, `.blackout-*`)

---

## Implementation Order

1. **Models + Enum** — Create entities, add enum, update Machine nav props
2. **DbContext + Migration** — Register DbSets, configure relationships, generate migration
3. **Service** — CRUD + validation logic in SchedulingRuleService
4. **Scheduling Enforcement** — Modify ProgramSchedulingService.FindEarliestSlotAsync()
5. **UI** — Rules tab on Detail.razor, BlackoutCalendarModal
6. **Seed Data** — Default rules for M4 machines
7. **CSS** — Styles for the new tab
8. **Tests** — Rule validation, slot-finding with rules, blackout overlap detection
