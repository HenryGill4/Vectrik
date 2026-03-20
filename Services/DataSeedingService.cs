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
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Depowdering", StageSlug = "depowdering", Department = "SLS",
                DefaultDurationHours = 1.0, IsBatchStage = true, IsBuildLevelStage = true, HasBuiltInPage = true,
                DisplayOrder = 2, StageIcon = "💨", StageColor = "#F59E0B",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Heat Treatment", StageSlug = "heat-treatment", Department = "Post-Process",
                DefaultDurationHours = 4.0, IsBatchStage = true, HasBuiltInPage = true,
                DisplayOrder = 3, StageIcon = "🔥", StageColor = "#EF4444",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Wire EDM", StageSlug = "wire-edm", Department = "EDM",
                DefaultDurationHours = 2.0, IsBatchStage = false, IsBuildLevelStage = true, HasBuiltInPage = true,
                DisplayOrder = 4, StageIcon = "⚡", StageColor = "#8B5CF6",
                RequiresMachineAssignment = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "CNC Machining", StageSlug = "cnc-machining", Department = "Machining",
                DefaultDurationHours = 3.0, IsBatchStage = false, HasBuiltInPage = true,
                DisplayOrder = 5, StageIcon = "⚙️", StageColor = "#06B6D4",
                RequiresMachineAssignment = true,
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
                MachineId = "TI1", Name = "TruPrint 1000 #1", MachineType = "SLS",
                MachineModel = "TruPrint 1000", Department = "SLS",
                HourlyRate = 150.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "TI2", Name = "TruPrint 1000 #2", MachineType = "SLS",
                MachineModel = "TruPrint 1000", Department = "SLS",
                HourlyRate = 150.00m, CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "INC1", Name = "Incidental SLS", MachineType = "SLS",
                Department = "SLS", HourlyRate = 100.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "EDM1", Name = "Wire EDM #1", MachineType = "EDM",
                Department = "EDM", HourlyRate = 85.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "CNC1", Name = "CNC Mill #1", MachineType = "CNC",
                Department = "Machining", HourlyRate = 95.00m,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                MachineId = "LATHE1", Name = "CNC Lathe #1", MachineType = "CNC",
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
            new PartStageRequirement { PartId = monocore.Id, ProductionStageId = slsPrinting.Id, ExecutionOrder = 1, EstimatedHours = 14.0, SetupTimeMinutes = 60, AssignedMachineId = "TI1", EstimatedCost = 2100.00m, MaterialCost = 45.60m, SpecialInstructions = "Inconel 718 powder, 30μm layer thickness. Orientation: vertical with cone apexes pointing up. Support structure on base plate interface only — internal channels are self-supporting at 45° minimum.", CreatedBy = "System", LastModifiedBy = "System" },
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
            new PartStageRequirement { PartId = blastBaffle.Id, ProductionStageId = slsPrinting.Id, ExecutionOrder = 1, EstimatedHours = 8.0, SetupTimeMinutes = 45, AssignedMachineId = "TI2", EstimatedCost = 1200.00m, MaterialCost = 14.40m, SpecialInstructions = "Inconel 718, 30μm layers. Print 2 per plate in single-stack configuration. Cone apex up, 45° internal overhang is self-supporting. Reinforce base with 3mm support block.", CreatedBy = "System", LastModifiedBy = "System" },
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
        if (await db.WorkOrders.AnyAsync()) return;

        var parts = await db.Parts.ToListAsync();
        var stages = await db.ProductionStages.OrderBy(s => s.DisplayOrder).ToListAsync();
        var machines = await db.Machines.ToListAsync();
        var users = await db.Users.ToListAsync();

        if (parts.Count < 8 || stages.Count == 0) return;

        var monocore = parts.First(p => p.PartNumber == "SUP-MONO-002");
        var blastBaffle = parts.First(p => p.PartNumber == "SUP-BBAF-006");
        var frontCap = parts.First(p => p.PartNumber == "SUP-ECAP-003");
        var rearCap = parts.First(p => p.PartNumber == "SUP-RCAP-004");
        var tube = parts.First(p => p.PartNumber == "SUP-TUBE-001");
        var adapter = parts.First(p => p.PartNumber == "SUP-ADPT-005");
        var clip = parts.First(p => p.PartNumber == "SUP-CLIP-007");
        var stageRequirements = await db.PartStageRequirements.ToListAsync();

        var ti1 = machines.FirstOrDefault(m => m.MachineId == "TI1");
        var ti2 = machines.FirstOrDefault(m => m.MachineId == "TI2");
        var edm1 = machines.FirstOrDefault(m => m.MachineId == "EDM1");
        var cnc1 = machines.FirstOrDefault(m => m.MachineId == "CNC1");
        var lathe1 = machines.FirstOrDefault(m => m.MachineId == "LATHE1");
        var operator1 = users.FirstOrDefault(u => u.Username == "operator1");
        var operator2 = users.FirstOrDefault(u => u.Username == "operator2");
        var qcInspector = users.FirstOrDefault(u => u.Username == "qcinspector");

        var acceptedQuote = await db.Quotes.FirstOrDefaultAsync(q => q.QuoteNumber == "QT-00001");

        // ── WO-1: In Progress — Defense suppressor kit production (from QT-00001) ──
        var wo1 = new WorkOrder
        {
            OrderNumber = "WO-00001",
            CustomerName = "Vanguard Defense Systems",
            CustomerPO = "VDS-PO-2026-0187",
            CustomerEmail = "contracts@vanguarddefense.com",
            CustomerPhone = "703-555-8800",
            OrderDate = DateTime.UtcNow.AddDays(-18),
            DueDate = DateTime.UtcNow.AddDays(45),
            ShipByDate = DateTime.UtcNow.AddDays(42),
            PromisedDate = DateTime.UtcNow.AddDays(45),
            Status = WorkOrderStatus.InProgress,
            Priority = JobPriority.High,
            QuoteId = acceptedQuote?.Id,
            Notes = "ITAR: SOCOM suppressor evaluation program. 24x complete internal kits (monocore + blast baffle + end caps). Export-controlled — no foreign nationals. Serialization required on all rear end caps.",
            IsDefenseContract = true,
            ContractNumber = "W56HZV-26-C-0187",
            ContractLineItem = "CLIN 0001",
            CreatedBy = "admin", LastModifiedBy = "admin",
            ApprovedBy = "admin", ApprovedDate = DateTime.UtcNow.AddDays(-17)
        };

        // ── WO-2: Released — Commercial replacement monocores ──
        var wo2 = new WorkOrder
        {
            OrderNumber = "WO-00002",
            CustomerName = "Summit Firearms LLC",
            CustomerPO = "SFL-2026-0044",
            CustomerEmail = "orders@summitfirearms.com",
            OrderDate = DateTime.UtcNow.AddDays(-12),
            DueDate = DateTime.UtcNow.AddDays(60),
            ShipByDate = DateTime.UtcNow.AddDays(58),
            Status = WorkOrderStatus.Released,
            Priority = JobPriority.Normal,
            Notes = "Commercial replacement monocore and blast baffle inventory. Standard FFL/SOT transfer. Ship in bulk packs of 10.",
            CreatedBy = "admin", LastModifiedBy = "admin",
            ApprovedBy = "admin", ApprovedDate = DateTime.UtcNow.AddDays(-11)
        };

        // ── WO-3: Draft — R&D prototype suppressors ──
        var wo3 = new WorkOrder
        {
            OrderNumber = "WO-00003",
            CustomerName = "Precision Arms Research",
            CustomerPO = "PAR-RD-2026-009",
            CustomerEmail = "engineering@precisionarms.com",
            OrderDate = DateTime.UtcNow.AddDays(-2),
            DueDate = DateTime.UtcNow.AddDays(90),
            Status = WorkOrderStatus.Draft,
            Priority = JobPriority.Normal,
            Notes = "R&D: .308 caliber prototype suppressors with custom bore. Hold for engineering approval on modified baffle geometry. First article inspection mandatory before lot production.",
            CreatedBy = "admin", LastModifiedBy = "admin"
        };

        db.WorkOrders.AddRange(wo1, wo2, wo3);
        await db.SaveChangesAsync();

        // ── Work Order Lines ──
        var wo1Line1 = new WorkOrderLine { WorkOrderId = wo1.Id, PartId = monocore.Id, Quantity = 24, Status = WorkOrderStatus.InProgress };
        var wo1Line2 = new WorkOrderLine { WorkOrderId = wo1.Id, PartId = blastBaffle.Id, Quantity = 24, Status = WorkOrderStatus.Released };
        var wo1Line3 = new WorkOrderLine { WorkOrderId = wo1.Id, PartId = frontCap.Id, Quantity = 24, Status = WorkOrderStatus.Released };
        var wo1Line4 = new WorkOrderLine { WorkOrderId = wo1.Id, PartId = rearCap.Id, Quantity = 24, Status = WorkOrderStatus.Released };
        var wo2Line1 = new WorkOrderLine { WorkOrderId = wo2.Id, PartId = monocore.Id, Quantity = 50, Status = WorkOrderStatus.Released };
        var wo2Line2 = new WorkOrderLine { WorkOrderId = wo2.Id, PartId = blastBaffle.Id, Quantity = 50, Status = WorkOrderStatus.Released };
        var wo3Line1 = new WorkOrderLine { WorkOrderId = wo3.Id, PartId = monocore.Id, Quantity = 6, Status = WorkOrderStatus.Draft };
        var wo3Line2 = new WorkOrderLine { WorkOrderId = wo3.Id, PartId = tube.Id, Quantity = 6, Status = WorkOrderStatus.Draft };
        var wo3Line3 = new WorkOrderLine { WorkOrderId = wo3.Id, PartId = adapter.Id, Quantity = 6, Status = WorkOrderStatus.Draft };

        db.WorkOrderLines.AddRange(wo1Line1, wo1Line2, wo1Line3, wo1Line4, wo2Line1, wo2Line2, wo3Line1, wo3Line2, wo3Line3);
        await db.SaveChangesAsync();

        // Link accepted quote to WO-1
        if (acceptedQuote != null)
        {
            acceptedQuote.ConvertedWorkOrderId = wo1.Id;
            await db.SaveChangesAsync();
        }

        // ── Helper to create a job + stage executions from part routing ──
        async Task<Job> CreateJobWithStagesAsync(
            Part part, int quantity, int? woLineId, string machineId, string jobNumber,
            JobStatus status, DateTime scheduledStart, double totalHours,
            int? operatorId, string createdBy)
        {
            var job = new Job
            {
                JobNumber = jobNumber,
                PartId = part.Id,
                MachineId = machineId,
                WorkOrderLineId = woLineId,
                ScheduledStart = scheduledStart,
                ScheduledEnd = scheduledStart.AddHours(totalHours),
                PartNumber = part.PartNumber,
                Quantity = quantity,
                EstimatedHours = totalHours,
                SlsMaterial = part.Material,
                Status = status,
                Priority = JobPriority.Normal,
                OperatorUserId = operatorId,
                CreatedBy = createdBy, LastModifiedBy = createdBy
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var partReqs = stageRequirements
                .Where(r => r.PartId == part.Id)
                .OrderBy(r => r.ExecutionOrder)
                .ToList();

            var runningTime = scheduledStart;
            var stageExecs = new List<StageExecution>();
            for (int i = 0; i < partReqs.Count; i++)
            {
                var req = partReqs[i];
                var stage = stages.FirstOrDefault(s => s.Id == req.ProductionStageId);
                var hours = req.EstimatedHours ?? stage?.DefaultDurationHours ?? 1.0;
                var assignedMachine = machines.FirstOrDefault(m => m.MachineId == req.AssignedMachineId);

                var exec = new StageExecution
                {
                    JobId = job.Id,
                    ProductionStageId = req.ProductionStageId,
                    Status = StageExecutionStatus.NotStarted,
                    EstimatedHours = hours,
                    ScheduledStartAt = runningTime,
                    ScheduledEndAt = runningTime.AddHours(hours),
                    MachineId = assignedMachine?.Id,
                    SortOrder = i + 1,
                    QualityCheckRequired = stage?.RequiresQualityCheck ?? false,
                    CreatedBy = createdBy, LastModifiedBy = createdBy
                };
                stageExecs.Add(exec);
                runningTime = runningTime.AddHours(hours);
            }
            db.StageExecutions.AddRange(stageExecs);
            await db.SaveChangesAsync();

            return job;
        }

        // ── JOB-00001: Monocore batch 1 (6x, in progress — SLS complete, doing depowdering) ──
        var monocoreJob1 = await CreateJobWithStagesAsync(
            monocore, 6, wo1Line1.Id, "TI1", "JOB-00001",
            JobStatus.InProgress, DateTime.UtcNow.AddDays(-10), 27.25,
            operator1?.Id, "admin");

        var mj1Stages = await db.StageExecutions.Where(s => s.JobId == monocoreJob1.Id).OrderBy(s => s.SortOrder).ToListAsync();
        if (mj1Stages.Count >= 3)
        {
            // SLS Printing — completed (14hr build, 3 double-stack cycles for 6 parts)
            mj1Stages[0].Status = StageExecutionStatus.Completed;
            mj1Stages[0].ActualStartAt = DateTime.UtcNow.AddDays(-10);
            mj1Stages[0].ActualEndAt = DateTime.UtcNow.AddDays(-7);
            mj1Stages[0].StartedAt = mj1Stages[0].ActualStartAt;
            mj1Stages[0].CompletedAt = mj1Stages[0].ActualEndAt;
            mj1Stages[0].ActualHours = 66.0; // 3 builds x 22hr each (double-stacked)
            mj1Stages[0].OperatorUserId = operator1?.Id;
            mj1Stages[0].OperatorName = "Mike Johnson";
            mj1Stages[0].CompletionNotes = "3x double-stack builds completed. All 6 monocores visually good. Minor support marks on base — will clean in depowdering.";

            // Depowdering — in progress (extended cycle for K-baffle channels)
            mj1Stages[1].Status = StageExecutionStatus.InProgress;
            mj1Stages[1].ActualStartAt = DateTime.UtcNow.AddHours(-4);
            mj1Stages[1].StartedAt = mj1Stages[1].ActualStartAt;
            mj1Stages[1].OperatorUserId = operator1?.Id;
            mj1Stages[1].OperatorName = "Mike Johnson";

            // Heat Treatment — not started yet
        }

        monocoreJob1.Status = JobStatus.InProgress;
        monocoreJob1.ActualStart = DateTime.UtcNow.AddDays(-10);
        monocoreJob1.ProducedQuantity = 0;
        await db.SaveChangesAsync();

        // ── JOB-00002: Monocore batch 2 (6x, scheduled) ──
        await CreateJobWithStagesAsync(
            monocore, 6, wo1Line1.Id, "TI1", "JOB-00002",
            JobStatus.Scheduled, DateTime.UtcNow.AddDays(5), 27.25,
            operator1?.Id, "admin");

        // ── JOB-00003: Blast baffle batch (12x, scheduled on TI2) ──
        await CreateJobWithStagesAsync(
            blastBaffle, 12, wo1Line2.Id, "TI2", "JOB-00003",
            JobStatus.Scheduled, DateTime.UtcNow.AddDays(3), 18.5,
            operator1?.Id, "admin");

        // ── JOB-00004: Front end caps (24x, scheduled CNC) ──
        await CreateJobWithStagesAsync(
            frontCap, 24, wo1Line3.Id, "CNC1", "JOB-00004",
            JobStatus.Scheduled, DateTime.UtcNow.AddDays(7), 2.25,
            operator2?.Id, "admin");

        // ── JOB-00005: Rear end caps (24x, scheduled CNC) ──
        await CreateJobWithStagesAsync(
            rearCap, 24, wo1Line4.Id, "CNC1", "JOB-00005",
            JobStatus.Scheduled, DateTime.UtcNow.AddDays(10), 2.5,
            operator2?.Id, "admin");

        // ── Create PartInstances for the in-progress monocore job ──
        var monocoreInstances = new List<PartInstance>();
        for (int i = 1; i <= 6; i++)
        {
            monocoreInstances.Add(new PartInstance
            {
                SerialNumber = $"MC-2026-{i:D5}",
                TemporaryTrackingId = $"MC-2026-{i:D5}",
                IsSerialAssigned = true,
                WorkOrderLineId = wo1Line1.Id,
                PartId = monocore.Id,
                CurrentStageId = mj1Stages.Count >= 2 ? mj1Stages[1].ProductionStageId : null,
                Status = PartInstanceStatus.InProcess,
                CreatedBy = "System"
            });
        }
        db.PartInstances.AddRange(monocoreInstances);
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Build Packages (SLS multi-part build plates)
    // ──────────────────────────────────────────────
    private static async Task SeedTestBuildPackagesAsync(TenantDbContext db)
    {
        if (await db.BuildPackages.AnyAsync()) return;

        var parts = await db.Parts.ToListAsync();
        var machines = await db.Machines.ToListAsync();
        var woLines = await db.WorkOrderLines.Include(l => l.WorkOrder).ToListAsync();

        if (parts.Count < 8 || woLines.Count == 0) return;

        var monocore = parts.First(p => p.PartNumber == "SUP-MONO-002");
        var blastBaffle = parts.First(p => p.PartNumber == "SUP-BBAF-006");
        var ti1 = machines.FirstOrDefault(m => m.MachineId == "TI1");
        var ti2 = machines.FirstOrDefault(m => m.MachineId == "TI2");

        var monocoreLine = woLines.FirstOrDefault(l => l.PartId == monocore.Id && l.WorkOrder?.OrderNumber == "WO-00001");
        var blastBaffleLine = woLines.FirstOrDefault(l => l.PartId == blastBaffle.Id && l.WorkOrder?.OrderNumber == "WO-00001");

        // ── BP-2026-001: Monocore double-stack build — Ready (next up on TI1) ──
        var bp1 = new BuildPackage
        {
            Name = "BP-2026-001 — Monocore Baffle Stack Build (Double-Stack)",
            Description = "2x Inconel 718 monocore baffle stacks, double-stacked on TI1. Vanguard Defense WO-00001 batch 2. 22hr estimated build time at 30μm layer thickness. Argon atmosphere required for IN718.",
            MachineId = ti1?.MachineId ?? "TI1",
            Status = BuildPackageStatus.Ready,
            Material = "Inconel 718",
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            EstimatedDurationHours = 22.0,
            IsSlicerDataEntered = true,
            CurrentRevision = 1,
            BuildParameters = """{"layerThickness_um":30,"laserPower_W":285,"scanSpeed_mm_s":960,"hatchDistance_um":110,"platformTemp_C":80,"atmosphere":"Argon","recoaterType":"Rubber"}""",
            Notes = "Second monocore build for Vanguard SOCOM order. Double-stack layout verified in Magics — 45° overhang on K-baffle cones is self-supporting. Monitor argon O2 level < 100ppm.",
            CreatedBy = "admin", LastModifiedBy = "admin"
        };

        // ── BP-2026-002: Completed monocore build (first batch, already processed) ──
        var bp2 = new BuildPackage
        {
            Name = "BP-2026-002 — Monocore Baffle Stack Build (Batch 1)",
            Description = "Completed build: 2x Inconel 718 monocore stacks, double-stacked. First batch for Vanguard Defense WO-00001. All parts passed visual and depowdering is in progress.",
            MachineId = ti1?.MachineId ?? "TI1",
            Status = BuildPackageStatus.Completed,
            Material = "Inconel 718",
            ScheduledDate = DateTime.UtcNow.AddDays(-12),
            EstimatedDurationHours = 22.0,
            IsSlicerDataEntered = true,
            IsLocked = true,
            PrintStartedAt = DateTime.UtcNow.AddDays(-11),
            PrintCompletedAt = DateTime.UtcNow.AddDays(-10),
            PlateReleasedAt = DateTime.UtcNow.AddDays(-9),
            CurrentRevision = 2,
            BuildParameters = """{"layerThickness_um":30,"laserPower_W":285,"scanSpeed_mm_s":960,"hatchDistance_um":110,"platformTemp_C":80,"atmosphere":"Argon","recoaterType":"Rubber"}""",
            Notes = "Completed successfully. 2 monocores printed in double-stack. Build time: 21.8hr actual. O2 level maintained <80ppm throughout. Parts released to depowdering.",
            CreatedBy = "admin", LastModifiedBy = "admin"
        };

        // ── BP-2026-003: Blast baffle build — Scheduled on TI2 ──
        var bp3 = new BuildPackage
        {
            Name = "BP-2026-003 — Blast Baffle Build (4x Single-Stack)",
            Description = "4x Inconel 718 blast baffles, single-stack on TI2. Two build plates needed for WO-00001 qty of 24 (2 per plate x 12 plates, but we batch 4 per plate with double-stack for efficiency).",
            MachineId = ti2?.MachineId ?? "TI2",
            Status = BuildPackageStatus.Scheduled,
            Material = "Inconel 718",
            ScheduledDate = DateTime.UtcNow.AddDays(3),
            EstimatedDurationHours = 13.0,
            IsSlicerDataEntered = true,
            IsLocked = true,
            CurrentRevision = 1,
            BuildParameters = """{"layerThickness_um":30,"laserPower_W":285,"scanSpeed_mm_s":960,"hatchDistance_um":110,"platformTemp_C":80,"atmosphere":"Argon","recoaterType":"Rubber"}""",
            Notes = "First blast baffle build for Vanguard order. Double-stack 4 baffles per plate. Parts go to depowder → heat treat → CNC finish bore after build.",
            CreatedBy = "admin", LastModifiedBy = "admin"
        };

        db.BuildPackages.AddRange(bp1, bp2, bp3);
        await db.SaveChangesAsync();

        // ── Build Package Parts ──
        var bpParts = new List<BuildPackagePart>
        {
            // BP1: 2x monocore (double-stacked)
            new()
            {
                BuildPackageId = bp1.Id, PartId = monocore.Id, Quantity = 2,
                StackLevel = 2,
                WorkOrderLineId = monocoreLine?.Id,
                Notes = "Double-stack: 1 monocore per stack level, 2 total. Vertical orientation, cone apexes up."
            },
            // BP2: 2x monocore (completed build)
            new()
            {
                BuildPackageId = bp2.Id, PartId = monocore.Id, Quantity = 2,
                StackLevel = 2,
                WorkOrderLineId = monocoreLine?.Id,
                Notes = "Completed — double-stacked. Both parts released to post-processing."
            },
            // BP3: 4x blast baffle (double-stacked)
            new()
            {
                BuildPackageId = bp3.Id, PartId = blastBaffle.Id, Quantity = 4,
                StackLevel = 2,
                WorkOrderLineId = blastBaffleLine?.Id,
                Notes = "Double-stack: 2 baffles per level x 2 levels. Cone apex up, 3mm support block on base."
            }
        };

        db.BuildPackageParts.AddRange(bpParts);
        await db.SaveChangesAsync();

        // ── Build File Info ──
        var buildFiles = new List<BuildFileInfo>
        {
            new()
            {
                BuildPackageId = bp1.Id,
                FileName = "BP-2026-001_Monocore_DblStack_v2.cli",
                LayerCount = 4200,
                BuildHeightMm = 126.0m,
                EstimatedPrintTimeHours = 22.0m,
                EstimatedPowderKg = 3.8m,
                PartPositionsJson = """[{"partId":"SUP-MONO-002","x":62,"y":62,"z":0,"rotation":0,"stackLevel":1},{"partId":"SUP-MONO-002","x":62,"y":62,"z":63,"rotation":0,"stackLevel":2}]""",
                SlicerSoftware = "Materialise Magics",
                SlicerVersion = "27.0.3",
                ImportedBy = "admin",
                ImportedDate = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                BuildPackageId = bp2.Id,
                FileName = "BP-2026-002_Monocore_DblStack_v1.cli",
                LayerCount = 4200,
                BuildHeightMm = 126.0m,
                EstimatedPrintTimeHours = 22.0m,
                EstimatedPowderKg = 3.8m,
                PartPositionsJson = """[{"partId":"SUP-MONO-002","x":62,"y":62,"z":0,"rotation":0,"stackLevel":1},{"partId":"SUP-MONO-002","x":62,"y":62,"z":63,"rotation":0,"stackLevel":2}]""",
                SlicerSoftware = "Materialise Magics",
                SlicerVersion = "27.0.3",
                ImportedBy = "admin",
                ImportedDate = DateTime.UtcNow.AddDays(-14)
            },
            new()
            {
                BuildPackageId = bp3.Id,
                FileName = "BP-2026-003_BlastBaffle_DblStack_v1.cli",
                LayerCount = 1800,
                BuildHeightMm = 54.0m,
                EstimatedPrintTimeHours = 13.0m,
                EstimatedPowderKg = 1.6m,
                PartPositionsJson = """[{"partId":"SUP-BBAF-006","x":40,"y":40,"z":0,"rotation":0,"stackLevel":1},{"partId":"SUP-BBAF-006","x":85,"y":40,"z":0,"rotation":0,"stackLevel":1},{"partId":"SUP-BBAF-006","x":40,"y":40,"z":27,"rotation":0,"stackLevel":2},{"partId":"SUP-BBAF-006","x":85,"y":40,"z":27,"rotation":0,"stackLevel":2}]""",
                SlicerSoftware = "Materialise Magics",
                SlicerVersion = "27.0.3",
                ImportedBy = "admin",
                ImportedDate = DateTime.UtcNow.AddDays(-1)
            }
        };

        db.BuildFileInfos.AddRange(buildFiles);
        await db.SaveChangesAsync();

        // ── Revisions for BP2 (showing build history) ──
        var revisions = new List<BuildPackageRevision>
        {
            new()
            {
                BuildPackageId = bp2.Id, RevisionNumber = 1,
                RevisionDate = DateTime.UtcNow.AddDays(-15),
                ChangedBy = "admin",
                ChangeNotes = "Initial layout — single monocore, single-stack. 14hr estimated.",
                PartsSnapshotJson = """[{"partNumber":"SUP-MONO-002","qty":1,"stackLevel":1}]""",
                ParametersSnapshotJson = bp2.BuildParameters
            },
            new()
            {
                BuildPackageId = bp2.Id, RevisionNumber = 2,
                RevisionDate = DateTime.UtcNow.AddDays(-14),
                ChangedBy = "admin",
                ChangeNotes = "Upgraded to double-stack (2x monocore). Build time increased to 22hr but throughput doubled. Updated slice file.",
                PartsSnapshotJson = """[{"partNumber":"SUP-MONO-002","qty":2,"stackLevel":2}]""",
                ParametersSnapshotJson = bp2.BuildParameters
            }
        };

        db.BuildPackageRevisions.AddRange(revisions);
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────
    //  Quality Data (NCRs, CAPAs, Inspections, SPC)
    // ──────────────────────────────────────────────
    private static async Task SeedTestQualityDataAsync(TenantDbContext db)
    {
        if (await db.NonConformanceReports.AnyAsync()) return;

        var parts = await db.Parts.ToListAsync();
        var jobs = await db.Jobs.ToListAsync();
        var users = await db.Users.ToListAsync();
        var stages = await db.ProductionStages.ToListAsync();

        if (parts.Count == 0 || users.Count == 0) return;

        var monocore = parts.First(p => p.PartNumber == "SUP-MONO-002");
        var blastBaffle = parts.First(p => p.PartNumber == "SUP-BBAF-006");
        var rearCap = parts.First(p => p.PartNumber == "SUP-RCAP-004");
        var qcInspector = users.FirstOrDefault(u => u.Username == "qcinspector");
        var monocoreJob = jobs.FirstOrDefault(j => j.JobNumber == "JOB-00001");

        // ── NCR 1: Monocore internal porosity (open, in review) ──
        var ncr1 = new NonConformanceReport
        {
            NcrNumber = "NCR-00001",
            JobId = monocoreJob?.Id,
            PartId = monocore.Id,
            Type = NcrType.InProcess,
            Description = "CT scan of monocore MC-2026-00003 revealed sub-surface porosity cluster in cone #4 (0.2-0.4mm voids). Density 99.2% vs. specification minimum 99.5%. Root cause suspected: argon flow rate dropped below setpoint during layer 2800-3100 range. Remaining 5 monocores from same build passed CT scan.",
            QuantityAffected = "1",
            Severity = NcrSeverity.Major,
            Disposition = NcrDisposition.PendingReview,
            Status = NcrStatus.InReview,
            ReportedByUserId = qcInspector?.Id.ToString() ?? "1",
            ReportedAt = DateTime.UtcNow.AddDays(-6)
        };

        // ── NCR 2: Inconel 718 powder PSD (closed, returned to vendor) ──
        var ncr2 = new NonConformanceReport
        {
            NcrNumber = "NCR-00002",
            PartId = monocore.Id,
            Type = NcrType.IncomingMaterial,
            Description = "Inconel 718 powder lot IN718-2026-001 particle size distribution out of spec. D50 measured at 48μm vs. specification 25-40μm. Satellite particles visible under SEM at 500x. Lot quarantined pending vendor disposition.",
            QuantityAffected = "80 kg",
            Severity = NcrSeverity.Critical,
            Disposition = NcrDisposition.ReturnToVendor,
            Status = NcrStatus.Closed,
            ReportedByUserId = qcInspector?.Id.ToString() ?? "1",
            ReportedAt = DateTime.UtcNow.AddDays(-25),
            ClosedAt = DateTime.UtcNow.AddDays(-18)
        };

        // ── NCR 3: Blast baffle bore tolerance (dispositioned — use as is) ──
        var ncr3 = new NonConformanceReport
        {
            NcrNumber = "NCR-00003",
            PartId = blastBaffle.Id,
            Type = NcrType.InProcess,
            Description = "CNC finish bore on blast baffle measured 0.3762\" — within tolerance (+0.002/-0.000 on 0.375\" nominal) but at 60% of tolerance band. Part is within spec but flagged for process monitoring. CNC tool wear detected — replacement recommended after 50 bores.",
            QuantityAffected = "1",
            Severity = NcrSeverity.Minor,
            Disposition = NcrDisposition.UseAsIs,
            Status = NcrStatus.Dispositioned,
            ReportedByUserId = qcInspector?.Id.ToString() ?? "1",
            ReportedAt = DateTime.UtcNow.AddDays(-4)
        };

        // ── NCR 4: Rear end cap engraving depth (dispositioned — rework) ──
        var ncr4 = new NonConformanceReport
        {
            NcrNumber = "NCR-00004",
            PartId = rearCap.Id,
            Type = NcrType.InProcess,
            Description = "Laser engraving depth on rear end cap serial RCAP-2026-00012 measured 0.002\" — below ATF minimum requirement of 0.003\". Laser power setting found at 80% vs. SOP requirement of 95%. Re-engrave required.",
            QuantityAffected = "1",
            Severity = NcrSeverity.Major,
            Disposition = NcrDisposition.Rework,
            Status = NcrStatus.Closed,
            ReportedByUserId = qcInspector?.Id.ToString() ?? "1",
            ReportedAt = DateTime.UtcNow.AddDays(-10),
            ClosedAt = DateTime.UtcNow.AddDays(-8)
        };

        db.NonConformanceReports.AddRange(ncr1, ncr2, ncr3, ncr4);
        await db.SaveChangesAsync();

        // ── CAPAs ──
        var capas = new List<CorrectiveAction>
        {
            // CAPA for NCR-00001 (monocore porosity)
            new()
            {
                CapaNumber = "CAPA-00001",
                Type = CapaType.Corrective,
                ProblemStatement = "Sub-surface porosity in Inconel 718 monocore baffle stack traced to argon flow rate drop during SLS build on TruPrint 1000 #1 (TI1).",
                RootCauseAnalysis = "Gas flow sensor on TI1 showed intermittent readings between layers 2800-3100. Maintenance log shows sensor last calibrated 6 months ago (overdue by 3 months). Low argon flow allowed O2 ingress >200ppm in build chamber, causing oxide inclusion porosity.",
                ImmediateAction = "Replaced argon flow sensor on TI1. Recalibrated all gas sensors on TI1 and TI2. Quarantined affected monocore MC-2026-00003 pending engineering review.",
                LongTermAction = "Add argon flow rate to real-time build monitoring dashboard with automatic pause if flow drops below 95% setpoint. Reduce sensor calibration interval from 6 months to 3 months.",
                PreventiveAction = "Install redundant O2 sensors in both TruPrint build chambers. Add automated build abort if O2 exceeds 150ppm for >30 seconds. Update PM schedule for all SLS machine gas systems.",
                OwnerId = qcInspector?.Id.ToString() ?? "1",
                DueDate = DateTime.UtcNow.AddDays(14),
                Status = CapaStatus.InProgress,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            // CAPA for NCR-00002 (vendor powder PSD)
            new()
            {
                CapaNumber = "CAPA-00002",
                Type = CapaType.Preventive,
                ProblemStatement = "Incoming Inconel 718 powder from Carpenter Technology failed particle size distribution specification. Satellite particles indicate atomization process issue at vendor.",
                RootCauseAnalysis = "Vendor atomization nozzle worn beyond tolerance. Lot passed vendor QC using outdated PSD specification (pre-2025 revision). Vendor acknowledged discrepancy.",
                ImmediateAction = "Returned 80kg lot to Carpenter Technology with RMA #CT-2026-0102. Switched to backup lot IN718-2025-004 (verified in-spec).",
                LongTermAction = "Require vendors to include PSD report (D10/D50/D90) and SEM imagery with each powder lot. Add incoming PSD verification to receiving SOP using Malvern Mastersizer.",
                PreventiveAction = "Qualify second Inconel 718 powder vendor (AP&C) to avoid single-source risk. Add vendor to quarterly audit schedule with focus on atomization equipment calibration.",
                OwnerId = qcInspector?.Id.ToString() ?? "1",
                DueDate = DateTime.UtcNow.AddDays(-4),
                CompletedAt = DateTime.UtcNow.AddDays(-4),
                EffectivenessVerification = "Verified: next 2 lots from Carpenter all within spec. Vendor provided updated nozzle replacement schedule and revised PSD specification. Incoming PSD check added to receiving SOP — first 5 lots all verified.",
                Status = CapaStatus.Closed,
                CreatedAt = DateTime.UtcNow.AddDays(-24)
            }
        };

        db.CorrectiveActions.AddRange(capas);
        await db.SaveChangesAsync();

        // Link CAPAs to NCRs
        ncr1.CorrectiveActionId = capas[0].Id;
        ncr2.CorrectiveActionId = capas[1].Id;
        await db.SaveChangesAsync();

        // ── Inspection Plans ──
        var inspPlans = new List<InspectionPlan>
        {
            // Monocore baffle stack inspection
            new()
            {
                PartId = monocore.Id,
                Name = "SUP-MONO-002 Monocore Baffle Stack Inspection",
                Revision = "D",
                IsDefault = true,
                Characteristics = new List<InspectionPlanCharacteristic>
                {
                    new() { Name = "Bore Diameter", DrawingCallout = "0.375\" +0.005/-0.000", NominalValue = 9.525m, TolerancePlus = 0.127m, ToleranceMinus = 0.000m, InstrumentType = "Pin Gauge Set", IsKeyCharacteristic = true, DisplayOrder = 1 },
                    new() { Name = "Bore Alignment (Full Length)", DrawingCallout = "≤0.005\" TIR over 6.5\"", NominalValue = 0.000m, TolerancePlus = 0.127m, ToleranceMinus = 0.000m, InstrumentType = "Alignment Rod + DTI", IsKeyCharacteristic = true, DisplayOrder = 2 },
                    new() { Name = "Overall Length", DrawingCallout = "6.500\" ±0.010", NominalValue = 165.10m, TolerancePlus = 0.254m, ToleranceMinus = 0.254m, InstrumentType = "Caliper", IsKeyCharacteristic = true, DisplayOrder = 3 },
                    new() { Name = "OD Profile (Max)", DrawingCallout = "1.350\" ±0.005", NominalValue = 34.29m, TolerancePlus = 0.127m, ToleranceMinus = 0.127m, InstrumentType = "CMM", IsKeyCharacteristic = true, DisplayOrder = 4 },
                    new() { Name = "Weight", DrawingCallout = "0.38 kg ±0.03", NominalValue = 0.38m, TolerancePlus = 0.03m, ToleranceMinus = 0.03m, InstrumentType = "Precision Scale", DisplayOrder = 5 },
                    new() { Name = "Surface Roughness (Ra) — External", DrawingCallout = "Ra ≤ 12.5μm", NominalValue = 8.0m, TolerancePlus = 4.5m, ToleranceMinus = 8.0m, InstrumentType = "Profilometer", DisplayOrder = 6 },
                    new() { Name = "Hardness (HRC)", DrawingCallout = "HRC 38-44", NominalValue = 41.0m, TolerancePlus = 3.0m, ToleranceMinus = 3.0m, InstrumentType = "Rockwell C Tester", IsKeyCharacteristic = true, DisplayOrder = 7 },
                    new() { Name = "Internal Channel Clearance", DrawingCallout = "All 6 channels clear (borescope)", NominalValue = 1.0m, TolerancePlus = 0.0m, ToleranceMinus = 0.0m, InstrumentType = "Borescope", IsKeyCharacteristic = true, DisplayOrder = 8 }
                }
            },
            // Rear end cap inspection (serialized part — critical for ATF compliance)
            new()
            {
                PartId = rearCap.Id,
                Name = "SUP-RCAP-004 Rear End Cap Inspection",
                Revision = "B",
                IsDefault = true,
                Characteristics = new List<InspectionPlanCharacteristic>
                {
                    new() { Name = "External Thread (1.375-24 TPI)", DrawingCallout = "Class 3A Go/No-Go", NominalValue = 34.925m, TolerancePlus = 0.050m, ToleranceMinus = 0.000m, InstrumentType = "Thread Ring Gauge", IsKeyCharacteristic = true, DisplayOrder = 1 },
                    new() { Name = "Internal Thread (1.375-24 TPI)", DrawingCallout = "Class 3B Go/No-Go", NominalValue = 34.925m, TolerancePlus = 0.050m, ToleranceMinus = 0.000m, InstrumentType = "Thread Plug Gauge", IsKeyCharacteristic = true, DisplayOrder = 2 },
                    new() { Name = "Concentricity (ID to OD)", DrawingCallout = "≤0.001\" TIR", NominalValue = 0.000m, TolerancePlus = 0.0254m, ToleranceMinus = 0.000m, InstrumentType = "DTI on V-Block", IsKeyCharacteristic = true, DisplayOrder = 3 },
                    new() { Name = "Engraving Depth", DrawingCallout = "≥0.003\" (ATF requirement)", NominalValue = 0.004m, TolerancePlus = 0.002m, ToleranceMinus = 0.001m, InstrumentType = "Depth Micrometer", IsKeyCharacteristic = true, DisplayOrder = 4 },
                    new() { Name = "Engraving Legibility", DrawingCallout = "Visual: serial, mfr, caliber readable", NominalValue = 1.0m, TolerancePlus = 0.0m, ToleranceMinus = 0.0m, InstrumentType = "Visual (10x Loupe)", IsKeyCharacteristic = true, DisplayOrder = 5 },
                    new() { Name = "Overall Height", DrawingCallout = "0.750\" ±0.005", NominalValue = 19.05m, TolerancePlus = 0.127m, ToleranceMinus = 0.127m, InstrumentType = "Caliper", DisplayOrder = 6 }
                }
            }
        };

        db.InspectionPlans.AddRange(inspPlans);
        await db.SaveChangesAsync();

        // ── QC Inspections (for completed monocore SLS stage) ──
        if (qcInspector != null && monocoreJob != null)
        {
            var inspection = new QCInspection
            {
                JobId = monocoreJob.Id,
                PartId = monocore.Id,
                InspectorUserId = qcInspector.Id,
                InspectionPlanId = inspPlans[0].Id,
                OverallResult = InspectionResult.Conditional,
                OverallPass = true,
                Notes = "5 of 6 monocores from JOB-00001 batch 1 passed all dimensional and NDT checks. MC-2026-00003 failed CT scan density — see NCR-00001. Remaining 5 parts approved for continued processing through heat treatment.",
                InspectionDate = DateTime.UtcNow.AddDays(-5)
            };
            db.QCInspections.Add(inspection);
            await db.SaveChangesAsync();

            // ── Measurements for the inspection (representative of passing parts) ──
            var measurements = new List<InspectionMeasurement>
            {
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Bore Diameter", ActualValue = 9.540m, Deviation = 0.015m, NominalValue = 9.525m, TolerancePlus = 0.127m, ToleranceMinus = 0.000m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Bore Alignment (Full Length)", ActualValue = 0.076m, Deviation = 0.076m, NominalValue = 0.000m, TolerancePlus = 0.127m, ToleranceMinus = 0.000m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Overall Length", ActualValue = 165.18m, Deviation = 0.08m, NominalValue = 165.10m, TolerancePlus = 0.254m, ToleranceMinus = 0.254m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "OD Profile (Max)", ActualValue = 34.32m, Deviation = 0.03m, NominalValue = 34.29m, TolerancePlus = 0.127m, ToleranceMinus = 0.127m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Weight", ActualValue = 0.382m, Deviation = 0.002m, NominalValue = 0.38m, TolerancePlus = 0.03m, ToleranceMinus = 0.03m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Surface Roughness (Ra) — External", ActualValue = 9.8m, Deviation = 1.8m, NominalValue = 8.0m, TolerancePlus = 4.5m, ToleranceMinus = 8.0m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Hardness (HRC)", ActualValue = 41.5m, Deviation = 0.5m, NominalValue = 41.0m, TolerancePlus = 3.0m, ToleranceMinus = 3.0m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Internal Channel Clearance", ActualValue = 1.0m, Deviation = 0.0m, NominalValue = 1.0m, TolerancePlus = 0.0m, ToleranceMinus = 0.0m, IsInSpec = true }
            };
            db.InspectionMeasurements.AddRange(measurements);
            await db.SaveChangesAsync();
        }

        // ── SPC Data Points (historical monocore bore diameter and alignment measurements) ──
        var spcData = new List<SpcDataPoint>();
        var rng = new Random(42); // Deterministic for reproducible test data
        for (int i = 0; i < 30; i++)
        {
            // Bore Diameter: nominal 9.525mm (0.375"), tolerance +0.127/-0.000
            spcData.Add(new SpcDataPoint
            {
                PartId = monocore.Id,
                CharacteristicName = "Bore Diameter",
                MeasuredValue = 9.525m + (decimal)(rng.NextDouble() * 0.100),
                NominalValue = 9.525m,
                TolerancePlus = 0.127m,
                ToleranceMinus = 0.000m,
                RecordedAt = DateTime.UtcNow.AddDays(-30 + i)
            });

            // Bore Alignment: nominal 0.000mm, tolerance +0.127/-0.000 (TIR)
            spcData.Add(new SpcDataPoint
            {
                PartId = monocore.Id,
                CharacteristicName = "Bore Alignment (Full Length)",
                MeasuredValue = (decimal)(rng.NextDouble() * 0.100),
                NominalValue = 0.000m,
                TolerancePlus = 0.127m,
                ToleranceMinus = 0.000m,
                RecordedAt = DateTime.UtcNow.AddDays(-30 + i)
            });

            // Overall Length: nominal 165.10mm (6.500"), tolerance ±0.254mm
            spcData.Add(new SpcDataPoint
            {
                PartId = monocore.Id,
                CharacteristicName = "Overall Length",
                MeasuredValue = 165.10m + (decimal)(rng.NextDouble() * 0.36 - 0.18),
                NominalValue = 165.10m,
                TolerancePlus = 0.254m,
                ToleranceMinus = 0.254m,
                RecordedAt = DateTime.UtcNow.AddDays(-30 + i)
            });
        }

        db.SpcDataPoints.AddRange(spcData);
        await db.SaveChangesAsync();
    }
}
