using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services.Auth;

namespace Vectrik.Services;

public class DataSeedingService : IDataSeedingService
{
    private readonly IAuthService _authService;

    public DataSeedingService(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task SeedAsync(TenantDbContext tenantDb)
    {
        // Foundation data (no dependencies)
        await SeedProductionStagesAsync(tenantDb);
        await SeedOperatorRolesAsync(tenantDb);
        await SeedMachinesAsync(tenantDb);

        // Additive: add missing machines/stages/priorities to existing databases
        await EnsureSeedDataAsync(tenantDb);

        // Link stages to machines (runs after both exist; idempotent for existing databases)
        await EnsureStageWorkstationsAsync(tenantDb);

        await SeedMaterialsAsync(tenantDb);
        await SeedManufacturingApproachesAsync(tenantDb);
        await SeedOperatingShiftsAsync(tenantDb);
        await SeedSystemSettingsAsync(tenantDb);
        await EnsureSystemSettingsAsync(tenantDb);
        await SeedDefaultAdminUserAsync(tenantDb);
        await SeedTestUsersAsync(tenantDb);
        await SeedDocumentTemplatesAsync(tenantDb);

        // Inventory (depends on materials)
        await SeedStockLocationsAsync(tenantDb);
        await SeedInventoryItemsAsync(tenantDb);

        // Manufacturing processes (depends on parts + production stages)
        await SeedManufacturingProcessesAsync(tenantDb);

        // Build templates (depends on parts + machines + materials)
        await SeedBuildTemplatesAsync(tenantDb);

        // Work instructions & sign-off checklists (depends on parts + production stages)
        await SeedWorkInstructionsAsync(tenantDb);

        // Scheduler demo data (depends on parts + approaches + stages + machines)
        await SeedSchedulerDemoDataAsync(tenantDb);

        // Dispatch system demo data (depends on machines + programs)
        await SeedDispatchDemoDataAsync(tenantDb);

        // Shift assignments (depends on machines + users + shifts)
        await SeedShiftAssignmentsAsync(tenantDb);

        // Part pricing, notes (depends on parts from scheduler seed)
        await SeedPartPricingAsync(tenantDb);
        await SeedPartNotesAsync(tenantDb);

        // Quotes, RFQs (depends on parts + work orders)
        await SeedQuotesAsync(tenantDb);
        await SeedRfqRequestsAsync(tenantDb);

        // Shipments (depends on completed work orders)
        await SeedShipmentsAsync(tenantDb);

        // Work order comments (depends on work orders + users)
        await SeedWorkOrderCommentsAsync(tenantDb);

        // Quality records (depends on parts + jobs)
        await SeedQualityDemoDataAsync(tenantDb);

        // Workflow definitions (no dependencies)
        await SeedWorkflowsAsync(tenantDb);

        // Dense demo schedule for demo day (idempotent — uses SystemSetting marker)
        await SeedDemoEnhancementAsync(tenantDb);
    }

    public async Task SeedCoreAsync(TenantDbContext tenantDb)
    {
        // Foundation data (no dependencies)
        await SeedProductionStagesAsync(tenantDb);
        await SeedOperatorRolesAsync(tenantDb);
        await SeedMachinesAsync(tenantDb);

        // Additive: add missing machines/stages/priorities
        await EnsureSeedDataAsync(tenantDb);

        // Link stages to machines
        await EnsureStageWorkstationsAsync(tenantDb);

        await SeedMaterialsAsync(tenantDb);
        await SeedManufacturingApproachesAsync(tenantDb);
        await SeedOperatingShiftsAsync(tenantDb);
        await SeedSystemSettingsAsync(tenantDb);
        await EnsureSystemSettingsAsync(tenantDb);
        await SeedDocumentTemplatesAsync(tenantDb);

        // Inventory (depends on materials)
        await SeedStockLocationsAsync(tenantDb);
        await SeedInventoryItemsAsync(tenantDb);

        // Manufacturing processes (depends on parts + production stages)
        await SeedManufacturingProcessesAsync(tenantDb);
    }

    public async Task SeedTenantAdminAsync(TenantDbContext tenantDb, string fullName, string email, string password)
    {
        if (await tenantDb.Users.AnyAsync(u => u.Role == "Admin")) return;

        var admin = new User
        {
            Username = email.Split('@')[0].ToLowerInvariant(),
            FullName = fullName,
            Email = email,
            PasswordHash = _authService.HashPassword(password),
            Role = "Admin",
            Department = "Operations",
            MustChangePassword = true,
            CreatedBy = "System",
            LastModifiedBy = "System"
        };

        tenantDb.Users.Add(admin);
        await tenantDb.SaveChangesAsync();
    }

    private static async Task SeedProductionStagesAsync(TenantDbContext db)
    {
        if (await db.ProductionStages.AnyAsync()) return;

        var stages = new List<ProductionStage>
        {
            // === BUILD-LEVEL STAGES (now configured via ProcessStage.ProcessingLevel) ===
            new()
            {
                Name = "SLS/LPBF Printing", StageSlug = "sls-printing", Department = "SLS",
                DefaultDurationHours = 8.0, HasBuiltInPage = true,
                DefaultHourlyRate = 85.00m, DefaultSetupMinutes = 0,
                DisplayOrder = 1, StageIcon = "🖨️", StageColor = "#3B82F6",
                RequiresMachineAssignment = true, RequiresQualityCheck = false,
                DefaultMachineId = "M4-1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Depowdering", StageSlug = "depowdering", Department = "Post-Process",
                DefaultDurationHours = 1.0, HasBuiltInPage = true,
                DefaultHourlyRate = 55.00m, DefaultSetupMinutes = 10,
                DisplayOrder = 2, StageIcon = "💨", StageColor = "#F59E0B",
                RequiresMachineAssignment = true,
                DefaultMachineId = "INC1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Heat Treatment", StageSlug = "heat-treatment", Department = "Post-Process",
                DefaultDurationHours = 4.0, HasBuiltInPage = true,
                DefaultHourlyRate = 65.00m, DefaultSetupMinutes = 20,
                DisplayOrder = 3, StageIcon = "🔥", StageColor = "#EF4444",
                RequiresMachineAssignment = true,
                DefaultMachineId = "HT1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Wire EDM", StageSlug = "wire-edm", Department = "EDM",
                DefaultDurationHours = 2.0, HasBuiltInPage = true,
                DefaultHourlyRate = 85.00m, DefaultSetupMinutes = 25,
                DisplayOrder = 4, StageIcon = "⚡", StageColor = "#8B5CF6",
                RequiresMachineAssignment = true,
                DefaultMachineId = "EDM1",
                CreatedBy = "System", LastModifiedBy = "System"
            },

            // === PER-PART / BATCH STAGES
            new()
            {
                Name = "CNC Machining", StageSlug = "cnc-machining", Department = "Machining",
                DefaultDurationHours = 0.5, HasBuiltInPage = true,
                DefaultHourlyRate = 95.00m, DefaultSetupMinutes = 30,
                DisplayOrder = 5, StageIcon = "⚙️", StageColor = "#06B6D4",
                RequiresMachineAssignment = true,
                DefaultMachineId = "CNC1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Laser Engraving", StageSlug = "laser-engraving", Department = "Engraving",
                DefaultDurationHours = 0.25, HasBuiltInPage = true,
                DefaultHourlyRate = 55.00m, DefaultSetupMinutes = 10,
                RequiresSerialNumber = true,
                DisplayOrder = 6, StageIcon = "✒️", StageColor = "#10B981",
                RequiresMachineAssignment = true, DefaultMachineId = "ENGRAVE1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Surface Finishing", StageSlug = "surface-finishing", Department = "Finishing",
                DefaultDurationHours = 0.33, HasBuiltInPage = true,
                DefaultHourlyRate = 45.00m, DefaultSetupMinutes = 10,
                DisplayOrder = 7, StageIcon = "🎨", StageColor = "#EC4899",
                RequiresMachineAssignment = true, DefaultMachineId = "FINISH1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Quality Control", StageSlug = "qc", Department = "Quality",
                DefaultDurationHours = 0.083, HasBuiltInPage = true,
                DefaultHourlyRate = 75.00m, DefaultSetupMinutes = 15,
                DisplayOrder = 8, StageIcon = "✅", StageColor = "#14B8A6",
                RequiresQualityCheck = true,
                RequiresMachineAssignment = true, DefaultMachineId = "QC1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Shipping", StageSlug = "shipping", Department = "Shipping",
                DefaultDurationHours = 0.083, HasBuiltInPage = true,
                DefaultHourlyRate = 35.00m, DefaultSetupMinutes = 5,
                DisplayOrder = 9, StageIcon = "🚚", StageColor = "#6366F1",
                RequiresQualityCheck = false,
                RequiresMachineAssignment = true, DefaultMachineId = "SHIP1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "CNC Turning", StageSlug = "cnc-turning", Department = "Machining",
                DefaultDurationHours = 0.25, HasBuiltInPage = true,
                DefaultHourlyRate = 90.00m, DefaultSetupMinutes = 10,
                DisplayOrder = 10, StageIcon = "🔩", StageColor = "#0891B2",
                RequiresMachineAssignment = true,
                DefaultMachineId = "LATHE1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Assembly", StageSlug = "assembly", Department = "Assembly",
                DefaultDurationHours = 0.167, HasBuiltInPage = true,
                DefaultHourlyRate = 60.00m, DefaultSetupMinutes = 10,
                DisplayOrder = 11, StageIcon = "🔧", StageColor = "#7C3AED",
                RequiresQualityCheck = false,
                RequiresMachineAssignment = true, DefaultMachineId = "ASSY1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Sandblasting", StageSlug = "sandblasting", Department = "Finishing",
                DefaultDurationHours = 0.25,
                DefaultHourlyRate = 40.00m, DefaultSetupMinutes = 5,
                DisplayOrder = 12, StageIcon = "🌪️", StageColor = "#A3A3A3",
                RequiresQualityCheck = false,
                RequiresMachineAssignment = true, DefaultMachineId = "BLAST1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "External Coating", StageSlug = "external-coating", Department = "External",
                DefaultDurationHours = 0, IsExternalOperation = true, DefaultTurnaroundDays = 14,
                DefaultHourlyRate = 0.00m, DefaultSetupMinutes = 0,
                DisplayOrder = 13, StageIcon = "🏢", StageColor = "#D97706",
                RequiresQualityCheck = true,
                RequiresMachineAssignment = true, DefaultMachineId = "EXT-COAT",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Oil & Sleeve Assembly", StageSlug = "oil-sleeve", Department = "Assembly",
                DefaultDurationHours = 0.083,
                DefaultHourlyRate = 50.00m, DefaultSetupMinutes = 5,
                DisplayOrder = 14, StageIcon = "🛢️", StageColor = "#059669",
                RequiresQualityCheck = false,
                RequiresMachineAssignment = true, DefaultMachineId = "OIL-ASSY1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Packaging & Shipping", StageSlug = "packaging", Department = "Shipping",
                DefaultDurationHours = 0.05,
                DefaultHourlyRate = 35.00m, DefaultSetupMinutes = 5,
                DisplayOrder = 15, StageIcon = "📦", StageColor = "#7C3AED",
                RequiresQualityCheck = false,
                RequiresMachineAssignment = true, DefaultMachineId = "PACK1",
                CreatedBy = "System", LastModifiedBy = "System"
            }
        };

        db.ProductionStages.AddRange(stages);
        await db.SaveChangesAsync();
    }

    private static async Task SeedOperatorRolesAsync(TenantDbContext db)
    {
        if (await db.OperatorRoles.AnyAsync()) return;

        var roles = new List<OperatorRole>
        {
            new() { Name = "SLS Operator", Slug = "sls-operator", Description = "Operates SLS/LPBF printers", DisplayOrder = 1 },
            new() { Name = "CNC Operator", Slug = "cnc-operator", Description = "Operates CNC mills and lathes", DisplayOrder = 2 },
            new() { Name = "EDM Operator", Slug = "edm-operator", Description = "Operates wire EDM machines", DisplayOrder = 3 },
            new() { Name = "QC Inspector", Slug = "qc-inspector", Description = "Performs quality control inspections", DisplayOrder = 4 },
            new() { Name = "Laser Engraver", Slug = "laser-engraver", Description = "Operates laser engraving equipment", DisplayOrder = 5 },
            new() { Name = "Surface Finishing", Slug = "surface-finishing", Description = "Sandblasting, tumbling, and coating", DisplayOrder = 6 },
            new() { Name = "Assembly Technician", Slug = "assembly-technician", Description = "Assembles multi-part products", DisplayOrder = 7 },
            new() { Name = "Shipping Clerk", Slug = "shipping-clerk", Description = "Handles packaging and shipping", DisplayOrder = 8 },
            new() { Name = "Scheduler", Slug = "scheduler", Description = "Plans and schedules production", DisplayOrder = 9 },
            new() { Name = "Supervisor", Slug = "supervisor", Description = "Production floor supervisor", DisplayOrder = 10 }
        };

        db.OperatorRoles.AddRange(roles);
        await db.SaveChangesAsync();
    }

    private static async Task SeedMachinesAsync(TenantDbContext db)
    {
        if (await db.Machines.AnyAsync()) return;

        var machines = new List<Machine>
        {
            new()
            {
                MachineId = "M4-1", Name = "EOS M4 Onyx #1", MachineType = "SLS",
                MachineModel = "EOS M 400-4", Department = "SLS",
                BuildLengthMm = 450, BuildWidthMm = 450, BuildHeightMm = 400,
                BuildPlateCapacity = 2, AutoChangeoverEnabled = true, ChangeoverMinutes = 30, OperatorUnloadMinutes = 90,
                LaserCount = 6, MaxLaserPowerWatts = 1000,
                HourlyRate = 200.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "M4-2", Name = "EOS M4 Onyx #2", MachineType = "SLS",
                MachineModel = "EOS M 400-4", Department = "SLS",
                BuildLengthMm = 450, BuildWidthMm = 450, BuildHeightMm = 400,
                BuildPlateCapacity = 2, AutoChangeoverEnabled = true, ChangeoverMinutes = 30, OperatorUnloadMinutes = 90,
                LaserCount = 6, MaxLaserPowerWatts = 1000,
                HourlyRate = 200.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "INC1", Name = "Incineris Depowder", MachineType = "Depowder",
                MachineModel = "Incineris", Department = "Post-Process",
                BuildPlateCapacity = 1, Priority = 1,
                HourlyRate = 85.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "HT1", Name = "Heat Treatment Furnace", MachineType = "Heat-Treat",
                MachineModel = "Vacuum Furnace", Department = "Post-Process",
                BuildPlateCapacity = 1, Priority = 2,
                HourlyRate = 75.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "EDM1", Name = "Wire EDM", MachineType = "EDM",
                Department = "EDM", Priority = 3, HourlyRate = 85.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "CNC1", Name = "Haas VF-2", MachineType = "CNC",
                MachineModel = "Haas VF-2", Department = "Machining",
                Priority = 4, HourlyRate = 95.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "CNC2", Name = "Haas VF-2SS #2", MachineType = "CNC",
                MachineModel = "Haas VF-2SS", Department = "Machining",
                Priority = 4, HourlyRate = 95.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "CNC3", Name = "Haas VF-4 #3", MachineType = "CNC",
                MachineModel = "Haas VF-4", Department = "Machining",
                Priority = 4, HourlyRate = 105.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "CNC4", Name = "DMG MORI NHX 4000", MachineType = "CNC",
                MachineModel = "DMG MORI NHX 4000", Department = "Machining",
                Priority = 4, HourlyRate = 125.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "LATHE1", Name = "Haas ST-20Y #1", MachineType = "CNC-Turning",
                MachineModel = "Haas ST-20Y", Department = "Machining",
                Priority = 4, HourlyRate = 90.00m, ToolSlotCount = 15,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "LATHE2", Name = "Haas ST-20Y #2", MachineType = "CNC-Turning",
                MachineModel = "Haas ST-20Y", Department = "Machining",
                Priority = 4, HourlyRate = 90.00m, ToolSlotCount = 15,
                CreatedBy = "System", LastModifiedBy = "System"
            },

            // === WORKSTATION MACHINES (one per stage that lacks dedicated equipment) ===
            new()
            {
                MachineId = "BLAST1", Name = "Sandblasting Cabinet", MachineType = "Finishing",
                Department = "Finishing", Priority = 5,
                HourlyRate = 40.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "FINISH1", Name = "Surface Finishing Station", MachineType = "Finishing",
                Department = "Finishing", Priority = 6,
                HourlyRate = 45.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "ENGRAVE1", Name = "Laser Engraver", MachineType = "Engraving",
                Department = "Engraving", Priority = 7,
                HourlyRate = 55.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "QC1", Name = "QC Inspection Station", MachineType = "QC",
                Department = "Quality", Priority = 8,
                HourlyRate = 75.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "ASSY1", Name = "Assembly Station", MachineType = "Assembly",
                Department = "Assembly", Priority = 9,
                HourlyRate = 60.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "OIL-ASSY1", Name = "Oil & Sleeve Assembly Station", MachineType = "Assembly",
                Department = "Assembly", Priority = 9,
                HourlyRate = 50.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "EXT-COAT", Name = "External Coating (Outsourced)", MachineType = "External",
                Department = "External", Priority = 10,
                HourlyRate = 0.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "PACK1", Name = "Packaging Station", MachineType = "Shipping",
                Department = "Shipping", Priority = 10,
                HourlyRate = 35.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "SHIP1", Name = "Shipping Station", MachineType = "Shipping",
                Department = "Shipping", Priority = 10,
                HourlyRate = 35.00m, CreatedBy = "System", LastModifiedBy = "System"
            }
        };

        db.Machines.AddRange(machines);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent: adds missing machines, production stages, and updates Priority values
    /// on existing machines. Safe to call on both fresh and existing databases.
    /// Also exposed publicly so the Debug page can trigger a re-seed.
    /// </summary>
    public async Task<SeedResult> EnsureSeedDataAsync(TenantDbContext db)
    {
        int machinesAdded = 0, machinesUpdated = 0, stagesAdded = 0, stagesUpdated = 0;

        // ── Machines: add missing, update Priority/Department on existing ──
        var expectedMachines = GetExpectedMachines();
        var existingMachines = await db.Machines.ToListAsync();
        var existingByBusinessId = existingMachines.ToDictionary(m => m.MachineId, m => m);

        foreach (var expected in expectedMachines)
        {
            if (existingByBusinessId.TryGetValue(expected.MachineId, out var existing))
            {
                // Update fields that may have changed
                var changed = false;

                if (existing.Priority != expected.Priority)
                { existing.Priority = expected.Priority; changed = true; }

                if (!string.IsNullOrWhiteSpace(expected.Department) && existing.Department != expected.Department)
                { existing.Department = expected.Department; changed = true; }

                if (existing.AutoChangeoverEnabled != expected.AutoChangeoverEnabled)
                { existing.AutoChangeoverEnabled = expected.AutoChangeoverEnabled; changed = true; }

                if (expected.ChangeoverMinutes > 0 && existing.ChangeoverMinutes != expected.ChangeoverMinutes)
                { existing.ChangeoverMinutes = expected.ChangeoverMinutes; changed = true; }

                if (existing.IsAdditiveMachine != expected.IsAdditiveMachine)
                { existing.IsAdditiveMachine = expected.IsAdditiveMachine; changed = true; }

                if (changed) machinesUpdated++;
            }
            else
            {
                db.Machines.Add(expected);
                machinesAdded++;
            }
        }

        if (machinesAdded > 0 || machinesUpdated > 0)
            await db.SaveChangesAsync();

        // ── Production stages: add missing, update DefaultMachineId on existing ──
        var expectedStages = GetExpectedProductionStages();
        var existingStages = await db.ProductionStages.ToListAsync();
        var existingBySlug = existingStages.ToDictionary(s => s.StageSlug, s => s);

        foreach (var expected in expectedStages)
        {
            if (existingBySlug.TryGetValue(expected.StageSlug, out var existing))
            {
                var changed = false;

                if (!string.IsNullOrWhiteSpace(expected.DefaultMachineId) &&
                    existing.DefaultMachineId != expected.DefaultMachineId)
                { existing.DefaultMachineId = expected.DefaultMachineId; changed = true; }

                if (!string.IsNullOrWhiteSpace(expected.Department) && existing.Department != expected.Department)
                { existing.Department = expected.Department; changed = true; }

                if (expected.DisplayOrder > 0 && existing.DisplayOrder != expected.DisplayOrder)
                { existing.DisplayOrder = expected.DisplayOrder; changed = true; }

                if (changed) stagesUpdated++;
            }
            else
            {
                db.ProductionStages.Add(expected);
                stagesAdded++;
            }
        }

        if (stagesAdded > 0 || stagesUpdated > 0)
            await db.SaveChangesAsync();

        // ── Fix: correct duration modes to match processing level ──
        // Duration modes must align: Build→PerBuild, Batch→PerBatch, Part→PerPart.
        // Mismatches cause wildly inflated durations (e.g., 50h depowder instead of 50min).
        var allProcessStages = await db.ProcessStages
            .Where(ps => !ps.DurationFromBuildConfig)
            .ToListAsync();

        var fixCount = 0;
        foreach (var ps in allProcessStages)
        {
            var expectedRunMode = ps.ProcessingLevel switch
            {
                ProcessingLevel.Build => DurationMode.PerBuild,
                ProcessingLevel.Batch => DurationMode.PerBatch,
                ProcessingLevel.Part => DurationMode.PerPart,
                _ => ps.RunDurationMode
            };

            if (ps.RunDurationMode != DurationMode.None && ps.RunDurationMode != expectedRunMode)
            {
                ps.RunDurationMode = expectedRunMode;
                ps.LastModifiedDate = DateTime.UtcNow;
                fixCount++;
            }

            // Also fix setup mode if it's mismatched (setup should match level or be None)
            if (ps.SetupDurationMode != DurationMode.None && ps.SetupDurationMode != expectedRunMode
                && ps.SetupTimeMinutes.HasValue)
            {
                ps.SetupDurationMode = expectedRunMode;
                ps.LastModifiedDate = DateTime.UtcNow;
            }
        }

        if (fixCount > 0)
            await db.SaveChangesAsync();

        // Also recalculate EstimatedHours on existing StageExecutions that used the
        // broken PerPart durations for build-level stages. These have inflated hours
        // (e.g., 50h depowder for 60 parts when it should be ~1h per build).
        {
            var inflatedExecutions = await db.StageExecutions
                .Include(se => se.ProcessStage)
                .Where(se => se.ProcessStageId != null
                    && se.ProcessStage!.ProcessingLevel == ProcessingLevel.Build
                    && !se.ProcessStage.DurationFromBuildConfig
                    && se.Status == StageExecutionStatus.NotStarted
                    && se.ProcessStage.RunTimeMinutes.HasValue)
                .ToListAsync();

            foreach (var se in inflatedExecutions)
            {
                var ps = se.ProcessStage!;
                var setupMins = ps.SetupTimeMinutes ?? 0;
                var runMins = ps.RunTimeMinutes ?? 0;
                // PerBuild: duration = setup + run (flat, not multiplied by part count)
                var correctHours = (setupMins + runMins) / 60.0;

                if (se.EstimatedHours.HasValue && Math.Abs(se.EstimatedHours.Value - correctHours) > 0.5)
                {
                    se.EstimatedHours = correctHours;
                    // Recalculate end time based on corrected duration
                    if (se.ScheduledStartAt.HasValue)
                        se.ScheduledEndAt = se.ScheduledStartAt.Value.AddHours(correctHours);
                    se.LastModifiedDate = DateTime.UtcNow;
                }
            }

            if (inflatedExecutions.Any())
                await db.SaveChangesAsync();
        }

        // ── Restore SLS print execution times from MachineProgram schedule ──
        // The re-sequencing pass may have moved SLS print stage times. Restore them
        // to match the MachineProgram.ScheduledDate + EstimatedPrintHours.
        {
            var slsPrintExecs = await db.StageExecutions
                .Include(se => se.MachineProgram)
                .Where(se => se.MachineProgramId != null
                    && se.MachineProgram!.ProgramType == ProgramType.BuildPlate
                    && se.MachineProgram.ScheduledDate != null
                    && se.MachineProgram.EstimatedPrintHours != null
                    && se.Status == StageExecutionStatus.NotStarted)
                .ToListAsync();

            foreach (var se in slsPrintExecs)
            {
                var expectedStart = se.MachineProgram!.ScheduledDate!.Value;
                var expectedEnd = expectedStart.AddHours(se.MachineProgram.EstimatedPrintHours!.Value);
                if (se.ScheduledStartAt != expectedStart || se.ScheduledEndAt != expectedEnd)
                {
                    se.ScheduledStartAt = expectedStart;
                    se.ScheduledEndAt = expectedEnd;
                    se.EstimatedHours = se.MachineProgram.EstimatedPrintHours;
                    se.LastModifiedDate = DateTime.UtcNow;
                }
            }
            if (slsPrintExecs.Any(se => db.Entry(se).State == Microsoft.EntityFrameworkCore.EntityState.Modified))
                await db.SaveChangesAsync();
        }

        // ── Re-sequence downstream build-level stages ──
        // The duration fix above corrected EstimatedHours but left ScheduledStartAt
        // based on old collision chains (inflated predecessor blocks). Rebuild the
        // schedule so depowder/heat-treatment/wire-EDM stack back-to-back.
        {
            var shifts = await db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

            // Load all jobs that have NotStarted downstream build-level stages
            var jobsWithDownstream = await db.Jobs
                .Include(j => j.Stages.OrderBy(se => se.SortOrder))
                .Where(j => j.Scope == JobScope.Build
                    && j.Stages.Any(se => se.Status == StageExecutionStatus.NotStarted
                        && se.ProcessStageId != null))
                .ToListAsync();

            // Track the end watermark for each machine (when that machine is next free)
            // Only seed from actively-running executions (InProgress) — not old completed/paused ones
            var machineWatermarks = new Dictionary<int, DateTime>();
            var activeBlocks = await db.StageExecutions
                .Where(se => se.MachineId != null && se.ScheduledEndAt != null
                    && se.Status == StageExecutionStatus.InProgress)
                .GroupBy(se => se.MachineId!.Value)
                .Select(g => new { MachineId = g.Key, LatestEnd = g.Max(se => se.ScheduledEndAt!.Value) })
                .ToListAsync();
            foreach (var b in activeBlocks)
                machineWatermarks[b.MachineId] = b.LatestEnd;

            // Sort jobs by their SLS print end time (earliest builds get post-processed first)
            var sortedJobs = jobsWithDownstream
                .OrderBy(j => j.Stages
                    .Where(se => se.Status != StageExecutionStatus.NotStarted && se.ScheduledEndAt.HasValue)
                    .Select(se => se.ScheduledEndAt!.Value)
                    .DefaultIfEmpty(j.ScheduledEnd)
                    .Max())
                .ToList();

            var resequenced = 0;
            foreach (var job in sortedJobs)
            {
                // Find the chain start: SLS print end (which may be NotStarted but has correct times),
                // or last completed/in-progress stage end
                var predecessorEnd = job.Stages
                    .Where(se => se.MachineProgramId.HasValue && se.ScheduledEndAt.HasValue) // SLS print stage
                    .Select(se => se.ScheduledEndAt!.Value)
                    .Concat(job.Stages
                        .Where(se => se.Status != StageExecutionStatus.NotStarted && se.ScheduledEndAt.HasValue)
                        .Select(se => se.ScheduledEndAt!.Value))
                    .DefaultIfEmpty(job.ScheduledEnd)
                    .Max();

                var jobCursor = predecessorEnd;

                foreach (var se in job.Stages.Where(se => se.Status == StageExecutionStatus.NotStarted))
                {
                    if (!se.MachineId.HasValue || !se.EstimatedHours.HasValue) continue;

                    // Skip SLS print stages — their timing is set by the program scheduler
                    // and must match the MachineProgram.ScheduledDate
                    if (se.MachineProgramId.HasValue) continue;

                    var machineId = se.MachineId.Value;
                    var hours = se.EstimatedHours.Value;

                    // Start = later of (previous stage in this job ended) and (machine is free)
                    var earliest = jobCursor;
                    if (machineWatermarks.TryGetValue(machineId, out var machineEnd) && machineEnd > earliest)
                        earliest = machineEnd;

                    // Snap to shift (operators required for post-process)
                    var start = ShiftTimeHelper.SnapToNextShiftStart(earliest, shifts);
                    var end = ShiftTimeHelper.AdvanceByWorkHours(start, hours, shifts);

                    if (se.ScheduledStartAt != start || se.ScheduledEndAt != end)
                    {
                        se.ScheduledStartAt = start;
                        se.ScheduledEndAt = end;
                        se.LastModifiedDate = DateTime.UtcNow;
                        resequenced++;
                    }

                    // Update watermarks
                    machineWatermarks[machineId] = end;
                    jobCursor = end;
                }
            }

            if (resequenced > 0)
                await db.SaveChangesAsync();
        }

        return new SeedResult(machinesAdded, machinesUpdated, stagesAdded, stagesUpdated);
    }

    /// <summary>
    /// Returns the canonical list of machines the system expects to exist.
    /// Used by both SeedMachinesAsync (fresh DB) and EnsureSeedDataAsync (existing DB).
    /// </summary>
    private static List<Machine> GetExpectedMachines() =>
    [
        new()
        {
            MachineId = "M4-1", Name = "EOS M4 Onyx #1", MachineType = "SLS",
            MachineModel = "EOS M 400-4", Department = "SLS",
            BuildLengthMm = 450, BuildWidthMm = 450, BuildHeightMm = 400,
            BuildPlateCapacity = 2, AutoChangeoverEnabled = true, ChangeoverMinutes = 30, OperatorUnloadMinutes = 90,
            LaserCount = 6, MaxLaserPowerWatts = 1000, IsAdditiveMachine = true,
            HourlyRate = 200.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "M4-2", Name = "EOS M4 Onyx #2", MachineType = "SLS",
            MachineModel = "EOS M 400-4", Department = "SLS",
            BuildLengthMm = 450, BuildWidthMm = 450, BuildHeightMm = 400,
            BuildPlateCapacity = 2, AutoChangeoverEnabled = true, ChangeoverMinutes = 30, OperatorUnloadMinutes = 90,
            LaserCount = 6, MaxLaserPowerWatts = 1000, IsAdditiveMachine = true,
            HourlyRate = 200.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "INC1", Name = "Incineris Depowder", MachineType = "Depowder",
            MachineModel = "Incineris", Department = "Post-Process",
            BuildPlateCapacity = 1, Priority = 1,
            HourlyRate = 85.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "HT1", Name = "Heat Treatment Furnace", MachineType = "Heat-Treat",
            MachineModel = "Vacuum Furnace", Department = "Post-Process",
            BuildPlateCapacity = 1, Priority = 2,
            HourlyRate = 75.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "EDM1", Name = "Wire EDM", MachineType = "EDM",
            Department = "EDM", Priority = 3, HourlyRate = 85.00m,
            CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "CNC1", Name = "Haas VF-2", MachineType = "CNC",
            MachineModel = "Haas VF-2", Department = "Machining",
            Priority = 4, HourlyRate = 95.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "CNC2", Name = "Haas VF-2SS #2", MachineType = "CNC",
            MachineModel = "Haas VF-2SS", Department = "Machining",
            Priority = 4, HourlyRate = 95.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "CNC3", Name = "Haas VF-4 #3", MachineType = "CNC",
            MachineModel = "Haas VF-4", Department = "Machining",
            Priority = 4, HourlyRate = 105.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "CNC4", Name = "DMG MORI NHX 4000", MachineType = "CNC",
            MachineModel = "DMG MORI NHX 4000", Department = "Machining",
            Priority = 4, HourlyRate = 125.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "LATHE1", Name = "CNC Lathe", MachineType = "CNC-Turning",
            MachineModel = "Haas ST-20Y", Department = "Machining",
            Priority = 4, HourlyRate = 95.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "BLAST1", Name = "Sandblasting Cabinet", MachineType = "Finishing",
            Department = "Finishing", Priority = 5,
            HourlyRate = 40.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "FINISH1", Name = "Surface Finishing Station", MachineType = "Finishing",
            Department = "Finishing", Priority = 6,
            HourlyRate = 45.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "ENGRAVE1", Name = "Laser Engraver", MachineType = "Engraving",
            Department = "Engraving", Priority = 7,
            HourlyRate = 55.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "QC1", Name = "QC Inspection Station", MachineType = "QC",
            Department = "Quality", Priority = 8,
            HourlyRate = 75.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "ASSY1", Name = "Assembly Station", MachineType = "Assembly",
            Department = "Assembly", Priority = 9,
            HourlyRate = 60.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "OIL-ASSY1", Name = "Oil & Sleeve Assembly Station", MachineType = "Assembly",
            Department = "Assembly", Priority = 9,
            HourlyRate = 50.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "EXT-COAT", Name = "External Coating (Outsourced)", MachineType = "External",
            Department = "External", Priority = 10,
            HourlyRate = 0.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "PACK1", Name = "Packaging Station", MachineType = "Shipping",
            Department = "Shipping", Priority = 10,
            HourlyRate = 35.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "SHIP1", Name = "Shipping Station", MachineType = "Shipping",
            Department = "Shipping", Priority = 10,
            HourlyRate = 35.00m, CreatedBy = "System", LastModifiedBy = "System"
        }
    ];

    /// <summary>
    /// Returns the canonical list of production stages the system expects.
    /// Used by EnsureSeedDataAsync to add missing stages to existing databases.
    /// </summary>
    private static List<ProductionStage> GetExpectedProductionStages() =>
    [
        new() { Name = "SLS/LPBF Printing", StageSlug = "sls-printing", Department = "SLS", DefaultDurationHours = 8.0, HasBuiltInPage = true, DefaultHourlyRate = 85.00m, DefaultSetupMinutes = 0, DisplayOrder = 1, StageIcon = "🖨️", StageColor = "#3B82F6", RequiresMachineAssignment = true, DefaultMachineId = "M4-1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Depowdering", StageSlug = "depowdering", Department = "Post-Process", DefaultDurationHours = 1.0, HasBuiltInPage = true, DefaultHourlyRate = 55.00m, DefaultSetupMinutes = 10, DisplayOrder = 2, StageIcon = "💨", StageColor = "#F59E0B", RequiresMachineAssignment = true, DefaultMachineId = "INC1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Heat Treatment", StageSlug = "heat-treatment", Department = "Post-Process", DefaultDurationHours = 4.0, HasBuiltInPage = true, DefaultHourlyRate = 65.00m, DefaultSetupMinutes = 20, DisplayOrder = 3, StageIcon = "🔥", StageColor = "#EF4444", RequiresMachineAssignment = true, DefaultMachineId = "HT1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Wire EDM", StageSlug = "wire-edm", Department = "EDM", DefaultDurationHours = 2.0, HasBuiltInPage = true, DefaultHourlyRate = 85.00m, DefaultSetupMinutes = 25, DisplayOrder = 4, StageIcon = "⚡", StageColor = "#8B5CF6", RequiresMachineAssignment = true, DefaultMachineId = "EDM1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "CNC Machining", StageSlug = "cnc-machining", Department = "Machining", DefaultDurationHours = 0.5, HasBuiltInPage = true, DefaultHourlyRate = 95.00m, DefaultSetupMinutes = 30, DisplayOrder = 5, StageIcon = "⚙️", StageColor = "#06B6D4", RequiresMachineAssignment = true, DefaultMachineId = "CNC1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Laser Engraving", StageSlug = "laser-engraving", Department = "Engraving", DefaultDurationHours = 0.25, HasBuiltInPage = true, DefaultHourlyRate = 55.00m, DefaultSetupMinutes = 10, RequiresSerialNumber = true, DisplayOrder = 6, StageIcon = "✒️", StageColor = "#10B981", RequiresMachineAssignment = true, DefaultMachineId = "ENGRAVE1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Surface Finishing", StageSlug = "surface-finishing", Department = "Finishing", DefaultDurationHours = 0.33, HasBuiltInPage = true, DefaultHourlyRate = 45.00m, DefaultSetupMinutes = 10, DisplayOrder = 7, StageIcon = "🎨", StageColor = "#EC4899", RequiresMachineAssignment = true, DefaultMachineId = "FINISH1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Quality Control", StageSlug = "qc", Department = "Quality", DefaultDurationHours = 0.083, HasBuiltInPage = true, DefaultHourlyRate = 75.00m, DefaultSetupMinutes = 15, DisplayOrder = 8, StageIcon = "✅", StageColor = "#14B8A6", RequiresQualityCheck = true, RequiresMachineAssignment = true, DefaultMachineId = "QC1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Shipping", StageSlug = "shipping", Department = "Shipping", DefaultDurationHours = 0.083, HasBuiltInPage = true, DefaultHourlyRate = 35.00m, DefaultSetupMinutes = 5, DisplayOrder = 9, StageIcon = "🚚", StageColor = "#6366F1", RequiresMachineAssignment = true, DefaultMachineId = "SHIP1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "CNC Turning", StageSlug = "cnc-turning", Department = "Machining", DefaultDurationHours = 0.25, HasBuiltInPage = true, DefaultHourlyRate = 90.00m, DefaultSetupMinutes = 10, DisplayOrder = 10, StageIcon = "🔩", StageColor = "#0891B2", RequiresMachineAssignment = true, DefaultMachineId = "LATHE1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Assembly", StageSlug = "assembly", Department = "Assembly", DefaultDurationHours = 0.167, HasBuiltInPage = true, DefaultHourlyRate = 60.00m, DefaultSetupMinutes = 10, DisplayOrder = 11, StageIcon = "🔧", StageColor = "#7C3AED", RequiresMachineAssignment = true, DefaultMachineId = "ASSY1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Sandblasting", StageSlug = "sandblasting", Department = "Finishing", DefaultDurationHours = 0.25, DefaultHourlyRate = 40.00m, DefaultSetupMinutes = 5, DisplayOrder = 12, StageIcon = "🌪️", StageColor = "#A3A3A3", RequiresMachineAssignment = true, DefaultMachineId = "BLAST1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "External Coating", StageSlug = "external-coating", Department = "External", DefaultDurationHours = 0, IsExternalOperation = true, DefaultTurnaroundDays = 14, DefaultHourlyRate = 0.00m, DefaultSetupMinutes = 0, DisplayOrder = 13, StageIcon = "🏢", StageColor = "#D97706", RequiresQualityCheck = true, RequiresMachineAssignment = true, DefaultMachineId = "EXT-COAT", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Oil & Sleeve Assembly", StageSlug = "oil-sleeve", Department = "Assembly", DefaultDurationHours = 0.083, DefaultHourlyRate = 50.00m, DefaultSetupMinutes = 5, DisplayOrder = 14, StageIcon = "🛢️", StageColor = "#059669", RequiresMachineAssignment = true, DefaultMachineId = "OIL-ASSY1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Packaging & Shipping", StageSlug = "packaging", Department = "Shipping", DefaultDurationHours = 0.05, DefaultHourlyRate = 35.00m, DefaultSetupMinutes = 5, DisplayOrder = 15, StageIcon = "📦", StageColor = "#7C3AED", RequiresMachineAssignment = true, DefaultMachineId = "PACK1", CreatedBy = "System", LastModifiedBy = "System" },
    ];

    /// <summary>
    /// Ensures every ProductionStage has at least one machine assigned so the Gantt chart
    /// shows a row for every stage in the part lifecycle. Idempotent — safe for existing databases.
    /// Creates workstation machines for stages that have a DefaultMachineId but no matching machine,
    /// and populates AssignedMachineIds with correct int PKs.
    /// </summary>
    private static async Task EnsureStageWorkstationsAsync(TenantDbContext db)
    {
        var stages = await db.ProductionStages.ToListAsync();
        var machines = await db.Machines.ToListAsync();
        var machineByBusinessId = machines.ToDictionary(m => m.MachineId, m => m);
        var dirty = false;

        foreach (var stage in stages)
        {
            // Resolve the default machine for this stage
            Machine? defaultMachine = null;
            if (!string.IsNullOrWhiteSpace(stage.DefaultMachineId))
            {
                machineByBusinessId.TryGetValue(stage.DefaultMachineId, out defaultMachine);
            }

            // If the stage has a DefaultMachineId but no machine exists for it, create a workstation
            if (!string.IsNullOrWhiteSpace(stage.DefaultMachineId) && defaultMachine == null)
            {
                defaultMachine = new Machine
                {
                    MachineId = stage.DefaultMachineId,
                    Name = $"{stage.Name} Workstation",
                    MachineType = stage.Department ?? "General",
                    Department = stage.Department ?? "General",
                    HourlyRate = stage.DefaultHourlyRate,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                };
                db.Machines.Add(defaultMachine);
                await db.SaveChangesAsync(); // flush to get PK
                machineByBusinessId[defaultMachine.MachineId] = defaultMachine;
            }

            // Build the correct AssignedMachineIds from int PKs
            if (defaultMachine != null)
            {
                var existingIntIds = stage.GetAssignedMachineIntIds();
                if (!existingIntIds.Contains(defaultMachine.Id))
                {
                    existingIntIds.Add(defaultMachine.Id);
                    stage.AssignedMachineIds = string.Join(",", existingIntIds);
                    dirty = true;
                }

                // For stages that already had AssignedMachineIds as string MachineIds,
                // detect and fix: if any entry doesn't parse as int, rebuild from scratch
                var rawIds = stage.GetAssignedMachineIds();
                var hasNonIntIds = rawIds.Any(id => !int.TryParse(id, out _));
                if (hasNonIntIds)
                {
                    var correctedIds = new List<int>();
                    foreach (var rawId in rawIds)
                    {
                        if (int.TryParse(rawId, out var intId))
                        {
                            if (!correctedIds.Contains(intId))
                                correctedIds.Add(intId);
                        }
                        else if (machineByBusinessId.TryGetValue(rawId, out var resolved))
                        {
                            if (!correctedIds.Contains(resolved.Id))
                                correctedIds.Add(resolved.Id);
                        }
                    }
                    // Ensure the default machine is included
                    if (!correctedIds.Contains(defaultMachine.Id))
                        correctedIds.Add(defaultMachine.Id);
                    stage.AssignedMachineIds = string.Join(",", correctedIds);
                    dirty = true;
                }

                // Ensure RequiresMachineAssignment is set
                if (!stage.RequiresMachineAssignment)
                {
                    stage.RequiresMachineAssignment = true;
                    dirty = true;
                }
            }
        }

        // Also fix stages that had AssignedMachineIds with string MachineIds but no DefaultMachineId
        // (e.g., CNC Machining had "CNC1,CNC2,CNC3,CNC4" as AssignedMachineIds)
        foreach (var stage in stages.Where(s => string.IsNullOrWhiteSpace(s.DefaultMachineId)
                                                && !string.IsNullOrWhiteSpace(s.AssignedMachineIds)))
        {
            var rawIds = stage.GetAssignedMachineIds();
            var hasNonIntIds = rawIds.Any(id => !int.TryParse(id, out _));
            if (hasNonIntIds)
            {
                var correctedIds = new List<int>();
                foreach (var rawId in rawIds)
                {
                    if (int.TryParse(rawId, out var intId))
                    {
                        if (!correctedIds.Contains(intId))
                            correctedIds.Add(intId);
                    }
                    else if (machineByBusinessId.TryGetValue(rawId, out var resolved))
                    {
                        if (!correctedIds.Contains(resolved.Id))
                            correctedIds.Add(resolved.Id);
                    }
                }
                stage.AssignedMachineIds = string.Join(",", correctedIds);
                dirty = true;
            }
        }

        // Ensure ALL additive machines are assigned to SLS/build-level stages.
        // EnsureStageWorkstationsAsync only adds the DefaultMachineId, so additional
        // SLS machines (e.g., M4-2) are missing from AssignedMachineIds.
        var additiveMachineIds = machines
            .Where(m => m.IsAdditiveMachine && m.IsActive)
            .Select(m => m.Id)
            .ToList();

        if (additiveMachineIds.Count > 0)
        {
            foreach (var stage in stages.Where(s =>
                s.Department?.Equals("SLS", StringComparison.OrdinalIgnoreCase) == true))
            {
                var existingIds = stage.GetAssignedMachineIntIds();
                foreach (var mid in additiveMachineIds)
                {
                    if (!existingIds.Contains(mid))
                    {
                        existingIds.Add(mid);
                        dirty = true;
                    }
                }
                stage.AssignedMachineIds = string.Join(",", existingIds);
            }
        }

        // Ensure ALL CNC-Turning machines are assigned to the CNC Turning stage.
        var turningMachineIds = machines
            .Where(m => m.MachineType == "CNC-Turning" && m.IsActive)
            .Select(m => m.Id)
            .ToList();

        if (turningMachineIds.Count > 0)
        {
            foreach (var stage in stages.Where(s => s.StageSlug == "cnc-turning"))
            {
                var existingIds = stage.GetAssignedMachineIntIds();
                foreach (var mid in turningMachineIds)
                {
                    if (!existingIds.Contains(mid))
                    {
                        existingIds.Add(mid);
                        dirty = true;
                    }
                }
                stage.AssignedMachineIds = string.Join(",", existingIds);
            }
        }

        if (dirty)
        {
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedMaterialsAsync(TenantDbContext db)
    {
        if (await db.Materials.AnyAsync()) return;

        var materials = new List<Material>
        {
            new()
            {
                Name = "Ti-6Al-4V Grade 5", Category = "Metal Powder",
                Density = 4.43, CostPerKg = 350.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "316L Stainless Steel", Category = "Metal Powder",
                Density = 7.99, CostPerKg = 80.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Inconel 718", Category = "Metal Powder",
                Density = 8.19, CostPerKg = 120.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "AlSi10Mg", Category = "Metal Powder",
                Density = 2.67, CostPerKg = 65.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "17-4PH Stainless Steel", Category = "Bar Stock",
                Density = 7.78, CostPerKg = 45.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            }
        };

        db.Materials.AddRange(materials);
        await db.SaveChangesAsync();
    }

    private static async Task SeedManufacturingApproachesAsync(TenantDbContext db)
    {
        if (await db.ManufacturingApproaches.AnyAsync()) return;

        // Helper to serialize enhanced routing templates
        static string RT(List<RoutingTemplateStage> stages) =>
            System.Text.Json.JsonSerializer.Serialize(stages, RoutingTemplateStage.JsonOptions);

        var approaches = new List<ManufacturingApproach>
        {
            new()
            {
                Name = "SLS-Based", Slug = "sls-based", IsAdditive = true, RequiresBuildPlate = true,
                DefaultBatchCapacity = 60, DisplayOrder = 1, IconEmoji = "🖨️",
                Description = "Selective Laser Sintering with full post-print processing",
                DefaultRoutingTemplate = RT(
                [
                    new() { Slug = "sls-printing",    Level = ProcessingLevel.Build, DurationFromBuildConfig = true },
                    new() { Slug = "depowdering",     Level = ProcessingLevel.Build },
                    new() { Slug = "heat-treatment",  Level = ProcessingLevel.Build },
                    new() { Slug = "wire-edm",        Level = ProcessingLevel.Build, IsPlateReleaseTrigger = true },
                    new() { Slug = "cnc-machining",   Level = ProcessingLevel.Part },
                    new() { Slug = "laser-engraving", Level = ProcessingLevel.Batch, BatchCapacityOverride = 36 },
                    new() { Slug = "sandblasting",    Level = ProcessingLevel.Batch, BatchCapacityOverride = 20 },
                    new() { Slug = "qc",              Level = ProcessingLevel.Part },
                    new() { Slug = "packaging",       Level = ProcessingLevel.Batch, BatchCapacityOverride = 50 },
                ])
            },
            new()
            {
                Name = "Suppressor (No HT)", Slug = "suppressor-no-ht", IsAdditive = true, RequiresBuildPlate = true,
                DefaultBatchCapacity = 60, DisplayOrder = 2, IconEmoji = "🔫",
                Description = "SLS-based suppressor manufacturing without heat treatment",
                DefaultRoutingTemplate = RT(
                [
                    new() { Slug = "sls-printing",    Level = ProcessingLevel.Build, DurationFromBuildConfig = true },
                    new() { Slug = "depowdering",     Level = ProcessingLevel.Build },
                    new() { Slug = "wire-edm",        Level = ProcessingLevel.Build, IsPlateReleaseTrigger = true },
                    new() { Slug = "cnc-turning",     Level = ProcessingLevel.Part },
                    new() { Slug = "laser-engraving", Level = ProcessingLevel.Batch, BatchCapacityOverride = 36 },
                    new() { Slug = "sandblasting",    Level = ProcessingLevel.Batch, BatchCapacityOverride = 20 },
                    new() { Slug = "qc",              Level = ProcessingLevel.Part },
                    new() { Slug = "packaging",       Level = ProcessingLevel.Batch, BatchCapacityOverride = 50 },
                ])
            },
            new()
            {
                Name = "CNC Machining", Slug = "cnc-machining", IsAdditive = false, RequiresBuildPlate = false,
                DefaultBatchCapacity = 50, DisplayOrder = 3, IconEmoji = "⚙️",
                Description = "Traditional CNC milling from bar/billet stock",
                DefaultRoutingTemplate = RT(
                [
                    new() { Slug = "cnc-machining", Level = ProcessingLevel.Part },
                    new() { Slug = "qc",            Level = ProcessingLevel.Part },
                ])
            },
            new()
            {
                Name = "CNC Turning", Slug = "cnc-turning", IsAdditive = false, RequiresBuildPlate = false,
                DefaultBatchCapacity = 50, DisplayOrder = 4, IconEmoji = "🔩",
                Description = "CNC lathe operations for rotational parts",
                DefaultRoutingTemplate = RT(
                [
                    new() { Slug = "cnc-machining", Level = ProcessingLevel.Part },
                    new() { Slug = "qc",            Level = ProcessingLevel.Part },
                ])
            },
            new()
            {
                Name = "Wire EDM", Slug = "wire-edm", IsAdditive = false, RequiresBuildPlate = false,
                DefaultBatchCapacity = 20, DisplayOrder = 5, IconEmoji = "⚡",
                Description = "Wire electrical discharge machining",
                DefaultRoutingTemplate = RT(
                [
                    new() { Slug = "wire-edm", Level = ProcessingLevel.Part },
                    new() { Slug = "qc",       Level = ProcessingLevel.Part },
                ])
            },
            new()
            {
                Name = "3D Printing (FDM)", Slug = "fdm", IsAdditive = true, RequiresBuildPlate = true,
                DefaultBatchCapacity = 30, DisplayOrder = 6, IconEmoji = "🖨️",
                Description = "Fused Deposition Modeling",
                DefaultRoutingTemplate = RT(
                [
                    new() { Slug = "sls-printing", Level = ProcessingLevel.Build, DurationFromBuildConfig = true },
                    new() { Slug = "qc",           Level = ProcessingLevel.Part },
                ])
            },
            new()
            {
                Name = "3D Printing (SLA)", Slug = "sla", IsAdditive = true, RequiresBuildPlate = true,
                DefaultBatchCapacity = 30, DisplayOrder = 7, IconEmoji = "🖨️",
                Description = "Stereolithography resin printing",
                DefaultRoutingTemplate = RT(
                [
                    new() { Slug = "sls-printing", Level = ProcessingLevel.Build, DurationFromBuildConfig = true },
                    new() { Slug = "qc",           Level = ProcessingLevel.Part },
                ])
            },
            new()
            {
                Name = "Additive + Subtractive", Slug = "additive-subtractive", IsAdditive = true, RequiresBuildPlate = true,
                DefaultBatchCapacity = 60, DisplayOrder = 8, IconEmoji = "🔧",
                Description = "Additive printing followed by CNC finishing",
                DefaultRoutingTemplate = RT(
                [
                    new() { Slug = "sls-printing",    Level = ProcessingLevel.Build, DurationFromBuildConfig = true },
                    new() { Slug = "depowdering",     Level = ProcessingLevel.Build },
                    new() { Slug = "heat-treatment",  Level = ProcessingLevel.Build },
                    new() { Slug = "wire-edm",        Level = ProcessingLevel.Build, IsPlateReleaseTrigger = true },
                    new() { Slug = "cnc-machining",   Level = ProcessingLevel.Part },
                    new() { Slug = "qc",              Level = ProcessingLevel.Part },
                ])
            },
            new()
            {
                Name = "Sheet Metal", Slug = "sheet-metal", IsAdditive = false, RequiresBuildPlate = false,
                DefaultBatchCapacity = 100, DisplayOrder = 9, IconEmoji = "📐",
                Description = "Sheet metal fabrication",
                DefaultRoutingTemplate = RT([new() { Slug = "qc", Level = ProcessingLevel.Part }])
            },
            new()
            {
                Name = "Casting", Slug = "casting", IsAdditive = false, RequiresBuildPlate = false,
                DefaultBatchCapacity = 50, DisplayOrder = 10, IconEmoji = "🏭",
                Description = "Metal casting with CNC finishing",
                DefaultRoutingTemplate = RT(
                [
                    new() { Slug = "cnc-machining", Level = ProcessingLevel.Part },
                    new() { Slug = "qc",            Level = ProcessingLevel.Part },
                ])
            },
            new()
            {
                Name = "Injection Molding", Slug = "injection-molding", IsAdditive = false, RequiresBuildPlate = false,
                DefaultBatchCapacity = 200, DisplayOrder = 11, IconEmoji = "💉",
                Description = "Plastic injection molding",
                DefaultRoutingTemplate = RT([new() { Slug = "qc", Level = ProcessingLevel.Part }])
            },
            new()
            {
                Name = "Assembly", Slug = "assembly", IsAdditive = false, RequiresBuildPlate = false,
                DefaultBatchCapacity = 50, DisplayOrder = 12, IconEmoji = "🔧",
                Description = "Assembly from sub-components",
                DefaultRoutingTemplate = RT([new() { Slug = "qc", Level = ProcessingLevel.Part }])
            },
            new()
            {
                Name = "Manual", Slug = "manual", IsAdditive = false, RequiresBuildPlate = false,
                DefaultBatchCapacity = 20, DisplayOrder = 13, IconEmoji = "✋",
                Description = "Manual hand-finishing operations",
                DefaultRoutingTemplate = RT([new() { Slug = "qc", Level = ProcessingLevel.Part }])
            },
            new()
            {
                Name = "Other", Slug = "other", IsAdditive = false, RequiresBuildPlate = false,
                DefaultBatchCapacity = 50, DisplayOrder = 14, IconEmoji = "❓",
                Description = "Custom or unclassified manufacturing approach",
                DefaultRoutingTemplate = "[]"
            },
        };

        db.ManufacturingApproaches.AddRange(approaches);
        await db.SaveChangesAsync();
    }

    private static async Task SeedOperatingShiftsAsync(TenantDbContext db)
    {
        if (await db.OperatingShifts.AnyAsync()) return;

        var shifts = new List<OperatingShift>
        {
            new() { Name = "Day Shift", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(18, 0, 0), DaysOfWeek = "Mon,Tue,Wed,Thu,Fri" },
            new() { Name = "Night Shift", StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(6, 0, 0), DaysOfWeek = "Mon,Tue,Wed,Thu,Fri" }
        };

        db.OperatingShifts.AddRange(shifts);
        await db.SaveChangesAsync();
    }

    private static async Task SeedSystemSettingsAsync(TenantDbContext db)
    {
        if (await db.SystemSettings.AnyAsync()) return;

        var settings = new List<SystemSetting>
        {
            // Existing settings
            new() { Key = "CompanyName", Value = "Polite Society Industries", Category = "Branding", Description = "Company name displayed in the app", LastModifiedBy = "System" },
            new() { Key = "SerialNumberPrefix", Value = "SN", Category = "Serial", Description = "Prefix for generated serial numbers", LastModifiedBy = "System" },
            new() { Key = "ShowDebugBuildForm", Value = "true", Category = "Debug", Description = "Show the debug build file form on the Builds page", LastModifiedBy = "System" },
            new() { Key = "DefaultDueDateDays", Value = "30", Category = "General", Description = "Default number of days for work order due dates", LastModifiedBy = "System" },
            new() { Key = "DelayReasonCodes", Value = "Material Shortage,Machine Breakdown,Operator Unavailable,Quality Hold,Tooling Issue,Other", Category = "General", Description = "Comma-separated delay reason codes", LastModifiedBy = "System" },

            // Branding (Stage 0.5)
            new() { Key = "company.name", Value = "Polite Society Industries", Category = "Branding", Description = "Company name on all documents", LastModifiedBy = "System" },
            new() { Key = "company.logo_url", Value = "/uploads/logos/psi-primary.svg", Category = "Branding", Description = "Logo for reports/packing lists", LastModifiedBy = "System" },
            new() { Key = "company.address", Value = "1847 Freedom Blvd, Suite 200\nAustin, TX 78745", Category = "Branding", Description = "Address for documents", LastModifiedBy = "System" },

            // Defense identifiers
            new() { Key = "company.cage_code", Value = "8P4K7", Category = "Defense", Description = "CAGE code for DLMS transactions", LastModifiedBy = "System" },
            new() { Key = "company.dodaac", Value = "", Category = "Defense", Description = "DoD Activity Address Code", LastModifiedBy = "System" },
            new() { Key = "company.duns", Value = "078451236", Category = "Defense", Description = "DUNS/SAM UEI number", LastModifiedBy = "System" },

            // Numbering
            new() { Key = "numbering.separator", Value = "-", Category = "Numbering", Description = "Separator between prefix and number", LastModifiedBy = "System" },
            new() { Key = "numbering.wo_prefix", Value = "WO", Category = "Numbering", Description = "Work order number prefix", LastModifiedBy = "System" },
            new() { Key = "numbering.wo_digits", Value = "5", Category = "Numbering", Description = "Digits in WO number", LastModifiedBy = "System" },
            new() { Key = "numbering.quote_prefix", Value = "QT", Category = "Numbering", Description = "Quote number prefix", LastModifiedBy = "System" },
            new() { Key = "numbering.quote_digits", Value = "5", Category = "Numbering", Description = "Digits in quote number", LastModifiedBy = "System" },
            new() { Key = "numbering.shipment_prefix", Value = "SHP", Category = "Numbering", Description = "Shipment number prefix", LastModifiedBy = "System" },
            new() { Key = "numbering.shipment_digits", Value = "5", Category = "Numbering", Description = "Digits in shipment number", LastModifiedBy = "System" },
            new() { Key = "numbering.ncr_prefix", Value = "NCR", Category = "Numbering", Description = "NCR number prefix", LastModifiedBy = "System" },
            new() { Key = "numbering.ncr_digits", Value = "5", Category = "Numbering", Description = "Digits in NCR number", LastModifiedBy = "System" },
            new() { Key = "numbering.po_prefix", Value = "PO", Category = "Numbering", Description = "Purchase order prefix", LastModifiedBy = "System" },
            new() { Key = "numbering.po_digits", Value = "5", Category = "Numbering", Description = "Digits in PO number", LastModifiedBy = "System" },
            new() { Key = "numbering.part_auto", Value = "false", Category = "Numbering", Description = "Auto-generate part numbers", LastModifiedBy = "System" },
            new() { Key = "numbering.part_prefix", Value = "PT", Category = "Numbering", Description = "Part number prefix", LastModifiedBy = "System" },
            new() { Key = "numbering.part_digits", Value = "5", Category = "Numbering", Description = "Digits in part number", LastModifiedBy = "System" },
            new() { Key = "numbering.serial_format", Value = "{PartNumber}-{Seq:0000}", Category = "Numbering", Description = "Serial number template", LastModifiedBy = "System" },

            // Quality
            new() { Key = "quality.require_fair", Value = "false", Category = "Quality", Description = "Require FAIR on first articles", LastModifiedBy = "System" },
            new() { Key = "quality.spc_default_subgroup", Value = "5", Category = "Quality", Description = "Default SPC subgroup size", LastModifiedBy = "System" },
            new() { Key = "quality.ncr_require_approval", Value = "true", Category = "Quality", Description = "NCR needs manager approval", LastModifiedBy = "System" },

            // Shipping
            new() { Key = "shipping.require_coc", Value = "true", Category = "Shipping", Description = "Require Certificate of Conformance", LastModifiedBy = "System" },
            new() { Key = "shipping.default_carrier", Value = "FedEx", Category = "Shipping", Description = "Default carrier name", LastModifiedBy = "System" },
            new() { Key = "shipping.generate_asn", Value = "false", Category = "Shipping", Description = "Auto-generate ASN (DLMS 856)", LastModifiedBy = "System" },

            // Inventory
            new() { Key = "inventory.track_lots", Value = "true", Category = "Inventory", Description = "Enable lot tracking", LastModifiedBy = "System" },
            new() { Key = "inventory.track_gfm", Value = "false", Category = "Inventory", Description = "Enable GFM/GFE tracking", LastModifiedBy = "System" },

            // Costing
            new() { Key = "costing.overhead_method", Value = "percentage", Category = "Costing", Description = "'percentage' or 'activity' overhead allocation", LastModifiedBy = "System" },
            new() { Key = "costing.default_margin_pct", Value = "30", Category = "Costing", Description = "Default quote margin", LastModifiedBy = "System" },

            // DLMS
            new() { Key = "dlms.enabled", Value = "false", Category = "Defense", Description = "Enable DLMS transaction features", LastModifiedBy = "System" },
            new() { Key = "dlms.iuid_construct", Value = "1", Category = "Defense", Description = "IUID construct type (1 or 2)", LastModifiedBy = "System" },
            new() { Key = "dlms.wawf_enabled", Value = "false", Category = "Defense", Description = "Enable WAWF invoice generation", LastModifiedBy = "System" },

            // Compliance
            new() { Key = "compliance.frameworks", Value = "[]", Category = "Compliance", Description = "Active compliance framework codes JSON", LastModifiedBy = "System" },

            // Workflow
            new() { Key = "workflow.wo_auto_release", Value = "false", Category = "Workflow", Description = "Auto-release WOs from quotes", LastModifiedBy = "System" },
            new() { Key = "workflow.require_job_approval", Value = "false", Category = "Workflow", Description = "Jobs need approval before start", LastModifiedBy = "System" },
            new() { Key = "workflow.stage_pause_allowed", Value = "true", Category = "Workflow", Description = "Allow operators to pause stages", LastModifiedBy = "System" },

            // Scheduling
            new() { Key = "scheduling.ema_alpha", Value = "0.3", Category = "Scheduling", Description = "EMA smoothing factor for duration learning (0.0-1.0, higher = faster adaptation)", LastModifiedBy = "System" },
            new() { Key = "scheduling.ema_auto_switch_samples", Value = "3", Category = "Scheduling", Description = "Number of actual samples before auto-switching estimate source to Auto", LastModifiedBy = "System" },
            new() { Key = "scheduling.parallel_build_limit", Value = "0", Category = "Scheduling", Description = "Max concurrent builds per additive machine (0 = unlimited by plate capacity)", LastModifiedBy = "System" },

            // Scheduler UI
            new() { Key = "scheduler.auto_refresh_seconds", Value = "30", Category = "Scheduler UI", Description = "Auto-refresh interval for the scheduler dashboard (seconds)", LastModifiedBy = "System" },
            new() { Key = "scheduler.gantt_lookback_days", Value = "7", Category = "Scheduler UI", Description = "Days of history to show on the Gantt chart", LastModifiedBy = "System" },
            new() { Key = "scheduler.gantt_lookahead_days", Value = "14", Category = "Scheduler UI", Description = "Days of future to show in the Gantt viewport", LastModifiedBy = "System" },
            new() { Key = "scheduler.gantt_data_range_days", Value = "21", Category = "Scheduler UI", Description = "Total days of data to query for Gantt rendering", LastModifiedBy = "System" },
            new() { Key = "scheduler.gantt_default_zoom", Value = "6.0", Category = "Scheduler UI", Description = "Default pixels-per-hour zoom on the Gantt chart", LastModifiedBy = "System" },

            // Builds
            new() { Key = "builds.name_template", Value = "{PARTS}-{DATE}-{SEQ}", Category = "Builds", Description = "Template for auto-generated build names. Tokens: {PARTS}, {MACHINE}, {DATE}, {SEQ}, {MATERIAL}", LastModifiedBy = "System" },
            new() { Key = "builds.default_batch_capacity", Value = "60", Category = "Builds", Description = "Default batch capacity for new manufacturing processes", LastModifiedBy = "System" },
        };

        db.SystemSettings.AddRange(settings);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent: adds any missing SystemSettings to existing databases without
    /// overwriting admin-customized values. Safe to call on every startup.
    /// </summary>
    private static async Task EnsureSystemSettingsAsync(TenantDbContext db)
    {
        var expectedSettings = new Dictionary<string, (string Value, string Category, string Description)>
        {
            ["scheduling.ema_alpha"] = ("0.3", "Scheduling", "EMA smoothing factor for duration learning (0.0-1.0, higher = faster adaptation)"),
            ["scheduling.ema_auto_switch_samples"] = ("3", "Scheduling", "Number of actual samples before auto-switching estimate source to Auto"),
            ["scheduling.parallel_build_limit"] = ("0", "Scheduling", "Max concurrent builds per additive machine (0 = unlimited by plate capacity)"),
            ["scheduler.auto_refresh_seconds"] = ("30", "Scheduler UI", "Auto-refresh interval for the scheduler dashboard (seconds)"),
            ["scheduler.gantt_lookback_days"] = ("7", "Scheduler UI", "Days of history to show on the Gantt chart"),
            ["scheduler.gantt_lookahead_days"] = ("14", "Scheduler UI", "Days of future to show in the Gantt viewport"),
            ["scheduler.gantt_data_range_days"] = ("21", "Scheduler UI", "Total days of data to query for Gantt rendering"),
            ["scheduler.gantt_default_zoom"] = ("6.0", "Scheduler UI", "Default pixels-per-hour zoom on the Gantt chart"),
            ["builds.name_template"] = ("{PARTS}-{DATE}-{SEQ}", "Builds", "Template for auto-generated build names. Tokens: {PARTS}, {MACHINE}, {DATE}, {SEQ}, {MATERIAL}"),
            ["builds.default_batch_capacity"] = ("60", "Builds", "Default batch capacity for new manufacturing processes"),
            ["scheduler.max_part_types_per_plate"] = ("4", "Scheduling", "Maximum number of different part types allowed on a single build plate"),
            ["scheduler.auto_create_downstream_programs"] = ("true", "Scheduling", "Automatically create placeholder downstream programs when scheduling builds"),

            // Dispatch system settings
            ["dispatch.auto_enabled"] = ("false", "Dispatch", "Master switch for auto-dispatch generation"),
            ["dispatch.lookahead_hours"] = ("8", "Dispatch", "How far ahead the auto-dispatch engine looks for candidates"),
            ["dispatch.changeover_weight"] = ("0.35", "Dispatch", "Changeover optimization weight in dispatch scoring (0.0-1.0)"),
            ["dispatch.duedate_weight"] = ("0.45", "Dispatch", "Due date urgency weight in dispatch scoring (0.0-1.0)"),
            ["dispatch.throughput_weight"] = ("0.20", "Dispatch", "Throughput maximization weight in dispatch scoring (0.0-1.0)"),
            ["dispatch.maintenance_buffer_hours"] = ("4", "Dispatch", "Hours before maintenance to start routing short jobs"),
            ["dispatch.max_queue_depth"] = ("3", "Dispatch", "Maximum dispatches per machine before auto-dispatch stops"),
            ["dispatch.setup_ema_alpha"] = ("0.3", "Dispatch", "EMA smoothing factor for setup time learning (0.0-1.0)"),
            ["dispatch.batch_grouping_window_hours"] = ("4", "Dispatch", "Hours within which same-program jobs are batched into one dispatch"),
            ["dispatch.require_scheduler_approval"] = ("true", "Dispatch", "Auto-generated dispatches require scheduler approval before entering queue"),
            ["dispatch.sls_load_lead_hours"] = ("2", "Dispatch", "Hours ahead of build start to create plate load dispatches"),
            ["dispatch.changeover_alert_hours"] = ("1", "Dispatch", "When to start escalating changeover dispatch priority"),
            ["dispatch.changeover_urgent_minutes"] = ("30", "Dispatch", "Minutes remaining in shift when changeover becomes URGENT"),
            ["dispatch.plate_layout_auto_notify"] = ("true", "Dispatch", "Auto-create PlateLayout dispatches when unmet demand detected"),
        };

        var existingKeys = await db.SystemSettings
            .Select(s => s.Key)
            .ToListAsync();
        var existingKeySet = new HashSet<string>(existingKeys);

        var toAdd = new List<SystemSetting>();
        foreach (var (key, (value, category, description)) in expectedSettings)
        {
            if (!existingKeySet.Contains(key))
            {
                toAdd.Add(new SystemSetting
                {
                    Key = key,
                    Value = value,
                    Category = category,
                    Description = description,
                    LastModifiedBy = "System"
                });
            }
        }

        if (toAdd.Count > 0)
        {
            db.SystemSettings.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
    }

    private async Task SeedDefaultAdminUserAsync(TenantDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var admin = new User
        {
            Username = "admin",
            FullName = "Henry Gill",
            Email = "henry@politesocietyind.com",
            PasswordHash = _authService.HashPassword("admin123"),
            Role = "Admin",
            Department = "Operations",
            MustChangePassword = true,
            CreatedBy = "System",
            LastModifiedBy = "System"
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }

    private async Task SeedTestUsersAsync(TenantDbContext db)
    {
        // Only seed test users if we just have the single admin
        if (await db.Users.CountAsync() > 1) return;

        var testUsers = new List<User>
        {
            new()
            {
                Username = "operator1",
                FullName = "Jake Marshall",
                Email = "jake@politesocietyind.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "Operator",
                Department = "SLS",
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                Username = "operator2",
                FullName = "Ryan Cole",
                Email = "ryan@politesocietyind.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "Operator",
                Department = "Machining",
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                Username = "manager",
                FullName = "Derek Simmons",
                Email = "derek@politesocietyind.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "Manager",
                Department = "Operations",
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                Username = "qcinspector",
                FullName = "Ana Reyes",
                Email = "ana@politesocietyind.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "QualityInspector",
                Department = "Quality",
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                Username = "operator3",
                FullName = "Marcus Hayes",
                Email = "marcus@politesocietyind.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "Operator",
                Department = "Post-Process",
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                Username = "operator4",
                FullName = "Brianna Torres",
                Email = "brianna@politesocietyind.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "Operator",
                Department = "Finishing",
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                Username = "shipping",
                FullName = "Sam Nguyen",
                Email = "sam@politesocietyind.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "Operator",
                Department = "Shipping",
                CreatedBy = "System",
                LastModifiedBy = "System"
            }
        };

        foreach (var u in testUsers) u.MustChangePassword = true;
        db.Users.AddRange(testUsers);
        await db.SaveChangesAsync();
    }

    private static async Task SeedDocumentTemplatesAsync(TenantDbContext db)
    {
        if (await db.DocumentTemplates.AnyAsync())
            return;

        db.DocumentTemplates.AddRange(
            new DocumentTemplate
            {
                Name = "Standard Quote",
                EntityType = "Quote",
                IsDefault = true,
                TemplateHtml = """
                <div class="header">
                    <h1 style="margin:0;font-size:1.6rem;">QUOTATION</h1>
                    <p style="margin:4px 0;color:#666;">{{QuoteNumber}} &mdash; Rev {{RevisionNumber}}</p>
                </div>
                <table>
                    <tr><td style="width:50%;border:none;padding:0;">
                        <strong>From:</strong><br/>Vectrik Manufacturing<br/>{{CompanyAddress}}
                    </td><td style="border:none;padding:0;">
                        <strong>To:</strong><br/>{{CustomerName}}<br/>{{CustomerEmail}}<br/>{{CustomerPhone}}
                    </td></tr>
                </table>
                <table>
                    <tr><td><strong>Quote Date</strong></td><td>{{CreatedDate}}</td>
                        <td><strong>Valid Until</strong></td><td>{{ExpirationDate}}</td></tr>
                    <tr><td><strong>Status</strong></td><td>{{Status}}</td>
                        <td><strong>Target Margin</strong></td><td>{{TargetMarginPct}}%</td></tr>
                </table>
                <h3 style="margin-top:20px;">Line Items</h3>
                {{LinesTable}}
                <div class="totals">
                    <table style="width:300px;margin-left:auto;">
                        <tr><td><strong>Estimated Cost</strong></td><td style="text-align:right;">{{TotalEstimatedCost}}</td></tr>
                        <tr><td><strong>Quoted Price</strong></td><td style="text-align:right;font-weight:600;">{{QuotedPrice}}</td></tr>
                    </table>
                </div>
                <div style="margin-top:30px;">
                    <p><strong>Notes:</strong> {{Notes}}</p>
                </div>
                <div style="margin-top:40px;">
                    <span class="sign-line"></span> Date: <span class="sign-line" style="width:120px;"></span><br/>
                    <small>Authorized Signature</small>
                </div>
                """,
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new DocumentTemplate
            {
                Name = "Job Traveler",
                EntityType = "WorkOrder",
                IsDefault = true,
                TemplateHtml = """
                <div class="header">
                    <h1 style="margin:0;font-size:1.6rem;">JOB TRAVELER</h1>
                    <p style="margin:4px 0;color:#666;">{{OrderNumber}}</p>
                </div>
                <table>
                    <tr><td><strong>Customer</strong></td><td>{{CustomerName}}</td>
                        <td><strong>Customer PO</strong></td><td>{{CustomerPO}}</td></tr>
                    <tr><td><strong>Order Date</strong></td><td>{{OrderDate}}</td>
                        <td><strong>Due Date</strong></td><td>{{DueDate}}</td></tr>
                    <tr><td><strong>Priority</strong></td><td>{{Priority}}</td>
                        <td><strong>Status</strong></td><td>{{Status}}</td></tr>
                </table>
                <h3 style="margin-top:20px;">Line Items</h3>
                {{LinesTable}}
                <h3 style="margin-top:20px;">Routing / Stage Sign-Off</h3>
                {{RoutingTable}}
                <div style="margin-top:30px;">
                    <p><strong>Notes:</strong> {{Notes}}</p>
                </div>
                """,
                CreatedBy = "System",
                LastModifiedBy = "System"
            }
        );

        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Stock Locations
    // ──────────────────────────────────────────────
    private static async Task SeedStockLocationsAsync(TenantDbContext db)
    {
        if (await db.StockLocations.AnyAsync()) return;

        var locations = new List<StockLocation>
        {
            new() { Code = "WH-MAIN", Name = "Main Warehouse", LocationType = LocationType.Warehouse },
            new() { Code = "WH-POWDER", Name = "Powder Storage", LocationType = LocationType.Warehouse, ParentLocationCode = "WH-MAIN" },
            new() { Code = "SF-SLS", Name = "SLS Print Floor", LocationType = LocationType.ShopFloor },
            new() { Code = "SF-POST", Name = "Post-Processing Area", LocationType = LocationType.ShopFloor },
            new() { Code = "SF-CNC", Name = "CNC Machining Area", LocationType = LocationType.ShopFloor },
            new() { Code = "QRN-HOLD", Name = "Quarantine Hold", LocationType = LocationType.Quarantine },
            new() { Code = "RCV-DOCK", Name = "Receiving Dock", LocationType = LocationType.Receiving },
            new() { Code = "SHP-DOCK", Name = "Shipping Dock", LocationType = LocationType.Shipping },
            new() { Code = "TOOL-CRIB", Name = "Cutting Tool Crib", LocationType = LocationType.CuttingToolCrib }
        };

        db.StockLocations.AddRange(locations);
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Inventory Items, Lots & Transactions
    // ──────────────────────────────────────────────
    private static async Task SeedInventoryItemsAsync(TenantDbContext db)
    {
        if (await db.InventoryItems.AnyAsync()) return;

        var materials = await db.Materials.ToListAsync();
        var locations = await db.StockLocations.ToListAsync();
        var powderStorage = locations.FirstOrDefault(l => l.Code == "WH-POWDER");
        var receiving = locations.FirstOrDefault(l => l.Code == "RCV-DOCK");

        if (powderStorage == null || materials.Count == 0) return;

        var items = new List<InventoryItem>
        {
            new()
            {
                ItemNumber = "PWD-TI64-001", Name = "Ti-6Al-4V Powder (15-45μm)",
                Description = "Grade 5 titanium powder for SLS/LPBF, particle size 15-45 micron",
                ItemType = InventoryItemType.RawMaterial,
                MaterialId = materials.FirstOrDefault(m => m.Name.StartsWith("Ti-6Al-4V"))?.Id,
                UnitOfMeasure = "kg", CurrentStockQty = 180m, ReservedQty = 25m,
                ReorderPoint = 50m, ReorderQuantity = 100m, UnitCost = 350.00m,
                TrackLots = true, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "PWD-SS316L-001", Name = "316L Stainless Steel Powder (15-45μm)",
                Description = "316L SS powder for SLS/LPBF, particle size 15-45 micron",
                ItemType = InventoryItemType.RawMaterial,
                MaterialId = materials.FirstOrDefault(m => m.Name.StartsWith("316L"))?.Id,
                UnitOfMeasure = "kg", CurrentStockQty = 250m, ReservedQty = 0m,
                ReorderPoint = 80m, ReorderQuantity = 200m, UnitCost = 80.00m,
                TrackLots = true, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "PWD-IN718-001", Name = "Inconel 718 Powder (15-53μm)",
                Description = "Inconel 718 nickel-alloy powder for SLS/LPBF",
                ItemType = InventoryItemType.RawMaterial,
                MaterialId = materials.FirstOrDefault(m => m.Name.StartsWith("Inconel"))?.Id,
                UnitOfMeasure = "kg", CurrentStockQty = 120m, ReservedQty = 10m,
                ReorderPoint = 30m, ReorderQuantity = 60m, UnitCost = 120.00m,
                TrackLots = true, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "PWD-ALSI-001", Name = "AlSi10Mg Powder (20-63μm)",
                Description = "Aluminum alloy powder for SLS/LPBF",
                ItemType = InventoryItemType.RawMaterial,
                MaterialId = materials.FirstOrDefault(m => m.Name.StartsWith("AlSi10Mg"))?.Id,
                UnitOfMeasure = "kg", CurrentStockQty = 90m, ReservedQty = 0m,
                ReorderPoint = 25m, ReorderQuantity = 50m, UnitCost = 65.00m,
                TrackLots = true, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "GAS-ARGON-001", Name = "Argon Gas (UHP 5.0)",
                Description = "Ultra-high purity argon for SLS build chamber atmosphere",
                ItemType = InventoryItemType.Consumable,
                UnitOfMeasure = "cylinder", CurrentStockQty = 12m, ReservedQty = 0m,
                ReorderPoint = 3m, ReorderQuantity = 6m, UnitCost = 285.00m,
                TrackLots = false, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "CON-WIRE-EDM-001", Name = "EDM Wire (Brass 0.25mm)",
                Description = "Brass wire for wire EDM cutting, 0.25mm diameter, 8kg spool",
                ItemType = InventoryItemType.Consumable,
                UnitOfMeasure = "spool", CurrentStockQty = 6m, ReservedQty = 0m,
                ReorderPoint = 2m, ReorderQuantity = 4m, UnitCost = 145.00m,
                TrackLots = false, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "TOOL-EM-6MM", Name = "Carbide End Mill 6mm",
                Description = "Solid carbide 3-flute end mill, 6mm diameter, 20mm flute length",
                ItemType = InventoryItemType.CuttingTool,
                UnitOfMeasure = "each", CurrentStockQty = 24m, ReservedQty = 0m,
                ReorderPoint = 5m, ReorderQuantity = 12m, UnitCost = 38.50m,
                TrackLots = false, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "BAR-174PH-150", Name = "17-4PH SS Bar Stock (1.500\" OD)",
                Description = "17-4PH stainless steel round bar, 1.500\" OD x 12\" length, condition H900",
                ItemType = InventoryItemType.RawMaterial,
                MaterialId = materials.FirstOrDefault(m => m.Name.StartsWith("17-4PH"))?.Id,
                UnitOfMeasure = "bar", CurrentStockQty = 48m, ReservedQty = 12m,
                ReorderPoint = 10m, ReorderQuantity = 24m, UnitCost = 42.00m,
                TrackLots = true, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "BAR-SS316L-100", Name = "316L SS Bar Stock (1.000\" OD)",
                Description = "316L stainless steel round bar, 1.000\" OD x 12\" length, annealed",
                ItemType = InventoryItemType.RawMaterial,
                MaterialId = materials.FirstOrDefault(m => m.Name.StartsWith("316L"))?.Id,
                UnitOfMeasure = "bar", CurrentStockQty = 60m, ReservedQty = 0m,
                ReorderPoint = 15m, ReorderQuantity = 30m, UnitCost = 18.00m,
                TrackLots = false, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "BAR-TI64-150", Name = "Ti-6Al-4V Bar Stock (1.500\" OD)",
                Description = "Ti-6Al-4V Grade 5 round bar, 1.500\" OD x 12\" length, annealed",
                ItemType = InventoryItemType.RawMaterial,
                MaterialId = materials.FirstOrDefault(m => m.Name.StartsWith("Ti-6Al-4V"))?.Id,
                UnitOfMeasure = "bar", CurrentStockQty = 24m, ReservedQty = 6m,
                ReorderPoint = 6m, ReorderQuantity = 12m, UnitCost = 155.00m,
                TrackLots = true, CreatedBy = "System"
            },
            new()
            {
                ItemNumber = "SHT-TI64-040", Name = "Ti-6Al-4V Sheet (0.040\" x 12\" x 12\")",
                Description = "Ti-6Al-4V Grade 5 sheet for Wire EDM cutting, 0.040\" thick",
                ItemType = InventoryItemType.RawMaterial,
                MaterialId = materials.FirstOrDefault(m => m.Name.StartsWith("Ti-6Al-4V"))?.Id,
                UnitOfMeasure = "sheet", CurrentStockQty = 15m, ReservedQty = 2m,
                ReorderPoint = 5m, ReorderQuantity = 10m, UnitCost = 85.00m,
                TrackLots = true, CreatedBy = "System"
            }
        };

        db.InventoryItems.AddRange(items);
        await db.SaveChangesAsync();

        // Add lots for the powder materials and bar stock
        var tiPowder = items[0];
        var ssPowder = items[1];
        var inPowder = items[2];
        var barStock174PH = items[7];
        var tiBarStock = items[9];

        var lots = new List<InventoryLot>
        {
            new()
            {
                InventoryItemId = tiPowder.Id, LotNumber = "TI64-2026-001",
                CertificateNumber = "CERT-AP&C-2026-0142",
                ReceivedQty = 100m, CurrentQty = 80m,
                StockLocationId = powderStorage.Id,
                ReceivedAt = DateTime.UtcNow.AddDays(-45),
                Status = LotStatus.Available, InspectionStatus = "Approved"
            },
            new()
            {
                InventoryItemId = tiPowder.Id, LotNumber = "TI64-2026-002",
                CertificateNumber = "CERT-AP&C-2026-0198",
                ReceivedQty = 100m, CurrentQty = 100m,
                StockLocationId = powderStorage.Id,
                ReceivedAt = DateTime.UtcNow.AddDays(-10),
                Status = LotStatus.Available, InspectionStatus = "Approved"
            },
            new()
            {
                InventoryItemId = ssPowder.Id, LotNumber = "SS316L-2026-001",
                CertificateNumber = "CERT-SANDVIK-2026-0055",
                ReceivedQty = 250m, CurrentQty = 250m,
                StockLocationId = powderStorage.Id,
                ReceivedAt = DateTime.UtcNow.AddDays(-20),
                Status = LotStatus.Available, InspectionStatus = "Approved"
            },
            new()
            {
                InventoryItemId = inPowder.Id, LotNumber = "IN718-2025-004",
                CertificateNumber = "CERT-CARTECH-2025-0891",
                ReceivedQty = 60m, CurrentQty = 40m,
                StockLocationId = powderStorage.Id,
                ReceivedAt = DateTime.UtcNow.AddDays(-90),
                ExpiresAt = DateTime.UtcNow.AddDays(275),
                Status = LotStatus.Available, InspectionStatus = "Approved"
            },
            new()
            {
                InventoryItemId = inPowder.Id, LotNumber = "IN718-2026-001",
                CertificateNumber = "CERT-CARTECH-2026-0102",
                ReceivedQty = 80m, CurrentQty = 80m,
                StockLocationId = powderStorage.Id,
                ReceivedAt = DateTime.UtcNow.AddDays(-5),
                Status = LotStatus.Quarantine, InspectionStatus = "Pending"
            },
            new()
            {
                InventoryItemId = barStock174PH.Id, LotNumber = "174PH-2026-001",
                CertificateNumber = "CERT-BODYCOTE-2026-0033",
                ReceivedQty = 48m, CurrentQty = 36m,
                StockLocationId = receiving?.Id ?? powderStorage.Id,
                ReceivedAt = DateTime.UtcNow.AddDays(-30),
                Status = LotStatus.Available, InspectionStatus = "Approved"
            },
            new()
            {
                InventoryItemId = tiBarStock.Id, LotNumber = "TI64B-2026-001",
                CertificateNumber = "CERT-TITANIUM-2026-0087",
                ReceivedQty = 24m, CurrentQty = 18m,
                StockLocationId = receiving?.Id ?? powderStorage.Id,
                ReceivedAt = DateTime.UtcNow.AddDays(-25),
                Status = LotStatus.Available, InspectionStatus = "Approved"
            }
        };

        db.InventoryLots.AddRange(lots);
        await db.SaveChangesAsync();

        // Add receipt transactions for each lot
        var transactions = new List<InventoryTransaction>
        {
            new()
            {
                InventoryItemId = tiPowder.Id, TransactionType = TransactionType.Receipt,
                Quantity = 100m, QuantityBefore = 0m, QuantityAfter = 100m,
                ToLocationId = powderStorage.Id, LotId = lots[0].Id,
                PerformedByUserId = "System", Reference = "PO-2026-001",
                Notes = "Initial Ti64 powder stock from AP&C", TransactedAt = DateTime.UtcNow.AddDays(-45)
            },
            new()
            {
                InventoryItemId = tiPowder.Id, TransactionType = TransactionType.JobConsumption,
                Quantity = -20m, QuantityBefore = 100m, QuantityAfter = 80m,
                FromLocationId = powderStorage.Id, LotId = lots[0].Id,
                PerformedByUserId = "System", Reference = "Build BP-2026-002",
                Notes = "Consumed for monocore baffle stack build", TransactedAt = DateTime.UtcNow.AddDays(-30)
            },
            new()
            {
                InventoryItemId = tiPowder.Id, TransactionType = TransactionType.Receipt,
                Quantity = 100m, QuantityBefore = 80m, QuantityAfter = 180m,
                ToLocationId = powderStorage.Id, LotId = lots[1].Id,
                PerformedByUserId = "System", Reference = "PO-2026-003",
                Notes = "Replenishment order from AP&C", TransactedAt = DateTime.UtcNow.AddDays(-10)
            },
            new()
            {
                InventoryItemId = ssPowder.Id, TransactionType = TransactionType.Receipt,
                Quantity = 250m, QuantityBefore = 0m, QuantityAfter = 250m,
                ToLocationId = powderStorage.Id, LotId = lots[2].Id,
                PerformedByUserId = "System", Reference = "PO-2026-002",
                Notes = "316L powder from Sandvik Osprey", TransactedAt = DateTime.UtcNow.AddDays(-20)
            },
            new()
            {
                InventoryItemId = inPowder.Id, TransactionType = TransactionType.Receipt,
                Quantity = 60m, QuantityBefore = 0m, QuantityAfter = 60m,
                ToLocationId = powderStorage.Id, LotId = lots[3].Id,
                PerformedByUserId = "System", Reference = "PO-2025-018",
                Notes = "Inconel 718 from Carpenter Technology", TransactedAt = DateTime.UtcNow.AddDays(-90)
            },
            new()
            {
                InventoryItemId = inPowder.Id, TransactionType = TransactionType.JobConsumption,
                Quantity = -20m, QuantityBefore = 60m, QuantityAfter = 40m,
                FromLocationId = powderStorage.Id, LotId = lots[3].Id,
                PerformedByUserId = "System", Reference = "Build BP-2026-002",
                Notes = "Consumed for monocore + blast baffle SLS build", TransactedAt = DateTime.UtcNow.AddDays(-60)
            },
            new()
            {
                InventoryItemId = barStock174PH.Id, TransactionType = TransactionType.Receipt,
                Quantity = 48m, QuantityBefore = 0m, QuantityAfter = 48m,
                ToLocationId = receiving?.Id ?? powderStorage.Id, LotId = lots[5].Id,
                PerformedByUserId = "System", Reference = "PO-2026-005",
                Notes = "17-4PH bar stock from Bodycote, H900 heat treated", TransactedAt = DateTime.UtcNow.AddDays(-30)
            },
            new()
            {
                InventoryItemId = barStock174PH.Id, TransactionType = TransactionType.JobConsumption,
                Quantity = -12m, QuantityBefore = 48m, QuantityAfter = 36m,
                FromLocationId = receiving?.Id ?? powderStorage.Id, LotId = lots[5].Id,
                PerformedByUserId = "System", Reference = "JOB-00004",
                Notes = "End cap machining — 12 bars for 24 caps (front + rear)", TransactedAt = DateTime.UtcNow.AddDays(-15)
            },
            new()
            {
                InventoryItemId = tiBarStock.Id, TransactionType = TransactionType.Receipt,
                Quantity = 24m, QuantityBefore = 0m, QuantityAfter = 24m,
                ToLocationId = receiving?.Id ?? powderStorage.Id, LotId = lots[6].Id,
                PerformedByUserId = "System", Reference = "PO-2026-006",
                Notes = "Ti-6Al-4V bar stock for suppressor tube turning", TransactedAt = DateTime.UtcNow.AddDays(-25)
            },
            new()
            {
                InventoryItemId = tiBarStock.Id, TransactionType = TransactionType.JobConsumption,
                Quantity = -6m, QuantityBefore = 24m, QuantityAfter = 18m,
                FromLocationId = receiving?.Id ?? powderStorage.Id, LotId = lots[6].Id,
                PerformedByUserId = "System", Reference = "JOB-00005",
                Notes = "Consumed for first batch of 6 suppressor tubes", TransactedAt = DateTime.UtcNow.AddDays(-12)
            }
        };

        db.InventoryTransactions.AddRange(transactions);
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Manufacturing Processes (for existing parts)
    // ──────────────────────────────────────────────
    private static async Task SeedManufacturingProcessesAsync(TenantDbContext db)
    {
        if (await db.ManufacturingProcesses.AnyAsync()) return;

        // Only seed if parts and production stages already exist
        var parts = await db.Parts.ToListAsync();
        if (parts.Count == 0) return;

        var stages = await db.ProductionStages.ToListAsync();
        if (stages.Count == 0) return;

        // Look up stages by slug
        var stageBySlug = stages.ToDictionary(s => s.StageSlug, s => s);

        // Look up machines by MachineId string → int Id
        var machineIdLookup = await db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.MachineId, m => m.Id);

        int? MachineIntId(string machineId) =>
            machineIdLookup.TryGetValue(machineId, out var id) ? id : null;

        foreach (var part in parts)
        {
            // Create a standard SLS manufacturing process for each part
            var process = new ManufacturingProcess
            {
                PartId = part.Id,
                Name = $"{part.PartNumber} Process",
                Description = $"Standard manufacturing process for {part.Name}",
                DefaultBatchCapacity = 60,
                IsActive = true,
                Version = 1,
                CreatedBy = "System",
                LastModifiedBy = "System",
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            db.ManufacturingProcesses.Add(process);
            await db.SaveChangesAsync();

            // Suppressor (No HT) routing: SLS → Depowder → Wire EDM → CNC Turning → Laser Engraving → Sandblasting → QC → Packaging
            var order = 1;
            var processStages = new List<ProcessStage>();

            // === BUILD-LEVEL STAGES ===
            if (stageBySlug.TryGetValue("sls-printing", out var slsStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = slsStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Build,
                    SetupDurationMode = DurationMode.None,
                    RunDurationMode = DurationMode.PerBuild,
                    DurationFromBuildConfig = true,
                    AssignedMachineId = MachineIntId("M4-1"),
                    RequiresSpecificMachine = false,
                    IsRequired = true,
                    IsBlocking = true,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            if (stageBySlug.TryGetValue("depowdering", out var depowStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = depowStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Build,
                    SetupDurationMode = DurationMode.PerBuild,
                    SetupTimeMinutes = 15,
                    RunDurationMode = DurationMode.PerBuild,
                    RunTimeMinutes = 45,
                    AssignedMachineId = MachineIntId("INC1"),
                    IsRequired = true,
                    IsBlocking = true,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            if (stageBySlug.TryGetValue("wire-edm", out var edmStage))
            {
                var wireEdmPs = new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = edmStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Build,
                    SetupDurationMode = DurationMode.PerBuild,
                    SetupTimeMinutes = 25,
                    RunDurationMode = DurationMode.PerBuild,
                    RunTimeMinutes = 90,
                    AssignedMachineId = MachineIntId("EDM1"),
                    IsRequired = true,
                    IsBlocking = true,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                };
                processStages.Add(wireEdmPs);
            }

            // === PART-LEVEL STAGES ===
            if (stageBySlug.TryGetValue("cnc-turning", out var turningStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = turningStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Part,
                    SetupDurationMode = DurationMode.None,
                    RunDurationMode = DurationMode.PerPart,
                    RunTimeMinutes = 6,
                    AssignedMachineId = MachineIntId("LATHE1"),
                    PreferredMachineIds = string.Join(",",
                        new[] { "LATHE1", "LATHE2" }
                            .Select(id => MachineIntId(id))
                            .Where(id => id.HasValue)
                            .Select(id => id!.Value)),
                    IsRequired = true,
                    IsBlocking = true,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            // === BATCH-LEVEL STAGES ===
            if (stageBySlug.TryGetValue("laser-engraving", out var engStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = engStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Batch,
                    SetupDurationMode = DurationMode.None,
                    RunDurationMode = DurationMode.PerBatch,
                    RunTimeMinutes = 5,
                    BatchCapacityOverride = 36,
                    AssignedMachineId = MachineIntId("ENGRAVE1"),
                    RequiresSerialNumber = true,
                    IsRequired = true,
                    IsBlocking = true,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            if (stageBySlug.TryGetValue("sandblasting", out var sbStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = sbStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Batch,
                    SetupDurationMode = DurationMode.None,
                    RunDurationMode = DurationMode.PerBatch,
                    RunTimeMinutes = 0,
                    BatchCapacityOverride = 20,
                    AssignedMachineId = MachineIntId("BLAST1"),
                    AllowRebatching = true,
                    IsRequired = true,
                    IsBlocking = false,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            if (stageBySlug.TryGetValue("qc", out var qcStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = qcStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Part,
                    SetupDurationMode = DurationMode.None,
                    RunDurationMode = DurationMode.PerPart,
                    RunTimeMinutes = 2,
                    AssignedMachineId = MachineIntId("QC1"),
                    RequiresQualityCheck = true,
                    IsRequired = true,
                    IsBlocking = true,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            if (stageBySlug.TryGetValue("packaging", out var pkgStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = pkgStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Batch,
                    SetupDurationMode = DurationMode.None,
                    RunDurationMode = DurationMode.PerBatch,
                    RunTimeMinutes = 3,
                    BatchCapacityOverride = 50,
                    AssignedMachineId = MachineIntId("PACK1"),
                    IsRequired = true,
                    IsBlocking = false,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            db.ProcessStages.AddRange(processStages);
            await db.SaveChangesAsync();

            // Set plate release to the wire EDM stage (last build-level stage)
            var wireEdmProcessStage = processStages
                .FirstOrDefault(ps => ps.ProductionStage?.StageSlug == "wire-edm"
                    || (stageBySlug.TryGetValue("wire-edm", out var ws) && ps.ProductionStageId == ws.Id));
            if (wireEdmProcessStage != null)
            {
                process.PlateReleaseStageId = wireEdmProcessStage.Id;
                await db.SaveChangesAsync();
            }
        }
    }

    // ──────────────────────────────────────────────
    //  Work Instructions & Sign-Off Checklists
    // ──────────────────────────────────────────────
    private static async Task SeedWorkInstructionsAsync(TenantDbContext db)
    {
        if (await db.WorkInstructions.AnyAsync()) return;

        var parts = await db.Parts.ToListAsync();
        if (parts.Count == 0) return;

        var stages = await db.ProductionStages.ToDictionaryAsync(s => s.StageSlug, s => s);

        // Stage-specific sign-off step templates keyed by stage slug.
        // Each tuple: (title, body, warningText?, tipText?, requiresSignoff)
        var stageStepTemplates = new Dictionary<string, List<(string Title, string Body, string? Warning, string? Tip, bool RequiresSignoff)>>
        {
            ["sls-printing"] =
            [
                ("Verify powder lot & material cert", "Confirm the powder lot number matches the job traveler and material certificate is on file.", "Using incorrect powder lot will result in full batch rejection.", "Check the lot label on the hopper against the traveler.", true),
                ("Confirm build file loaded", "Load the correct .sli/.cls build file and verify part orientation matches the approved layout.", null, "Double-check the file hash if available.", true),
                ("Set machine parameters", "Verify laser power, scan speed, layer thickness, and hatch spacing match the approved parameter set for this material.", "Deviations require engineering sign-off.", null, false),
                ("Inspect build plate surface", "Ensure the build plate is clean, flat, and free of residual material from previous builds.", null, null, true),
                ("Start inert atmosphere", "Purge the build chamber with argon until O₂ level is below 0.1%.", "Do not start the build if O₂ exceeds 0.1%.", null, true),
                ("Confirm build start", "Initiate the build and record the start time on the traveler.", null, null, true),
            ],
            ["depowdering"] =
            [
                ("Verify build plate ID", "Confirm the build plate serial matches the job traveler before starting depowdering.", null, null, true),
                ("Don required PPE", "Ensure respirator, gloves, and face shield are worn before opening the build chamber.", "Titanium powder is a respiratory and fire hazard.", null, true),
                ("Remove loose powder", "Use the depowdering station to remove all loose powder from the build plate and part cavities.", null, "Work from top to bottom, paying special attention to internal channels.", true),
                ("Inspect for trapped powder", "Visually and tactilely inspect all internal features for residual powder.", null, "Use compressed air for hard-to-reach areas.", true),
                ("Record powder recovery weight", "Weigh the recovered powder and record on the traveler for material tracking.", null, null, false),
            ],
            ["heat-treatment"] =
            [
                ("Verify furnace calibration", "Confirm the furnace calibration is current and temperature uniformity survey is within spec.", "Do not proceed if calibration is expired.", null, true),
                ("Load build plate into furnace", "Place the build plate with parts into the furnace in the correct orientation. Ensure thermocouple placement per procedure.", null, null, true),
                ("Set heat treatment profile", "Program the furnace with the correct temperature ramp, hold time, and cooling rate per the material spec.", "Ti-6Al-4V: 800°C ± 10°C, hold 2 hrs, furnace cool below 500°C.", null, true),
                ("Start cycle & record", "Start the heat treatment cycle and record the start time. Monitor for the first 15 minutes.", null, null, true),
                ("Verify cycle completion", "Confirm the full cycle completed without interruption. Record the actual peak temperature and hold time from the chart recorder.", null, null, true),
            ],
            ["wire-edm"] =
            [
                ("Verify build plate fixture", "Mount the build plate securely in the EDM fixture. Confirm alignment using the edge finder.", null, null, true),
                ("Load cutting program", "Load the correct EDM cutting program and verify the wire path matches the approved cut layout.", "Wrong cut program can destroy the entire plate of parts.", "Dry-run the program path before engaging the wire.", true),
                ("Thread & tension wire", "Thread the brass wire and set tension per machine spec. Verify wire diameter matches program requirements (typically 0.25mm).", null, null, true),
                ("Set flush parameters", "Configure dielectric flush pressure and nozzle positions for the cut geometry.", null, null, false),
                ("Start cut & monitor", "Begin the EDM cut. Monitor for wire breaks during the first pass. Record the start time.", null, "Keep spare wire spools nearby for quick rethread.", true),
                ("Verify parts released", "Confirm all parts are fully separated from the build plate. Inspect cut surfaces for quality.", null, null, true),
            ],
            ["sandblasting"] =
            [
                ("Verify media type & pressure", "Confirm the blast media and pressure setting match the part spec. Typical: alumina 120 grit @ 40 PSI.", null, null, true),
                ("Mask critical surfaces", "Apply masking tape or plugs to any surfaces that must not be blasted (threads, sealing faces, datum surfaces).", "Blasting datum surfaces will cause CNC setup issues.", null, true),
                ("Blast all surfaces uniformly", "Blast all exposed surfaces to achieve uniform matte finish. Rotate parts to cover all angles.", null, null, true),
                ("Inspect finish quality", "Visually inspect that all support marks and print lines are removed. Surface should be uniformly matte.", null, null, true),
            ],
            ["cnc-machining"] =
            [
                ("Verify fixture & work holding", "Set up the correct fixture for this part number. Confirm clamping pressure and datum alignment.", null, "Use the fixture setup sheet posted at the machine.", true),
                ("Load & verify CNC program", "Load the correct G-code program. Verify tool list matches the program requirements and all tools are presettled.", "Running with wrong tool offsets will scrap the part.", null, true),
                ("Set work coordinate zero", "Touch off the part and set the work coordinate system origin. Verify against the setup sheet dimensions.", null, null, true),
                ("Run first article", "Machine the first part and measure all critical dimensions before running the batch.", null, "Document all first article measurements on the inspection form.", true),
                ("Confirm dimensions in tolerance", "Verify all critical dimensions are within the drawing tolerances using calibrated instruments.", null, null, true),
                ("Run remaining parts", "Continue machining remaining parts. Spot-check every 5th part.", null, null, false),
                ("Deburr & clean", "Remove all burrs, sharp edges, and machining chips. Clean parts with solvent before passing to next stage.", null, null, true),
            ],
            ["cnc-turning"] =
            [
                ("Verify chuck setup & bar stock", "Set up the correct collet/chuck for the bar diameter. Verify material matches the traveler.", null, null, true),
                ("Load & verify turning program", "Load the correct CNC turning program. Verify all tool offsets and turret positions.", null, null, true),
                ("Set work coordinate zero", "Face-off and set Z-zero. Confirm bar stick-out per the setup sheet.", null, null, true),
                ("Run first article", "Turn the first part and measure all critical diameters, lengths, and thread specs.", null, null, true),
                ("Confirm dimensions in tolerance", "Verify all dimensions are within drawing tolerances.", null, null, true),
                ("Run remaining parts & deburr", "Continue the batch. Deburr parting-line witness marks.", null, null, false),
            ],
            ["laser-engraving"] =
            [
                ("Verify serial number sequence", "Confirm the serial number range on the traveler. Verify the engraving file has the correct sequence loaded.", "Duplicate or out-of-sequence serials cause traceability failures.", null, true),
                ("Align part in fixture", "Place the part in the engraving fixture. Verify the engraving location matches the drawing callout.", null, null, true),
                ("Set laser parameters", "Confirm power, speed, and frequency settings for the material. Use the approved parameter set.", null, "Titanium: lower power, slower speed to avoid heat discoloration.", false),
                ("Engrave & verify readability", "Run the engraving cycle. Verify each serial is legible and correctly positioned using a magnifier.", null, null, true),
            ],
            ["surface-finishing"] =
            [
                ("Verify finishing specification", "Confirm the required surface finish (Ra value) and process (tumble, polish, bead blast) per the drawing.", null, null, true),
                ("Prepare finishing media/compounds", "Load the correct media or compound for the specified finish. Check media condition.", null, null, false),
                ("Process parts", "Run the finishing cycle per the approved time and parameters.", null, null, true),
                ("Inspect surface finish", "Measure the surface roughness with a profilometer if specified. Visual inspection for uniform finish.", null, null, true),
            ],
            ["qc"] =
            [
                ("Visual inspection", "Inspect all parts for visible defects: cracks, porosity, surface imperfections, incomplete features.", null, null, true),
                ("Dimensional inspection", "Measure all critical dimensions per the inspection plan. Record on the dimensional report.", null, "Use calibrated instruments only. Check calibration stickers.", true),
                ("Thread / feature check", "Verify all threads with go/no-go gauges. Check all assembly features with mating components if available.", null, null, true),
                ("Material cert & lot traceability", "Verify the material certificate is on file and the lot number chain is complete from powder to finished part.", "Parts without complete traceability must be quarantined.", null, true),
                ("Record inspection results", "Complete the inspection report. Stamp or sign the traveler.", null, null, true),
            ],
            ["shipping"] =
            [
                ("Verify packing list", "Confirm all parts on the packing list are present and match the work order quantities.", null, null, true),
                ("Include required documentation", "Attach Certificate of Conformance, material certs, and inspection reports as required by the PO.", null, null, true),
                ("Package parts securely", "Wrap or bag parts individually. Use appropriate dunnage to prevent damage in transit.", null, "Titanium parts should be wrapped in VCI paper.", true),
            ],
            ["packaging"] =
            [
                ("Verify order completeness", "Count all parts and compare against the packing list and work order.", null, null, true),
                ("Apply part labels", "Attach serialized labels or tags to each part or bag as required.", null, null, true),
                ("Package for shipment", "Place parts in the shipping container with protective packaging. Seal and label the outer box.", null, null, true),
            ],
            ["assembly"] =
            [
                ("Verify all components present", "Check the bill of materials against the kit. All sub-components must be QC-passed before assembly.", null, null, true),
                ("Follow assembly sequence", "Assemble per the approved work instruction sequence. Do not skip steps.", null, null, true),
                ("Torque all fasteners", "Apply the specified torque values to all fasteners. Use a calibrated torque wrench.", "Under/over-torqued fasteners are a safety-critical defect.", null, true),
                ("Verify function & fit", "Perform the functional check per the assembly procedure. Verify all fits and clearances.", null, null, true),
            ],
            ["oil-sleeve"] =
            [
                ("Verify sleeve dimensions", "Measure the sleeve ID/OD and confirm fit against the mating component.", null, null, true),
                ("Apply lubricant", "Apply the specified oil or lubricant per the procedure. Ensure even coverage.", null, null, true),
                ("Press-fit sleeve", "Press the sleeve into position using the arbor press. Verify final seated position.", null, null, true),
                ("Verify assembly", "Confirm the sleeve is fully seated and the assembly moves freely.", null, null, true),
            ],
            ["external-coating"] =
            [
                ("Verify coating specification", "Confirm the coating type, thickness, and spec per the drawing (e.g., Cerakote, DLC, anodize).", null, null, true),
                ("Prepare parts for shipping", "Clean parts per the coating vendor's pre-treatment requirements. Package to prevent contamination in transit.", null, null, true),
                ("Record outgoing parts", "Log the parts, quantities, and PO number being sent to the coating vendor.", null, null, true),
                ("Inspect returned parts", "When parts return, inspect coating thickness, adhesion, and appearance before accepting.", null, null, true),
            ],
        };

        foreach (var part in parts)
        {
            // Get the stages assigned to this part via its manufacturing process
            var processStageIds = await db.ProcessStages
                .Include(ps => ps.ProductionStage)
                .Where(ps => ps.ManufacturingProcess!.PartId == part.Id)
                .OrderBy(ps => ps.ExecutionOrder)
                .Select(ps => ps.ProductionStage!)
                .ToListAsync();

            if (processStageIds.Count == 0) continue;

            foreach (var stage in processStageIds)
            {
                if (!stageStepTemplates.TryGetValue(stage.StageSlug, out var templates))
                    continue;

                var instruction = new WorkInstruction
                {
                    PartId = part.Id,
                    ProductionStageId = stage.Id,
                    Title = $"{stage.Name} — {part.PartNumber}",
                    Description = $"Standard work instructions and sign-off checklist for {stage.Name} on {part.Name}.",
                    RevisionNumber = 1,
                    IsActive = true,
                    CreatedByUserId = "System",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                db.WorkInstructions.Add(instruction);
                await db.SaveChangesAsync();

                var stepOrder = 1;
                foreach (var (title, body, warning, tip, requiresSignoff) in templates)
                {
                    db.WorkInstructionSteps.Add(new WorkInstructionStep
                    {
                        WorkInstructionId = instruction.Id,
                        StepOrder = stepOrder++,
                        Title = title,
                        Body = body,
                        WarningText = warning,
                        TipText = tip,
                        RequiresOperatorSignoff = requiresSignoff,
                    });
                }

                await db.SaveChangesAsync();
            }
        }
    }

    // ──────────────────────────────────────────────
    //  Build Templates (reusable build file library)
    // ──────────────────────────────────────────────
    private static async Task SeedBuildTemplatesAsync(TenantDbContext db)
    {
        if (await db.BuildTemplates.AnyAsync()) return;

        // Only seed if parts and machines already exist
        var parts = await db.Parts.ToListAsync();
        if (parts.Count == 0) return;

        var tiMaterial = await db.Materials.FirstOrDefaultAsync(m => m.Name.StartsWith("Ti-6Al-4V"));
        var ssMaterial = await db.Materials.FirstOrDefaultAsync(m => m.Name.StartsWith("316L"));
        var m4Machine = await db.Machines.FirstOrDefaultAsync(m => m.MachineId == "M4-1");

        // Group parts by material for realistic template assignments
        var tiParts = parts.Where(p => p.MaterialId == tiMaterial?.Id).Take(3).ToList();
        var ssParts = parts.Where(p => p.MaterialId == ssMaterial?.Id).Take(2).ToList();

        // Fall back to first available parts if material filtering yields nothing
        if (tiParts.Count == 0 && parts.Count > 0)
            tiParts = parts.Take(Math.Min(2, parts.Count)).ToList();

        var templates = new List<BuildTemplate>();

        // Template 1: Certified Ti-6Al-4V build with slicer data
        if (tiParts.Count > 0)
        {
            var t1 = new BuildTemplate
            {
                Name = "Ti64 Standard Build",
                Description = "Standard Ti-6Al-4V build plate — certified and ready for scheduling.",
                Status = BuildTemplateStatus.Certified,
                MaterialId = tiMaterial?.Id,
                StackLevel = 1,
                EstimatedDurationHours = 18.5,
                FileName = "Ti64_Standard_v3.sli",
                LayerCount = 2800,
                BuildHeightMm = 84.0,
                EstimatedPowderKg = 12.5,
                SlicerSoftware = "EOSPRINT 2",
                SlicerVersion = "2.12.1",
                BuildParameters = System.Text.Json.JsonSerializer.Serialize(new
                {
                    laserPower = 370,
                    scanSpeed = 1200,
                    layerThickness = 0.03,
                    hatchSpacing = 0.12,
                    contourCount = 2
                }),
                CertifiedBy = "System",
                CertifiedDate = DateTime.UtcNow.AddDays(-7),
                UseCount = 3,
                LastUsedDate = DateTime.UtcNow.AddDays(-2),
                CreatedBy = "System",
                LastModifiedBy = "System"
            };
            db.BuildTemplates.Add(t1);
            await db.SaveChangesAsync();

            var positions = new List<object>();
            var order = 0;
            foreach (var part in tiParts)
            {
                db.BuildTemplateParts.Add(new BuildTemplatePart
                {
                    BuildTemplateId = t1.Id,
                    PartId = part.Id,
                    Quantity = 20,
                    StackLevel = 1,
                    PositionNotes = $"Row {order + 1}"
                });
                positions.Add(new { partId = part.Id, x = 25.0 + order * 45.0, y = 25.0, rotation = 0 });
                order++;
            }
            t1.PartPositionsJson = System.Text.Json.JsonSerializer.Serialize(positions);
            await db.SaveChangesAsync();

            templates.Add(t1);
        }

        // Template 2: Draft 316L build (not yet certified)
        if (ssParts.Count > 0)
        {
            var t2 = new BuildTemplate
            {
                Name = "316L Bracket Build",
                Description = "316L stainless bracket plate — draft, pending slicer validation.",
                Status = BuildTemplateStatus.Draft,
                MaterialId = ssMaterial?.Id,
                StackLevel = 1,
                EstimatedDurationHours = 12.0,
                CreatedBy = "System",
                LastModifiedBy = "System"
            };
            db.BuildTemplates.Add(t2);
            await db.SaveChangesAsync();

            foreach (var part in ssParts)
            {
                db.BuildTemplateParts.Add(new BuildTemplatePart
                {
                    BuildTemplateId = t2.Id,
                    PartId = part.Id,
                    Quantity = 30,
                    StackLevel = 1
                });
            }
            await db.SaveChangesAsync();

            templates.Add(t2);
        }

        // Template 3: Double-stack Ti-6Al-4V for high-volume runs
        if (tiParts.Count > 0 && m4Machine != null)
        {
            var t3 = new BuildTemplate
            {
                Name = "Ti64 Double-Stack High Volume",
                Description = "Double-stacked Ti-6Al-4V build for maximum plate utilization.",
                Status = BuildTemplateStatus.Certified,
                MaterialId = tiMaterial?.Id,
                StackLevel = 2,
                EstimatedDurationHours = 32.0,
                FileName = "Ti64_DoubleStack_v1.sli",
                LayerCount = 4200,
                BuildHeightMm = 126.0,
                EstimatedPowderKg = 22.8,
                SlicerSoftware = "EOSPRINT 2",
                SlicerVersion = "2.12.1",
                BuildParameters = System.Text.Json.JsonSerializer.Serialize(new
                {
                    laserPower = 370,
                    scanSpeed = 1200,
                    layerThickness = 0.03,
                    hatchSpacing = 0.12,
                    contourCount = 2,
                    stackHeight2 = 63.0
                }),
                CertifiedBy = "System",
                CertifiedDate = DateTime.UtcNow.AddDays(-3),
                UseCount = 1,
                LastUsedDate = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "System",
                LastModifiedBy = "System"
            };
            db.BuildTemplates.Add(t3);
            await db.SaveChangesAsync();

            // Stack level 1
            foreach (var part in tiParts)
            {
                db.BuildTemplateParts.Add(new BuildTemplatePart
                {
                    BuildTemplateId = t3.Id,
                    PartId = part.Id,
                    Quantity = 20,
                    StackLevel = 1,
                    PositionNotes = "Bottom layer"
                });
            }
            // Stack level 2
            foreach (var part in tiParts)
            {
                db.BuildTemplateParts.Add(new BuildTemplatePart
                {
                    BuildTemplateId = t3.Id,
                    PartId = part.Id,
                    Quantity = 20,
                    StackLevel = 2,
                    PositionNotes = "Top layer"
                });
            }
            await db.SaveChangesAsync();

            templates.Add(t3);
        }
    }

    /// <summary>
    /// Seeds demo data: 4 EMC Silencers suppressor variants (Tinman, Handyman, Gargoyle, Pilate),
    /// 10 work orders across 3 distributors, 14 completed builds, 2 active builds, 3 ready builds.
    /// Based on real EOS M4 ONYX DMLS production specs: Ti-6Al-4V at 60um, ~$88 cost/part.
    /// </summary>
    private static async Task SeedSchedulerDemoDataAsync(TenantDbContext db)
    {
        if (await db.Jobs.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var slsApproach = await db.ManufacturingApproaches.FirstOrDefaultAsync(a => a.Slug == "sls-based");
        var suppApproach = await db.ManufacturingApproaches.FirstOrDefaultAsync(a => a.Slug == "suppressor-no-ht");
        var tiMat = await db.Materials.FirstOrDefaultAsync(m => m.Name.StartsWith("Ti-6Al-4V"));
        var stages = await db.ProductionStages.ToDictionaryAsync(s => s.StageSlug, s => s);
        var machines = await db.Machines.Where(m => m.IsActive).ToDictionaryAsync(m => m.MachineId, m => m);
        int? Mid(string id) => machines.TryGetValue(id, out var m) ? m.Id : null;
        var appId = suppApproach?.Id ?? slsApproach?.Id;

        // Backfill approach FK on orphan parts
        if (slsApproach != null)
        {
            var orphans = await db.Parts.Where(p => p.ManufacturingApproachId == null && p.IsActive).ToListAsync();
            foreach (var p in orphans) p.ManufacturingApproachId = slsApproach.Id;
            if (orphans.Count > 0) await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════
        // PARTS — EMC Silencers Phase 1 product line (all Ti-6Al-4V, DMLS)
        //
        //   Tinman:    7.62mm multi-purpose  — 56/build, 22.5 hrs (stackable DS: 80, 30h)
        //   Handyman:  9mm multi-purpose     — 64/build, 20.0 hrs
        //   Gargoyle:  5.56mm AR-specific    — 72/build, 18.5 hrs
        //   Pilate:    .22lr rimfire         — 96/build, 16.0 hrs (stackable DS: 144, 22h)
        // ════════════════════════════════════════════════════════════

        async Task<Part> EnsurePart(string pn, string name, string desc,
            bool stacking, int ppsS, double durS, int? ppsD = null, double? durD = null)
        {
            var part = await db.Parts.Include(p => p.AdditiveBuildConfig).FirstOrDefaultAsync(p => p.PartNumber == pn);
            if (part == null)
            {
                part = new Part { PartNumber = pn, Name = name, Description = desc,
                    Material = "Ti-6Al-4V Grade 5", MaterialId = tiMat?.Id, ManufacturingApproachId = appId,
                    IsActive = true, CreatedBy = "System", LastModifiedBy = "System" };
                db.Parts.Add(part);
                await db.SaveChangesAsync();
            }
            else if (part.ManufacturingApproachId == null)
            {
                part.ManufacturingApproachId = appId;
                await db.SaveChangesAsync();
            }
            if (part.AdditiveBuildConfig == null)
            {
                var bc = new PartAdditiveBuildConfig { PartId = part.Id, AllowStacking = stacking,
                    MaxStackCount = stacking ? 2 : 1, PlannedPartsPerBuildSingle = ppsS,
                    SingleStackDurationHours = durS, EnableDoubleStack = stacking, EnableTripleStack = false };
                if (stacking && ppsD.HasValue) { bc.PlannedPartsPerBuildDouble = ppsD.Value; bc.DoubleStackDurationHours = durD; }
                db.Set<PartAdditiveBuildConfig>().Add(bc);
                await db.SaveChangesAsync();
            }
            return part;
        }

        var tinman = await EnsurePart("EMC-TIN-001", "Tinman 7.62mm Suppressor",
            "Ti-6Al-4V multi-purpose 7.62mm silencer — DMLS printed, EDM cut, CNC finished. 7.25\" L x 1.75\" D. MSRP $599",
            true, 56, 22.5, 80, 30.0);

        var handyman = await EnsurePart("EMC-HAN-001", "Handyman 9mm Suppressor",
            "Ti-6Al-4V multi-purpose 9mm silencer — DMLS printed, EDM cut, CNC finished. 6.75\" L x 1.75\" D. MSRP $599",
            false, 64, 20.0);

        var gargoyle = await EnsurePart("EMC-GAR-001", "Gargoyle 5.56mm Suppressor",
            "Ti-6Al-4V AR-specific 5.56mm silencer — DMLS printed, EDM cut, CNC finished. 5.0\" L x 1.75\" D. MSRP $599",
            false, 72, 18.5);

        var pilate = await EnsurePart("EMC-PIL-001", "Pilate .22LR Suppressor",
            "Ti-6Al-4V .22LR rimfire silencer — DMLS printed, EDM cut, CNC finished. 5.5\" L x 1.25\" D. MSRP $299",
            true, 96, 16.0, 144, 22.0);

        // ════════════════════════════════════════════════════════════
        // PART PRICING — sell prices for profit dashboard
        // ════════════════════════════════════════════════════════════
        foreach (var (part, sellPrice, matCost, margin) in new[] {
            (tinman,   599.00m, 42.00m, 35.0m),
            (handyman, 599.00m, 38.00m, 35.0m),
            (gargoyle, 599.00m, 35.00m, 35.0m),
            (pilate,   299.00m, 22.00m, 40.0m) })
        {
            if (!await db.Set<PartPricing>().AnyAsync(pp => pp.PartId == part.Id))
            {
                db.Set<PartPricing>().Add(new PartPricing
                {
                    PartId = part.Id,
                    SellPricePerUnit = sellPrice,
                    MaterialCostPerUnit = matCost,
                    TargetMarginPct = margin,
                    MinimumOrderQty = 1,
                    Currency = "USD",
                    EffectiveDate = now.AddDays(-90),
                    CreatedBy = "System", LastModifiedBy = "System"
                });
            }
        }
        await db.SaveChangesAsync();

        // ════════════════════════════════════════════════════════════
        // MANUFACTURING PROCESSES — one per part, all Suppressor (No HT)
        // SLS → Depowder → Wire EDM → CNC Turning → Laser Engraving → Sandblasting → QC → Packaging
        // ════════════════════════════════════════════════════════════

        // Part-to-lathe assignment: Tinman+Handyman → LATHE1, Gargoyle+Pilate → LATHE2
        var partLatheMap = new Dictionary<int, string> {
            { tinman.Id, "LATHE1" }, { handyman.Id, "LATHE1" },
            { gargoyle.Id, "LATHE2" }, { pilate.Id, "LATHE2" }
        };

        (string slug, ProcessingLevel lvl, double? setup, double? run, string machineId, bool slicer, bool release, int? batch)[]
            SuppRouting(string latheId) => [
            ("sls-printing",    ProcessingLevel.Build, null, null, "M4-1",    true,  false, null),
            ("depowdering",     ProcessingLevel.Build, 15,  45,   "INC1",    false, false, null),
            ("wire-edm",        ProcessingLevel.Build, 25,  90,   "EDM1",    false, true,  null),
            ("cnc-turning",     ProcessingLevel.Part,  null, 6,   latheId,   false, false, null),
            ("laser-engraving", ProcessingLevel.Batch, null, 5,   "ENGRAVE1",false, false, 36),
            ("sandblasting",    ProcessingLevel.Batch, null, 0,   "BLAST1",  false, false, 20),
            ("qc",              ProcessingLevel.Part,  null, 2,   "QC1",     false, false, null),
            ("packaging",       ProcessingLevel.Batch, null, 3,   "PACK1",   false, false, 50),
        ];

        foreach (var part in new[] { tinman, handyman, gargoyle, pilate })
        {
            if (await db.ManufacturingProcesses.AnyAsync(p => p.PartId == part.Id && p.IsActive)) continue;
            var proc = new ManufacturingProcess { PartId = part.Id, ManufacturingApproachId = appId,
                Name = $"{part.PartNumber} — Suppressor (No HT)", Description = $"Manufacturing process for {part.Name}",
                DefaultBatchCapacity = 72, IsActive = true, Version = 1,
                CreatedBy = "System", LastModifiedBy = "System", CreatedDate = now, LastModifiedDate = now };
            db.ManufacturingProcesses.Add(proc);
            await db.SaveChangesAsync();
            var ord = 1;
            var latheId = partLatheMap.GetValueOrDefault(part.Id, "LATHE1");
            foreach (var r in SuppRouting(latheId))
            {
                if (!stages.TryGetValue(r.slug, out var stg)) continue;
                var durationMode = r.lvl switch {
                    ProcessingLevel.Build => DurationMode.PerBuild,
                    ProcessingLevel.Batch => DurationMode.PerBatch,
                    _ => DurationMode.PerPart
                };
                var setupMode = r.setup.HasValue ? durationMode : DurationMode.None;
                db.ProcessStages.Add(new ProcessStage { ManufacturingProcessId = proc.Id, ProductionStageId = stg.Id,
                    ExecutionOrder = ord++, ProcessingLevel = r.lvl,
                    SetupDurationMode = setupMode, SetupTimeMinutes = r.setup,
                    RunDurationMode = durationMode, RunTimeMinutes = r.run,
                    DurationFromBuildConfig = r.slicer, AssignedMachineId = Mid(r.machineId),
                    BatchCapacityOverride = r.batch, IsRequired = true, IsBlocking = true,
                    CreatedBy = "System", LastModifiedBy = "System" });
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════
        // WORK ORDERS — 10 orders across 3 distributors
        //   Silencer Shop, Capitol Armory, Silencer Central
        // ════════════════════════════════════════════════════════════

        var wos = new List<WorkOrder>();

        async Task<WorkOrder> CreateWO(string num, string customer, string po, int daysAgo, int dueDays,
            WorkOrderStatus status, JobPriority priority, (Part part, int qty)[] lines)
        {
            var wo = new WorkOrder { OrderNumber = num, CustomerName = customer, CustomerPO = po,
                OrderDate = now.AddDays(-daysAgo), DueDate = now.AddDays(dueDays),
                ShipByDate = now.AddDays(dueDays - 2), Status = status, Priority = priority,
                CreatedBy = "System", LastModifiedBy = "System" };
            // Note: LastModifiedDate for completed WOs is fixed up at the end of SeedSchedulerDemoDataAsync
            // to avoid EF change tracking resetting it during subsequent saves
            db.WorkOrders.Add(wo);
            await db.SaveChangesAsync();
            foreach (var (part, qty) in lines)
            {
                db.Set<WorkOrderLine>().Add(new WorkOrderLine { WorkOrderId = wo.Id, PartId = part.Id,
                    Quantity = qty, Status = status });
            }
            await db.SaveChangesAsync();
            wos.Add(wo);
            return wo;
        }

        // ── Completed & shipped ──
        var wo1 = await CreateWO("WO-00001", "Silencer Shop",     "SS-2026-1001", 60, -42,
            WorkOrderStatus.Complete, JobPriority.Normal,
            [(tinman, 112)]);                                      // 2× Tinman builds (56/build)

        var wo2 = await CreateWO("WO-00002", "Capitol Armory",    "CA-2026-0341", 52, -35,
            WorkOrderStatus.Complete, JobPriority.Normal,
            [(handyman, 192)]);                                    // 3× Handyman builds (64/build)

        var wo3 = await CreateWO("WO-00003", "Silencer Central",  "SC-2026-0080", 45, -28,
            WorkOrderStatus.Complete, JobPriority.High,
            [(gargoyle, 144)]);                                    // 2× Gargoyle builds (72/build)

        var wo4 = await CreateWO("WO-00004", "Silencer Shop",     "SS-2026-1102", 38, -2,
            WorkOrderStatus.Complete, JobPriority.Normal,
            [(tinman, 168), (pilate, 96)]);                        // 3× Tinman + 1× Pilate

        // ── In progress ──
        var wo5 = await CreateWO("WO-00005", "Capitol Armory",    "CA-2026-0512", 18, 8,
            WorkOrderStatus.InProgress, JobPriority.High,
            [(tinman, 112), (handyman, 64)]);                      // 2× Tinman + 1× Handyman

        var wo6 = await CreateWO("WO-00006", "Silencer Central",  "SC-2026-1201", 10, 16,
            WorkOrderStatus.InProgress, JobPriority.Normal,
            [(gargoyle, 144)]);                                    // 2× Gargoyle, 1 done, 1 printing

        var wo7 = await CreateWO("WO-00007", "Silencer Shop",     "SS-2026-0120", 6, 22,
            WorkOrderStatus.InProgress, JobPriority.Normal,
            [(pilate, 144)]);                                      // 1× Pilate DS (144), in depowder

        // ── Released — needs scheduling ──
        var wo8 = await CreateWO("WO-00008", "Capitol Armory",    "CA-2026-1301", 3, 28,
            WorkOrderStatus.Released, JobPriority.High,
            [(tinman, 224), (gargoyle, 72)]);                      // 4× Tinman + 1× Gargoyle

        var wo9 = await CreateWO("WO-00009", "Silencer Central",  "SC-2026-0601", 2, 14,
            WorkOrderStatus.Released, JobPriority.Rush,
            [(handyman, 192)]);                                    // 3× Handyman needed (urgent)

        var wo10 = await CreateWO("WO-00010", "Silencer Shop",    "SS-2026-0201", 1, 35,
            WorkOrderStatus.Released, JobPriority.Normal,
            [(gargoyle, 144), (pilate, 96), (tinman, 56)]);        // mixed order, all 4 types

        // ════════════════════════════════════════════════════════════
        // BUILD PLATE PROGRAMS — master templates
        // ════════════════════════════════════════════════════════════

        var slicerParams = System.Text.Json.JsonSerializer.Serialize(new {
            laserPower = 370, scanSpeed = 1200, layerThickness = 0.06, hatchSpacing = 0.12, contourCount = 2 });

        async Task<MachineProgram> CreateProgram(string num, string name, ProgramType type, ProgramScheduleStatus schedStatus,
            int? matId, double printHrs, int layers, double heightMm, double powderKg, string slicerFile,
            bool locked, DateTime? schedDate, DateTime? printStart, DateTime? printEnd, DateTime? plateRel,
            int? sourceId, string? machineKey = null)
        {
            var prog = new MachineProgram { ProgramNumber = num, Name = name, ProgramType = type,
                Status = ProgramStatus.Active, ScheduleStatus = schedStatus, MachineType = "SLS",
                MaterialId = matId, EstimatedPrintHours = printHrs, LayerCount = layers,
                BuildHeightMm = heightMm, EstimatedPowderKg = powderKg, SlicerFileName = slicerFile,
                SlicerSoftware = "EOSPRINT 2", SlicerVersion = "2.12.1", Parameters = slicerParams,
                IsLocked = locked, ScheduledDate = schedDate, PrintStartedAt = printStart,
                PrintCompletedAt = printEnd, PlateReleasedAt = plateRel, SourceProgramId = sourceId,
                MachineId = machineKey != null ? Mid(machineKey) : null,
                CreatedBy = "System", LastModifiedBy = "System" };
            db.MachinePrograms.Add(prog);
            await db.SaveChangesAsync();
            return prog;
        }

        async Task LinkProgParts(MachineProgram prog, (Part part, int qty, int stack, WorkOrder wo)[] items)
        {
            foreach (var (part, qty, stack, wo) in items)
            {
                var woLine = await db.Set<WorkOrderLine>().FirstOrDefaultAsync(l => l.WorkOrderId == wo.Id && l.PartId == part.Id);
                db.ProgramParts.Add(new ProgramPart { MachineProgramId = prog.Id, PartId = part.Id,
                    Quantity = qty, StackLevel = stack, WorkOrderLineId = woLine?.Id });
            }
            await db.SaveChangesAsync();
        }

        // ── Masters (reusable templates, not scheduled) ──
        var masterTinman = await CreateProgram("BP-00001", "Tinman 56x", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 22.5, 3067, 184.0, 16.2, "EMC_Tinman_56x_v1.sli",
            false, null, null, null, null, null);
        await LinkProgParts(masterTinman, [(tinman, 56, 1, wo8)]);

        var masterTinmanDs = await CreateProgram("BP-00002", "Tinman 80x Double", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 30.0, 4083, 245.0, 22.5, "EMC_Tinman_80x_DS_v1.sli",
            false, null, null, null, null, null);
        await LinkProgParts(masterTinmanDs, [(tinman, 40, 1, wo8), (tinman, 40, 2, wo8)]);

        var masterHandyman = await CreateProgram("BP-00003", "Handyman 64x", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 20.0, 2856, 171.0, 14.0, "EMC_Handyman_64x_v1.sli",
            false, null, null, null, null, null);
        await LinkProgParts(masterHandyman, [(handyman, 64, 1, wo9)]);

        var masterGargoyle = await CreateProgram("BP-00004", "Gargoyle 72x", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 18.5, 2117, 127.0, 11.5, "EMC_Gargoyle_72x_v1.sli",
            false, null, null, null, null, null);
        await LinkProgParts(masterGargoyle, [(gargoyle, 72, 1, wo8)]);

        var masterPilate = await CreateProgram("BP-00005", "Pilate 96x", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 16.0, 2333, 140.0, 9.8, "EMC_Pilate_96x_v1.sli",
            false, null, null, null, null, null);
        await LinkProgParts(masterPilate, [(pilate, 96, 1, wo10)]);

        var masterPilateDs = await CreateProgram("BP-00006", "Pilate 144x Double", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 22.0, 3217, 193.0, 14.5, "EMC_Pilate_144x_DS_v1.sli",
            false, null, null, null, null, null);
        await LinkProgParts(masterPilateDs, [(pilate, 72, 1, wo10), (pilate, 72, 2, wo10)]);

        // ── CNC Turning Programs — one per part with specific tool lists ──
        // Each suppressor caliber needs different bore bars and thread mills.
        // Shared tools: T1 OD insert, chamfer, part-off are common across all.

        async Task<MachineProgram> CreateCncProgram(string progNum, string name, Part part, string latheId,
            (string pos, string toolName, bool isFixture)[] tools)
        {
            var prog = new MachineProgram
            {
                ProgramNumber = progNum, Name = name,
                ProgramType = ProgramType.Standard, Status = ProgramStatus.Active,
                ScheduleStatus = ProgramScheduleStatus.Ready, MachineType = "CNC-Turning",
                MachineId = Mid(latheId), PartId = part.Id,
                SetupTimeMinutes = 12, RunTimeMinutes = 6, CycleTimeMinutes = 6,
                IsLocked = true, CreatedBy = "System", LastModifiedBy = "System"
            };
            db.MachinePrograms.Add(prog);
            await db.SaveChangesAsync();

            db.ProgramParts.Add(new ProgramPart { MachineProgramId = prog.Id, PartId = part.Id, Quantity = 1, StackLevel = 1 });

            var order = 1;
            foreach (var (pos, toolName, isFixture) in tools)
            {
                db.Set<ProgramToolingItem>().Add(new ProgramToolingItem
                {
                    MachineProgramId = prog.Id, ToolPosition = pos, Name = toolName,
                    IsFixture = isFixture, SortOrder = order++, IsActive = true,
                    WearLifeHours = isFixture ? null : 40.0, WarningThresholdPercent = 80,
                    CreatedBy = "System", LastModifiedBy = "System"
                });
            }
            await db.SaveChangesAsync();
            return prog;
        }

        // Tinman 7.62mm → LATHE1
        var cncTinman = await CreateCncProgram("EMC-TIN-001-TURN-01", "Tinman 7.62mm CNC Turning", tinman, "LATHE1",
        [
            ("T1", "CNMG 120408 Face/Turn OD Insert", false),
            ("T2", "7.62mm Carbide Bore Bar", false),
            ("T3", "5/8-24 UNEF Thread Mill", false),
            ("T4", "2mm ID Groove Tool", false),
            ("T5", "45° Chamfer Tool", false),
            ("T6", "3mm Part-Off Blade", false),
        ]);

        // Handyman 9mm → LATHE1
        var cncHandyman = await CreateCncProgram("EMC-HAN-001-TURN-01", "Handyman 9mm CNC Turning", handyman, "LATHE1",
        [
            ("T1", "CNMG 120408 Face/Turn OD Insert", false),
            ("T2", "9mm Carbide Bore Bar", false),
            ("T3", "5/8-24 UNEF Thread Mill", false),
            ("T4", "2mm ID Groove Tool", false),
            ("T5", "45° Chamfer Tool", false),
            ("T6", "3mm Part-Off Blade", false),
            ("T7", "M13.5x1 LH Thread Tap", false),
        ]);

        // Gargoyle 5.56mm → LATHE2
        var cncGargoyle = await CreateCncProgram("EMC-GAR-001-TURN-01", "Gargoyle 5.56mm CNC Turning", gargoyle, "LATHE2",
        [
            ("T1", "CNMG 120408 Face/Turn OD Insert", false),
            ("T2", "5.56mm Carbide Bore Bar", false),
            ("T3", "1/2-28 UNEF Thread Mill", false),
            ("T4", "2mm ID Groove Tool", false),
            ("T5", "45° Chamfer Tool", false),
            ("T6", "3mm Part-Off Blade", false),
        ]);

        // Pilate .22LR → LATHE2
        var cncPilate = await CreateCncProgram("EMC-PIL-001-TURN-01", "Pilate .22LR CNC Turning", pilate, "LATHE2",
        [
            ("T1", "CNMG 120408 Face/Turn OD Insert", false),
            ("T2", ".22 Cal Carbide Bore Bar", false),
            ("T3", "1/2-28 UNEF Thread Mill", false),
            ("T4", "45° Chamfer Tool", false),
            ("T5", "3mm Part-Off Blade", false),
        ]);

        // Map part → CNC program for use in routing and dispatch
        var partCncPrograms = new Dictionary<int, MachineProgram> {
            { tinman.Id, cncTinman }, { handyman.Id, cncHandyman },
            { gargoyle.Id, cncGargoyle }, { pilate.Id, cncPilate }
        };

        // Link ProcessStage.MachineProgramId for CNC Turning stages to per-part programs
        var cncTurningStageId = stages.TryGetValue("cnc-turning", out var cncStg) ? cncStg.Id : 0;
        if (cncTurningStageId > 0)
        {
            var cncProcessStages = await db.ProcessStages
                .Include(ps => ps.ManufacturingProcess)
                .Where(ps => ps.ProductionStageId == cncTurningStageId)
                .ToListAsync();
            foreach (var ps in cncProcessStages)
            {
                if (ps.ManufacturingProcess != null
                    && partCncPrograms.TryGetValue(ps.ManufacturingProcess.PartId, out var prog))
                {
                    ps.MachineProgramId = prog.Id;
                }
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════
        // STAGE ROUTING TEMPLATES — per-build cost/time definitions
        // Routing: SLS → Depowder → Wire EDM → CNC Turning → Laser Engraving → Sandblasting → QC → Packaging
        // CNC Turning: 10min setup + 5min run = 15min/part @ $90/hr
        // (stageSlug, defaultMachineId, estimatedHours, estimatedCost)
        // ════════════════════════════════════════════════════════════

        // Tinman 56x: 7.62mm — 56 parts × 6min/part CNC = 5.6h → LATHE1
        var tinmanRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-1", 22.5, 1913m), ("depowdering", "INC1", 1.0, 55m),
            ("wire-edm", "EDM1", 1.92, 163m), ("cnc-turning", "LATHE1", 5.6, 504m),
            ("laser-engraving", "ENGRAVE1", 0.14, 8m), ("sandblasting", "BLAST1", 0.0, 0m),
            ("qc", "QC1", 1.87, 140m), ("packaging", "PACK1", 0.06, 2m) };

        // Tinman 80x DS: double-stack — 80 parts × 6min = 8.0h → LATHE1
        var tinmanDsRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-1", 30.0, 2550m), ("depowdering", "INC1", 1.0, 55m),
            ("wire-edm", "EDM1", 1.92, 163m), ("cnc-turning", "LATHE1", 8.0, 720m),
            ("laser-engraving", "ENGRAVE1", 0.19, 10m), ("sandblasting", "BLAST1", 0.0, 0m),
            ("qc", "QC1", 2.67, 200m), ("packaging", "PACK1", 0.08, 3m) };

        // Handyman 64x: 9mm — 64 parts × 6min = 6.4h → LATHE1
        var handymanRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-1", 20.0, 1700m), ("depowdering", "INC1", 1.0, 55m),
            ("wire-edm", "EDM1", 1.92, 163m), ("cnc-turning", "LATHE1", 6.4, 576m),
            ("laser-engraving", "ENGRAVE1", 0.15, 8m), ("sandblasting", "BLAST1", 0.0, 0m),
            ("qc", "QC1", 2.13, 160m), ("packaging", "PACK1", 0.06, 2m) };

        // Gargoyle 72x: 5.56mm — 72 parts × 6min = 7.2h → LATHE2
        var gargoyleRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-1", 18.5, 1573m), ("depowdering", "INC1", 1.0, 55m),
            ("wire-edm", "EDM1", 1.92, 163m), ("cnc-turning", "LATHE2", 7.2, 648m),
            ("laser-engraving", "ENGRAVE1", 0.17, 9m), ("sandblasting", "BLAST1", 0.0, 0m),
            ("qc", "QC1", 2.4, 180m), ("packaging", "PACK1", 0.07, 2m) };

        // Pilate 96x: .22lr — 96 parts × 6min = 9.6h → LATHE2
        var pilateRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-1", 16.0, 1360m), ("depowdering", "INC1", 1.0, 55m),
            ("wire-edm", "EDM1", 1.92, 163m), ("cnc-turning", "LATHE2", 9.6, 864m),
            ("laser-engraving", "ENGRAVE1", 0.22, 12m), ("sandblasting", "BLAST1", 0.0, 0m),
            ("qc", "QC1", 3.2, 240m), ("packaging", "PACK1", 0.1, 3m) };

        // Pilate 144x DS: double-stack — 144 parts × 6min = 14.4h → LATHE2
        var pilateDsRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-1", 22.0, 1870m), ("depowdering", "INC1", 1.0, 55m),
            ("wire-edm", "EDM1", 1.92, 163m), ("cnc-turning", "LATHE2", 14.4, 1296m),
            ("laser-engraving", "ENGRAVE1", 0.33, 18m), ("sandblasting", "BLAST1", 0.0, 0m),
            ("qc", "QC1", 4.8, 360m), ("packaging", "PACK1", 0.15, 5m) };

        // ════════════════════════════════════════════════════════════
        // JOBS + STAGE EXECUTIONS — helpers
        // ════════════════════════════════════════════════════════════

        // Shift-aware scheduling: operator stages only run Mon-Fri 06:00-18:00.
        // SLS printing runs 24/7 unmanned; everything else needs operators.
        static DateTime SkipWeekend(DateTime dt)
        {
            // If Saturday, advance to Monday 06:00
            if (dt.DayOfWeek == DayOfWeek.Saturday)
                return dt.Date.AddDays(2).AddHours(6);
            // If Sunday, advance to Monday 06:00
            if (dt.DayOfWeek == DayOfWeek.Sunday)
                return dt.Date.AddDays(1).AddHours(6);
            return dt;
        }

        // Advance a time past weekends and outside shift hours (before 6am → wait for 6am)
        static DateTime NextShiftStart(DateTime dt)
        {
            dt = SkipWeekend(dt);
            // If before 6am, wait until 6am same day
            if (dt.Hour < 6)
                dt = dt.Date.AddHours(6);
            // If after 18:00, advance to next weekday 6am
            if (dt.Hour >= 18)
            {
                dt = dt.Date.AddDays(1).AddHours(6);
                dt = SkipWeekend(dt);
            }
            return dt;
        }

        int jobNum = 1;

        async Task<Job> CreateJob(Part part, int? machineId, int qty, JobScope scope, JobStatus status,
            DateTime schedStart, DateTime schedEnd, DateTime? actStart, DateTime? actEnd,
            WorkOrder? wo, int produced = 0)
        {
            var woLine = wo != null
                ? await db.Set<WorkOrderLine>().FirstOrDefaultAsync(l => l.WorkOrderId == wo.Id && l.PartId == part.Id)
                : null;
            var job = new Job { JobNumber = $"JOB-{jobNum++:D5}", PartId = part.Id, MachineId = machineId,
                Scope = scope, Status = status, Priority = wo?.Priority ?? JobPriority.Normal,
                Quantity = qty, ProducedQuantity = produced,
                ScheduledStart = schedStart, ScheduledEnd = schedEnd,
                ActualStart = actStart, ActualEnd = actEnd,
                WorkOrderLineId = woLine?.Id, CreatedBy = "System", LastModifiedBy = "System" };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            return job;
        }

        async Task CreateStageExec(Job job, string stageSlug, string machineId, StageExecutionStatus status,
            DateTime? schedStart, DateTime? schedEnd, DateTime? actStart, DateTime? actEnd,
            double estHours, double? actHours, decimal estCost, decimal? actCost,
            int? machineProgramId = null)
        {
            if (!stages.TryGetValue(stageSlug, out var stg)) return;
            db.StageExecutions.Add(new StageExecution { JobId = job.Id, ProductionStageId = stg.Id,
                MachineId = Mid(machineId), Status = status,
                ScheduledStartAt = schedStart, ScheduledEndAt = schedEnd,
                ActualStartAt = actStart, ActualEndAt = actEnd,
                EstimatedHours = estHours, ActualHours = actHours,
                EstimatedCost = estCost, ActualCost = actCost,
                MachineProgramId = machineProgramId,
                QualityCheckRequired = stageSlug == "qc",
                QualityCheckPassed = status == StageExecutionStatus.Completed && stageSlug == "qc" ? true : null,
                CreatedBy = "System", LastModifiedBy = "System" });
        }

        // Machine watermarks — tracks when each machine is next free (prevents overlaps)
        var machineWatermark = new Dictionary<string, DateTime>();

        // Get or update the next available time for a machine (no overlaps)
        DateTime GetMachineAvailable(string machineId, DateTime earliest)
        {
            var shifted = NextShiftStart(earliest);
            if (machineWatermark.TryGetValue(machineId, out var watermark))
            {
                var afterWatermark = NextShiftStart(watermark.AddHours(0.25)); // 15min gap between jobs
                if (afterWatermark > shifted) shifted = afterWatermark;
            }
            return shifted;
        }

        void SetMachineWatermark(string machineId, DateTime endTime)
        {
            machineWatermark[machineId] = endTime;
        }

        // ── Helper: create a fully completed build with all stage executions ──
        async Task<MachineProgram> CompletedBuild(int idx, string num, string name,
            Part part, int qty, double printHrs, int layers, double height, double powder,
            string slicer, int? sourceId, string machine, DateTime start, WorkOrder wo,
            (string slug, string mid, double hrs, decimal cost)[] routing,
            (Part p2, int qty2)? stack2 = null)
        {
            var end = start.AddHours(printHrs);
            var prog = await CreateProgram(num, name, ProgramType.BuildPlate,
                ProgramScheduleStatus.Completed, tiMat?.Id, printHrs, layers, height, powder, slicer,
                true, start, start, end, end.AddHours(1.5), sourceId, machine);

            if (stack2.HasValue)
                await LinkProgParts(prog, [(part, qty, 1, wo), (stack2.Value.p2, stack2.Value.qty2, 2, wo)]);
            else
                await LinkProgParts(prog, [(part, qty, 1, wo)]);

            var totalQty = qty + (stack2?.qty2 ?? 0);
            var downHrs = routing.Skip(1).Sum(r => r.hrs + 0.3);
            var jobEnd = end.AddHours(downHrs + 1);
            var j = await CreateJob(part, Mid(machine), totalQty, JobScope.Build, JobStatus.Completed,
                start, jobEnd, start, jobEnd.AddHours(-0.5), wo, totalQty);

            var t = start;
            var v = 1.0 + (idx % 5 - 2) * 0.01; // ±2% deterministic variation
            foreach (var (slug, mid, hrs, cost) in routing)
            {
                var actualMachine = slug == "sls-printing" ? machine : mid;
                // SLS runs 24/7; all other stages need operators and must not overlap
                if (slug != "sls-printing")
                    t = GetMachineAvailable(actualMachine, t);

                var stageEnd = t.AddHours(hrs);
                await CreateStageExec(j, slug, actualMachine, StageExecutionStatus.Completed,
                    t, stageEnd, t.AddHours(0.15), t.AddHours(hrs * v + 0.15),
                    hrs, hrs * v, cost, cost * (decimal)v,
                    slug == "sls-printing" ? prog.Id : null);
                if (slug != "sls-printing")
                    SetMachineWatermark(actualMachine, stageEnd);
                t = stageEnd.AddHours(0.15);
            }
            await db.SaveChangesAsync();
            return prog;
        }

        // ════════════════════════════════════════════════════════════
        // COMPLETED BUILDS — M4-1 (7 builds: Tinman, Gargoyle, Pilate)
        // ════════════════════════════════════════════════════════════

        int bpn = 10;

        // B1: Tinman 56x → wo1 batch 1 (started 55 days ago)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Tinman 56x — Run #1",
            tinman, 56, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli", masterTinman.Id,
            "M4-1", now.AddDays(-55), wo1, tinmanRouting);

        // B2: Tinman 56x → wo1 batch 2
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Tinman 56x — Run #2",
            tinman, 56, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli", masterTinman.Id,
            "M4-1", now.AddDays(-50), wo1, tinmanRouting);

        // B3: Gargoyle 72x → wo3 batch 1
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Gargoyle 72x — Run #1",
            gargoyle, 72, 18.5, 2117, 127, 11.5, "EMC_Gargoyle_72x_v1.sli", masterGargoyle.Id,
            "M4-1", now.AddDays(-44), wo3, gargoyleRouting);

        // B4: Tinman 56x → wo4 batch 1
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Tinman 56x — Run #3",
            tinman, 56, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli", masterTinman.Id,
            "M4-1", now.AddDays(-38), wo4, tinmanRouting);

        // B5: Tinman 56x → wo4 batch 2 (kept older for history)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Tinman 56x — Run #4",
            tinman, 56, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli", masterTinman.Id,
            "M4-1", now.AddDays(-6), wo4, tinmanRouting);

        // B6: Tinman 56x → wo5 batch 1 (visible in Gantt lookback)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Tinman 56x — Run #5",
            tinman, 56, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli", masterTinman.Id,
            "M4-1", now.AddDays(-5), wo5, tinmanRouting);

        // B7: Tinman 56x → wo5 batch 2 (visible in Gantt lookback, just before active)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Tinman 56x — Run #6",
            tinman, 56, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli", masterTinman.Id,
            "M4-1", now.AddDays(-3.5), wo5, tinmanRouting);

        // ════════════════════════════════════════════════════════════
        // COMPLETED BUILDS — M4-2 (7 builds: Handyman, Gargoyle, Pilate)
        // ════════════════════════════════════════════════════════════

        // B8: Handyman 64x → wo2 batch 1
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Handyman 64x — Run #1",
            handyman, 64, 20.0, 2856, 171, 14.0, "EMC_Handyman_64x_v1.sli", masterHandyman.Id,
            "M4-2", now.AddDays(-53), wo2, handymanRouting);

        // B9: Handyman 64x → wo2 batch 2
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Handyman 64x — Run #2",
            handyman, 64, 20.0, 2856, 171, 14.0, "EMC_Handyman_64x_v1.sli", masterHandyman.Id,
            "M4-2", now.AddDays(-48), wo2, handymanRouting);

        // B10: Handyman 64x → wo2 batch 3
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Handyman 64x — Run #3",
            handyman, 64, 20.0, 2856, 171, 14.0, "EMC_Handyman_64x_v1.sli", masterHandyman.Id,
            "M4-2", now.AddDays(-42), wo2, handymanRouting);

        // B11: Gargoyle 72x → wo3 batch 2
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Gargoyle 72x — Run #2",
            gargoyle, 72, 18.5, 2117, 127, 11.5, "EMC_Gargoyle_72x_v1.sli", masterGargoyle.Id,
            "M4-2", now.AddDays(-35), wo3, gargoyleRouting);

        // B12: Tinman 56x → wo4 batch 3 (visible in Gantt lookback)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Tinman 56x — Run #7",
            tinman, 56, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli", masterTinman.Id,
            "M4-2", now.AddDays(-6.5), wo4, tinmanRouting);

        // B13: Pilate 96x → wo4 (visible in Gantt lookback)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Pilate 96x — Run #1",
            pilate, 96, 16.0, 2333, 140, 9.8, "EMC_Pilate_96x_v1.sli", masterPilate.Id,
            "M4-2", now.AddDays(-5), wo4, pilateRouting);

        // B14: Handyman 64x → wo5 (visible in Gantt lookback, just before active)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Handyman 64x — Run #4",
            handyman, 64, 20.0, 2856, 171, 14.0, "EMC_Handyman_64x_v1.sli", masterHandyman.Id,
            "M4-2", now.AddDays(-3), wo5, handymanRouting);

        // ════════════════════════════════════════════════════════════
        // ACTIVE BUILDS — currently on the machines
        // ════════════════════════════════════════════════════════════

        // M4-1: Gargoyle 72x currently PRINTING (~60% done) → wo6
        var activeM41Start = now.AddHours(-11);
        var activeM41 = await CreateProgram($"BP-{bpn++:D5}", "Gargoyle 72x — Run #3", ProgramType.BuildPlate,
            ProgramScheduleStatus.Printing, tiMat?.Id, 18.5, 2117, 127, 11.5, "EMC_Gargoyle_72x_v1.sli",
            true, activeM41Start, activeM41Start, null, null, masterGargoyle.Id, "M4-1");
        await LinkProgParts(activeM41, [(gargoyle, 72, 1, wo6)]);
        {
            var j = await CreateJob(gargoyle, Mid("M4-1"), 72, JobScope.Build, JobStatus.InProgress,
                activeM41Start, activeM41Start.AddHours(50), activeM41Start, null, wo6);
            await CreateStageExec(j, "sls-printing", "M4-1", StageExecutionStatus.InProgress,
                activeM41Start, activeM41Start.AddHours(18.5), activeM41Start, null, 18.5, null, 1573m, null, activeM41.Id);
            var m41DepowStart = GetMachineAvailable("INC1", activeM41Start.AddHours(19.0));
            await CreateStageExec(j, "depowdering", "INC1", StageExecutionStatus.NotStarted,
                m41DepowStart, m41DepowStart.AddHours(0.8), null, null, 0.8, null, 44m, null);
            SetMachineWatermark("INC1", m41DepowStart.AddHours(0.8));
            var m41EdmStart = GetMachineAvailable("EDM1", m41DepowStart.AddHours(1.0));
            await CreateStageExec(j, "wire-edm", "EDM1", StageExecutionStatus.NotStarted,
                m41EdmStart, m41EdmStart.AddHours(1.7), null, null, 1.7, null, 145m, null);
            SetMachineWatermark("EDM1", m41EdmStart.AddHours(1.7));
            await db.SaveChangesAsync();
        }

        // M4-2: Pilate 144x DS in POST-PRINT → wo7 (print done ~6h ago, in depowder)
        var activeM42Start = now.AddHours(-28);
        var activeM42End = activeM42Start.AddHours(22.0);
        var activeM42 = await CreateProgram($"BP-{bpn++:D5}", "Pilate 144x DS — Run #1", ProgramType.BuildPlate,
            ProgramScheduleStatus.PostPrint, tiMat?.Id, 22.0, 3217, 193, 14.5, "EMC_Pilate_144x_DS_v1.sli",
            true, activeM42Start, activeM42Start, activeM42End, null, masterPilateDs.Id, "M4-2");
        await LinkProgParts(activeM42, [(pilate, 72, 1, wo7), (pilate, 72, 2, wo7)]);
        {
            var j = await CreateJob(pilate, Mid("M4-2"), 144, JobScope.Build, JobStatus.InProgress,
                activeM42Start, activeM42Start.AddHours(60), activeM42Start, null, wo7);
            // print completed
            await CreateStageExec(j, "sls-printing", "M4-2", StageExecutionStatus.Completed,
                activeM42Start, activeM42Start.AddHours(22.0), activeM42Start, activeM42End,
                22.0, 22.0, 1870m, 1870m, activeM42.Id);
            // depowder in progress (started 3 hours ago)
            var m42DepowStart = GetMachineAvailable("INC1", activeM42End.AddHours(0.5));
            await CreateStageExec(j, "depowdering", "INC1", StageExecutionStatus.InProgress,
                m42DepowStart, m42DepowStart.AddHours(1.0), now.AddHours(-3), null,
                1.0, null, 55m, null);
            SetMachineWatermark("INC1", m42DepowStart.AddHours(1.0));
            // wire-edm not started yet
            var m42EdmStart = GetMachineAvailable("EDM1", m42DepowStart.AddHours(1.2));
            await CreateStageExec(j, "wire-edm", "EDM1", StageExecutionStatus.NotStarted,
                m42EdmStart, m42EdmStart.AddHours(1.92), null, null, 1.92, null, 163m, null);
            SetMachineWatermark("EDM1", m42EdmStart.AddHours(1.92));
            // cnc-turning not started (Pilate → LATHE2, 144 parts × 6min = 14.4h)
            var m42CncStart = GetMachineAvailable("LATHE2", m42EdmStart.AddHours(2.1));
            await CreateStageExec(j, "cnc-turning", "LATHE2", StageExecutionStatus.NotStarted,
                m42CncStart, m42CncStart.AddHours(14.4), null, null, 14.4, null, 1296m, null);
            SetMachineWatermark("LATHE2", m42CncStart.AddHours(14.4));
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════
        // SCHEDULED BUILDS — queued for the near future (visible on Gantt lookahead)
        // These have ScheduledDate set so they render as bars on the Gantt.
        // ════════════════════════════════════════════════════════════

        // ── Helper: calculate next SLS build start after changeover ──
        // Auto-changeover is 30min. If the previous build ends outside operator shift,
        // the machine goes DOWN until an operator can clear the cooldown chamber.
        static DateTime NextBuildStart(DateTime previousBuildEnd)
        {
            const double changeoverHours = 0.5; // 30 minutes

            // Check if the changeover window (when operator must unload cooldown) falls in shift
            bool duringShift = previousBuildEnd.DayOfWeek != DayOfWeek.Saturday
                && previousBuildEnd.DayOfWeek != DayOfWeek.Sunday
                && previousBuildEnd.Hour >= 6 && previousBuildEnd.Hour < 18;

            if (duringShift)
            {
                // Operator available — safe changeover, next build starts after 30min
                return previousBuildEnd.AddHours(changeoverHours);
            }
            else
            {
                // No operator — machine goes DOWN until next shift start, then 30min changeover
                var nextShift = NextShiftStart(previousBuildEnd);
                return nextShift.AddHours(changeoverHours);
            }
        }

        // ── Helper: create all downstream stage executions for a scheduled build ──
        async Task CreateScheduledDownstream(Job job, DateTime printEnd,
            (string slug, string mid, double hrs, decimal cost)[] routing, int? progId = null)
        {
            // Skip sls-printing (index 0) — already created by caller
            var t = printEnd.AddHours(0.5);
            foreach (var (slug, mid, hrs, cost) in routing.Skip(1))
            {
                // Find earliest slot: after predecessor AND after this machine is free
                t = GetMachineAvailable(mid, t);
                var end = t.AddHours(hrs);
                await CreateStageExec(job, slug, mid, StageExecutionStatus.NotStarted,
                    t, end, null, null, hrs, null, cost, null);
                SetMachineWatermark(mid, end);
                t = end.AddHours(0.15); // small gap before next stage
            }
            await db.SaveChangesAsync();
        }

        // M4-1 next up: Tinman 56x → wo8 (starts after active Gargoyle finishes + changeover)
        var m41PrevEnd = activeM41Start.AddHours(18.5); // Gargoyle finishes
        var m41NextStart = NextBuildStart(m41PrevEnd);
        var schedM41a = await CreateProgram($"BP-{bpn++:D5}", "Tinman 56x — Run #8", ProgramType.BuildPlate,
            ProgramScheduleStatus.Scheduled, tiMat?.Id, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli",
            false, m41NextStart, null, null, null, masterTinman.Id, "M4-1");
        await LinkProgParts(schedM41a, [(tinman, 56, 1, wo8)]);
        {
            var j = await CreateJob(tinman, Mid("M4-1"), 56, JobScope.Build, JobStatus.Scheduled,
                m41NextStart, m41NextStart.AddHours(50), null, null, wo8);
            await CreateStageExec(j, "sls-printing", "M4-1", StageExecutionStatus.NotStarted,
                m41NextStart, m41NextStart.AddHours(22.5), null, null, 22.5, null, 1913m, null, schedM41a.Id);
            await CreateScheduledDownstream(j, m41NextStart.AddHours(22.5), tinmanRouting, schedM41a.Id);
        }

        // M4-1 after that: Gargoyle 72x → wo8 (starts after Tinman)
        var m41Next2Start = NextBuildStart(m41NextStart.AddHours(22.5));
        var schedM41b = await CreateProgram($"BP-{bpn++:D5}", "Gargoyle 72x — Run #4", ProgramType.BuildPlate,
            ProgramScheduleStatus.Scheduled, tiMat?.Id, 18.5, 2117, 127, 11.5, "EMC_Gargoyle_72x_v1.sli",
            false, m41Next2Start, null, null, null, masterGargoyle.Id, "M4-1");
        await LinkProgParts(schedM41b, [(gargoyle, 72, 1, wo8)]);
        {
            var j = await CreateJob(gargoyle, Mid("M4-1"), 72, JobScope.Build, JobStatus.Scheduled,
                m41Next2Start, m41Next2Start.AddHours(50), null, null, wo8);
            await CreateStageExec(j, "sls-printing", "M4-1", StageExecutionStatus.NotStarted,
                m41Next2Start, m41Next2Start.AddHours(18.5), null, null, 18.5, null, 1573m, null, schedM41b.Id);
            await CreateScheduledDownstream(j, m41Next2Start.AddHours(18.5), gargoyleRouting, schedM41b.Id);
        }

        // M4-1 third: Tinman 56x → wo8 (4th Tinman build for wo8's 224 total)
        var m41Next3Start = NextBuildStart(m41Next2Start.AddHours(18.5));
        var schedM41c = await CreateProgram($"BP-{bpn++:D5}", "Tinman 56x — Run #9", ProgramType.BuildPlate,
            ProgramScheduleStatus.Scheduled, tiMat?.Id, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli",
            false, m41Next3Start, null, null, null, masterTinman.Id, "M4-1");
        await LinkProgParts(schedM41c, [(tinman, 56, 1, wo8)]);
        {
            var j = await CreateJob(tinman, Mid("M4-1"), 56, JobScope.Build, JobStatus.Scheduled,
                m41Next3Start, m41Next3Start.AddHours(50), null, null, wo8);
            await CreateStageExec(j, "sls-printing", "M4-1", StageExecutionStatus.NotStarted,
                m41Next3Start, m41Next3Start.AddHours(22.5), null, null, 22.5, null, 1913m, null, schedM41c.Id);
            await CreateScheduledDownstream(j, m41Next3Start.AddHours(22.5), tinmanRouting, schedM41c.Id);
        }

        // M4-2 next up: Handyman 64x → wo9 (starts after Pilate DS changeover)
        // M4-2 print finished at activeM42End, use changeover logic
        var m42NextStart = NextBuildStart(activeM42End);
        var schedM42a = await CreateProgram($"BP-{bpn++:D5}", "Handyman 64x — Run #5", ProgramType.BuildPlate,
            ProgramScheduleStatus.Scheduled, tiMat?.Id, 20.0, 2856, 171, 14.0, "EMC_Handyman_64x_v1.sli",
            false, m42NextStart, null, null, null, masterHandyman.Id, "M4-2");
        await LinkProgParts(schedM42a, [(handyman, 64, 1, wo9)]);
        {
            var j = await CreateJob(handyman, Mid("M4-2"), 64, JobScope.Build, JobStatus.Scheduled,
                m42NextStart, m42NextStart.AddHours(50), null, null, wo9);
            await CreateStageExec(j, "sls-printing", "M4-2", StageExecutionStatus.NotStarted,
                m42NextStart, m42NextStart.AddHours(20.0), null, null, 20.0, null, 1700m, null, schedM42a.Id);
            await CreateScheduledDownstream(j, m42NextStart.AddHours(20.0), handymanRouting, schedM42a.Id);
        }

        // M4-2 after that: Handyman 64x → wo9 batch 2
        var m42Next2Start = NextBuildStart(m42NextStart.AddHours(20.0));
        var schedM42b = await CreateProgram($"BP-{bpn++:D5}", "Handyman 64x — Run #6", ProgramType.BuildPlate,
            ProgramScheduleStatus.Scheduled, tiMat?.Id, 20.0, 2856, 171, 14.0, "EMC_Handyman_64x_v1.sli",
            false, m42Next2Start, null, null, null, masterHandyman.Id, "M4-2");
        await LinkProgParts(schedM42b, [(handyman, 64, 1, wo9)]);
        {
            var j = await CreateJob(handyman, Mid("M4-2"), 64, JobScope.Build, JobStatus.Scheduled,
                m42Next2Start, m42Next2Start.AddHours(50), null, null, wo9);
            await CreateStageExec(j, "sls-printing", "M4-2", StageExecutionStatus.NotStarted,
                m42Next2Start, m42Next2Start.AddHours(20.0), null, null, 20.0, null, 1700m, null, schedM42b.Id);
            await CreateScheduledDownstream(j, m42Next2Start.AddHours(20.0), handymanRouting, schedM42b.Id);
        }

        // M4-2 third: Handyman 64x → wo9 batch 3 (last of 3 needed for 192 total)
        var m42Next3Start = NextBuildStart(m42Next2Start.AddHours(20.0));
        var schedM42c = await CreateProgram($"BP-{bpn++:D5}", "Handyman 64x — Run #7", ProgramType.BuildPlate,
            ProgramScheduleStatus.Scheduled, tiMat?.Id, 20.0, 2856, 171, 14.0, "EMC_Handyman_64x_v1.sli",
            false, m42Next3Start, null, null, null, masterHandyman.Id, "M4-2");
        await LinkProgParts(schedM42c, [(handyman, 64, 1, wo9)]);
        {
            var j = await CreateJob(handyman, Mid("M4-2"), 64, JobScope.Build, JobStatus.Scheduled,
                m42Next3Start, m42Next3Start.AddHours(50), null, null, wo9);
            await CreateStageExec(j, "sls-printing", "M4-2", StageExecutionStatus.NotStarted,
                m42Next3Start, m42Next3Start.AddHours(20.0), null, null, 20.0, null, 1700m, null, schedM42c.Id);
            await CreateScheduledDownstream(j, m42Next3Start.AddHours(20.0), handymanRouting, schedM42c.Id);
        }

        // ════════════════════════════════════════════════════════════
        // READY BUILDS — prepared but not yet scheduled
        // These have no ScheduledDate — available for the Next Build Advisor
        // ════════════════════════════════════════════════════════════

        // Tinman 56x ready (for remaining wo8 demand after scheduled builds)
        var readyM41 = await CreateProgram($"BP-{bpn++:D5}", "Tinman 56x — Run #10", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 22.5, 3067, 184, 16.2, "EMC_Tinman_56x_v1.sli",
            false, null, null, null, null, masterTinman.Id);
        await LinkProgParts(readyM41, [(tinman, 56, 1, wo8)]);

        // Gargoyle 72x ready (for wo10)
        var readyM41b = await CreateProgram($"BP-{bpn++:D5}", "Gargoyle 72x — Run #5", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 18.5, 2117, 127, 11.5, "EMC_Gargoyle_72x_v1.sli",
            false, null, null, null, null, masterGargoyle.Id);
        await LinkProgParts(readyM41b, [(gargoyle, 72, 1, wo10)]);

        // ════════════════════════════════════════════════════════════
        // LINK CNC PROGRAMS TO STAGE EXECUTIONS — enables tool-based dispatch
        // ════════════════════════════════════════════════════════════
        if (cncTurningStageId > 0)
        {
            var cncExecs = await db.StageExecutions
                .Include(se => se.Job)
                .Where(se => se.ProductionStageId == cncTurningStageId && se.MachineProgramId == null)
                .ToListAsync();
            foreach (var se in cncExecs)
            {
                if (se.Job != null && partCncPrograms.TryGetValue(se.Job.PartId, out var prog))
                    se.MachineProgramId = prog.Id;
            }
            if (cncExecs.Any()) await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════
        // UPDATE WO LINE QUANTITIES — mark produced/shipped
        // ════════════════════════════════════════════════════════════

        async Task UpdateLine(WorkOrder wo, Part part, int produced, int? shipped = null)
        {
            var line = await db.Set<WorkOrderLine>().FirstOrDefaultAsync(l => l.WorkOrderId == wo.Id && l.PartId == part.Id);
            if (line != null)
            {
                line.ProducedQuantity = produced;
                if (shipped.HasValue) line.ShippedQuantity = shipped.Value;
            }
        }

        // wo1: Complete — 112× Tinman (2 builds on M4-1, all shipped)
        await UpdateLine(wo1, tinman, 112, 112);

        // wo2: Complete — 192× Handyman (3 builds on M4-2, all shipped)
        await UpdateLine(wo2, handyman, 192, 192);

        // wo3: Complete — 144× Gargoyle (1 M4-1 + 1 M4-2, all shipped)
        await UpdateLine(wo3, gargoyle, 144, 144);

        // wo4: Complete — 168× Tinman (2 M4-1 + 1 M4-2) + 96× Pilate (1 M4-2), all shipped
        await UpdateLine(wo4, tinman, 168, 168);
        await UpdateLine(wo4, pilate, 96, 96);

        // wo5: InProgress — 112× Tinman done (2 M4-1), 64× Handyman done (1 M4-2)
        await UpdateLine(wo5, tinman, 112);
        await UpdateLine(wo5, handyman, 64);

        // wo6: InProgress — 72 Gargoyle produced (1 completed build on M4-2 via wo3 leftover),
        //      72 more printing on M4-1
        await UpdateLine(wo6, gargoyle, 72);

        // wo7: InProgress — 144× Pilate in post-print (depowdering DS build on M4-2)
        // (nothing "produced" yet — still on the plate)

        // wo8-10: Released — nothing produced yet

        // ── Fix up completed WO dates for On-Time Delivery ──
        // Must happen AFTER all jobs/builds are created to avoid EF change tracking overwriting
        var completedWOs = await db.WorkOrders
            .Where(w => w.Status == WorkOrderStatus.Complete)
            .ToListAsync();
        foreach (var cwo in completedWOs)
        {
            cwo.LastModifiedDate = cwo.DueDate.AddDays(-2);
            cwo.ActualShipDate = cwo.DueDate.AddDays(-3);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedDispatchDemoDataAsync(TenantDbContext db)
    {
        if (await db.DispatchConfigurations.AnyAsync()) return;

        var machines = await db.Machines.Where(m => m.IsActive).ToListAsync();
        if (machines.Count == 0) return;

        // Global dispatch configuration
        db.DispatchConfigurations.Add(new DispatchConfiguration
        {
            AutoDispatchEnabled = false,
            MaxQueueDepth = 3,
            LookAheadHours = 8,
            DueDateWeight = 0.45m,
            ChangeoverPenaltyWeight = 0.35m,
            ThroughputWeight = 0.20m,
            MaintenanceBufferHours = 4,
            RequiresVerification = true,
            AutoAssignOperator = false,
            NotifyOnDispatch = true,
            BatchGroupingWindowHours = 4,
            RequiresSchedulerApproval = true
        });

        // Per-machine configs for SLS machines
        foreach (var machine in machines.Where(m => m.IsAdditiveMachine))
        {
            db.DispatchConfigurations.Add(new DispatchConfiguration
            {
                MachineId = machine.Id,
                AutoDispatchEnabled = true,
                MaxQueueDepth = 2,
                LookAheadHours = 12,
                DueDateWeight = 0.40m,
                ChangeoverPenaltyWeight = 0.40m,
                ThroughputWeight = 0.20m,
                MaintenanceBufferHours = 6,
                RequiresVerification = false,
                AutoAssignOperator = true,
                NotifyOnDispatch = true,
                BatchGroupingWindowHours = 8,
                RequiresSchedulerApproval = false
            });
        }

        // Per-machine configs for CNC Turning lathes (auto-dispatch with part-specific setup)
        var cncTurningStage = await db.ProductionStages.FirstOrDefaultAsync(s => s.StageSlug == "cnc-turning");
        foreach (var latheId in new[] { "LATHE1", "LATHE2" })
        {
            var lathe = machines.FirstOrDefault(m => m.MachineId == latheId);
            if (lathe != null)
            {
                db.DispatchConfigurations.Add(new DispatchConfiguration
                {
                    MachineId = lathe.Id,
                    ProductionStageId = cncTurningStage?.Id,
                    AutoDispatchEnabled = true,
                    MaxQueueDepth = 3,
                    LookAheadHours = 24,
                    DueDateWeight = 0.35m,
                    ChangeoverPenaltyWeight = 0.45m,
                    ThroughputWeight = 0.20m,
                    MaintenanceBufferHours = 4,
                    RequiresVerification = true,
                    AutoAssignOperator = true,
                    NotifyOnDispatch = true,
                    BatchGroupingWindowHours = 12,
                    RequiresSchedulerApproval = false
                });
            }
        }

        // Seed operator setup profiles for demo operator
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == "Admin");
        var operator1 = await db.Users.FirstOrDefaultAsync(u => u.Username == "operator1");
        var operator2 = await db.Users.FirstOrDefaultAsync(u => u.Username == "operator2");

        if (admin != null)
        {
            foreach (var machine in machines.Take(3))
            {
                db.OperatorSetupProfiles.Add(new OperatorSetupProfile
                {
                    UserId = admin.Id,
                    MachineId = machine.Id,
                    AverageSetupMinutes = 35 + (machine.Id % 3) * 10,
                    SampleCount = 8,
                    VarianceMinutes = 5 + (machine.Id % 3) * 2,
                    FastestSetupMinutes = 20 + (machine.Id % 3) * 5,
                    ProficiencyLevel = 4,
                    IsPreferred = machine.IsAdditiveMachine
                });
            }
        }

        // Lathe-specific operator setup profiles — trained operators for each part/lathe combo
        var lathe1 = machines.FirstOrDefault(m => m.MachineId == "LATHE1");
        var lathe2 = machines.FirstOrDefault(m => m.MachineId == "LATHE2");

        // LATHE1 setup profiles: Tinman + Handyman (operator1 is primary, admin backup)
        if (lathe1 != null)
        {
            foreach (var (user, proficiency, isPreferred) in new[] {
                (operator1, 5, true), (admin, 3, false) })
            {
                if (user == null) continue;
                db.OperatorSetupProfiles.Add(new OperatorSetupProfile
                {
                    UserId = user.Id,
                    MachineId = lathe1.Id,
                    AverageSetupMinutes = 12,
                    SampleCount = 25,
                    VarianceMinutes = 3,
                    FastestSetupMinutes = 8,
                    ProficiencyLevel = proficiency,
                    IsPreferred = isPreferred
                });
            }
        }

        // LATHE2 setup profiles: Gargoyle + Pilate (operator2 is primary, admin backup)
        if (lathe2 != null)
        {
            foreach (var (user, proficiency, isPreferred) in new[] {
                (operator2, 5, true), (admin, 3, false) })
            {
                if (user == null) continue;
                db.OperatorSetupProfiles.Add(new OperatorSetupProfile
                {
                    UserId = user.Id,
                    MachineId = lathe2.Id,
                    AverageSetupMinutes = 10,
                    SampleCount = 30,
                    VarianceMinutes = 2,
                    FastestSetupMinutes = 7,
                    ProficiencyLevel = proficiency,
                    IsPreferred = isPreferred
                });
            }
        }

        // Seed completed dispatches + history for analytics
        var programs = await db.MachinePrograms.Take(3).ToListAsync();
        // Set CurrentProgramId on lathes (what's currently loaded)
        // LATHE1 has Tinman program loaded, LATHE2 has Gargoyle program loaded
        var lathe1Db = await db.Machines.FirstOrDefaultAsync(m => m.MachineId == "LATHE1");
        var lathe2Db = await db.Machines.FirstOrDefaultAsync(m => m.MachineId == "LATHE2");
        var cncTinmanProg = await db.MachinePrograms.FirstOrDefaultAsync(p => p.ProgramNumber == "EMC-TIN-001-TURN-01");
        var cncGargoyleProg = await db.MachinePrograms.FirstOrDefaultAsync(p => p.ProgramNumber == "EMC-GAR-001-TURN-01");
        if (lathe1Db != null && cncTinmanProg != null) { lathe1Db.CurrentProgramId = cncTinmanProg.Id; lathe1Db.SetupState = MachineSetupState.SetUp; }
        if (lathe2Db != null && cncGargoyleProg != null) { lathe2Db.CurrentProgramId = cncGargoyleProg.Id; lathe2Db.SetupState = MachineSetupState.SetUp; }
        await db.SaveChangesAsync();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
        {
            var machine = machines[i % machines.Count];
            var program = programs.Count > 0 ? programs[i % programs.Count] : null;
            var completedAt = now.AddDays(-(i * 2));
            var setupMinutes = 25.0 + (i * 3) % 40;

            var dispatch = new SetupDispatch
            {
                DispatchNumber = $"DSP-SEED-{i + 1:D4}",
                MachineId = machine.Id,
                MachineProgramId = program?.Id,
                DispatchType = i % 3 == 0 ? DispatchType.Changeover : DispatchType.Setup,
                Status = DispatchStatus.Completed,
                Priority = 50,
                EstimatedSetupMinutes = setupMinutes,
                ActualSetupMinutes = setupMinutes,
                QueuedAt = completedAt.AddHours(-2),
                StartedAt = completedAt.AddMinutes(-setupMinutes),
                CompletedAt = completedAt,
                AssignedOperatorId = admin?.Id,
                CreatedBy = "seed"
            };
            db.SetupDispatches.Add(dispatch);
            await db.SaveChangesAsync();

            db.SetupHistories.Add(new SetupHistory
            {
                SetupDispatchId = dispatch.Id,
                MachineId = machine.Id,
                MachineProgramId = program?.Id,
                OperatorUserId = admin?.Id,
                SetupDurationMinutes = setupMinutes,
                ChangeoverDurationMinutes = i % 3 == 0 ? 15 + i : null,
                WasChangeover = i % 3 == 0,
                CompletedAt = completedAt,
                QualityResult = i % 5 == 0 ? "conditional" : "pass"
            });
        }

        // Lathe-specific dispatches — completed setups for CNC Turning program
        // LATHE1: Tinman + Handyman setups (10 completed dispatches)
        // LATHE2: Gargoyle + Pilate setups (10 completed dispatches)
        // Lookup per-part CNC programs for dispatch history
        var cncHandymanProg = await db.MachinePrograms.FirstOrDefaultAsync(p => p.ProgramNumber == "EMC-HAN-001-TURN-01");
        var cncPilateProg = await db.MachinePrograms.FirstOrDefaultAsync(p => p.ProgramNumber == "EMC-PIL-001-TURN-01");
        var latheDispProgMap = new Dictionary<string, MachineProgram?> {
            { "Tinman", cncTinmanProg }, { "Handyman", cncHandymanProg },
            { "Gargoyle", cncGargoyleProg }, { "Pilate", cncPilateProg }
        };

        var latheDispatches = new[] {
            (lathe1, operator1, "Tinman",   12.0, 10.5),
            (lathe1, operator1, "Handyman", 11.0, 10.0),
            (lathe1, operator1, "Tinman",   12.5, 11.0),
            (lathe1, operator1, "Handyman", 10.5, 9.5),
            (lathe1, operator1, "Tinman",   11.5, 10.0),
            (lathe2, operator2, "Gargoyle",  9.0,  8.0),
            (lathe2, operator2, "Pilate",    8.5,  7.5),
            (lathe2, operator2, "Gargoyle", 10.0,  8.5),
            (lathe2, operator2, "Pilate",    9.5,  8.0),
            (lathe2, operator2, "Gargoyle",  9.0,  7.0),
        };
        for (int i = 0; i < latheDispatches.Length; i++)
        {
            var (lathe, op, partName, estMin, actMin) = latheDispatches[i];
            if (lathe == null) continue;
            var completedAt = now.AddDays(-(i * 3 + 1));
            var isChangeover = i > 0 && latheDispatches[i].Item3 != latheDispatches[i - 1].Item3
                && latheDispatches[i].Item1 == latheDispatches[i - 1].Item1;
            latheDispProgMap.TryGetValue(partName, out var dispProg);

            var dispatch = new SetupDispatch
            {
                DispatchNumber = $"DSP-LATHE-{i + 1:D4}",
                MachineId = lathe.Id,
                MachineProgramId = dispProg?.Id,
                DispatchType = isChangeover ? DispatchType.Changeover : DispatchType.Setup,
                Status = DispatchStatus.Completed,
                Priority = 60,
                EstimatedSetupMinutes = estMin,
                ActualSetupMinutes = actMin,
                QueuedAt = completedAt.AddHours(-1),
                StartedAt = completedAt.AddMinutes(-actMin),
                CompletedAt = completedAt,
                AssignedOperatorId = op?.Id,
                CreatedBy = "seed"
            };
            db.SetupDispatches.Add(dispatch);
            await db.SaveChangesAsync();

            db.SetupHistories.Add(new SetupHistory
            {
                SetupDispatchId = dispatch.Id,
                MachineId = lathe.Id,
                MachineProgramId = dispProg?.Id,
                OperatorUserId = op?.Id,
                SetupDurationMinutes = actMin,
                ChangeoverDurationMinutes = isChangeover ? actMin * 0.4 : null,
                WasChangeover = isChangeover,
                CompletedAt = completedAt,
                QualityResult = "pass"
            });
        }

        // ── Pending changeover dispatches — tool swaps needed for queued demand ──
        // LATHE1 has Tinman loaded, but Handyman parts are queued → changeover needed
        // LATHE2 has Gargoyle loaded, but Pilate parts are queued → changeover needed
        var cncHandymanProg2 = await db.MachinePrograms.FirstOrDefaultAsync(p => p.ProgramNumber == "EMC-HAN-001-TURN-01");
        var cncPilateProg2 = await db.MachinePrograms.FirstOrDefaultAsync(p => p.ProgramNumber == "EMC-PIL-001-TURN-01");

        // Find a Handyman CNC execution on LATHE1 that needs changeover
        var lathe1IntId = lathe1?.Id ?? 0;
        var hanProgId = cncHandymanProg2?.Id ?? 0;
        var handymanExec = lathe1IntId > 0 && hanProgId > 0 ? await db.StageExecutions
            .Include(se => se.Job)
            .Where(se => se.ProductionStageId == cncTurningStage.Id
                && se.MachineId == lathe1IntId
                && se.Status == StageExecutionStatus.NotStarted
                && se.MachineProgramId == hanProgId
                && se.SetupDispatchId == null)
            .FirstOrDefaultAsync() : null;

        if (handymanExec != null && lathe1 != null && cncHandymanProg2 != null)
        {
            var d = new SetupDispatch
            {
                DispatchNumber = "DSP-PEND-0001",
                MachineId = lathe1.Id,
                MachineProgramId = cncHandymanProg2.Id,
                StageExecutionId = handymanExec.Id,
                JobId = handymanExec.JobId,
                PartId = handymanExec.Job?.PartId,
                DispatchType = DispatchType.Changeover,
                Status = DispatchStatus.Queued,
                Priority = 70,
                PriorityReason = "Tool changeover: Tinman → Handyman (2 tool changes: bore bar T2, thread tap T7)",
                ChangeoverFromProgramId = cncTinmanProg?.Id,
                ChangeoverToProgramId = cncHandymanProg2.Id,
                ToolingRequired = "Swap T2: 7.62mm bore bar → 9mm bore bar; Add T7: M13.5x1 LH thread tap",
                EstimatedSetupMinutes = 12,
                IsAutoGenerated = true,
                QueuedAt = now,
                CreatedBy = "system"
            };
            db.SetupDispatches.Add(d);
            await db.SaveChangesAsync();
            handymanExec.SetupDispatchId = d.Id;
        }

        // Find a Pilate CNC execution on LATHE2 that needs changeover
        var lathe2IntId = lathe2?.Id ?? 0;
        var pilProgId = cncPilateProg2?.Id ?? 0;
        var pilateExec = lathe2IntId > 0 && pilProgId > 0 ? await db.StageExecutions
            .Include(se => se.Job)
            .Where(se => se.ProductionStageId == cncTurningStage.Id
                && se.MachineId == lathe2IntId
                && se.Status == StageExecutionStatus.NotStarted
                && se.MachineProgramId == pilProgId
                && se.SetupDispatchId == null)
            .FirstOrDefaultAsync() : null;

        if (pilateExec != null && lathe2 != null && cncPilateProg2 != null)
        {
            var d = new SetupDispatch
            {
                DispatchNumber = "DSP-PEND-0002",
                MachineId = lathe2.Id,
                MachineProgramId = cncPilateProg2.Id,
                StageExecutionId = pilateExec.Id,
                JobId = pilateExec.JobId,
                PartId = pilateExec.Job?.PartId,
                DispatchType = DispatchType.Changeover,
                Status = DispatchStatus.Queued,
                Priority = 65,
                PriorityReason = "Tool changeover: Gargoyle → Pilate (2 tool changes: bore bar T2, remove groove tool T4)",
                ChangeoverFromProgramId = cncGargoyleProg?.Id,
                ChangeoverToProgramId = cncPilateProg2.Id,
                ToolingRequired = "Swap T2: 5.56mm bore bar → .22 bore bar; Remove T4: ID groove tool (Pilate uses T4 for chamfer)",
                EstimatedSetupMinutes = 10,
                IsAutoGenerated = true,
                QueuedAt = now,
                CreatedBy = "system"
            };
            db.SetupDispatches.Add(d);
            await db.SaveChangesAsync();
            pilateExec.SetupDispatchId = d.Id;
        }

        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Shift Assignments (machines + operators to shifts)
    // ──────────────────────────────────────────────
    private static async Task SeedShiftAssignmentsAsync(TenantDbContext db)
    {
        if (await db.MachineShiftAssignments.AnyAsync()) return;

        var shifts = await db.OperatingShifts.ToListAsync();
        var machines = await db.Machines.Where(m => m.IsActive).ToListAsync();
        var users = await db.Users.ToListAsync();
        if (shifts.Count == 0 || machines.Count == 0) return;

        var dayShift = shifts.FirstOrDefault(s => s.Name.Contains("Day"));
        var nightShift = shifts.FirstOrDefault(s => s.Name.Contains("Night"));
        if (dayShift == null) return;

        // Assign all machines to day shift
        foreach (var machine in machines)
        {
            db.MachineShiftAssignments.Add(new MachineShiftAssignment
            {
                MachineId = machine.Id,
                OperatingShiftId = dayShift.Id
            });
        }

        // SLS machines also run on night shift (they run 24/7)
        if (nightShift != null)
        {
            foreach (var machine in machines.Where(m => m.IsAdditiveMachine))
            {
                db.MachineShiftAssignments.Add(new MachineShiftAssignment
                {
                    MachineId = machine.Id,
                    OperatingShiftId = nightShift.Id
                });
            }
        }

        // Assign operators to shifts
        var opsByDept = new Dictionary<string, string[]>
        {
            ["SLS"] = ["operator1"],
            ["Machining"] = ["operator2"],
            ["Post-Process"] = ["operator3"],
            ["Finishing"] = ["operator4"],
            ["Shipping"] = ["shipping"],
            ["Quality"] = ["qcinspector"],
            ["Operations"] = ["manager"]
        };

        foreach (var (dept, usernames) in opsByDept)
        {
            foreach (var username in usernames)
            {
                var user = users.FirstOrDefault(u => u.Username == username);
                if (user != null)
                {
                    db.UserShiftAssignments.Add(new UserShiftAssignment
                    {
                        UserId = user.Id,
                        OperatingShiftId = dayShift.Id,
                        IsPrimary = true,
                        EffectiveFrom = DateTime.UtcNow.AddDays(-90),
                        AssignedBy = "System"
                    });
                }
            }
        }

        // Jake (SLS) also covers night shift for changeovers
        var jake = users.FirstOrDefault(u => u.Username == "operator1");
        if (jake != null && nightShift != null)
        {
            db.UserShiftAssignments.Add(new UserShiftAssignment
            {
                UserId = jake.Id,
                OperatingShiftId = nightShift.Id,
                IsPrimary = false,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                AssignedBy = "System"
            });
        }

        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Part Pricing
    // ──────────────────────────────────────────────
    private static async Task SeedPartPricingAsync(TenantDbContext db)
    {
        if (await db.PartPricings.AnyAsync()) return;

        var parts = await db.Parts.Where(p => p.IsActive).ToListAsync();
        if (parts.Count == 0) return;

        var pricingMap = new Dictionary<string, (decimal sell, decimal matCost, decimal matWeight, decimal margin, string tier)>
        {
            ["EMC-TIN-001"] = (599.00m, 88.00m, 0.290m, 85.3m, "Standard"),
            ["EMC-HAN-001"] = (599.00m, 82.00m, 0.265m, 86.3m, "Standard"),
            ["EMC-GAR-001"] = (599.00m, 78.00m, 0.230m, 87.0m, "Standard"),
            ["EMC-PIL-001"] = (299.00m, 52.00m, 0.155m, 82.6m, "Standard"),
        };

        foreach (var part in parts)
        {
            if (!pricingMap.TryGetValue(part.PartNumber, out var p)) continue;
            db.PartPricings.Add(new PartPricing
            {
                PartId = part.Id,
                SellPricePerUnit = p.sell,
                MaterialCostPerUnit = p.matCost,
                MaterialWeightPerUnitKg = p.matWeight,
                TargetMarginPct = p.margin,
                PricingTier = p.tier,
                Currency = "USD",
                MinimumOrderQty = 56,
                EffectiveDate = DateTime.UtcNow.AddDays(-90),
                PricingNotes = part.PartNumber == "EMC-PIL-001"
                    ? "Volume pricing: 500+ units at $269, 1000+ at $249"
                    : "Volume pricing: 500+ units at $549, 1000+ at $499",
                CreatedBy = "System", LastModifiedBy = "System"
            });
        }

        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Part Notes
    // ──────────────────────────────────────────────
    private static async Task SeedPartNotesAsync(TenantDbContext db)
    {
        if (await db.PartNotes.AnyAsync()) return;

        var parts = await db.Parts.Where(p => p.IsActive).ToDictionaryAsync(p => p.PartNumber, p => p);
        if (parts.Count == 0) return;

        var notes = new List<PartNote>();

        if (parts.TryGetValue("EMC-TIN-001", out var tinman))
        {
            notes.Add(new PartNote { PartId = tinman.Id, Title = "Thread spec update — ATF Form 4", NoteType = "Engineering",
                Content = "Mount thread changed to 1.375x24 RH per final ATF approval (was 1.375x24 LH in prototype). All tooling updated. CNC program BP-TIN-THREAD-v3 is current.", CreatedBy = "Henry Gill" });
            notes.Add(new PartNote { PartId = tinman.Id, Title = "Double-stack build validated", NoteType = "Manufacturing",
                Content = "DS builds (80x) confirmed stable at 245mm height. No warping observed across 4 runs. Recommend DS for orders >100 units to improve throughput.", CreatedBy = "Jake Marshall" });
        }

        if (parts.TryGetValue("EMC-HAN-001", out var handyman))
        {
            notes.Add(new PartNote { PartId = handyman.Id, Title = "Bore concentricity critical", NoteType = "Quality",
                Content = "Bore concentricity tolerance tightened to 0.002\" TIR per customer feedback. QC must use the dedicated bore gauge (CAL-0034) for every part. See NCR-00001 for history.", CreatedBy = "Ana Reyes", IsPinned = true });
        }

        if (parts.TryGetValue("EMC-GAR-001", out var gargoyle))
        {
            notes.Add(new PartNote { PartId = gargoyle.Id, Title = "Blast media change — glass bead", NoteType = "Manufacturing",
                Content = "Switched from alumina 120 grit to glass bead 100 grit. Alumina was embedding particles in the bore that caused thread gauge failures. Glass bead gives equivalent surface finish without contamination risk.", CreatedBy = "Marcus Hayes" });
            notes.Add(new PartNote { PartId = gargoyle.Id, Title = "5.56mm gas port alignment", NoteType = "Engineering",
                Content = "Gas port alignment is critical for 5.56mm suppression. CNC fixture PSI-FIX-GAR-01 has alignment pins — do not use generic fixture.", CreatedBy = "Henry Gill" });
        }

        if (parts.TryGetValue("EMC-PIL-001", out var pilate))
        {
            notes.Add(new PartNote { PartId = pilate.Id, Title = "DS builds confirmed — no warping", NoteType = "Engineering",
                Content = "Double-stack builds (144x) confirmed at 193mm height with current support strategy. EOS recommended 0.5mm additional offset between layers which resolved the minor warping seen in early prototypes.", CreatedBy = "Henry Gill" });
            notes.Add(new PartNote { PartId = pilate.Id, Title = "Reduced CNC time", NoteType = "Manufacturing",
                Content = "CNC cycle time reduced from 12 min to 8 min per part by switching to the 4-flute carbide end mill (TOOL-EM-6MM) and updating feed rates. Program BP-PIL-CNC-v4 is current.", CreatedBy = "Ryan Cole" });
        }

        db.PartNotes.AddRange(notes);
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Quotes
    // ──────────────────────────────────────────────
    private static async Task SeedQuotesAsync(TenantDbContext db)
    {
        if (await db.Quotes.AnyAsync()) return;

        var parts = await db.Parts.Where(p => p.IsActive).ToDictionaryAsync(p => p.PartNumber, p => p);
        var wos = await db.WorkOrders.ToListAsync();
        if (parts.Count == 0) return;

        var now = DateTime.UtcNow;

        // QT-00001: Silencer Shop initial Tinman order → accepted, converted to WO-00001
        var wo1 = wos.FirstOrDefault(w => w.OrderNumber == "WO-00001");
        var qt1 = new Quote
        {
            QuoteNumber = "QT-00001", CustomerName = "Silencer Shop",
            CustomerEmail = "purchasing@silencershop.com", CustomerPhone = "(512) 931-4556",
            Status = QuoteStatus.Accepted, RevisionNumber = 1,
            TotalEstimatedCost = 9856.00m, QuotedPrice = 67088.00m,
            EstimatedLaborCost = 5200.00m, EstimatedMaterialCost = 3256.00m, EstimatedOverheadCost = 1400.00m,
            Markup = 580.7m, TargetMarginPct = 85.3m,
            CreatedDate = now.AddDays(-75), ExpirationDate = now.AddDays(-45),
            SentAt = now.AddDays(-74), AcceptedAt = now.AddDays(-65),
            ConvertedWorkOrderId = wo1?.Id,
            CreatedBy = "Henry Gill", LastModifiedBy = "Henry Gill"
        };
        db.Quotes.Add(qt1);
        await db.SaveChangesAsync();
        if (parts.TryGetValue("EMC-TIN-001", out var tin))
        {
            db.QuoteLines.Add(new QuoteLine { QuoteId = qt1.Id, PartId = tin.Id, Quantity = 112,
                EstimatedCostPerPart = 88.00m, QuotedPricePerPart = 599.00m,
                LaborMinutes = 26.0, SetupMinutes = 8.0, MaterialCostEach = 50.75m });
        }
        await db.SaveChangesAsync();

        // QT-00002: Capitol Armory — Handyman + Gargoyle → accepted, converted to WO-00002/WO-00003
        var wo2 = wos.FirstOrDefault(w => w.OrderNumber == "WO-00002");
        var qt2 = new Quote
        {
            QuoteNumber = "QT-00002", CustomerName = "Capitol Armory",
            CustomerEmail = "orders@capitolarmory.com", CustomerPhone = "(512) 961-8585",
            Status = QuoteStatus.Accepted, RevisionNumber = 1,
            TotalEstimatedCost = 27024.00m, QuotedPrice = 200208.00m,
            EstimatedLaborCost = 15000.00m, EstimatedMaterialCost = 8524.00m, EstimatedOverheadCost = 3500.00m,
            TargetMarginPct = 86.5m,
            CreatedDate = now.AddDays(-68), ExpirationDate = now.AddDays(-38),
            SentAt = now.AddDays(-67), AcceptedAt = now.AddDays(-57),
            ConvertedWorkOrderId = wo2?.Id,
            CreatedBy = "Henry Gill", LastModifiedBy = "Henry Gill"
        };
        db.Quotes.Add(qt2);
        await db.SaveChangesAsync();
        if (parts.TryGetValue("EMC-HAN-001", out var han))
            db.QuoteLines.Add(new QuoteLine { QuoteId = qt2.Id, PartId = han.Id, Quantity = 192,
                EstimatedCostPerPart = 82.00m, QuotedPricePerPart = 599.00m,
                LaborMinutes = 24.0, SetupMinutes = 7.0, MaterialCostEach = 46.50m });
        if (parts.TryGetValue("EMC-GAR-001", out var gar))
            db.QuoteLines.Add(new QuoteLine { QuoteId = qt2.Id, PartId = gar.Id, Quantity = 144,
                EstimatedCostPerPart = 78.00m, QuotedPricePerPart = 599.00m,
                LaborMinutes = 22.0, SetupMinutes = 6.5, MaterialCostEach = 40.25m });
        await db.SaveChangesAsync();

        // QT-00003: Silencer Central — mixed order, sent but awaiting response
        var qt3 = new Quote
        {
            QuoteNumber = "QT-00003", CustomerName = "Silencer Central",
            CustomerEmail = "procurement@silencercentral.com", CustomerPhone = "(605) 286-3014",
            Status = QuoteStatus.Sent, RevisionNumber = 1,
            TotalEstimatedCost = 18600.00m, QuotedPrice = 131780.00m,
            EstimatedLaborCost = 10200.00m, EstimatedMaterialCost = 5900.00m, EstimatedOverheadCost = 2500.00m,
            TargetMarginPct = 85.9m,
            CreatedDate = now.AddDays(-5), ExpirationDate = now.AddDays(25),
            SentAt = now.AddDays(-4),
            CreatedBy = "Henry Gill", LastModifiedBy = "Henry Gill"
        };
        db.Quotes.Add(qt3);
        await db.SaveChangesAsync();
        if (tin != null) db.QuoteLines.Add(new QuoteLine { QuoteId = qt3.Id, PartId = tin.Id, Quantity = 56,
            EstimatedCostPerPart = 88.00m, QuotedPricePerPart = 599.00m,
            LaborMinutes = 26.0, SetupMinutes = 8.0, MaterialCostEach = 50.75m });
        if (han != null) db.QuoteLines.Add(new QuoteLine { QuoteId = qt3.Id, PartId = han.Id, Quantity = 64,
            EstimatedCostPerPart = 82.00m, QuotedPricePerPart = 599.00m,
            LaborMinutes = 24.0, SetupMinutes = 7.0, MaterialCostEach = 46.50m });
        if (gar != null) db.QuoteLines.Add(new QuoteLine { QuoteId = qt3.Id, PartId = gar.Id, Quantity = 72,
            EstimatedCostPerPart = 78.00m, QuotedPricePerPart = 599.00m,
            LaborMinutes = 22.0, SetupMinutes = 6.5, MaterialCostEach = 40.25m });
        await db.SaveChangesAsync();

        // QT-00004: Palmetto State Armory — 1000x Pilate, draft being prepared
        var qt4 = new Quote
        {
            QuoteNumber = "QT-00004", CustomerName = "Palmetto State Armory",
            CustomerEmail = "sourcing@palmettostatearmory.com", CustomerPhone = "(803) 724-6950",
            Status = QuoteStatus.Draft, RevisionNumber = 1,
            TotalEstimatedCost = 52000.00m, QuotedPrice = 249000.00m,
            EstimatedLaborCost = 28000.00m, EstimatedMaterialCost = 16000.00m, EstimatedOverheadCost = 8000.00m,
            TargetMarginPct = 79.1m,
            CreatedDate = now.AddDays(-1), ExpirationDate = now.AddDays(29),
            CreatedBy = "Derek Simmons", LastModifiedBy = "Derek Simmons",
            Notes = "Volume pricing request — 1000 units at $249/ea. Need to verify powder inventory before committing. Lead time ~8 weeks."
        };
        db.Quotes.Add(qt4);
        await db.SaveChangesAsync();
        if (parts.TryGetValue("EMC-PIL-001", out var pil))
            db.QuoteLines.Add(new QuoteLine { QuoteId = qt4.Id, PartId = pil.Id, Quantity = 1000,
                EstimatedCostPerPart = 52.00m, QuotedPricePerPart = 249.00m,
                LaborMinutes = 14.0, SetupMinutes = 3.0, MaterialCostEach = 27.25m,
                Notes = "Volume discount applied — standard MSRP $299" });
        await db.SaveChangesAsync();

        // QT-00005: Silencer Shop reorder — accepted, converted to WO-00008/09/10
        var wo8 = wos.FirstOrDefault(w => w.OrderNumber == "WO-00008");
        var qt5 = new Quote
        {
            QuoteNumber = "QT-00005", CustomerName = "Silencer Shop",
            CustomerEmail = "purchasing@silencershop.com", CustomerPhone = "(512) 931-4556",
            Status = QuoteStatus.Accepted, RevisionNumber = 1,
            TotalEstimatedCost = 42768.00m, QuotedPrice = 295204.00m,
            EstimatedLaborCost = 24000.00m, EstimatedMaterialCost = 13268.00m, EstimatedOverheadCost = 5500.00m,
            TargetMarginPct = 85.5m,
            CreatedDate = now.AddDays(-10), ExpirationDate = now.AddDays(20),
            SentAt = now.AddDays(-9), AcceptedAt = now.AddDays(-4),
            ConvertedWorkOrderId = wo8?.Id,
            CreatedBy = "Henry Gill", LastModifiedBy = "Henry Gill"
        };
        db.Quotes.Add(qt5);
        await db.SaveChangesAsync();
        if (tin != null) db.QuoteLines.Add(new QuoteLine { QuoteId = qt5.Id, PartId = tin.Id, Quantity = 280,
            EstimatedCostPerPart = 88.00m, QuotedPricePerPart = 549.00m,
            LaborMinutes = 26.0, SetupMinutes = 8.0, MaterialCostEach = 50.75m,
            Notes = "500+ unit pricing" });
        if (gar != null) db.QuoteLines.Add(new QuoteLine { QuoteId = qt5.Id, PartId = gar.Id, Quantity = 216,
            EstimatedCostPerPart = 78.00m, QuotedPricePerPart = 549.00m,
            LaborMinutes = 22.0, SetupMinutes = 6.5, MaterialCostEach = 40.25m,
            Notes = "500+ unit pricing" });
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  RFQ Requests
    // ──────────────────────────────────────────────
    private static async Task SeedRfqRequestsAsync(TenantDbContext db)
    {
        if (await db.RfqRequests.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var qt5 = await db.Quotes.FirstOrDefaultAsync(q => q.QuoteNumber == "QT-00005");

        db.RfqRequests.AddRange(
            new RfqRequest
            {
                CompanyName = "Rugged Suppressors", ContactName = "Matt Jensen",
                Email = "matt@ruggedsuppressors.com", Phone = "(770) 565-1820",
                Description = "Looking for OEM manufacturing partner for a new .300 Win Mag suppressor design. Ti-6Al-4V construction, full-auto rated. We have STEP files ready. Need 200 units/month starting Q3. Can you quote DMLS production with your EOS M4 capacity?",
                Quantity = 200, Material = "Ti-6Al-4V", NeededByDate = now.AddDays(90),
                Status = "New", SubmittedDate = now.AddDays(-1)
            },
            new RfqRequest
            {
                CompanyName = "SilencerCo", ContactName = "Alex Park",
                Email = "manufacturing@silencerco.com", Phone = "(801) 417-5384",
                Description = "Partnership inquiry: We're evaluating additive manufacturing for our next-gen Omega 36M line. Interested in your Ti-6Al-4V DMLS capability and capacity. Would like to schedule a facility tour and discuss NDA for prototype production. Initial run would be 500 units for qualification.",
                Quantity = 500, Material = "Ti-6Al-4V", NeededByDate = now.AddDays(120),
                Status = "Reviewed", SubmittedDate = now.AddDays(-8),
                ReviewedBy = "Henry Gill", ReviewedDate = now.AddDays(-6)
            },
            new RfqRequest
            {
                CompanyName = "Silencer Shop", ContactName = "Mike Williams",
                Email = "purchasing@silencershop.com", Phone = "(512) 931-4556",
                Description = "Reorder request: Need updated pricing for next quarter allocation. Looking at 280x Tinman + 216x Gargoyle. Can you beat current pricing with a 6-month commitment?",
                Quantity = 496, Material = "Ti-6Al-4V",
                Status = "Quoted", SubmittedDate = now.AddDays(-12),
                ReviewedBy = "Henry Gill", ReviewedDate = now.AddDays(-10),
                ConvertedQuoteId = qt5?.Id
            }
        );

        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Shipments for completed work orders
    // ──────────────────────────────────────────────
    private static async Task SeedShipmentsAsync(TenantDbContext db)
    {
        if (await db.Shipments.AnyAsync()) return;

        var wos = await db.WorkOrders.Include(w => w.Lines).Where(w => w.Status == WorkOrderStatus.Complete).ToListAsync();
        if (wos.Count == 0) return;

        var now = DateTime.UtcNow;
        int shipNum = 1;

        var carriers = new[] { "FedEx Freight", "UPS Freight", "FedEx Freight", "FedEx Freight" };
        var trackingPrefixes = new[] { "7489", "1Z5E", "7491", "7493" };

        foreach (var wo in wos.OrderBy(w => w.OrderNumber))
        {
            var shippedAt = wo.DueDate.AddDays(-3);
            var shipment = new Shipment
            {
                ShipmentNumber = $"SHP-{shipNum:D5}",
                WorkOrderId = wo.Id,
                Status = ShipmentStatus.Delivered,
                CarrierName = carriers[Math.Min(shipNum - 1, carriers.Length - 1)],
                TrackingNumber = $"{trackingPrefixes[Math.Min(shipNum - 1, trackingPrefixes.Length - 1)]}{900000 + shipNum * 1234}",
                PackageCount = Math.Max(1, wo.Lines?.Sum(l => l.Quantity) / 50 ?? 1),
                ShipperNotes = $"Shipped to {wo.CustomerName}. All parts serialized and CoC included.",
                ShippedBy = "Sam Nguyen",
                ShippedAt = shippedAt,
                CreatedDate = shippedAt.AddHours(-2)
            };
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();

            if (wo.Lines != null)
            {
                foreach (var line in wo.Lines)
                {
                    db.ShipmentLines.Add(new ShipmentLine
                    {
                        ShipmentId = shipment.Id,
                        WorkOrderLineId = line.Id,
                        QuantityShipped = line.Quantity
                    });
                }
                await db.SaveChangesAsync();
            }

            shipNum++;
        }
    }

    // ──────────────────────────────────────────────
    //  Work Order Comments
    // ──────────────────────────────────────────────
    private static async Task SeedWorkOrderCommentsAsync(TenantDbContext db)
    {
        if (await db.WorkOrderComments.AnyAsync()) return;

        var wos = await db.WorkOrders.ToDictionaryAsync(w => w.OrderNumber, w => w);
        var users = await db.Users.ToDictionaryAsync(u => u.Username, u => u);
        if (wos.Count == 0) return;

        var now = DateTime.UtcNow;
        var comments = new List<WorkOrderComment>();

        if (wos.TryGetValue("WO-00001", out var wo1))
        {
            comments.Add(new WorkOrderComment { WorkOrderId = wo1.Id,
                Content = "First production run for Silencer Shop. Two builds of 56x Tinman completed on M4-1 without issues. All 112 units passed QC.",
                AuthorName = "Henry Gill", AuthorUserId = users.GetValueOrDefault("admin")?.Id, CreatedDate = now.AddDays(-35) });
            comments.Add(new WorkOrderComment { WorkOrderId = wo1.Id,
                Content = "Shipment SHP-00001 picked up by FedEx. CoC and material certs included per PO requirements.",
                AuthorName = "Sam Nguyen", AuthorUserId = users.GetValueOrDefault("shipping")?.Id, CreatedDate = now.AddDays(-33) });
        }

        if (wos.TryGetValue("WO-00005", out var wo5))
        {
            comments.Add(new WorkOrderComment { WorkOrderId = wo5.Id,
                Content = "Expedite request from Capitol Armory — they're running low on Tinman inventory. Both Tinman builds completed, Handyman build done on M4-2. All parts through CNC, moving to engraving.",
                AuthorName = "Derek Simmons", AuthorUserId = users.GetValueOrDefault("manager")?.Id, CreatedDate = now.AddDays(-5) });
            comments.Add(new WorkOrderComment { WorkOrderId = wo5.Id,
                Content = "112x Tinman finished QC — all pass. 64x Handyman in engraving now, should be done by end of shift.",
                AuthorName = "Ana Reyes", AuthorUserId = users.GetValueOrDefault("qcinspector")?.Id, CreatedDate = now.AddDays(-3) });
        }

        if (wos.TryGetValue("WO-00006", out var wo6))
        {
            comments.Add(new WorkOrderComment { WorkOrderId = wo6.Id,
                Content = "Gargoyle run #3 printing on M4-1 — started this morning, ~60% complete. Should finish overnight. First batch of 72 already through packaging.",
                AuthorName = "Jake Marshall", AuthorUserId = users.GetValueOrDefault("operator1")?.Id, CreatedDate = now.AddHours(-6) });
        }

        if (wos.TryGetValue("WO-00008", out var wo8))
        {
            comments.Add(new WorkOrderComment { WorkOrderId = wo8.Id,
                Content = "Customer confirmed OK to ship partial — first Tinman batch ASAP, remainder on standard schedule. 4x Tinman + 1x Gargoyle builds needed total.",
                AuthorName = "Derek Simmons", AuthorUserId = users.GetValueOrDefault("manager")?.Id, CreatedDate = now.AddDays(-2) });
            comments.Add(new WorkOrderComment { WorkOrderId = wo8.Id,
                Content = "Tinman 56x ready to schedule on M4-1 (BP queued). Gargoyle 72x also prepped. Waiting for current Gargoyle run to finish on M4-1.",
                AuthorName = "Henry Gill", AuthorUserId = users.GetValueOrDefault("admin")?.Id, CreatedDate = now.AddDays(-1) });
        }

        if (wos.TryGetValue("WO-00009", out var wo9))
        {
            comments.Add(new WorkOrderComment { WorkOrderId = wo9.Id,
                Content = "RUSH — Silencer Central needs 192x Handyman for dealer allocation. Prioritize M4-2 for 3 consecutive Handyman builds. 64x per build = 3 builds needed.",
                AuthorName = "Derek Simmons", AuthorUserId = users.GetValueOrDefault("manager")?.Id, CreatedDate = now.AddDays(-1), IsInternal = true });
        }

        if (wos.TryGetValue("WO-00010", out var wo10))
        {
            comments.Add(new WorkOrderComment { WorkOrderId = wo10.Id,
                Content = "Mixed order from Silencer Shop — 144x Gargoyle, 96x Pilate, 56x Tinman. Standard lead time, no rush. Schedule after WO-00008 and WO-00009.",
                AuthorName = "Henry Gill", AuthorUserId = users.GetValueOrDefault("admin")?.Id, CreatedDate = now.AddHours(-12) });
        }

        db.WorkOrderComments.AddRange(comments);
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Quality Records (NCRs + SPC data)
    // ──────────────────────────────────────────────
    private static async Task SeedQualityDemoDataAsync(TenantDbContext db)
    {
        if (await db.NonConformanceReports.AnyAsync()) return;

        var parts = await db.Parts.Where(p => p.IsActive).ToDictionaryAsync(p => p.PartNumber, p => p);
        var jobs = await db.Jobs.Where(j => j.Status == JobStatus.Completed).Take(5).ToListAsync();
        var now = DateTime.UtcNow;

        // NCR-00001: Bore concentricity issue on Handyman
        if (parts.TryGetValue("EMC-HAN-001", out var handyman))
        {
            var job = jobs.FirstOrDefault(j => j.PartId == handyman.Id);
            var ncr1 = new NonConformanceReport
            {
                NcrNumber = "NCR-00001",
                PartId = handyman.Id,
                JobId = job?.Id,
                Type = NcrType.InProcess,
                Description = "3 units from Handyman Run #2 found with bore concentricity exceeding 0.003\" TIR (spec is 0.002\" TIR). Root cause: CNC fixture PSI-FIX-HAN-01 had worn locating pin causing 0.001\" shift. Fixture repaired and recalibrated.",
                QuantityAffected = "3",
                Severity = NcrSeverity.Major,
                Disposition = NcrDisposition.Rework,
                Status = NcrStatus.Closed,
                ReportedByUserId = "Ana Reyes",
                ReportedAt = now.AddDays(-40),
                ClosedAt = now.AddDays(-37)
            };
            db.NonConformanceReports.Add(ncr1);
            await db.SaveChangesAsync();

            // Corrective action for NCR-00001
            db.CorrectiveActions.Add(new CorrectiveAction
            {
                CapaNumber = "CAPA-00001",
                Type = CapaType.Corrective,
                ProblemStatement = "3 Handyman units from Run #2 exceeded bore concentricity spec (0.003\" TIR vs 0.002\" max).",
                RootCauseAnalysis = "CNC fixture PSI-FIX-HAN-01 had worn locating pin causing 0.001\" shift. Pin wear was not detected during routine visual inspection.",
                ImmediateAction = "Replaced worn locating pin on PSI-FIX-HAN-01. Re-machined the 3 affected parts — all passed re-inspection.",
                LongTermAction = "Implemented weekly fixture inspection checklist for all CNC fixtures. Added bore concentricity to first-article inspection for every Handyman batch.",
                PreventiveAction = "Added locating pin wear measurement to preventive maintenance schedule — replace at 0.0005\" wear or every 500 cycles.",
                OwnerId = "Ryan Cole",
                DueDate = now.AddDays(-35),
                CompletedAt = now.AddDays(-36),
                EffectivenessVerification = "Verified: 4 subsequent Handyman batches (256 units) all within spec. No recurrence.",
                Status = CapaStatus.Closed,
                CreatedAt = now.AddDays(-39)
            });
            await db.SaveChangesAsync();
        }

        // NCR-00002: Thread gauge failure on Gargoyle
        if (parts.TryGetValue("EMC-GAR-001", out var gargoyle))
        {
            var job = jobs.FirstOrDefault(j => j.PartId == gargoyle.Id);
            db.NonConformanceReports.Add(new NonConformanceReport
            {
                NcrNumber = "NCR-00002",
                PartId = gargoyle.Id,
                JobId = job?.Id,
                Type = NcrType.InProcess,
                Description = "1 unit from Gargoyle Run #1 failed thread go/no-go gauge. Investigation showed embedded alumina particles from sandblasting contaminated the thread form. Switched blast media to glass bead per engineering recommendation.",
                QuantityAffected = "1",
                Severity = NcrSeverity.Minor,
                Disposition = NcrDisposition.Scrap,
                Status = NcrStatus.Closed,
                ReportedByUserId = "Ana Reyes",
                ReportedAt = now.AddDays(-42),
                ClosedAt = now.AddDays(-40)
            });
        }

        // NCR-00003: Incoming material — powder moisture content
        db.NonConformanceReports.Add(new NonConformanceReport
        {
            NcrNumber = "NCR-00003",
            Type = NcrType.IncomingMaterial,
            Description = "Inconel 718 powder lot IN718-2026-001 received with moisture content 0.08% (spec max 0.05%). Quarantined pending vendor response. Supplier (Carpenter Technology) notified — replacement shipment expected within 5 business days.",
            QuantityAffected = "60 kg",
            Severity = NcrSeverity.Major,
            Disposition = NcrDisposition.ReturnToVendor,
            Status = NcrStatus.Dispositioned,
            ReportedByUserId = "Marcus Hayes",
            ReportedAt = now.AddDays(-5)
        });
        await db.SaveChangesAsync();

        // SPC Data — bore diameter measurements for Tinman (shows process capability)
        if (parts.TryGetValue("EMC-TIN-001", out var tinman))
        {
            var rng = new Random(42); // deterministic
            var spcPoints = new List<SpcDataPoint>();
            for (int i = 0; i < 50; i++)
            {
                // Nominal 1.375", tolerance ±0.002"
                var variation = (rng.NextDouble() - 0.5) * 0.003; // ±0.0015" — well within spec
                spcPoints.Add(new SpcDataPoint
                {
                    PartId = tinman.Id,
                    CharacteristicName = "Bore Diameter",
                    MeasuredValue = 1.375m + (decimal)variation,
                    NominalValue = 1.375m,
                    TolerancePlus = 0.002m,
                    ToleranceMinus = 0.002m,
                    JobId = jobs.Count > 0 ? jobs[i % jobs.Count].Id : null,
                    RecordedAt = now.AddDays(-50).AddHours(i * 12)
                });
            }
            db.SpcDataPoints.AddRange(spcPoints);

            // Also add thread pitch diameter measurements
            for (int i = 0; i < 30; i++)
            {
                var variation = (rng.NextDouble() - 0.5) * 0.002;
                spcPoints.Add(new SpcDataPoint
                {
                    PartId = tinman.Id,
                    CharacteristicName = "Thread Pitch Diameter",
                    MeasuredValue = 1.3410m + (decimal)variation,
                    NominalValue = 1.3410m,
                    TolerancePlus = 0.0015m,
                    ToleranceMinus = 0.0015m,
                    JobId = jobs.Count > 0 ? jobs[i % jobs.Count].Id : null,
                    RecordedAt = now.AddDays(-30).AddHours(i * 12)
                });
            }
            db.SpcDataPoints.AddRange(spcPoints);
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedWorkflowsAsync(TenantDbContext db)
    {
        if (await db.WorkflowDefinitions.AnyAsync()) return;

        var workflows = new List<WorkflowDefinition>
        {
            new()
            {
                Name = "Work Order Release",
                EntityType = "WorkOrder",
                TriggerEvent = "StatusChange",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "System",
                Steps = new List<WorkflowStep>
                {
                    new() { StepOrder = 1, ActionType = "RequireApproval", AssignToRole = "Supervisor" },
                    new() { StepOrder = 2, ActionType = "RequireApproval", AssignToRole = "Manager" }
                }
            },
            new()
            {
                Name = "Quote Approval",
                EntityType = "Quote",
                TriggerEvent = "StatusChange",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "System",
                Steps = new List<WorkflowStep>
                {
                    new() { StepOrder = 1, ActionType = "RequireApproval", AssignToRole = "Manager" }
                }
            },
            new()
            {
                Name = "NCR Disposition",
                EntityType = "NCR",
                TriggerEvent = "StatusChange",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "System",
                Steps = new List<WorkflowStep>
                {
                    new() { StepOrder = 1, ActionType = "RequireApproval", AssignToRole = "Quality" },
                    new() { StepOrder = 2, ActionType = "RequireApproval", AssignToRole = "Engineering" }
                }
            },
            new()
            {
                Name = "CAPA Closure",
                EntityType = "CAPA",
                TriggerEvent = "StatusChange",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "System",
                Steps = new List<WorkflowStep>
                {
                    new() { StepOrder = 1, ActionType = "RequireApproval", AssignToRole = "Quality" },
                    new() { StepOrder = 2, ActionType = "RequireApproval", AssignToRole = "Manager" }
                }
            },
            new()
            {
                Name = "Program Release",
                EntityType = "MachineProgram",
                TriggerEvent = "StatusChange",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "System",
                Steps = new List<WorkflowStep>
                {
                    new() { StepOrder = 1, ActionType = "RequireApproval", AssignToRole = "Engineering" }
                }
            }
        };

        db.WorkflowDefinitions.AddRange(workflows);
        await db.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════════════
    // DEMO ENHANCEMENT — Dense 2-week schedule for demo day
    // Idempotent: checks SystemSetting "DemoEnhancementV1" marker.
    // Creates new work orders, builds, and downstream stage executions
    // to give the Gantt chart a dense, realistic look.
    // ════════════════════════════════════════════════════════════════════════
    private static async Task SeedDemoEnhancementAsync(TenantDbContext db)
    {
        const string marker = "DemoEnhancementV3";
        if (await db.SystemSettings.AnyAsync(s => s.Key == marker)) return;

        // ── Cleanup duplicate runs from V2 ──────────────────────────
        // Remove all enhancement data and re-create cleanly
        var oldMarkers = await db.SystemSettings.Where(s => s.Key.StartsWith("DemoEnhancement")).ToListAsync();
        if (oldMarkers.Count > 0) db.SystemSettings.RemoveRange(oldMarkers);

        // Delete WOs created by prior enhancement runs (PO pattern matching)
        var enhancementPOs = new[] { "RS-2026-0401", "SC-2026-0402", "CA-2026-0405", "DA-2026-0408", "SS-2026-0410", "TB-2026-0412" };
        var dupeWOs = await db.WorkOrders.Where(w => enhancementPOs.Contains(w.CustomerPO)).ToListAsync();
        if (dupeWOs.Count > 0)
        {
            var dupeWOIds = dupeWOs.Select(w => w.Id).ToList();
            var dupeLines = await db.Set<WorkOrderLine>().Where(l => dupeWOIds.Contains(l.WorkOrderId)).ToListAsync();
            var dupeLineIds = dupeLines.Select(l => l.Id).ToHashSet();

            // Delete jobs linked to these WO lines
            var dupeJobs = await db.Jobs.Where(j => j.WorkOrderLineId != null && dupeLineIds.Contains(j.WorkOrderLineId.Value)).ToListAsync();
            var dupeJobIds = dupeJobs.Select(j => j.Id).ToHashSet();

            // Delete stage executions for these jobs
            var dupeExecs = await db.StageExecutions.Where(se => se.JobId != null && dupeJobIds.Contains(se.JobId.Value)).ToListAsync();
            db.StageExecutions.RemoveRange(dupeExecs);

            // Delete programs linked to these jobs
            var dupeProgIds = dupeExecs.Where(se => se.MachineProgramId != null).Select(se => se.MachineProgramId!.Value).Distinct().ToList();
            var dupeProgs = await db.MachinePrograms.Where(p => dupeProgIds.Contains(p.Id)).ToListAsync();
            var dupeProgramParts = await db.ProgramParts.Where(pp => dupeProgIds.Contains(pp.MachineProgramId)).ToListAsync();
            db.ProgramParts.RemoveRange(dupeProgramParts);
            db.MachinePrograms.RemoveRange(dupeProgs);

            db.Jobs.RemoveRange(dupeJobs);
            db.Set<WorkOrderLine>().RemoveRange(dupeLines);
            db.WorkOrders.RemoveRange(dupeWOs);
            await db.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;
        var stages = await db.ProductionStages.ToDictionaryAsync(s => s.StageSlug, s => s);
        var machines = await db.Machines.Where(m => m.IsActive).ToDictionaryAsync(m => m.MachineId, m => m);
        int? Mid(string id) => machines.TryGetValue(id, out var m) ? m.Id : null;

        var tinman = await db.Parts.FirstOrDefaultAsync(p => p.PartNumber == "EMC-TIN-001");
        var handyman = await db.Parts.FirstOrDefaultAsync(p => p.PartNumber == "EMC-HAN-001");
        var gargoyle = await db.Parts.FirstOrDefaultAsync(p => p.PartNumber == "EMC-GAR-001");
        var pilate = await db.Parts.FirstOrDefaultAsync(p => p.PartNumber == "EMC-PIL-001");
        if (tinman == null || handyman == null || gargoyle == null || pilate == null) return;

        var tiMat = await db.Materials.FirstOrDefaultAsync(m => m.Name.StartsWith("Ti-6Al-4V"));

        // Track machine availability (watermarks)
        var watermark = new Dictionary<string, DateTime>();

        // Initialize watermarks from existing scheduled/in-progress stage executions
        var existingExecs = await db.StageExecutions
            .Where(se => se.Status != StageExecutionStatus.Completed && se.ScheduledEndAt != null)
            .ToListAsync();
        foreach (var se in existingExecs)
        {
            var machineIdStr = se.MachineId != null
                ? machines.Values.FirstOrDefault(m => m.Id == se.MachineId)?.MachineId
                : null;
            if (machineIdStr != null && se.ScheduledEndAt.HasValue)
            {
                if (!watermark.ContainsKey(machineIdStr) || se.ScheduledEndAt.Value > watermark[machineIdStr])
                    watermark[machineIdStr] = se.ScheduledEndAt.Value;
            }
        }

        // Also check existing jobs for SLS machines
        var existingJobs = await db.Jobs
            .Where(j => j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled && j.ScheduledEnd > DateTime.MinValue)
            .ToListAsync();
        foreach (var ej in existingJobs)
        {
            var machineIdStr = ej.MachineId != null
                ? machines.Values.FirstOrDefault(m => m.Id == ej.MachineId)?.MachineId
                : null;
            if (machineIdStr != null && ej.ScheduledEnd > DateTime.MinValue)
            {
                if (!watermark.ContainsKey(machineIdStr) || ej.ScheduledEnd > watermark[machineIdStr])
                    watermark[machineIdStr] = ej.ScheduledEnd;
            }
        }

        // Default watermarks if no existing data — start scheduling from now
        if (!watermark.ContainsKey("M4-1")) watermark["M4-1"] = now;
        if (!watermark.ContainsKey("M4-2")) watermark["M4-2"] = now;

        DateTime GetAvailable(string mid, DateTime earliest)
        {
            var t = earliest;
            if (watermark.TryGetValue(mid, out var wm) && wm.AddHours(0.5) > t)
                t = wm.AddHours(0.5); // 30min changeover
            return t;
        }
        void SetWatermark(string mid, DateTime end) => watermark[mid] = end;

        static DateTime SnapToShift(DateTime dt)
        {
            // SLS runs 24/7, post-process runs Mon-Fri 6am-6pm
            if (dt.DayOfWeek == DayOfWeek.Saturday)
                return dt.Date.AddDays(2).AddHours(6);
            if (dt.DayOfWeek == DayOfWeek.Sunday)
                return dt.Date.AddDays(1).AddHours(6);
            if (dt.Hour < 6) return dt.Date.AddHours(6);
            if (dt.Hour >= 18) {
                var next = dt.Date.AddDays(1).AddHours(6);
                if (next.DayOfWeek == DayOfWeek.Saturday) next = next.AddDays(2);
                else if (next.DayOfWeek == DayOfWeek.Sunday) next = next.AddDays(1);
                return next;
            }
            return dt;
        }

        int jobNum = await db.Jobs.CountAsync() + 1;
        int bpNum = await db.MachinePrograms.CountAsync() + 1;
        int woNum = await db.WorkOrders.CountAsync() + 1;

        // ── Routing definitions per part ─────────────────────────────
        var tinmanRouting = new (string slug, string mid, double hrs, decimal cost)[] {
            ("sls-printing","M4-1",22.5,1913m), ("depowdering","INC1",1.0,55m),
            ("wire-edm","EDM1",1.9,163m), ("cnc-turning","LATHE1",5.6,504m),
            ("laser-engraving","ENGRAVE1",0.14,8m), ("sandblasting","BLAST1",0.1,4m),
            ("qc","QC1",1.87,140m), ("packaging","PACK1",0.06,2m) };

        var handymanRouting = new (string slug, string mid, double hrs, decimal cost)[] {
            ("sls-printing","M4-2",20.0,1700m), ("depowdering","INC1",1.0,55m),
            ("wire-edm","EDM1",1.9,163m), ("cnc-turning","LATHE1",4.8,432m),
            ("laser-engraving","ENGRAVE1",0.14,8m), ("sandblasting","BLAST1",0.1,4m),
            ("qc","QC1",1.6,120m), ("packaging","PACK1",0.06,2m) };

        var gargoyleRouting = new (string slug, string mid, double hrs, decimal cost)[] {
            ("sls-printing","M4-1",18.5,1573m), ("depowdering","INC1",1.0,55m),
            ("wire-edm","EDM1",1.9,163m), ("cnc-turning","LATHE2",7.2,648m),
            ("laser-engraving","ENGRAVE1",0.17,9m), ("sandblasting","BLAST1",0.1,4m),
            ("qc","QC1",2.4,180m), ("packaging","PACK1",0.07,2m) };

        var pilateRouting = new (string slug, string mid, double hrs, decimal cost)[] {
            ("sls-printing","M4-2",16.0,1360m), ("depowdering","INC1",1.0,55m),
            ("wire-edm","EDM1",1.9,163m), ("cnc-turning","LATHE1",3.8,342m),
            ("laser-engraving","ENGRAVE1",0.10,6m), ("sandblasting","BLAST1",0.1,4m),
            ("qc","QC1",1.5,113m), ("packaging","PACK1",0.05,2m) };

        // ── Helper: Create a full build (program + job + all stages) ──────
        async Task CreateBuild(Part part, string machineId, int qty, double printHrs,
            string progName, WorkOrder wo, JobPriority priority,
            (string slug, string mid, double hrs, decimal cost)[] routing)
        {
            var slotStart = GetAvailable(machineId, now);

            // Create program
            var prog = new MachineProgram
            {
                ProgramNumber = $"BP-{bpNum++:D5}",
                Name = progName,
                ProgramType = ProgramType.BuildPlate,
                Status = ProgramStatus.Active,
                ScheduleStatus = ProgramScheduleStatus.Scheduled,
                MachineType = "SLS",
                MaterialId = tiMat?.Id,
                EstimatedPrintHours = printHrs,
                LayerCount = (int)(printHrs * 136),
                BuildHeightMm = printHrs * 8.2,
                EstimatedPowderKg = printHrs * 0.72,
                SlicerFileName = $"EMC_{part.PartNumber}_{qty}x.sli",
                SlicerSoftware = "EOSPRINT 2",
                SlicerVersion = "2.12.1",
                IsLocked = false,
                ScheduledDate = slotStart,
                MachineId = Mid(machineId),
                CreatedBy = "System",
                LastModifiedBy = "System"
            };
            db.MachinePrograms.Add(prog);
            await db.SaveChangesAsync();

            // Link part to program
            var woLine = await db.Set<WorkOrderLine>()
                .FirstOrDefaultAsync(l => l.WorkOrderId == wo.Id && l.PartId == part.Id);
            db.ProgramParts.Add(new ProgramPart
            {
                MachineProgramId = prog.Id,
                PartId = part.Id,
                Quantity = qty,
                StackLevel = 1,
                WorkOrderLineId = woLine?.Id
            });
            await db.SaveChangesAsync();

            // Create job
            var printEnd = slotStart.AddHours(printHrs);
            var downstreamHrs = routing.Skip(1).Sum(r => r.hrs + 0.3);
            var jobEnd = printEnd.AddHours(downstreamHrs + 2);
            var job = new Job
            {
                JobNumber = $"JOB-{jobNum++:D5}",
                PartId = part.Id,
                MachineId = Mid(machineId),
                Scope = JobScope.Build,
                Status = JobStatus.Scheduled,
                Priority = priority,
                Quantity = qty,
                ProducedQuantity = 0,
                ScheduledStart = slotStart,
                ScheduledEnd = jobEnd,
                WorkOrderLineId = woLine?.Id,
                CreatedBy = "System",
                LastModifiedBy = "System"
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            // Create SLS printing stage
            db.StageExecutions.Add(new StageExecution
            {
                JobId = job.Id,
                ProductionStageId = stages.TryGetValue("sls-printing", out var slsStg) ? slsStg.Id : 0,
                MachineId = Mid(machineId),
                Status = StageExecutionStatus.NotStarted,
                ScheduledStartAt = slotStart,
                ScheduledEndAt = printEnd,
                EstimatedHours = printHrs,
                EstimatedCost = routing[0].cost,
                MachineProgramId = prog.Id,
                CreatedBy = "System",
                LastModifiedBy = "System"
            });
            SetWatermark(machineId, printEnd);

            // Create downstream stages
            var t = printEnd.AddHours(0.5);
            foreach (var (slug, mid, hrs, cost) in routing.Skip(1))
            {
                if (!stages.TryGetValue(slug, out var stg)) continue;
                t = SnapToShift(t);
                var dsAvail = GetAvailable(mid, t);
                if (dsAvail > t) t = dsAvail;

                db.StageExecutions.Add(new StageExecution
                {
                    JobId = job.Id,
                    ProductionStageId = stg.Id,
                    MachineId = Mid(mid),
                    Status = StageExecutionStatus.NotStarted,
                    ScheduledStartAt = t,
                    ScheduledEndAt = t.AddHours(hrs),
                    EstimatedHours = hrs,
                    EstimatedCost = cost,
                    QualityCheckRequired = slug == "qc",
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
                SetWatermark(mid, t.AddHours(hrs));
                t = t.AddHours(hrs + 0.15);
            }
            await db.SaveChangesAsync();
        }

        // ── NEW WORK ORDERS ──────────────────────────────────────────
        // Mix of RUSH/HIGH/NORMAL, staggered due dates across 3 weeks

        async Task<WorkOrder> CreateWO(string customer, string po, int dueDays,
            WorkOrderStatus status, JobPriority priority, (Part part, int qty)[] lines)
        {
            var wo = new WorkOrder
            {
                OrderNumber = $"WO-{woNum++:D5}",
                CustomerName = customer,
                CustomerPO = po,
                OrderDate = now.AddDays(-3),
                DueDate = now.AddDays(dueDays),
                ShipByDate = now.AddDays(dueDays - 2),
                Status = status,
                Priority = priority,
                CreatedBy = "System",
                LastModifiedBy = "System"
            };
            db.WorkOrders.Add(wo);
            await db.SaveChangesAsync();
            foreach (var (part, qty) in lines)
            {
                db.Set<WorkOrderLine>().Add(new WorkOrderLine
                {
                    WorkOrderId = wo.Id,
                    PartId = part.Id,
                    Quantity = qty,
                    Status = status
                });
            }
            await db.SaveChangesAsync();
            return wo;
        }

        // RUSH orders — due within 5-7 days
        var woRush1 = await CreateWO("Rugged Suppressors", "RS-2026-0401", 5,
            WorkOrderStatus.Released, JobPriority.Rush,
            [(pilate, 192), (tinman, 56)]);

        var woRush2 = await CreateWO("SilencerCo", "SC-2026-0402", 7,
            WorkOrderStatus.Released, JobPriority.Rush,
            [(handyman, 128)]);

        // HIGH priority — due in 10-14 days
        var woHigh1 = await CreateWO("Capitol Armory", "CA-2026-0405", 10,
            WorkOrderStatus.Released, JobPriority.High,
            [(gargoyle, 144), (tinman, 112)]);

        var woHigh2 = await CreateWO("Dead Air Silencers", "DA-2026-0408", 14,
            WorkOrderStatus.Released, JobPriority.High,
            [(handyman, 64), (pilate, 96)]);

        // NORMAL priority — due in 14-21 days
        var woNorm1 = await CreateWO("Silencer Shop", "SS-2026-0410", 18,
            WorkOrderStatus.Released, JobPriority.Normal,
            [(tinman, 168), (gargoyle, 72)]);

        var woNorm2 = await CreateWO("Thunder Beast Arms", "TB-2026-0412", 21,
            WorkOrderStatus.Released, JobPriority.Normal,
            [(pilate, 288), (handyman, 64)]);

        // ── SCHEDULE BUILDS — Dense 2-week Gantt ─────────────────────
        // Alternate between M4-1 and M4-2, mixing part types

        // M4-1 builds (Tinman, Gargoyle)
        await CreateBuild(pilate, "M4-1", 96, 16.0, $"Pilate 96x — Rush", woRush1, JobPriority.Rush, pilateRouting);
        await CreateBuild(tinman, "M4-1", 56, 22.5, $"Tinman 56x — Rush", woRush1, JobPriority.Rush, tinmanRouting);
        await CreateBuild(gargoyle, "M4-1", 72, 18.5, $"Gargoyle 72x — Capitol", woHigh1, JobPriority.High, gargoyleRouting);
        await CreateBuild(tinman, "M4-1", 56, 22.5, $"Tinman 56x — Capitol", woHigh1, JobPriority.High, tinmanRouting);
        await CreateBuild(gargoyle, "M4-1", 72, 18.5, $"Gargoyle 72x — SS", woNorm1, JobPriority.Normal, gargoyleRouting);

        // M4-2 builds (Handyman, Pilate)
        await CreateBuild(handyman, "M4-2", 64, 20.0, $"Handyman 64x — Rush", woRush2, JobPriority.Rush, handymanRouting);
        await CreateBuild(handyman, "M4-2", 64, 20.0, $"Handyman 64x — Rush #2", woRush2, JobPriority.Rush, handymanRouting);
        await CreateBuild(pilate, "M4-2", 96, 16.0, $"Pilate 96x — Dead Air", woHigh2, JobPriority.High, pilateRouting);
        await CreateBuild(handyman, "M4-2", 64, 20.0, $"Handyman 64x — Dead Air", woHigh2, JobPriority.Normal, handymanRouting);
        await CreateBuild(pilate, "M4-2", 96, 16.0, $"Pilate 96x — TBAC", woNorm2, JobPriority.Normal, pilateRouting);
        await CreateBuild(tinman, "M4-2", 56, 22.5, $"Tinman 56x — SS", woNorm1, JobPriority.Normal, tinmanRouting);

        // Mark as done
        db.SystemSettings.Add(new SystemSetting { Key = marker, Value = "true", LastModifiedBy = "System" });
        await db.SaveChangesAsync();
    }

    }
