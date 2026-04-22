-- One-time repair for a dev tenant DB that got stuck on migrations AddSchedulingWeights /
-- AddSchedulingRules. Both were authored with Quotes-table drop operations that only work
-- against an intermediate schema state some DBs skipped, leaving EF unable to advance.
--
-- This script creates the net-new tables/columns those migrations were meant to add and
-- marks them as applied, so EF can continue past them on the next startup.
--
-- Usage:  sqlite3 Data/tenants/<tenant>.db < scripts/repair-demo-db.sql
-- Then:   run the SchedulingRuleWeight column ALTER from the terminal (see below).

BEGIN TRANSACTION;

-- From 20260403210413_AddSchedulingWeights
CREATE TABLE IF NOT EXISTS "SchedulingWeights" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SchedulingWeights" PRIMARY KEY AUTOINCREMENT,
    "BaseScore" INTEGER NOT NULL,
    "ChangeoverAlignmentBonus" INTEGER NOT NULL,
    "DowntimePenaltyPerHour" INTEGER NOT NULL,
    "MaxDowntimePenalty" INTEGER NOT NULL,
    "EarlinessBonus4h" INTEGER NOT NULL,
    "EarlinessBonus24h" INTEGER NOT NULL,
    "OverproductionPenaltyMax" INTEGER NOT NULL,
    "WeekendOptimizationBonus" INTEGER NOT NULL,
    "ShiftAlignedBonus" INTEGER NOT NULL,
    "StackChangeoverBonus" INTEGER NOT NULL,
    "StackDemandFitBonus" INTEGER NOT NULL,
    "StackEfficiencyMultiplier" TEXT NOT NULL,
    "LastModifiedDate" TEXT NOT NULL,
    "LastModifiedBy" TEXT NULL
);

-- From 20260403221221_AddSchedulingRules
CREATE TABLE IF NOT EXISTS "BlackoutPeriods" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_BlackoutPeriods" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "StartDate" TEXT NOT NULL,
    "EndDate" TEXT NOT NULL,
    "Reason" TEXT NULL,
    "IsRecurringAnnually" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreatedDate" TEXT NOT NULL,
    "CreatedBy" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "MachineSchedulingRules" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_MachineSchedulingRules" PRIMARY KEY AUTOINCREMENT,
    "MachineId" INTEGER NOT NULL,
    "RuleType" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "MaxConsecutiveBuilds" INTEGER NULL,
    "MinBreakHours" REAL NULL,
    "CreatedDate" TEXT NOT NULL,
    "LastModifiedDate" TEXT NOT NULL,
    "CreatedBy" TEXT NOT NULL,
    "LastModifiedBy" TEXT NOT NULL,
    CONSTRAINT "FK_MachineSchedulingRules_Machines_MachineId"
        FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "MachineBlackoutAssignments" (
    "MachineId" INTEGER NOT NULL,
    "BlackoutPeriodId" INTEGER NOT NULL,
    CONSTRAINT "PK_MachineBlackoutAssignments" PRIMARY KEY ("MachineId", "BlackoutPeriodId"),
    CONSTRAINT "FK_MachineBlackoutAssignments_BlackoutPeriods_BlackoutPeriodId"
        FOREIGN KEY ("BlackoutPeriodId") REFERENCES "BlackoutPeriods" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_MachineBlackoutAssignments_Machines_MachineId"
        FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_MachineSchedulingRules_MachineId"
    ON "MachineSchedulingRules" ("MachineId");
CREATE INDEX IF NOT EXISTS "IX_MachineBlackoutAssignments_BlackoutPeriodId"
    ON "MachineBlackoutAssignments" ("BlackoutPeriodId");

-- Mark the three Scheduling* migrations as applied so EF skips the broken Quotes ops.
-- From 20260403222750_AddSchedulingRuleWeight: run this ALTER separately after the script
-- if the column is missing:
--   ALTER TABLE DispatchConfigurations ADD COLUMN SchedulingRuleWeight decimal(4,2) NOT NULL DEFAULT 0;
INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260403210413_AddSchedulingWeights', '10.0.0');
INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260403221221_AddSchedulingRules', '10.0.0');
INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260403222750_AddSchedulingRuleWeight', '10.0.0');

COMMIT;
