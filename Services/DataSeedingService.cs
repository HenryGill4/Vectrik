using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services.Auth;

namespace Opcentrix_V3.Services;

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
                DefaultHourlyRate = 225.00m, DefaultSetupMinutes = 60,
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
                DefaultDurationHours = 0.33, HasBuiltInPage = true,
                DefaultHourlyRate = 90.00m, DefaultSetupMinutes = 25,
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
                BuildPlateCapacity = 2, AutoChangeoverEnabled = true, ChangeoverMinutes = 30,
                LaserCount = 6, MaxLaserPowerWatts = 1000,
                HourlyRate = 200.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "M4-2", Name = "EOS M4 Onyx #2", MachineType = "SLS",
                MachineModel = "EOS M 400-4", Department = "SLS",
                BuildLengthMm = 450, BuildWidthMm = 450, BuildHeightMm = 400,
                BuildPlateCapacity = 2, AutoChangeoverEnabled = true, ChangeoverMinutes = 30,
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
                MachineId = "LATHE1", Name = "CNC Lathe", MachineType = "CNC-Turning",
                MachineModel = "Haas ST-20Y", Department = "Machining",
                Priority = 4, HourlyRate = 95.00m, CreatedBy = "System", LastModifiedBy = "System"
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
            BuildPlateCapacity = 2, AutoChangeoverEnabled = true, ChangeoverMinutes = 30,
            LaserCount = 6, MaxLaserPowerWatts = 1000, IsAdditiveMachine = true,
            HourlyRate = 200.00m, CreatedBy = "System", LastModifiedBy = "System"
        },
        new()
        {
            MachineId = "M4-2", Name = "EOS M4 Onyx #2", MachineType = "SLS",
            MachineModel = "EOS M 400-4", Department = "SLS",
            BuildLengthMm = 450, BuildWidthMm = 450, BuildHeightMm = 400,
            BuildPlateCapacity = 2, AutoChangeoverEnabled = true, ChangeoverMinutes = 30,
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
        new() { Name = "SLS/LPBF Printing", StageSlug = "sls-printing", Department = "SLS", DefaultDurationHours = 8.0, HasBuiltInPage = true, DefaultHourlyRate = 225.00m, DefaultSetupMinutes = 60, DisplayOrder = 1, StageIcon = "🖨️", StageColor = "#3B82F6", RequiresMachineAssignment = true, DefaultMachineId = "M4-1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Depowdering", StageSlug = "depowdering", Department = "Post-Process", DefaultDurationHours = 1.0, HasBuiltInPage = true, DefaultHourlyRate = 55.00m, DefaultSetupMinutes = 10, DisplayOrder = 2, StageIcon = "💨", StageColor = "#F59E0B", RequiresMachineAssignment = true, DefaultMachineId = "INC1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Heat Treatment", StageSlug = "heat-treatment", Department = "Post-Process", DefaultDurationHours = 4.0, HasBuiltInPage = true, DefaultHourlyRate = 65.00m, DefaultSetupMinutes = 20, DisplayOrder = 3, StageIcon = "🔥", StageColor = "#EF4444", RequiresMachineAssignment = true, DefaultMachineId = "HT1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Wire EDM", StageSlug = "wire-edm", Department = "EDM", DefaultDurationHours = 2.0, HasBuiltInPage = true, DefaultHourlyRate = 85.00m, DefaultSetupMinutes = 25, DisplayOrder = 4, StageIcon = "⚡", StageColor = "#8B5CF6", RequiresMachineAssignment = true, DefaultMachineId = "EDM1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "CNC Machining", StageSlug = "cnc-machining", Department = "Machining", DefaultDurationHours = 0.5, HasBuiltInPage = true, DefaultHourlyRate = 95.00m, DefaultSetupMinutes = 30, DisplayOrder = 5, StageIcon = "⚙️", StageColor = "#06B6D4", RequiresMachineAssignment = true, DefaultMachineId = "CNC1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Laser Engraving", StageSlug = "laser-engraving", Department = "Engraving", DefaultDurationHours = 0.25, HasBuiltInPage = true, DefaultHourlyRate = 55.00m, DefaultSetupMinutes = 10, RequiresSerialNumber = true, DisplayOrder = 6, StageIcon = "✒️", StageColor = "#10B981", RequiresMachineAssignment = true, DefaultMachineId = "ENGRAVE1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Surface Finishing", StageSlug = "surface-finishing", Department = "Finishing", DefaultDurationHours = 0.33, HasBuiltInPage = true, DefaultHourlyRate = 45.00m, DefaultSetupMinutes = 10, DisplayOrder = 7, StageIcon = "🎨", StageColor = "#EC4899", RequiresMachineAssignment = true, DefaultMachineId = "FINISH1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Quality Control", StageSlug = "qc", Department = "Quality", DefaultDurationHours = 0.083, HasBuiltInPage = true, DefaultHourlyRate = 75.00m, DefaultSetupMinutes = 15, DisplayOrder = 8, StageIcon = "✅", StageColor = "#14B8A6", RequiresQualityCheck = true, RequiresMachineAssignment = true, DefaultMachineId = "QC1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "Shipping", StageSlug = "shipping", Department = "Shipping", DefaultDurationHours = 0.083, HasBuiltInPage = true, DefaultHourlyRate = 35.00m, DefaultSetupMinutes = 5, DisplayOrder = 9, StageIcon = "🚚", StageColor = "#6366F1", RequiresMachineAssignment = true, DefaultMachineId = "SHIP1", CreatedBy = "System", LastModifiedBy = "System" },
        new() { Name = "CNC Turning", StageSlug = "cnc-turning", Department = "Machining", DefaultDurationHours = 0.33, HasBuiltInPage = true, DefaultHourlyRate = 90.00m, DefaultSetupMinutes = 25, DisplayOrder = 10, StageIcon = "🔩", StageColor = "#0891B2", RequiresMachineAssignment = true, DefaultMachineId = "LATHE1", CreatedBy = "System", LastModifiedBy = "System" },
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
                    new() { Slug = "sandblasting",    Level = ProcessingLevel.Batch, BatchCapacityOverride = 20 },
                    new() { Slug = "cnc-machining",   Level = ProcessingLevel.Part },
                    new() { Slug = "laser-engraving", Level = ProcessingLevel.Batch, BatchCapacityOverride = 36 },
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
                    new() { Slug = "sandblasting",    Level = ProcessingLevel.Batch, BatchCapacityOverride = 20 },
                    new() { Slug = "cnc-machining",   Level = ProcessingLevel.Part },
                    new() { Slug = "laser-engraving", Level = ProcessingLevel.Batch, BatchCapacityOverride = 36 },
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
            new() { Key = "CompanyName", Value = "My Company", Category = "Branding", Description = "Company name displayed in the app", LastModifiedBy = "System" },
            new() { Key = "SerialNumberPrefix", Value = "SN", Category = "Serial", Description = "Prefix for generated serial numbers", LastModifiedBy = "System" },
            new() { Key = "ShowDebugBuildForm", Value = "true", Category = "Debug", Description = "Show the debug build file form on the Builds page", LastModifiedBy = "System" },
            new() { Key = "DefaultDueDateDays", Value = "30", Category = "General", Description = "Default number of days for work order due dates", LastModifiedBy = "System" },
            new() { Key = "DelayReasonCodes", Value = "Material Shortage,Machine Breakdown,Operator Unavailable,Quality Hold,Tooling Issue,Other", Category = "General", Description = "Comma-separated delay reason codes", LastModifiedBy = "System" },

            // Branding (Stage 0.5)
            new() { Key = "company.name", Value = "", Category = "Branding", Description = "Company name on all documents", LastModifiedBy = "System" },
            new() { Key = "company.logo_url", Value = "", Category = "Branding", Description = "Logo for reports/packing lists", LastModifiedBy = "System" },
            new() { Key = "company.address", Value = "", Category = "Branding", Description = "Address for documents", LastModifiedBy = "System" },

            // Defense identifiers
            new() { Key = "company.cage_code", Value = "", Category = "Defense", Description = "CAGE code for DLMS transactions", LastModifiedBy = "System" },
            new() { Key = "company.dodaac", Value = "", Category = "Defense", Description = "DoD Activity Address Code", LastModifiedBy = "System" },
            new() { Key = "company.duns", Value = "", Category = "Defense", Description = "DUNS/SAM UEI number", LastModifiedBy = "System" },

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
            new() { Key = "shipping.default_carrier", Value = "", Category = "Shipping", Description = "Default carrier name", LastModifiedBy = "System" },
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
            FullName = "Tenant Admin",
            Email = "admin@company.com",
            PasswordHash = _authService.HashPassword("admin123"),
            Role = "Admin",
            Department = "Administration",
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
                FullName = "Mike Johnson",
                Email = "mike@testcompany.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "Operator",
                Department = "SLS",
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                Username = "operator2",
                FullName = "Sarah Chen",
                Email = "sarah@testcompany.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "Operator",
                Department = "Machining",
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                Username = "manager",
                FullName = "Tom Bradley",
                Email = "tom@testcompany.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "Manager",
                Department = "Operations",
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                Username = "qcinspector",
                FullName = "Lisa Park",
                Email = "lisa@testcompany.com",
                PasswordHash = _authService.HashPassword("test123"),
                Role = "QualityInspector",
                Department = "Quality",
                CreatedBy = "System",
                LastModifiedBy = "System"
            }
        };

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
                        <strong>From:</strong><br/>OpCentrix Manufacturing<br/>{{CompanyAddress}}
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

            var order = 1;
            var processStages = new List<ProcessStage>();

            // Build-level stages
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

            if (stageBySlug.TryGetValue("heat-treatment", out var htStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = htStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Build,
                    SetupDurationMode = DurationMode.PerBuild,
                    SetupTimeMinutes = 30,
                    RunDurationMode = DurationMode.PerBuild,
                    RunTimeMinutes = 210,
                    AssignedMachineId = MachineIntId("HT1"),
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
                    SetupTimeMinutes = 20,
                    RunDurationMode = DurationMode.PerBuild,
                    RunTimeMinutes = 100,
                    AssignedMachineId = MachineIntId("EDM1"),
                    IsRequired = true,
                    IsBlocking = true,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                };
                processStages.Add(wireEdmPs);
            }

            // Batch-level stages
            if (stageBySlug.TryGetValue("sandblasting", out var sbStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = sbStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Batch,
                    SetupDurationMode = DurationMode.PerBatch,
                    SetupTimeMinutes = 5,
                    RunDurationMode = DurationMode.PerBatch,
                    RunTimeMinutes = 15,
                    BatchCapacityOverride = 20,
                    AssignedMachineId = MachineIntId("BLAST1"),
                    AllowRebatching = true,
                    IsRequired = false,
                    AllowSkip = true,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            // Surface Finishing (batch-level, after sandblasting)
            if (stageBySlug.TryGetValue("surface-finishing", out var sfStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = sfStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Batch,
                    SetupDurationMode = DurationMode.PerBatch,
                    SetupTimeMinutes = 10,
                    RunDurationMode = DurationMode.PerBatch,
                    RunTimeMinutes = 20,
                    BatchCapacityOverride = 30,
                    AssignedMachineId = MachineIntId("FINISH1"),
                    IsRequired = false,
                    AllowSkip = true,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            // Part-level stages
            if (stageBySlug.TryGetValue("cnc-machining", out var cncStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = cncStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Part,
                    SetupDurationMode = DurationMode.PerBatch,
                    SetupTimeMinutes = 30,
                    RunDurationMode = DurationMode.PerPart,
                    RunTimeMinutes = 8,
                    AssignedMachineId = MachineIntId("CNC1"),
                    PreferredMachineIds = string.Join(",",
                        new[] { "CNC1", "CNC2", "CNC3", "CNC4" }
                            .Select(id => MachineIntId(id))
                            .Where(id => id.HasValue)
                            .Select(id => id!.Value)),
                    IsRequired = true,
                    IsBlocking = true,
                    RequiresQualityCheck = false,
                    CreatedBy = "System",
                    LastModifiedBy = "System"
                });
            }

            if (stageBySlug.TryGetValue("laser-engraving", out var engStage))
            {
                processStages.Add(new ProcessStage
                {
                    ManufacturingProcessId = process.Id,
                    ProductionStageId = engStage.Id,
                    ExecutionOrder = order++,
                    ProcessingLevel = ProcessingLevel.Batch,
                    SetupDurationMode = DurationMode.PerBatch,
                    SetupTimeMinutes = 5,
                    RunDurationMode = DurationMode.PerBatch,
                    RunTimeMinutes = 15,
                    BatchCapacityOverride = 36,
                    AssignedMachineId = MachineIntId("ENGRAVE1"),
                    RequiresSerialNumber = true,
                    IsRequired = true,
                    IsBlocking = true,
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
                    SetupDurationMode = DurationMode.PerBatch,
                    SetupTimeMinutes = 5,
                    RunDurationMode = DurationMode.PerPart,
                    RunTimeMinutes = 5,
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
                    SetupDurationMode = DurationMode.PerBatch,
                    SetupTimeMinutes = 5,
                    RunDurationMode = DurationMode.PerBatch,
                    RunTimeMinutes = 10,
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
    /// Seeds comprehensive demo data: 5 parts, 12 work orders, 19+ completed builds,
    /// active and scheduled builds, with full stage execution history.
    /// </summary>
    private static async Task SeedSchedulerDemoDataAsync(TenantDbContext db)
    {
        if (await db.Jobs.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var slsApproach = await db.ManufacturingApproaches.FirstOrDefaultAsync(a => a.Slug == "sls-based");
        var suppApproach = await db.ManufacturingApproaches.FirstOrDefaultAsync(a => a.Slug == "suppressor-no-ht");
        var tiMat = await db.Materials.FirstOrDefaultAsync(m => m.Name.StartsWith("Ti-6Al-4V"));
        var ssMat = await db.Materials.FirstOrDefaultAsync(m => m.Name.StartsWith("316L"));
        var stages = await db.ProductionStages.ToDictionaryAsync(s => s.StageSlug, s => s);
        var machines = await db.Machines.Where(m => m.IsActive).ToDictionaryAsync(m => m.MachineId, m => m);
        int? Mid(string id) => machines.TryGetValue(id, out var m) ? m.Id : null;

        // Backfill approach FK on orphan parts
        if (slsApproach != null)
        {
            var orphans = await db.Parts.Where(p => p.ManufacturingApproachId == null && p.IsActive).ToListAsync();
            foreach (var p in orphans) p.ManufacturingApproachId = slsApproach.Id;
            if (orphans.Count > 0) await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════
        // PARTS — 5 SLS manufacturing parts
        // ════════════════════════════════════════════════════════════

        async Task<Part> EnsurePart(string pn, string name, string desc, string mat, int? matId, int? appId,
            bool stacking, int ppsS, double durS, int? ppsD = null, double? durD = null)
        {
            var part = await db.Parts.Include(p => p.AdditiveBuildConfig).FirstOrDefaultAsync(p => p.PartNumber == pn);
            if (part == null)
            {
                part = new Part { PartNumber = pn, Name = name, Description = desc, Material = mat,
                    MaterialId = matId, ManufacturingApproachId = appId, IsActive = true,
                    CreatedBy = "System", LastModifiedBy = "System" };
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
                    SingleStackDurationHours = durS, EnableDoubleStack = stacking,
                    EnableTripleStack = false };
                if (stacking && ppsD.HasValue) { bc.PlannedPartsPerBuildDouble = ppsD.Value; bc.DoubleStackDurationHours = durD; }
                db.Set<PartAdditiveBuildConfig>().Add(bc);
                await db.SaveChangesAsync();
            }
            return part;
        }

        var suppPart = await EnsurePart("PSA-SUPP-001", "PSA Suppressor Body",
            "Titanium PSA suppressor body — SLS printed, EDM cut, CNC finished",
            "Ti-6Al-4V Grade 5", tiMat?.Id, suppApproach?.Id ?? slsApproach?.Id,
            false, 72, 20.2);

        var bracketPart = await EnsurePart("DM-BRK-001", "Mounting Bracket",
            "316L stainless mounting bracket — SLS printed with full post-processing",
            "316L Stainless Steel", ssMat?.Id, slsApproach?.Id,
            true, 48, 14.5, 96, 22.0);

        var manifoldPart = await EnsurePart("DM-MAN-001", "Hydraulic Manifold",
            "Titanium hydraulic manifold — SLS printed, heat treated, CNC finished",
            "Ti-6Al-4V Grade 5", tiMat?.Id, slsApproach?.Id,
            false, 24, 26.8);

        var nozzlePart = await EnsurePart("PSA-NOZ-001", "Nozzle Assembly",
            "Titanium nozzle assembly — SLS printed, EDM cut, precision CNC finished",
            "Ti-6Al-4V Grade 5", tiMat?.Id, suppApproach?.Id ?? slsApproach?.Id,
            false, 36, 18.5);

        var housingPart = await EnsurePart("DM-HSG-001", "Sensor Housing",
            "316L stainless sensor housing — SLS printed, heat treated, CNC finished",
            "316L Stainless Steel", ssMat?.Id, slsApproach?.Id,
            true, 60, 16.0, 120, 24.5);

        // ════════════════════════════════════════════════════════════
        // MANUFACTURING PROCESSES — one per part
        // ════════════════════════════════════════════════════════════

        async Task EnsureProcess(Part part, int? appId, string appName,
            (string slug, ProcessingLevel lvl, double? setup, double? run, string? machineId, bool slicer, bool release, int? batch)[] routing)
        {
            if (await db.ManufacturingProcesses.AnyAsync(p => p.PartId == part.Id && p.IsActive)) return;
            var proc = new ManufacturingProcess { PartId = part.Id, ManufacturingApproachId = appId,
                Name = $"{part.PartNumber} — {appName}", Description = $"Manufacturing process for {part.Name}",
                DefaultBatchCapacity = 60, IsActive = true, Version = 1,
                CreatedBy = "System", LastModifiedBy = "System", CreatedDate = now, LastModifiedDate = now };
            db.ManufacturingProcesses.Add(proc);
            await db.SaveChangesAsync();
            var ord = 1;
            foreach (var r in routing)
            {
                if (!stages.TryGetValue(r.slug, out var stg)) continue;
                db.ProcessStages.Add(new ProcessStage { ManufacturingProcessId = proc.Id, ProductionStageId = stg.Id,
                    ExecutionOrder = ord++, ProcessingLevel = r.lvl, SetupTimeMinutes = r.setup, RunTimeMinutes = r.run,
                    DurationFromBuildConfig = r.slicer, AssignedMachineId = Mid(r.machineId ?? ""),
                    BatchCapacityOverride = r.batch, IsRequired = true, IsBlocking = true,
                    CreatedBy = "System", LastModifiedBy = "System" });
            }
            await db.SaveChangesAsync();
        }

        await EnsureProcess(suppPart, suppApproach?.Id ?? slsApproach?.Id, "Suppressor (No HT)", [
            ("sls-printing",    ProcessingLevel.Build, null, null, "M4-1",    true,  false, null),
            ("depowdering",     ProcessingLevel.Build, 15,  45,   "INC1",    false, false, null),
            ("wire-edm",        ProcessingLevel.Build, 25,  90,   "EDM1",    false, true,  null),
            ("sandblasting",    ProcessingLevel.Batch, null, 15,  "BLAST1",  false, false, 20),
            ("cnc-machining",   ProcessingLevel.Part,  30,  18,   "CNC1",    false, false, null),
            ("laser-engraving", ProcessingLevel.Batch, null, 5,   "ENGRAVE1",false, false, 36),
            ("qc",              ProcessingLevel.Part,  null, 5,   "QC1",     false, false, null),
            ("packaging",       ProcessingLevel.Batch, null, 3,   "PACK1",   false, false, 50),
        ]);

        await EnsureProcess(bracketPart, slsApproach?.Id, "SLS-Based", [
            ("sls-printing",    ProcessingLevel.Build, null, null, "M4-2",    true,  false, null),
            ("depowdering",     ProcessingLevel.Build, 15,  60,   "INC1",    false, false, null),
            ("heat-treatment",  ProcessingLevel.Build, 20,  240,  "HT1",     false, false, null),
            ("wire-edm",        ProcessingLevel.Build, 25,  75,   "EDM1",    false, true,  null),
            ("sandblasting",    ProcessingLevel.Batch, null, 12,  "BLAST1",  false, false, 24),
            ("cnc-machining",   ProcessingLevel.Part,  20,  12,   "CNC2",    false, false, null),
            ("laser-engraving", ProcessingLevel.Batch, null, 4,   "ENGRAVE1",false, false, 36),
            ("qc",              ProcessingLevel.Part,  null, 4,   "QC1",     false, false, null),
            ("packaging",       ProcessingLevel.Batch, null, 3,   "PACK1",   false, false, 50),
        ]);

        await EnsureProcess(manifoldPart, slsApproach?.Id, "SLS-Based", [
            ("sls-printing",    ProcessingLevel.Build, null, null, "M4-1",    true,  false, null),
            ("depowdering",     ProcessingLevel.Build, 15,  50,   "INC1",    false, false, null),
            ("heat-treatment",  ProcessingLevel.Build, 20,  240,  "HT1",     false, false, null),
            ("wire-edm",        ProcessingLevel.Build, 25,  120,  "EDM1",    false, true,  null),
            ("cnc-machining",   ProcessingLevel.Part,  45,  35,   "CNC4",    false, false, null),
            ("surface-finishing",ProcessingLevel.Part, null, 15,   "FINISH1", false, false, null),
            ("qc",              ProcessingLevel.Part,  null, 8,   "QC1",     false, false, null),
            ("packaging",       ProcessingLevel.Batch, null, 3,   "PACK1",   false, false, 24),
        ]);

        await EnsureProcess(nozzlePart, suppApproach?.Id ?? slsApproach?.Id, "Nozzle (No HT)", [
            ("sls-printing",    ProcessingLevel.Build, null, null, "M4-1",    true,  false, null),
            ("depowdering",     ProcessingLevel.Build, 15,  40,   "INC1",    false, false, null),
            ("wire-edm",        ProcessingLevel.Build, 20,  75,   "EDM1",    false, true,  null),
            ("sandblasting",    ProcessingLevel.Batch, null, 12,  "BLAST1",  false, false, 20),
            ("cnc-machining",   ProcessingLevel.Part,  25,  18,   "CNC1",    false, false, null),
            ("laser-engraving", ProcessingLevel.Batch, null, 4,   "ENGRAVE1",false, false, 36),
            ("qc",              ProcessingLevel.Part,  null, 5,   "QC1",     false, false, null),
            ("packaging",       ProcessingLevel.Batch, null, 3,   "PACK1",   false, false, 50),
        ]);

        await EnsureProcess(housingPart, slsApproach?.Id, "SLS-Based", [
            ("sls-printing",     ProcessingLevel.Build, null, null, "M4-2",   true,  false, null),
            ("depowdering",      ProcessingLevel.Build, 15,  50,   "INC1",   false, false, null),
            ("heat-treatment",   ProcessingLevel.Build, 20,  240,  "HT1",    false, false, null),
            ("wire-edm",         ProcessingLevel.Build, 25,  75,   "EDM1",   false, true,  null),
            ("cnc-machining",    ProcessingLevel.Part,  25,  12,   "CNC2",   false, false, null),
            ("surface-finishing",ProcessingLevel.Part,  null, 5,   "FINISH1",false, false, null),
            ("qc",               ProcessingLevel.Part,  null, 4,   "QC1",    false, false, null),
            ("packaging",        ProcessingLevel.Batch, null, 3,   "PACK1",  false, false, 24),
        ]);

        // ════════════════════════════════════════════════════════════
        // WORK ORDERS — 12 realistic customer orders
        // ════════════════════════════════════════════════════════════

        var wos = new List<WorkOrder>();

        async Task<WorkOrder> CreateWO(string num, string customer, string po, int daysAgo, int dueDays,
            WorkOrderStatus status, JobPriority priority, (Part part, int qty)[] lines)
        {
            var wo = new WorkOrder { OrderNumber = num, CustomerName = customer, CustomerPO = po,
                OrderDate = now.AddDays(-daysAgo), DueDate = now.AddDays(dueDays),
                ShipByDate = now.AddDays(dueDays - 2), Status = status, Priority = priority,
                CreatedBy = "System", LastModifiedBy = "System" };
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
        var wo1 = await CreateWO("WO-00001", "Apex Industries", "APX-2026-0341", 60, -30,
            WorkOrderStatus.Complete, JobPriority.Normal,
            [(suppPart, 72), (bracketPart, 48)]);

        var wo2 = await CreateWO("WO-00002", "TechForge Solutions", "TF-PO-8801", 52, -22,
            WorkOrderStatus.Complete, JobPriority.Normal,
            [(nozzlePart, 36), (housingPart, 60)]);

        var wo3 = await CreateWO("WO-00003", "PSA Defense", "PSA-2026-2101", 45, -15,
            WorkOrderStatus.Complete, JobPriority.High,
            [(suppPart, 144), (bracketPart, 96)]);

        var wo4 = await CreateWO("WO-00004", "Summit Dynamics", "SD-PO-3301", 40, -10,
            WorkOrderStatus.Complete, JobPriority.Normal,
            [(manifoldPart, 24), (bracketPart, 48)]);

        var wo5 = await CreateWO("WO-00005", "Henry Gill", "HG-2026-0103", 32, -5,
            WorkOrderStatus.Complete, JobPriority.Normal,
            [(suppPart, 72), (housingPart, 60)]);

        var wo6 = await CreateWO("WO-00006", "Apex Industries", "APX-2026-0441", 24, -2,
            WorkOrderStatus.Complete, JobPriority.Normal,
            [(suppPart, 72), (bracketPart, 96)]);

        // ── In progress ──
        var wo7 = await CreateWO("WO-00007", "TechForge Solutions", "TF-PO-8891", 16, 5,
            WorkOrderStatus.InProgress, JobPriority.Normal,
            [(nozzlePart, 36), (housingPart, 120)]);

        var wo8 = await CreateWO("WO-00008", "PSA Defense", "PSA-2026-2201", 12, 14,
            WorkOrderStatus.InProgress, JobPriority.High,
            [(suppPart, 144), (bracketPart, 96)]);

        var wo9 = await CreateWO("WO-00009", "Summit Dynamics", "SD-PO-3377", 7, 21,
            WorkOrderStatus.InProgress, JobPriority.Normal,
            [(manifoldPart, 24), (suppPart, 72)]);

        // ── Released — needs scheduling ──
        var wo10 = await CreateWO("WO-00010", "Henry Gill", "HG-2026-0201", 3, 28,
            WorkOrderStatus.Released, JobPriority.High,
            [(suppPart, 216), (bracketPart, 192)]);

        var wo11 = await CreateWO("WO-00011", "Apex Industries", "APX-2026-0512", 2, 14,
            WorkOrderStatus.Released, JobPriority.Rush,
            [(manifoldPart, 48)]);

        var wo12 = await CreateWO("WO-00012", "TechForge Solutions", "TF-PO-9004", 1, 35,
            WorkOrderStatus.Released, JobPriority.Normal,
            [(bracketPart, 144), (nozzlePart, 72)]);

        // ════════════════════════════════════════════════════════════
        // BUILD PLATE PROGRAMS — masters, completed, active, scheduled
        // ════════════════════════════════════════════════════════════

        var slicerParams = System.Text.Json.JsonSerializer.Serialize(new {
            laserPower = 370, scanSpeed = 1200, layerThickness = 0.03, hatchSpacing = 0.12, contourCount = 2 });

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

        async Task LinkParts(MachineProgram prog, (Part part, int qty, int stack, WorkOrder wo)[] items)
        {
            foreach (var (part, qty, stack, wo) in items)
            {
                var woLine = await db.Set<WorkOrderLine>().FirstOrDefaultAsync(l => l.WorkOrderId == wo.Id && l.PartId == part.Id);
                db.ProgramParts.Add(new ProgramPart { MachineProgramId = prog.Id, PartId = part.Id,
                    Quantity = qty, StackLevel = stack, WorkOrderLineId = woLine?.Id });
            }
            await db.SaveChangesAsync();
        }

        // ── Master templates (reusable, not scheduled) ──
        var masterSupp72 = await CreateProgram("BP-00001", "PSA Suppressor 72x", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 20.2, 3100, 95.0, 14.8, "PSA_Supp_72x_v2.sli",
            false, null, null, null, null, null);
        await LinkParts(masterSupp72, [(suppPart, 72, 1, wo10)]);

        var masterBrk48 = await CreateProgram("BP-00002", "Bracket 48x Single Stack", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, ssMat?.Id, 14.5, 2200, 68.0, 9.2, "Bracket_48x_SS_v1.sli",
            false, null, null, null, null, null);
        await LinkParts(masterBrk48, [(bracketPart, 48, 1, wo10)]);

        var masterBrk96 = await CreateProgram("BP-00003", "Bracket 96x Double Stack", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, ssMat?.Id, 22.0, 3400, 132.0, 17.6, "Bracket_96x_DS_v1.sli",
            false, null, null, null, null, null);
        await LinkParts(masterBrk96, [(bracketPart, 48, 1, wo10), (bracketPart, 48, 2, wo10)]);

        var masterMan24 = await CreateProgram("BP-00004", "Manifold 24x Build", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 26.8, 4100, 155.0, 22.5, "Manifold_24x_v1.sli",
            false, null, null, null, null, null);
        await LinkParts(masterMan24, [(manifoldPart, 24, 1, wo11)]);

        var masterNoz36 = await CreateProgram("BP-00005", "Nozzle 36x Build", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, tiMat?.Id, 18.5, 2600, 78.0, 11.2, "Nozzle_36x_v1.sli",
            false, null, null, null, null, null);
        await LinkParts(masterNoz36, [(nozzlePart, 36, 1, wo12)]);

        var masterHsg60 = await CreateProgram("BP-00006", "Housing 60x Build", ProgramType.BuildPlate,
            ProgramScheduleStatus.Ready, ssMat?.Id, 16.0, 2400, 72.0, 10.0, "Housing_60x_v1.sli",
            false, null, null, null, null, null);
        await LinkParts(masterHsg60, [(housingPart, 60, 1, wo7)]);

        // ════════════════════════════════════════════════════════════
        // STAGE ROUTING TEMPLATES — per-part cost/time definitions
        // (stageSlug, defaultMachineId, estimatedHours, estimatedCost)
        // ════════════════════════════════════════════════════════════

        var suppRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-1", 20.2, 4545m), ("depowdering", "INC1", 1.0, 55m),
            ("wire-edm", "EDM1", 1.9, 162m), ("sandblasting", "BLAST1", 1.0, 40m),
            ("cnc-machining", "CNC1", 21.6, 2052m), ("laser-engraving", "ENGRAVE1", 0.6, 33m),
            ("qc", "QC1", 6.0, 450m), ("packaging", "PACK1", 0.7, 25m) };

        var brkRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-2", 14.5, 3263m), ("depowdering", "INC1", 1.25, 69m),
            ("heat-treatment", "HT1", 4.3, 280m), ("wire-edm", "EDM1", 1.7, 145m),
            ("sandblasting", "BLAST1", 0.5, 20m), ("cnc-machining", "CNC2", 9.6, 912m),
            ("laser-engraving", "ENGRAVE1", 0.5, 28m), ("qc", "QC1", 3.2, 240m),
            ("packaging", "PACK1", 0.5, 18m) };

        var brkDsRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-2", 22.0, 4950m), ("depowdering", "INC1", 1.25, 69m),
            ("heat-treatment", "HT1", 4.3, 280m), ("wire-edm", "EDM1", 1.7, 145m),
            ("sandblasting", "BLAST1", 1.0, 40m), ("cnc-machining", "CNC2", 19.2, 1824m),
            ("laser-engraving", "ENGRAVE1", 1.0, 55m), ("qc", "QC1", 6.4, 480m),
            ("packaging", "PACK1", 1.0, 35m) };

        var manRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-1", 26.8, 6030m), ("depowdering", "INC1", 1.1, 61m),
            ("heat-treatment", "HT1", 4.3, 280m), ("wire-edm", "EDM1", 2.4, 204m),
            ("cnc-machining", "CNC4", 14.0, 1470m), ("surface-finishing", "FINISH1", 6.0, 270m),
            ("qc", "QC1", 3.2, 240m), ("packaging", "PACK1", 0.5, 18m) };

        var nozRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-1", 18.5, 4163m), ("depowdering", "INC1", 0.8, 44m),
            ("wire-edm", "EDM1", 1.5, 128m), ("sandblasting", "BLAST1", 0.8, 32m),
            ("cnc-machining", "CNC1", 10.8, 1026m), ("laser-engraving", "ENGRAVE1", 0.4, 22m),
            ("qc", "QC1", 3.0, 225m), ("packaging", "PACK1", 0.4, 14m) };

        var hsgRouting = new (string, string, double, decimal)[] {
            ("sls-printing", "M4-2", 16.0, 3600m), ("depowdering", "INC1", 1.0, 55m),
            ("heat-treatment", "HT1", 4.3, 280m), ("wire-edm", "EDM1", 1.5, 128m),
            ("cnc-machining", "CNC2", 12.0, 1140m), ("surface-finishing", "FINISH1", 5.0, 225m),
            ("qc", "QC1", 4.0, 300m), ("packaging", "PACK1", 0.5, 18m) };

        // ════════════════════════════════════════════════════════════
        // JOBS + STAGE EXECUTIONS — helpers
        // ════════════════════════════════════════════════════════════

        int jobNum = 1;

        async Task<Job> CreateJob(Part part, int? machineId, int qty, JobScope scope, JobStatus status,
            DateTime schedStart, DateTime schedEnd, DateTime? actStart, DateTime? actEnd,
            int? woLinePartId, WorkOrder? wo, int produced = 0)
        {
            var woLine = wo != null && woLinePartId.HasValue
                ? await db.Set<WorkOrderLine>().FirstOrDefaultAsync(l => l.WorkOrderId == wo.Id && l.PartId == woLinePartId)
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

        // ── Helper: create a fully completed build with all stage executions ──
        async Task<MachineProgram> CompletedBuild(int idx, string num, string name,
            Part part, int qty, double printHrs, int layers, double height, double powder,
            string slicer, int? matId, int? sourceId, string machine, DateTime start, WorkOrder wo,
            (string slug, string mid, double hrs, decimal cost)[] routing,
            (Part p2, int qty2)? stack2 = null)
        {
            var end = start.AddHours(printHrs);
            var prog = await CreateProgram(num, name, ProgramType.BuildPlate,
                ProgramScheduleStatus.Completed, matId, printHrs, layers, height, powder, slicer,
                true, start, start, end, end.AddHours(1.5), sourceId, machine);

            if (stack2.HasValue)
                await LinkParts(prog, [(part, qty, 1, wo), (stack2.Value.p2, stack2.Value.qty2, 2, wo)]);
            else
                await LinkParts(prog, [(part, qty, 1, wo)]);

            var totalQty = qty + (stack2?.qty2 ?? 0);
            var downHrs = routing.Skip(1).Sum(r => r.hrs + 0.3);
            var jobEnd = end.AddHours(downHrs + 1);
            var j = await CreateJob(part, Mid(machine), totalQty, JobScope.Build, JobStatus.Completed,
                start, jobEnd, start, jobEnd.AddHours(-0.5), part.Id, wo, totalQty);

            var t = start;
            var v = 1.0 + (idx % 5 - 2) * 0.01; // ±2% deterministic variation
            foreach (var (slug, mid, hrs, cost) in routing)
            {
                var actualMachine = slug == "sls-printing" ? machine : mid;
                await CreateStageExec(j, slug, actualMachine, StageExecutionStatus.Completed,
                    t, t.AddHours(hrs), t.AddHours(0.15), t.AddHours(hrs * v + 0.15),
                    hrs, hrs * v, cost, cost * (decimal)v,
                    slug == "sls-printing" ? prog.Id : null);
                t = t.AddHours(hrs + 0.3);
            }
            await db.SaveChangesAsync();
            return prog;
        }

        // ════════════════════════════════════════════════════════════
        // COMPLETED BUILDS — M4-1 (Ti-6Al-4V parts: Supp, Noz, Man)
        // Builds spaced ~5 days apart, each prints 18-27 hours
        // ════════════════════════════════════════════════════════════

        int bpn = 10;

        // B1: Supp 72x → wo1 (started 52 days ago)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "PSA Supp 72x — Run #1", suppPart, 72,
            20.2, 3100, 95, 14.8, "PSA_Supp_72x_v2.sli", tiMat?.Id, masterSupp72.Id,
            "M4-1", now.AddDays(-52), wo1, suppRouting);

        // B2: Noz 36x → wo2
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Nozzle 36x — Run #1", nozzlePart, 36,
            18.5, 2600, 78, 11.2, "Nozzle_36x_v1.sli", tiMat?.Id, masterNoz36.Id,
            "M4-1", now.AddDays(-47), wo2, nozRouting);

        // B3: Supp 72x → wo3 (first of 2 batches)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "PSA Supp 72x — Run #2", suppPart, 72,
            20.2, 3100, 95, 14.8, "PSA_Supp_72x_v2.sli", tiMat?.Id, masterSupp72.Id,
            "M4-1", now.AddDays(-42), wo3, suppRouting);

        // B4: Man 24x → wo4
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Manifold 24x — Run #1", manifoldPart, 24,
            26.8, 4100, 155, 22.5, "Manifold_24x_v1.sli", tiMat?.Id, masterMan24.Id,
            "M4-1", now.AddDays(-37), wo4, manRouting);

        // B5: Supp 72x → wo3 (second of 2 batches)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "PSA Supp 72x — Run #3", suppPart, 72,
            20.2, 3100, 95, 14.8, "PSA_Supp_72x_v2.sli", tiMat?.Id, masterSupp72.Id,
            "M4-1", now.AddDays(-31), wo3, suppRouting);

        // B6: Supp 72x → wo5
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "PSA Supp 72x — Run #4", suppPart, 72,
            20.2, 3100, 95, 14.8, "PSA_Supp_72x_v2.sli", tiMat?.Id, masterSupp72.Id,
            "M4-1", now.AddDays(-26), wo5, suppRouting);

        // B7: Supp 72x → wo6
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "PSA Supp 72x — Run #5", suppPart, 72,
            20.2, 3100, 95, 14.8, "PSA_Supp_72x_v2.sli", tiMat?.Id, masterSupp72.Id,
            "M4-1", now.AddDays(-20), wo6, suppRouting);

        // B8: Noz 36x → wo7
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Nozzle 36x — Run #2", nozzlePart, 36,
            18.5, 2600, 78, 11.2, "Nozzle_36x_v1.sli", tiMat?.Id, masterNoz36.Id,
            "M4-1", now.AddDays(-15), wo7, nozRouting);

        // B9: Supp 72x → wo8 (first of 2)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "PSA Supp 72x — Run #6", suppPart, 72,
            20.2, 3100, 95, 14.8, "PSA_Supp_72x_v2.sli", tiMat?.Id, masterSupp72.Id,
            "M4-1", now.AddDays(-10), wo8, suppRouting);

        // B10: Supp 72x → wo8 (second of 2)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "PSA Supp 72x — Run #7", suppPart, 72,
            20.2, 3100, 95, 14.8, "PSA_Supp_72x_v2.sli", tiMat?.Id, masterSupp72.Id,
            "M4-1", now.AddDays(-5), wo8, suppRouting);

        // ════════════════════════════════════════════════════════════
        // COMPLETED BUILDS — M4-2 (316L SS parts: Bracket, Housing)
        // ════════════════════════════════════════════════════════════

        // B1: Brk 48x → wo1
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Bracket 48x — Run #1", bracketPart, 48,
            14.5, 2200, 68, 9.2, "Bracket_48x_SS_v1.sli", ssMat?.Id, masterBrk48.Id,
            "M4-2", now.AddDays(-51), wo1, brkRouting);

        // B2: Hsg 60x → wo2
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Housing 60x — Run #1", housingPart, 60,
            16.0, 2400, 72, 10.0, "Housing_60x_v1.sli", ssMat?.Id, masterHsg60.Id,
            "M4-2", now.AddDays(-46), wo2, hsgRouting);

        // B3: Brk 96x DS → wo3 (48 stack1 + 48 stack2 = 96)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Bracket 96x DS — Run #1", bracketPart, 48,
            22.0, 3400, 132, 17.6, "Bracket_96x_DS_v1.sli", ssMat?.Id, masterBrk96.Id,
            "M4-2", now.AddDays(-41), wo3, brkDsRouting, stack2: (bracketPart, 48));

        // B4: Brk 48x → wo4
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Bracket 48x — Run #2", bracketPart, 48,
            14.5, 2200, 68, 9.2, "Bracket_48x_SS_v1.sli", ssMat?.Id, masterBrk48.Id,
            "M4-2", now.AddDays(-36), wo4, brkRouting);

        // B5: Hsg 60x → wo5
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Housing 60x — Run #2", housingPart, 60,
            16.0, 2400, 72, 10.0, "Housing_60x_v1.sli", ssMat?.Id, masterHsg60.Id,
            "M4-2", now.AddDays(-30), wo5, hsgRouting);

        // B6: Brk 96x DS → wo6
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Bracket 96x DS — Run #2", bracketPart, 48,
            22.0, 3400, 132, 17.6, "Bracket_96x_DS_v1.sli", ssMat?.Id, masterBrk96.Id,
            "M4-2", now.AddDays(-25), wo6, brkDsRouting, stack2: (bracketPart, 48));

        // B7: Hsg 60x → wo7 (first batch — 60 of 120)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Housing 60x — Run #3", housingPart, 60,
            16.0, 2400, 72, 10.0, "Housing_60x_v1.sli", ssMat?.Id, masterHsg60.Id,
            "M4-2", now.AddDays(-19), wo7, hsgRouting);

        // B8: Brk 48x → wo8 (first of 2)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Bracket 48x — Run #3", bracketPart, 48,
            14.5, 2200, 68, 9.2, "Bracket_48x_SS_v1.sli", ssMat?.Id, masterBrk48.Id,
            "M4-2", now.AddDays(-13), wo8, brkRouting);

        // B9: Brk 48x → wo8 (second of 2)
        await CompletedBuild(bpn, $"BP-{bpn++:D5}", "Bracket 48x — Run #4", bracketPart, 48,
            14.5, 2200, 68, 9.2, "Bracket_48x_SS_v1.sli", ssMat?.Id, masterBrk48.Id,
            "M4-2", now.AddDays(-8), wo8, brkRouting);

        // ════════════════════════════════════════════════════════════
        // ACTIVE BUILDS — currently on the machines
        // ════════════════════════════════════════════════════════════

        // M4-1: Manifold 24x currently PRINTING (~75% done) → wo9
        var activeM41Start = now.AddHours(-20);
        var activeM41 = await CreateProgram($"BP-{bpn++:D5}", "Manifold 24x — Run #2", ProgramType.BuildPlate,
            ProgramScheduleStatus.Printing, tiMat?.Id, 26.8, 4100, 155, 22.5, "Manifold_24x_v1.sli",
            true, activeM41Start, activeM41Start, null, null, masterMan24.Id, "M4-1");
        await LinkParts(activeM41, [(manifoldPart, 24, 1, wo9)]);
        {
            var j = await CreateJob(manifoldPart, Mid("M4-1"), 24, JobScope.Build, JobStatus.InProgress,
                activeM41Start, activeM41Start.AddHours(60), activeM41Start, null, manifoldPart.Id, wo9);
            await CreateStageExec(j, "sls-printing", "M4-1", StageExecutionStatus.InProgress,
                activeM41Start, activeM41Start.AddHours(26.8), activeM41Start, null, 26.8, null, 6030m, null, activeM41.Id);
            await CreateStageExec(j, "depowdering", "INC1", StageExecutionStatus.NotStarted,
                activeM41Start.AddHours(27.3), activeM41Start.AddHours(28.4), null, null, 1.1, null, 61m, null);
            await CreateStageExec(j, "heat-treatment", "HT1", StageExecutionStatus.NotStarted,
                activeM41Start.AddHours(28.9), activeM41Start.AddHours(33.2), null, null, 4.3, null, 280m, null);
            await CreateStageExec(j, "wire-edm", "EDM1", StageExecutionStatus.NotStarted,
                activeM41Start.AddHours(33.7), activeM41Start.AddHours(36.1), null, null, 2.4, null, 204m, null);
            await db.SaveChangesAsync();
        }

        // M4-2: Housing 60x in POST-PRINT (print done ~10h ago) → wo7
        var activeM42Start = now.AddHours(-26);
        var activeM42End = activeM42Start.AddHours(16.0);
        var activeM42 = await CreateProgram($"BP-{bpn++:D5}", "Housing 60x — Run #4", ProgramType.BuildPlate,
            ProgramScheduleStatus.PostPrint, ssMat?.Id, 16.0, 2400, 72, 10.0, "Housing_60x_v1.sli",
            true, activeM42Start, activeM42Start, activeM42End, null, masterHsg60.Id, "M4-2");
        await LinkParts(activeM42, [(housingPart, 60, 1, wo7)]);
        {
            var j = await CreateJob(housingPart, Mid("M4-2"), 60, JobScope.Build, JobStatus.InProgress,
                activeM42Start, activeM42Start.AddHours(50), activeM42Start, null, housingPart.Id, wo7);
            await CreateStageExec(j, "sls-printing", "M4-2", StageExecutionStatus.Completed,
                activeM42Start, activeM42Start.AddHours(16), activeM42Start, activeM42End, 16.0, 16.0, 3600m, 3600m, activeM42.Id);
            await CreateStageExec(j, "depowdering", "INC1", StageExecutionStatus.NotStarted,
                activeM42End.AddHours(0.5), activeM42End.AddHours(1.5), null, null, 1.0, null, 55m, null);
            await CreateStageExec(j, "heat-treatment", "HT1", StageExecutionStatus.NotStarted,
                activeM42End.AddHours(2), activeM42End.AddHours(6.3), null, null, 4.3, null, 280m, null);
            await CreateStageExec(j, "wire-edm", "EDM1", StageExecutionStatus.NotStarted,
                activeM42End.AddHours(6.8), activeM42End.AddHours(8.3), null, null, 1.5, null, 128m, null);
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════
        // SCHEDULED BUILD — queued for M4-1 after active finishes
        // ════════════════════════════════════════════════════════════

        var schedStart = activeM41Start.AddHours(26.8 + 0.5); // 30min after active M4-1 finishes
        var schedM41 = await CreateProgram($"BP-{bpn++:D5}", "PSA Supp 72x — Run #8", ProgramType.BuildPlate,
            ProgramScheduleStatus.Scheduled, tiMat?.Id, 20.2, 3100, 95, 14.8, "PSA_Supp_72x_v2.sli",
            true, schedStart, null, null, null, masterSupp72.Id, "M4-1");
        await LinkParts(schedM41, [(suppPart, 72, 1, wo9)]);

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

        // wo1: Complete — 72 supp + 48 brk (all shipped)
        await UpdateLine(wo1, suppPart, 72, 72);
        await UpdateLine(wo1, bracketPart, 48, 48);

        // wo2: Complete — 36 noz + 60 hsg (all shipped)
        await UpdateLine(wo2, nozzlePart, 36, 36);
        await UpdateLine(wo2, housingPart, 60, 60);

        // wo3: Complete — 144 supp (2 builds) + 96 brk DS (all shipped)
        await UpdateLine(wo3, suppPart, 144, 144);
        await UpdateLine(wo3, bracketPart, 96, 96);

        // wo4: Complete — 24 man + 48 brk (all shipped)
        await UpdateLine(wo4, manifoldPart, 24, 24);
        await UpdateLine(wo4, bracketPart, 48, 48);

        // wo5: Complete — 72 supp + 60 hsg (all shipped)
        await UpdateLine(wo5, suppPart, 72, 72);
        await UpdateLine(wo5, housingPart, 60, 60);

        // wo6: Complete — 72 supp + 96 brk DS (all shipped)
        await UpdateLine(wo6, suppPart, 72, 72);
        await UpdateLine(wo6, bracketPart, 96, 96);

        // wo7: InProgress — 36 noz done, 60/120 hsg (second batch on M4-2 post-print)
        await UpdateLine(wo7, nozzlePart, 36);
        await UpdateLine(wo7, housingPart, 60);

        // wo8: InProgress — 144 supp done (2 builds), 96 brk done (2 builds)
        await UpdateLine(wo8, suppPart, 144);
        await UpdateLine(wo8, bracketPart, 96);

        // wo9: InProgress — manifold printing, supp scheduled (0 produced yet)
        // wo10-12: Released — nothing produced yet

        await db.SaveChangesAsync();
    }


    }
