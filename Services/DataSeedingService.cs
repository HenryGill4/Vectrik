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

        // Parts + routing (depends on stages, machines, materials)
        await SeedTestPartsAsync(tenantDb);

        // Inventory (depends on materials)
        await SeedStockLocationsAsync(tenantDb);
        await SeedInventoryItemsAsync(tenantDb);

        // Quotes (depends on parts)
        await SeedTestQuotesAsync(tenantDb);

        // Work orders, jobs, stage executions (depends on parts, stages, machines, users)
        await SeedTestWorkOrdersAsync(tenantDb);

        // Build packages (depends on parts, work order lines, machines)
        await SeedTestBuildPackagesAsync(tenantDb);

        // Quality data (depends on jobs, parts, users)
        await SeedTestQualityDataAsync(tenantDb);
    }

    private static async Task SeedProductionStagesAsync(TenantDbContext db)
    {
        if (await db.ProductionStages.AnyAsync()) return;

        var stages = new List<ProductionStage>
        {
            new()
            {
                Name = "SLS/LPBF Printing", StageSlug = "sls-printing", Department = "SLS",
                DefaultDurationHours = 8.0, IsBatchStage = true, IsBuildLevelStage = true, HasBuiltInPage = true,
                DisplayOrder = 1, StageIcon = "🖨️", StageColor = "#3B82F6",
                RequiresMachineAssignment = true, RequiresQualityCheck = false,
                DefaultMachineId = "M4-1", AssignedMachineIds = "M4-1,M4-2",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Depowdering", StageSlug = "depowdering", Department = "Post-Process",
                DefaultDurationHours = 1.0, IsBatchStage = true, IsBuildLevelStage = true, HasBuiltInPage = true,
                DisplayOrder = 2, StageIcon = "💨", StageColor = "#F59E0B",
                RequiresMachineAssignment = true,
                DefaultMachineId = "INC1", AssignedMachineIds = "INC1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Heat Treatment", StageSlug = "heat-treatment", Department = "Post-Process",
                DefaultDurationHours = 4.0, IsBatchStage = true, IsBuildLevelStage = true, HasBuiltInPage = true,
                DisplayOrder = 3, StageIcon = "🔥", StageColor = "#EF4444",
                RequiresMachineAssignment = true,
                DefaultMachineId = "HT1", AssignedMachineIds = "HT1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Wire EDM", StageSlug = "wire-edm", Department = "EDM",
                DefaultDurationHours = 2.0, IsBatchStage = false, IsBuildLevelStage = true, HasBuiltInPage = true,
                DisplayOrder = 4, StageIcon = "⚡", StageColor = "#8B5CF6",
                RequiresMachineAssignment = true,
                DefaultMachineId = "EDM1", AssignedMachineIds = "EDM1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "CNC Machining", StageSlug = "cnc-machining", Department = "Machining",
                DefaultDurationHours = 3.0, IsBatchStage = false, HasBuiltInPage = true,
                DisplayOrder = 5, StageIcon = "⚙️", StageColor = "#06B6D4",
                RequiresMachineAssignment = true,
                DefaultMachineId = "CNC1", AssignedMachineIds = "CNC1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Laser Engraving", StageSlug = "laser-engraving", Department = "Engraving",
                DefaultDurationHours = 0.5, IsBatchStage = false, HasBuiltInPage = true,
                RequiresSerialNumber = true,
                DisplayOrder = 6, StageIcon = "✒️", StageColor = "#10B981",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Surface Finishing", StageSlug = "surface-finishing", Department = "Finishing",
                DefaultDurationHours = 1.5, IsBatchStage = true, HasBuiltInPage = true,
                DisplayOrder = 7, StageIcon = "🎨", StageColor = "#EC4899",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Quality Control", StageSlug = "qc", Department = "Quality",
                DefaultDurationHours = 0.5, IsBatchStage = false, HasBuiltInPage = true,
                DisplayOrder = 8, StageIcon = "✅", StageColor = "#14B8A6",
                RequiresQualityCheck = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Shipping", StageSlug = "shipping", Department = "Shipping",
                DefaultDurationHours = 0.5, IsBatchStage = false, HasBuiltInPage = true,
                DisplayOrder = 9, StageIcon = "🚚", StageColor = "#6366F1",
                RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "CNC Turning", StageSlug = "cnc-turning", Department = "Machining",
                DefaultDurationHours = 2.0, IsBatchStage = false, HasBuiltInPage = true,
                DisplayOrder = 10, StageIcon = "🔩", StageColor = "#0891B2",
                RequiresMachineAssignment = true,
                DefaultMachineId = "LATHE1", AssignedMachineIds = "LATHE1",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Assembly", StageSlug = "assembly", Department = "Assembly",
                DefaultDurationHours = 1.0, IsBatchStage = false, HasBuiltInPage = true,
                DisplayOrder = 11, StageIcon = "🔧", StageColor = "#7C3AED",
                RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Sandblasting", StageSlug = "sandblasting", Department = "Finishing",
                DefaultDurationHours = 1.0, IsBatchStage = true,
                DisplayOrder = 12, StageIcon = "🌪️", StageColor = "#A3A3A3",
                RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "External Coating", StageSlug = "external-coating", Department = "External",
                DefaultDurationHours = 0, IsExternalOperation = true, DefaultTurnaroundDays = 14,
                DisplayOrder = 13, StageIcon = "🏢", StageColor = "#D97706",
                RequiresQualityCheck = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Oil & Sleeve Assembly", StageSlug = "oil-sleeve", Department = "Assembly",
                DefaultDurationHours = 0.5, IsBatchStage = false,
                DisplayOrder = 14, StageIcon = "🛢️", StageColor = "#059669",
                RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Packaging & Shipping", StageSlug = "packaging", Department = "Shipping",
                DefaultDurationHours = 0.5, IsBatchStage = false,
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

        var approaches = new List<ManufacturingApproach>
        {
            new() { Name = "SLS-Based",              Slug = "sls-based",           IsAdditive = true,  RequiresBuildPlate = true,  HasPostPrintBatching = true,  DefaultRoutingTemplate = """["sls-printing","depowdering","heat-treatment","qc"]""",          DisplayOrder = 1,  IconEmoji = "🖨️" },
            new() { Name = "CNC Machining",          Slug = "cnc-machining",        IsAdditive = false, RequiresBuildPlate = false, HasPostPrintBatching = false, DefaultRoutingTemplate = """["cnc-machining","qc"]""",                                       DisplayOrder = 2,  IconEmoji = "⚙️" },
            new() { Name = "CNC Turning",            Slug = "cnc-turning",          IsAdditive = false, RequiresBuildPlate = false, HasPostPrintBatching = false, DefaultRoutingTemplate = """["cnc-machining","qc"]""",                                       DisplayOrder = 3,  IconEmoji = "🔩" },
            new() { Name = "Wire EDM",               Slug = "wire-edm",             IsAdditive = false, RequiresBuildPlate = false, HasPostPrintBatching = false, DefaultRoutingTemplate = """["wire-edm","qc"]""",                                            DisplayOrder = 4,  IconEmoji = "⚡" },
            new() { Name = "3D Printing (FDM)",      Slug = "fdm",                  IsAdditive = true,  RequiresBuildPlate = true,  HasPostPrintBatching = false, DefaultRoutingTemplate = """["sls-printing","qc"]""",                                        DisplayOrder = 5,  IconEmoji = "🖨️" },
            new() { Name = "3D Printing (SLA)",      Slug = "sla",                  IsAdditive = true,  RequiresBuildPlate = true,  HasPostPrintBatching = false, DefaultRoutingTemplate = """["sls-printing","qc"]""",                                        DisplayOrder = 6,  IconEmoji = "🖨️" },
            new() { Name = "Additive + Subtractive", Slug = "additive-subtractive", IsAdditive = true,  RequiresBuildPlate = true,  HasPostPrintBatching = true,  DefaultRoutingTemplate = """["sls-printing","depowdering","cnc-machining","qc"]""",          DisplayOrder = 7,  IconEmoji = "🔧" },
            new() { Name = "Sheet Metal",            Slug = "sheet-metal",          IsAdditive = false, RequiresBuildPlate = false, HasPostPrintBatching = false, DefaultRoutingTemplate = """["qc"]""",                                                       DisplayOrder = 8,  IconEmoji = "📐" },
            new() { Name = "Casting",                Slug = "casting",              IsAdditive = false, RequiresBuildPlate = false, HasPostPrintBatching = false, DefaultRoutingTemplate = """["cnc-machining","qc"]""",                                       DisplayOrder = 9,  IconEmoji = "🏭" },
            new() { Name = "Injection Molding",      Slug = "injection-molding",    IsAdditive = false, RequiresBuildPlate = false, HasPostPrintBatching = false, DefaultRoutingTemplate = """["qc"]""",                                                       DisplayOrder = 10, IconEmoji = "💉" },
            new() { Name = "Assembly",               Slug = "assembly",             IsAdditive = false, RequiresBuildPlate = false, HasPostPrintBatching = false, DefaultRoutingTemplate = """["qc"]""",                                                       DisplayOrder = 11, IconEmoji = "🔧" },
            new() { Name = "Manual",                 Slug = "manual",               IsAdditive = false, RequiresBuildPlate = false, HasPostPrintBatching = false, DefaultRoutingTemplate = """["qc"]""",                                                       DisplayOrder = 12, IconEmoji = "✋" },
            new() { Name = "Other",                  Slug = "other",                IsAdditive = false, RequiresBuildPlate = false, HasPostPrintBatching = false, DefaultRoutingTemplate = "[]",                                                                DisplayOrder = 13, IconEmoji = "❓" },
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

    private static async Task SeedTestPartsAsync(TenantDbContext db)
    {
        if (await db.Parts.AnyAsync()) return;

        // Get stage IDs for building stage requirements
        var stages = await db.ProductionStages.ToListAsync();
        var slsPrinting = stages.FirstOrDefault(s => s.StageSlug == "sls-printing");
        var depowdering = stages.FirstOrDefault(s => s.StageSlug == "depowdering");
        var heatTreatment = stages.FirstOrDefault(s => s.StageSlug == "heat-treatment");
        var wireEdm = stages.FirstOrDefault(s => s.StageSlug == "wire-edm");
        var cncMachining = stages.FirstOrDefault(s => s.StageSlug == "cnc-machining");
        var cncTurning = stages.FirstOrDefault(s => s.StageSlug == "cnc-turning");
        var laserEngraving = stages.FirstOrDefault(s => s.StageSlug == "laser-engraving");
        var surfaceFinishing = stages.FirstOrDefault(s => s.StageSlug == "surface-finishing");
        var assembly = stages.FirstOrDefault(s => s.StageSlug == "assembly");
        var qc = stages.FirstOrDefault(s => s.StageSlug == "qc");
        var shipping = stages.FirstOrDefault(s => s.StageSlug == "shipping");

        if (slsPrinting == null) return;

        var materialLookup = await db.Materials.ToDictionaryAsync(m => m.Name, m => (int?)m.Id);
        var approachLookup = await db.ManufacturingApproaches.ToDictionaryAsync(a => a.Slug, a => (int?)a.Id);

        // ── 8 Suppressor Parts ──
        var parts = new List<Part>
        {
            // Part 1: Outer tube — CNC turned from Ti-6Al-4V bar stock
            new()
            {
                PartNumber = "SUP-TUBE-001",
                Name = "Titanium Outer Tube",
                Description = "Precision CNC-turned outer housing tube from Ti-6Al-4V bar stock. 1.375\" OD x 1.125\" ID x 7.5\" overall length. Threads 1.375-24 TPI both ends for end cap engagement. Wall thickness optimized for strength-to-weight ratio in rifle-caliber suppressor applications.",
                Material = "Ti-6Al-4V Grade 5",
                MaterialId = materialLookup.GetValueOrDefault("Ti-6Al-4V Grade 5"),
                ManufacturingApproachId = approachLookup.GetValueOrDefault("cnc-turning"),
                Revision = "B",
                RevisionDate = DateTime.UtcNow.AddDays(-45),
                EstimatedWeightKg = 0.28,
                IsDefensePart = true,
                ItarClassification = ItarClassification.ITAR,
                IsActive = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            // Part 2: Monocore baffle stack
            new()
            {
                PartNumber = "SUP-MONO-002",
                Name = "Monocore Baffle Stack",
                Description = "Monolithic 6-cone K-baffle stack with integrated flow diffuser channels. SLS/LPBF printed as single unit in Inconel 718 for extreme heat and erosion resistance. Internal geometry impossible to manufacture subtractively — true additive-only design. Rated for 5.56 NATO / .300 BLK / 7.62 NATO.",
                Material = "Inconel 718",
                MaterialId = materialLookup.GetValueOrDefault("Inconel 718"),
                ManufacturingApproachId = approachLookup.GetValueOrDefault("sls-based"),
                Revision = "D",
                RevisionDate = DateTime.UtcNow.AddDays(-10),
                EstimatedWeightKg = 0.38,
                IsDefensePart = true,
                ItarClassification = ItarClassification.ITAR,
                IsActive = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            // Part 3: Front end cap — CNC machined from 17-4PH
            new()
            {
                PartNumber = "SUP-ECAP-003",
                Name = "Front End Cap",
                Description = "Muzzle-side end cap with integrated blast chamber pocket. CNC machined from 17-4PH stainless steel bar, condition H900 for maximum hardness. 1.375-24 TPI external thread for tube engagement, 0.375\" bore for projectile clearance with 0.010\" radial tolerance.",
                Material = "17-4PH Stainless Steel",
                MaterialId = materialLookup.GetValueOrDefault("17-4PH Stainless Steel"),
                ManufacturingApproachId = approachLookup.GetValueOrDefault("cnc-machining"),
                Revision = "A",
                RevisionDate = DateTime.UtcNow.AddDays(-60),
                EstimatedWeightKg = 0.15,
                IsActive = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            // Part 4: Rear end cap — CNC machined from 17-4PH, serialized
            new()
            {
                PartNumber = "SUP-RCAP-004",
                Name = "Rear End Cap (Mount Interface)",
                Description = "Rear mount-side end cap with adapter thread interface. 1.375-24 TPI external for tube, internal 1.375-24 TPI for adapter retention. Laser-engraved with serial number, manufacturer, and caliber designation per ATF requirements. 17-4PH H900 condition.",
                Material = "17-4PH Stainless Steel",
                MaterialId = materialLookup.GetValueOrDefault("17-4PH Stainless Steel"),
                ManufacturingApproachId = approachLookup.GetValueOrDefault("cnc-machining"),
                Revision = "B",
                RevisionDate = DateTime.UtcNow.AddDays(-30),
                EstimatedWeightKg = 0.18,
                IsDefensePart = true,
                ItarClassification = ItarClassification.ITAR,
                IsActive = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            // Part 5: Muzzle adapter
            new()
            {
                PartNumber = "SUP-ADPT-005",
                Name = "Direct Thread Muzzle Adapter (1/2x28)",
                Description = "Direct thread muzzle adapter for 5.56 NATO / .223 Rem host barrels. 1/2-28 UNEF male thread (barrel side), 1.375-24 TPI male thread (suppressor side). 316L stainless steel for corrosion resistance. Includes crush washer detent groove.",
                Material = "316L Stainless Steel",
                MaterialId = materialLookup.GetValueOrDefault("316L Stainless Steel"),
                ManufacturingApproachId = approachLookup.GetValueOrDefault("cnc-turning"),
                Revision = "C",
                RevisionDate = DateTime.UtcNow.AddDays(-20),
                EstimatedWeightKg = 0.09,
                IsActive = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            // Part 6: Blast baffle — SLS + CNC finish (Additive+Subtractive)
            new()
            {
                PartNumber = "SUP-BBAF-006",
                Name = "Blast Baffle",
                Description = "First-contact blast baffle with reinforced stellite-tipped cone geometry. SLS printed in Inconel 718 then CNC finish-machined for critical bore tolerance (0.375\" +0.002/-0.000). Takes the highest thermal and erosive loading in the suppressor stack. Designed for 50,000+ round service life.",
                Material = "Inconel 718",
                MaterialId = materialLookup.GetValueOrDefault("Inconel 718"),
                ManufacturingApproachId = approachLookup.GetValueOrDefault("additive-subtractive"),
                Revision = "B",
                RevisionDate = DateTime.UtcNow.AddDays(-15),
                EstimatedWeightKg = 0.12,
                IsActive = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            // Part 7: Retention clips
            new()
            {
                PartNumber = "SUP-CLIP-007",
                Name = "Baffle Retention Spring Clip",
                Description = "Wire EDM cut retention clip from 0.040\" Ti-6Al-4V sheet. C-clip design with spring detent that locks monocore baffle stack inside outer tube. 2 clips required per suppressor assembly. Designed for tool-free disassembly during cleaning.",
                Material = "Ti-6Al-4V Grade 5",
                MaterialId = materialLookup.GetValueOrDefault("Ti-6Al-4V Grade 5"),
                ManufacturingApproachId = approachLookup.GetValueOrDefault("wire-edm"),
                Revision = "A",
                RevisionDate = DateTime.UtcNow.AddDays(-90),
                EstimatedWeightKg = 0.02,
                IsActive = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            // Part 8: Complete suppressor assembly
            new()
            {
                PartNumber = "SUP-ASSY-008",
                Name = "Complete Suppressor Assembly",
                Description = "Final assembled suppressor unit: outer tube, monocore baffle stack, blast baffle, front end cap, rear end cap, muzzle adapter, and 2x retention clips. Full functional test, sound reduction verification, and serialization. Rated for 5.56 NATO, .300 BLK subsonic/supersonic, 7.62 NATO.",
                Material = "Ti-6Al-4V Grade 5",
                MaterialId = materialLookup.GetValueOrDefault("Ti-6Al-4V Grade 5"),
                ManufacturingApproachId = approachLookup.GetValueOrDefault("assembly"),
                Revision = "A",
                RevisionDate = DateTime.UtcNow,
                EstimatedWeightKg = 0.72,
                IsDefensePart = true,
                ItarClassification = ItarClassification.ITAR,
                IsActive = true,
                CreatedBy = "System", LastModifiedBy = "System"
            }
        };

        db.Parts.AddRange(parts);
        await db.SaveChangesAsync();

        var tube = parts[0];      // SUP-TUBE-001
        var monocore = parts[1];  // SUP-MONO-002
        var frontCap = parts[2];  // SUP-ECAP-003
        var rearCap = parts[3];   // SUP-RCAP-004
        var adapter = parts[4];   // SUP-ADPT-005
        var blastBaffle = parts[5]; // SUP-BBAF-006
        var clip = parts[6];      // SUP-CLIP-007
        var assy = parts[7];      // SUP-ASSY-008

        // Additive build configs for SLS/additive parts
        var additiveConfigs = new List<PartAdditiveBuildConfig>
        {
            // SUP-MONO-002: Monocore (SLS-Based) — single or double stack
            new()
            {
                PartId = monocore.Id,
                AllowStacking = true,
                MaxStackCount = 2,
                EnableDoubleStack = true,
                PlannedPartsPerBuildSingle = 1,
                PlannedPartsPerBuildDouble = 2,
                SingleStackDurationHours = 14.0,
                DoubleStackDurationHours = 22.0,
                DepowderingDurationHours = 2.5,
                DepowderingPartsPerBatch = 4,
                HeatTreatmentDurationHours = 8.0,
                HeatTreatmentPartsPerBatch = 8,
                WireEdmDurationHours = 1.5,
                WireEdmPartsPerSession = 2
            },
            // SUP-BBAF-006: Blast Baffle (Additive+Subtractive) — single or double stack
            new()
            {
                PartId = blastBaffle.Id,
                AllowStacking = true,
                MaxStackCount = 2,
                EnableDoubleStack = true,
                PlannedPartsPerBuildSingle = 2,
                PlannedPartsPerBuildDouble = 4,
                SingleStackDurationHours = 8.0,
                DoubleStackDurationHours = 13.0,
                DepowderingDurationHours = 1.0,
                DepowderingPartsPerBatch = 8,
                HeatTreatmentDurationHours = 8.0,
                HeatTreatmentPartsPerBatch = 16
            }
        };

        db.PartAdditiveBuildConfigs.AddRange(additiveConfigs);
        await db.SaveChangesAsync();

        var requirements = new List<PartStageRequirement>();

        // SUP-TUBE-001: CNC Turning → Surface Finishing → Laser Engraving → QC → Shipping
        if (cncTurning != null)
        {
            requirements.AddRange(new[]
            {
                new PartStageRequirement { PartId = tube.Id, ProductionStageId = cncTurning.Id, ExecutionOrder = 1, EstimatedHours = 1.5, SetupTimeMinutes = 30, AssignedMachineId = "LATHE1", RequiresSpecificMachine = true, EstimatedCost = 142.50m, MaterialCost = 98.00m, SpecialInstructions = "Ti-6Al-4V bar stock 1.500\" OD. Use flood coolant, 200 SFM, 0.004 IPR feed. Bore ID to 1.125\" ±0.001\". Thread both ends 1.375-24 TPI, class 3A.", CreatedBy = "System", LastModifiedBy = "System" },
                new PartStageRequirement { PartId = tube.Id, ProductionStageId = surfaceFinishing!.Id, ExecutionOrder = 2, EstimatedHours = 0.5, EstimatedCost = 42.50m, SpecialInstructions = "Tumble deburr and bead blast to uniform matte finish. No media entrapment in threads.", CreatedBy = "System", LastModifiedBy = "System" },
                new PartStageRequirement { PartId = tube.Id, ProductionStageId = laserEngraving!.Id, ExecutionOrder = 3, EstimatedHours = 0.25, EstimatedCost = 21.25m, SpecialInstructions = "Engrave serial number, caliber, and manufacturer per ATF markings requirement. Depth 0.003\" min.", CreatedBy = "System", LastModifiedBy = "System" },
                new PartStageRequirement { PartId = tube.Id, ProductionStageId = qc!.Id, ExecutionOrder = 4, EstimatedHours = 0.5, EstimatedCost = 42.50m, SpecialInstructions = "Verify OD/ID concentricity ≤0.002\" TIR. Thread gauge both ends. Visual inspect bore for tool marks.", CreatedBy = "System", LastModifiedBy = "System" },
                new PartStageRequirement { PartId = tube.Id, ProductionStageId = shipping!.Id, ExecutionOrder = 5, EstimatedHours = 0.25, EstimatedCost = 21.25m, CreatedBy = "System", LastModifiedBy = "System" }
            });
        }

        // SUP-MONO-002: SLS → Depowder → Heat Treat → Wire EDM → Surface Finishing → QC → Shipping
        requirements.AddRange(new[]
        {
            new PartStageRequirement { PartId = monocore.Id, ProductionStageId = slsPrinting.Id, ExecutionOrder = 1, EstimatedHours = 14.0, SetupTimeMinutes = 60, AssignedMachineId = "M4-1", EstimatedCost = 2100.00m, MaterialCost = 45.60m, SpecialInstructions = "Inconel 718 powder, 30μm layer thickness. Orientation: vertical with cone apexes pointing up. Support structure on base plate interface only — internal channels are self-supporting at 45° minimum.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = monocore.Id, ProductionStageId = depowdering!.Id, ExecutionOrder = 2, EstimatedHours = 2.5, EstimatedCost = 212.50m, SpecialInstructions = "Extended depowdering cycle required — internal K-baffle channels trap powder. Use compressed air + ultrasonic agitation. Verify all 6 cone passages are clear with borescope before proceeding.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = monocore.Id, ProductionStageId = heatTreatment!.Id, ExecutionOrder = 3, EstimatedHours = 8.0, EstimatedCost = 680.00m, SpecialInstructions = "Solution anneal 1750°F / 1hr, air cool. Age harden 1325°F / 8hr, furnace cool to 1150°F / 8hr, air cool. Argon atmosphere required. Verify Rockwell C 38-44 post-treatment.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = monocore.Id, ProductionStageId = wireEdm!.Id, ExecutionOrder = 4, EstimatedHours = 1.5, AssignedMachineId = "EDM1", EstimatedCost = 127.50m, SpecialInstructions = "Wire EDM cut from build plate. Leave 0.5mm stock on interface face for final dress on surface grinder.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = monocore.Id, ProductionStageId = surfaceFinishing.Id, ExecutionOrder = 5, EstimatedHours = 1.0, EstimatedCost = 85.00m, SpecialInstructions = "Vibratory tumble to remove loose particles and break sharp edges. Passivate per AMS 2700. Do NOT plug bore openings — media must flow through.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = monocore.Id, ProductionStageId = qc.Id, ExecutionOrder = 6, EstimatedHours = 1.0, EstimatedCost = 85.00m, SpecialInstructions = "CMM inspect OD profile. Borescope all 6 internal channels. CT scan sample (1 per lot of 10). Measure bore alignment ≤0.005\" over full length. Weigh and record.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = monocore.Id, ProductionStageId = shipping.Id, ExecutionOrder = 7, EstimatedHours = 0.25, EstimatedCost = 21.25m, CreatedBy = "System", LastModifiedBy = "System" }
        });

        // SUP-ECAP-003: CNC Machining → Surface Finishing → QC → Shipping
        requirements.AddRange(new[]
        {
            new PartStageRequirement { PartId = frontCap.Id, ProductionStageId = cncMachining!.Id, ExecutionOrder = 1, EstimatedHours = 1.0, SetupTimeMinutes = 20, AssignedMachineId = "CNC1", EstimatedCost = 95.00m, MaterialCost = 7.00m, SpecialInstructions = "17-4PH bar stock 1.500\" OD, H900 condition. Two-op: OP10 turn OD + thread 1.375-24, OP20 flip and bore blast chamber pocket 0.750\" Ø x 0.250\" deep + through-bore 0.375\" +0.002/-0.000.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = frontCap.Id, ProductionStageId = surfaceFinishing.Id, ExecutionOrder = 2, EstimatedHours = 0.5, EstimatedCost = 42.50m, SpecialInstructions = "Tumble deburr. Black oxide finish per MIL-DTL-13924, Class 1.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = frontCap.Id, ProductionStageId = qc.Id, ExecutionOrder = 3, EstimatedHours = 0.5, EstimatedCost = 42.50m, SpecialInstructions = "Thread gauge 1.375-24. Pin gauge bore 0.375\". Visual inspect blast chamber for tool marks.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = frontCap.Id, ProductionStageId = shipping.Id, ExecutionOrder = 4, EstimatedHours = 0.25, EstimatedCost = 21.25m, CreatedBy = "System", LastModifiedBy = "System" }
        });

        // SUP-RCAP-004: CNC Machining → Laser Engraving → QC → Shipping
        requirements.AddRange(new[]
        {
            new PartStageRequirement { PartId = rearCap.Id, ProductionStageId = cncMachining.Id, ExecutionOrder = 1, EstimatedHours = 1.25, SetupTimeMinutes = 20, AssignedMachineId = "CNC1", EstimatedCost = 118.75m, MaterialCost = 8.00m, SpecialInstructions = "17-4PH bar stock 1.500\" OD, H900 condition. OP10 turn OD + external thread 1.375-24. OP20 bore internal thread 1.375-24 for adapter retention. Concentricity ≤0.001\" TIR.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = rearCap.Id, ProductionStageId = laserEngraving.Id, ExecutionOrder = 2, EstimatedHours = 0.5, EstimatedCost = 42.50m, SpecialInstructions = "Engrave serial number (unique per unit), manufacturer name, caliber marking, and model designation per ATF markings requirements. Depth 0.003\" minimum, 0.070\" character height.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = rearCap.Id, ProductionStageId = qc.Id, ExecutionOrder = 3, EstimatedHours = 0.5, EstimatedCost = 42.50m, SpecialInstructions = "Thread gauge both internal and external threads. Verify engraving legibility and depth. Check concentricity.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = rearCap.Id, ProductionStageId = shipping.Id, ExecutionOrder = 4, EstimatedHours = 0.25, EstimatedCost = 21.25m, CreatedBy = "System", LastModifiedBy = "System" }
        });

        // SUP-ADPT-005: CNC Turning → QC → Shipping
        if (cncTurning != null)
        {
            requirements.AddRange(new[]
            {
                new PartStageRequirement { PartId = adapter.Id, ProductionStageId = cncTurning.Id, ExecutionOrder = 1, EstimatedHours = 0.75, SetupTimeMinutes = 15, AssignedMachineId = "LATHE1", EstimatedCost = 71.25m, MaterialCost = 6.40m, SpecialInstructions = "316L SS bar stock 1.000\" OD. Turn OD profile, cut 1/2-28 UNEF male thread (barrel side), 1.375-24 TPI male thread (suppressor side). Crush washer groove 0.030\" wide x 0.015\" deep.", CreatedBy = "System", LastModifiedBy = "System" },
                new PartStageRequirement { PartId = adapter.Id, ProductionStageId = qc.Id, ExecutionOrder = 2, EstimatedHours = 0.25, EstimatedCost = 21.25m, SpecialInstructions = "Thread gauge both ends. Verify crush washer groove depth. Check runout ≤0.001\" TIR.", CreatedBy = "System", LastModifiedBy = "System" },
                new PartStageRequirement { PartId = adapter.Id, ProductionStageId = shipping.Id, ExecutionOrder = 3, EstimatedHours = 0.25, EstimatedCost = 21.25m, CreatedBy = "System", LastModifiedBy = "System" }
            });
        }

        // SUP-BBAF-006: SLS → Depowder → Heat Treat → CNC Machining → QC → Shipping
        requirements.AddRange(new[]
        {
            new PartStageRequirement { PartId = blastBaffle.Id, ProductionStageId = slsPrinting.Id, ExecutionOrder = 1, EstimatedHours = 8.0, SetupTimeMinutes = 45, AssignedMachineId = "M4-2", EstimatedCost = 1200.00m, MaterialCost = 14.40m, SpecialInstructions = "Inconel 718, 30μm layers. Print 2 per plate in single-stack configuration. Cone apex up, 45° internal overhang is self-supporting. Reinforce base with 3mm support block.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = blastBaffle.Id, ProductionStageId = depowdering.Id, ExecutionOrder = 2, EstimatedHours = 1.0, EstimatedCost = 85.00m, SpecialInstructions = "Standard depowdering — single cone chamber, no complex internal channels. Compressed air sufficient.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = blastBaffle.Id, ProductionStageId = heatTreatment.Id, ExecutionOrder = 3, EstimatedHours = 8.0, EstimatedCost = 680.00m, SpecialInstructions = "Same heat treat cycle as monocore: solution anneal + double age. Argon atmosphere. Verify HRC 38-44.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = blastBaffle.Id, ProductionStageId = cncMachining.Id, ExecutionOrder = 4, EstimatedHours = 0.75, SetupTimeMinutes = 15, AssignedMachineId = "CNC1", EstimatedCost = 71.25m, SpecialInstructions = "Finish-machine bore to 0.375\" +0.002/-0.000. Face both ends. This is the critical tolerance — bore alignment affects accuracy.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = blastBaffle.Id, ProductionStageId = qc.Id, ExecutionOrder = 5, EstimatedHours = 0.5, EstimatedCost = 42.50m, SpecialInstructions = "Pin gauge bore. Verify hardness. Visual inspect cone geometry for print defects.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = blastBaffle.Id, ProductionStageId = shipping.Id, ExecutionOrder = 6, EstimatedHours = 0.25, EstimatedCost = 21.25m, CreatedBy = "System", LastModifiedBy = "System" }
        });

        // SUP-CLIP-007: Wire EDM → Surface Finishing → QC → Shipping
        requirements.AddRange(new[]
        {
            new PartStageRequirement { PartId = clip.Id, ProductionStageId = wireEdm.Id, ExecutionOrder = 1, EstimatedHours = 0.5, SetupTimeMinutes = 15, AssignedMachineId = "EDM1", EstimatedCost = 42.50m, MaterialCost = 12.00m, SpecialInstructions = "Wire EDM cut from 0.040\" Ti-6Al-4V sheet. Nest 8 clips per sheet for efficiency. 0.010\" wire, 2-pass (rough + skim) for clean edge.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = clip.Id, ProductionStageId = surfaceFinishing.Id, ExecutionOrder = 2, EstimatedHours = 0.25, EstimatedCost = 21.25m, SpecialInstructions = "Tumble deburr to remove EDM recast layer and break edges. Clips must flex without cracking.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = clip.Id, ProductionStageId = qc.Id, ExecutionOrder = 3, EstimatedHours = 0.25, EstimatedCost = 21.25m, SpecialInstructions = "Verify spring force 5-8 lbf. Go/no-go gauge fit check in dummy tube section. Visual for cracks.", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = clip.Id, ProductionStageId = shipping.Id, ExecutionOrder = 4, EstimatedHours = 0.25, EstimatedCost = 21.25m, CreatedBy = "System", LastModifiedBy = "System" }
        });

        // SUP-ASSY-008: Assembly → QC → Shipping
        if (assembly != null)
        {
            requirements.AddRange(new[]
            {
                new PartStageRequirement { PartId = assy.Id, ProductionStageId = assembly.Id, ExecutionOrder = 1, EstimatedHours = 0.75, SetupTimeMinutes = 10, EstimatedCost = 63.75m, SpecialInstructions = "Assembly sequence: (1) Insert blast baffle into tube, (2) Insert monocore stack, (3) Install retention clips, (4) Thread front end cap, (5) Thread rear end cap, (6) Thread muzzle adapter into rear cap. Torque end caps to 25 ft-lbs with anti-seize on threads.", CreatedBy = "System", LastModifiedBy = "System" },
                new PartStageRequirement { PartId = assy.Id, ProductionStageId = qc.Id, ExecutionOrder = 2, EstimatedHours = 0.5, EstimatedCost = 42.50m, SpecialInstructions = "Bore alignment check with alignment rod — must pass freely. Shake test for loose internals. Weigh complete unit (spec: 0.72 ±0.05 kg). Record serial number from rear end cap.", CreatedBy = "System", LastModifiedBy = "System" },
                new PartStageRequirement { PartId = assy.Id, ProductionStageId = shipping.Id, ExecutionOrder = 3, EstimatedHours = 0.25, EstimatedCost = 21.25m, SpecialInstructions = "Individual protective case with foam insert. Include CoC, test report, and user manual. ITAR-controlled shipment — verify end-user documentation.", CreatedBy = "System", LastModifiedBy = "System" }
            });
        }

        db.PartStageRequirements.AddRange(requirements);
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
    //  Quotes
    // ──────────────────────────────────────────────
    private static async Task SeedTestQuotesAsync(TenantDbContext db)
    {
        if (await db.Quotes.AnyAsync()) return;

        var parts = await db.Parts.ToListAsync();
        if (parts.Count < 8) return;

        var monocore = parts.First(p => p.PartNumber == "SUP-MONO-002");
        var frontCap = parts.First(p => p.PartNumber == "SUP-ECAP-003");
        var rearCap = parts.First(p => p.PartNumber == "SUP-RCAP-004");
        var blastBaffle = parts.First(p => p.PartNumber == "SUP-BBAF-006");
        var clip = parts.First(p => p.PartNumber == "SUP-CLIP-007");
        var tube = parts.First(p => p.PartNumber == "SUP-TUBE-001");
        var adapter = parts.First(p => p.PartNumber == "SUP-ADPT-005");
        var assy = parts.First(p => p.PartNumber == "SUP-ASSY-008");

        var quotes = new List<Quote>
        {
            // Quote 1 — Accepted (defense contract for complete suppressor kits)
            new()
            {
                QuoteNumber = "QT-00001",
                CustomerName = "Vanguard Defense Systems",
                CustomerEmail = "contracts@vanguarddefense.com",
                CustomerPhone = "703-555-8800",
                Status = QuoteStatus.Accepted,
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                ExpirationDate = DateTime.UtcNow.AddDays(60),
                TotalEstimatedCost = 82500.00m,
                QuotedPrice = 115500.00m,
                Markup = 40m,
                EstimatedLaborCost = 42000.00m,
                EstimatedMaterialCost = 18500.00m,
                EstimatedOverheadCost = 22000.00m,
                TargetMarginPct = 40m,
                RevisionNumber = 2,
                SentAt = DateTime.UtcNow.AddDays(-28),
                AcceptedAt = DateTime.UtcNow.AddDays(-20),
                CreatedBy = "admin",
                LastModifiedBy = "admin",
                Notes = "24x complete suppressor kits for SOCOM evaluation program. ITAR controlled — no foreign nationals. Includes monocore, blast baffles, end caps, and retention hardware. Excludes tubes and adapters (GFE).",
                IsDefenseContract = true,
                ContractNumber = "W56HZV-26-C-0187"
            },
            // Quote 2 — Sent (commercial replacement parts)
            new()
            {
                QuoteNumber = "QT-00002",
                CustomerName = "Summit Firearms LLC",
                CustomerEmail = "orders@summitfirearms.com",
                CustomerPhone = "406-555-2200",
                Status = QuoteStatus.Sent,
                CreatedDate = DateTime.UtcNow.AddDays(-7),
                ExpirationDate = DateTime.UtcNow.AddDays(23),
                TotalEstimatedCost = 38200.00m,
                QuotedPrice = 49660.00m,
                Markup = 30m,
                EstimatedLaborCost = 20000.00m,
                EstimatedMaterialCost = 8200.00m,
                EstimatedOverheadCost = 10000.00m,
                TargetMarginPct = 30m,
                RevisionNumber = 1,
                SentAt = DateTime.UtcNow.AddDays(-5),
                CreatedBy = "admin",
                LastModifiedBy = "admin",
                Notes = "Replacement monocore and blast baffle inventory for Summit's suppressor refurbishment program. Commercial sale — standard FFL/SOT transfer."
            },
            // Quote 3 — Draft (R&D prototype)
            new()
            {
                QuoteNumber = "QT-00003",
                CustomerName = "Precision Arms Research",
                CustomerEmail = "engineering@precisionarms.com",
                CustomerPhone = "480-555-7100",
                Status = QuoteStatus.Draft,
                CreatedDate = DateTime.UtcNow.AddDays(-2),
                ExpirationDate = DateTime.UtcNow.AddDays(28),
                TotalEstimatedCost = 14400.00m,
                QuotedPrice = 0m,
                EstimatedLaborCost = 7200.00m,
                EstimatedMaterialCost = 3200.00m,
                EstimatedOverheadCost = 4000.00m,
                TargetMarginPct = 35m,
                RevisionNumber = 1,
                CreatedBy = "admin",
                LastModifiedBy = "admin",
                Notes = "R&D prototype: 6x complete suppressor sets for .308 caliber testing. Custom bore diameter (0.390\" vs standard 0.375\"). Monocore baffle geometry TBD after first article test."
            }
        };

        db.Quotes.AddRange(quotes);
        await db.SaveChangesAsync();

        var lines = new List<QuoteLine>
        {
            // QT-00001 lines (defense kit — 4 part types)
            new()
            {
                QuoteId = quotes[0].Id, PartId = monocore.Id, Quantity = 24,
                EstimatedCostPerPart = 2400.00m, QuotedPricePerPart = 3360.00m,
                LaborMinutes = 420, SetupMinutes = 60, MaterialCostEach = 45.60m,
                Notes = "Inconel 718 monocore — full SLS + post-process routing. 14hr build per unit, 2 per double-stack."
            },
            new()
            {
                QuoteId = quotes[0].Id, PartId = blastBaffle.Id, Quantity = 24,
                EstimatedCostPerPart = 850.00m, QuotedPricePerPart = 1190.00m,
                LaborMinutes = 180, SetupMinutes = 45, MaterialCostEach = 14.40m,
                Notes = "Inconel 718 blast baffle — SLS printed + CNC finish bore to 0.375\" +0.002/-0.000."
            },
            new()
            {
                QuoteId = quotes[0].Id, PartId = frontCap.Id, Quantity = 24,
                EstimatedCostPerPart = 85.00m, QuotedPricePerPart = 119.00m,
                LaborMinutes = 60, SetupMinutes = 20, MaterialCostEach = 7.00m,
                Notes = "17-4PH front end cap with blast chamber pocket. CNC milled, black oxide finish."
            },
            new()
            {
                QuoteId = quotes[0].Id, PartId = rearCap.Id, Quantity = 24,
                EstimatedCostPerPart = 120.00m, QuotedPricePerPart = 168.00m,
                LaborMinutes = 75, SetupMinutes = 20, MaterialCostEach = 8.00m,
                Notes = "17-4PH rear end cap — serialized per ATF requirements. Laser-engraved markings."
            },
            // QT-00002 lines (commercial replacements)
            new()
            {
                QuoteId = quotes[1].Id, PartId = monocore.Id, Quantity = 50,
                EstimatedCostPerPart = 620.00m, QuotedPricePerPart = 806.00m,
                LaborMinutes = 420, SetupMinutes = 60, MaterialCostEach = 45.60m,
                Notes = "Volume pricing — double-stack builds to maximize throughput. Est. 25 build cycles."
            },
            new()
            {
                QuoteId = quotes[1].Id, PartId = blastBaffle.Id, Quantity = 50,
                EstimatedCostPerPart = 144.00m, QuotedPricePerPart = 187.20m,
                LaborMinutes = 180, SetupMinutes = 45, MaterialCostEach = 14.40m,
                Notes = "Volume blast baffle batch — 25 build cycles at 2 per plate."
            },
            // QT-00003 lines (R&D prototype)
            new()
            {
                QuoteId = quotes[2].Id, PartId = monocore.Id, Quantity = 6,
                EstimatedCostPerPart = 1800.00m, QuotedPricePerPart = 0m,
                LaborMinutes = 480, SetupMinutes = 90, MaterialCostEach = 45.60m,
                Notes = "Custom .308 bore diameter (0.390\"). Modified baffle geometry — requires new build file. First article inspection mandatory."
            },
            new()
            {
                QuoteId = quotes[2].Id, PartId = tube.Id, Quantity = 6,
                EstimatedCostPerPart = 350.00m, QuotedPricePerPart = 0m,
                LaborMinutes = 90, SetupMinutes = 30, MaterialCostEach = 98.00m,
                Notes = "Ti-6Al-4V tubes with enlarged bore for .308 clearance."
            },
            new()
            {
                QuoteId = quotes[2].Id, PartId = adapter.Id, Quantity = 6,
                EstimatedCostPerPart = 80.00m, QuotedPricePerPart = 0m,
                LaborMinutes = 45, SetupMinutes = 15, MaterialCostEach = 6.40m,
                Notes = "5/8-24 UNEF thread (standard .308 muzzle thread) instead of 1/2-28."
            }
        };

        db.QuoteLines.AddRange(lines);
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Work Orders, Jobs & Stage Executions
    // ──────────────────────────────────────────────
    private static async Task SeedTestWorkOrdersAsync(TenantDbContext db)
    {
        // Intentionally empty — user creates work orders, jobs, and schedules manually.
        await Task.CompletedTask;
    }

    // ──────────────────────────────────────────────
    //  Build Packages (SLS multi-part build plates)
    // ──────────────────────────────────────────────
    private static async Task SeedTestBuildPackagesAsync(TenantDbContext db)
    {
        // Intentionally empty — user creates builds and schedules them manually.
        await Task.CompletedTask;
    }

    // ──────────────────────────────────────────────
    //  Quality Data (NCRs, CAPAs, Inspections, SPC)
    // ──────────────────────────────────────────────
    private static async Task SeedTestQualityDataAsync(TenantDbContext db)
    {
        // Intentionally empty — quality data created from real production runs.
        await Task.CompletedTask;
    }
}
