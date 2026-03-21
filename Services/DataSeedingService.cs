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
        await SeedMaterialsAsync(tenantDb);
        await SeedManufacturingApproachesAsync(tenantDb);
        await SeedOperatingShiftsAsync(tenantDb);
        await SeedSystemSettingsAsync(tenantDb);
        await SeedDefaultAdminUserAsync(tenantDb);
        await SeedTestUsersAsync(tenantDb);
        await SeedDocumentTemplatesAsync(tenantDb);

        // Inventory (depends on materials)
        await SeedStockLocationsAsync(tenantDb);
        await SeedInventoryItemsAsync(tenantDb);

        // Manufacturing processes (depends on parts + production stages)
        await SeedManufacturingProcessesAsync(tenantDb);
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
                DefaultMachineId = "M4-1", AssignedMachineIds = "M4-1,M4-2",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Depowdering", StageSlug = "depowdering", Department = "Post-Process",
                DefaultDurationHours = 1.0, HasBuiltInPage = true,
                DefaultHourlyRate = 55.00m, DefaultSetupMinutes = 10,
                DisplayOrder = 2, StageIcon = "💨", StageColor = "#F59E0B",
                RequiresMachineAssignment = true,
                DefaultMachineId = "INC1", AssignedMachineIds = "INC1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Heat Treatment", StageSlug = "heat-treatment", Department = "Post-Process",
                DefaultDurationHours = 4.0, HasBuiltInPage = true,
                DefaultHourlyRate = 65.00m, DefaultSetupMinutes = 20,
                DisplayOrder = 3, StageIcon = "🔥", StageColor = "#EF4444",
                RequiresMachineAssignment = true,
                DefaultMachineId = "HT1", AssignedMachineIds = "HT1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Wire EDM", StageSlug = "wire-edm", Department = "EDM",
                DefaultDurationHours = 2.0, HasBuiltInPage = true,
                DefaultHourlyRate = 85.00m, DefaultSetupMinutes = 25,
                DisplayOrder = 4, StageIcon = "⚡", StageColor = "#8B5CF6",
                RequiresMachineAssignment = true,
                DefaultMachineId = "EDM1", AssignedMachineIds = "EDM1",
                CreatedBy = "System", LastModifiedBy = "System"
            },

            // === PER-PART / BATCH STAGES (batch behavior now on ProcessStage) ===
            new()
            {
                Name = "CNC Machining", StageSlug = "cnc-machining", Department = "Machining",
                DefaultDurationHours = 0.5, HasBuiltInPage = true,
                DefaultHourlyRate = 95.00m, DefaultSetupMinutes = 30,
                DisplayOrder = 5, StageIcon = "⚙️", StageColor = "#06B6D4",
                RequiresMachineAssignment = true,
                DefaultMachineId = "CNC1", AssignedMachineIds = "CNC1,CNC2,CNC3,CNC4",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Laser Engraving", StageSlug = "laser-engraving", Department = "Engraving",
                DefaultDurationHours = 0.25, HasBuiltInPage = true,
                DefaultHourlyRate = 55.00m, DefaultSetupMinutes = 10,
                RequiresSerialNumber = true,
                DisplayOrder = 6, StageIcon = "✒️", StageColor = "#10B981",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Surface Finishing", StageSlug = "surface-finishing", Department = "Finishing",
                DefaultDurationHours = 0.33, HasBuiltInPage = true,
                DefaultHourlyRate = 45.00m, DefaultSetupMinutes = 10,
                DisplayOrder = 7, StageIcon = "🎨", StageColor = "#EC4899",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Quality Control", StageSlug = "qc", Department = "Quality",
                DefaultDurationHours = 0.083, HasBuiltInPage = true,
                DefaultHourlyRate = 75.00m, DefaultSetupMinutes = 15,
                DisplayOrder = 8, StageIcon = "✅", StageColor = "#14B8A6",
                RequiresQualityCheck = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Shipping", StageSlug = "shipping", Department = "Shipping",
                DefaultDurationHours = 0.083, HasBuiltInPage = true,
                DefaultHourlyRate = 35.00m, DefaultSetupMinutes = 5,
                DisplayOrder = 9, StageIcon = "🚚", StageColor = "#6366F1",
                RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "CNC Turning", StageSlug = "cnc-turning", Department = "Machining",
                DefaultDurationHours = 0.33, HasBuiltInPage = true,
                DefaultHourlyRate = 90.00m, DefaultSetupMinutes = 25,
                DisplayOrder = 10, StageIcon = "🔩", StageColor = "#0891B2",
                RequiresMachineAssignment = true,
                DefaultMachineId = "LATHE1", AssignedMachineIds = "LATHE1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Assembly", StageSlug = "assembly", Department = "Assembly",
                DefaultDurationHours = 0.167, HasBuiltInPage = true,
                DefaultHourlyRate = 60.00m, DefaultSetupMinutes = 10,
                DisplayOrder = 11, StageIcon = "🔧", StageColor = "#7C3AED",
                RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Sandblasting", StageSlug = "sandblasting", Department = "Finishing",
                DefaultDurationHours = 0.25,
                DefaultHourlyRate = 40.00m, DefaultSetupMinutes = 5,
                DisplayOrder = 12, StageIcon = "🌪️", StageColor = "#A3A3A3",
                RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "External Coating", StageSlug = "external-coating", Department = "External",
                DefaultDurationHours = 0, IsExternalOperation = true, DefaultTurnaroundDays = 14,
                DefaultHourlyRate = 0.00m, DefaultSetupMinutes = 0,
                DisplayOrder = 13, StageIcon = "🏢", StageColor = "#D97706",
                RequiresQualityCheck = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Oil & Sleeve Assembly", StageSlug = "oil-sleeve", Department = "Assembly",
                DefaultDurationHours = 0.083,
                DefaultHourlyRate = 50.00m, DefaultSetupMinutes = 5,
                DisplayOrder = 14, StageIcon = "🛢️", StageColor = "#059669",
                RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Packaging & Shipping", StageSlug = "packaging", Department = "Shipping",
                DefaultDurationHours = 0.05,
                DefaultHourlyRate = 35.00m, DefaultSetupMinutes = 5,
                DisplayOrder = 15, StageIcon = "📦", StageColor = "#7C3AED",
                RequiresQualityCheck = false,
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
                BuildPlateCapacity = 1,
                HourlyRate = 85.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "HT1", Name = "Heat Treatment Furnace", MachineType = "Heat-Treat",
                MachineModel = "Vacuum Furnace", Department = "Post-Process",
                BuildPlateCapacity = 1,
                HourlyRate = 75.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "EDM1", Name = "Wire EDM", MachineType = "EDM",
                Department = "EDM", HourlyRate = 85.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "CNC1", Name = "Haas VF-2", MachineType = "CNC",
                MachineModel = "Haas VF-2", Department = "Machining",
                HourlyRate = 95.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "CNC2", Name = "Haas VF-2SS #2", MachineType = "CNC",
                MachineModel = "Haas VF-2SS", Department = "Machining",
                HourlyRate = 95.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "CNC3", Name = "Haas VF-4 #3", MachineType = "CNC",
                MachineModel = "Haas VF-4", Department = "Machining",
                HourlyRate = 105.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "CNC4", Name = "DMG MORI NHX 4000", MachineType = "CNC",
                MachineModel = "DMG MORI NHX 4000", Department = "Machining",
                HourlyRate = 125.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "LATHE1", Name = "CNC Lathe", MachineType = "CNC-Turning",
                MachineModel = "Haas ST-20Y", Department = "Machining",
                HourlyRate = 95.00m, CreatedBy = "System", LastModifiedBy = "System"
            }
        };

        db.Machines.AddRange(machines);
        await db.SaveChangesAsync();
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
        };

        db.SystemSettings.AddRange(settings);
        await db.SaveChangesAsync();
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
                    AllowRebatching = true,
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
                    RunDurationMode = DurationMode.PerPart,
                    RunTimeMinutes = 5,
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

    }
