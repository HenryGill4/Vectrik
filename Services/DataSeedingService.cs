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
        await SeedMachinesAsync(tenantDb);
        await SeedMaterialsAsync(tenantDb);
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
                DisplayOrder = 1, StageIcon = "fas fa-print", StageColor = "#3B82F6",
                RequiresMachineAssignment = true, RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Depowdering", StageSlug = "depowdering", Department = "SLS",
                DefaultDurationHours = 1.0, IsBatchStage = true, IsBuildLevelStage = true, HasBuiltInPage = true,
                DisplayOrder = 2, StageIcon = "fas fa-wind", StageColor = "#F59E0B",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Heat Treatment", StageSlug = "heat-treatment", Department = "Post-Process",
                DefaultDurationHours = 4.0, IsBatchStage = true, HasBuiltInPage = true,
                DisplayOrder = 3, StageIcon = "fas fa-fire", StageColor = "#EF4444",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Wire EDM", StageSlug = "wire-edm", Department = "EDM",
                DefaultDurationHours = 2.0, IsBatchStage = false, IsBuildLevelStage = true, HasBuiltInPage = true,
                DisplayOrder = 4, StageIcon = "fas fa-bolt", StageColor = "#8B5CF6",
                RequiresMachineAssignment = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "CNC Machining", StageSlug = "cnc-machining", Department = "Machining",
                DefaultDurationHours = 3.0, IsBatchStage = false, HasBuiltInPage = true,
                DisplayOrder = 5, StageIcon = "fas fa-cog", StageColor = "#06B6D4",
                RequiresMachineAssignment = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Laser Engraving", StageSlug = "laser-engraving", Department = "Engraving",
                DefaultDurationHours = 0.5, IsBatchStage = false, HasBuiltInPage = true,
                RequiresSerialNumber = true,
                DisplayOrder = 6, StageIcon = "fas fa-pen-nib", StageColor = "#10B981",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Surface Finishing", StageSlug = "surface-finishing", Department = "Finishing",
                DefaultDurationHours = 1.5, IsBatchStage = true, HasBuiltInPage = true,
                DisplayOrder = 7, StageIcon = "fas fa-spray-can", StageColor = "#EC4899",
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Quality Control", StageSlug = "qc", Department = "Quality",
                DefaultDurationHours = 0.5, IsBatchStage = false, HasBuiltInPage = true,
                DisplayOrder = 8, StageIcon = "fas fa-clipboard-check", StageColor = "#14B8A6",
                RequiresQualityCheck = true,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Shipping", StageSlug = "shipping", Department = "Shipping",
                DefaultDurationHours = 0.5, IsBatchStage = false, HasBuiltInPage = true,
                DisplayOrder = 9, StageIcon = "fas fa-truck", StageColor = "#6366F1",
                RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            }
        };

        db.ProductionStages.AddRange(stages);
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
            }
        };

        db.Materials.AddRange(materials);
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
        var laserEngraving = stages.FirstOrDefault(s => s.StageSlug == "laser-engraving");
        var surfaceFinishing = stages.FirstOrDefault(s => s.StageSlug == "surface-finishing");
        var qc = stages.FirstOrDefault(s => s.StageSlug == "qc");
        var shipping = stages.FirstOrDefault(s => s.StageSlug == "shipping");

        if (slsPrinting == null) return; // Stages not seeded yet

        // Build material lookup for FK backfill (PI.6)
        var materialLookup = await db.Materials.ToDictionaryAsync(m => m.Name, m => (int?)m.Id);

        var parts = new List<Part>
        {
            new()
            {
                PartNumber = "TI-BRACKET-001",
                Name = "Titanium Mounting Bracket",
                Description = "SLS-printed titanium bracket for aerospace mounting application",
                Material = "Ti-6Al-4V Grade 5",
                MaterialId = materialLookup.GetValueOrDefault("Ti-6Al-4V Grade 5"),
                ManufacturingApproach = "SLS/LPBF",
                Revision = "A",
                RevisionDate = DateTime.UtcNow,
                EstimatedWeightKg = 0.45,
                AllowStacking = true,
                MaxStackCount = 2,
                EnableDoubleStack = true,
                PartsPerBuildSingle = 4,
                PartsPerBuildDouble = 8,
                SingleStackDurationHours = 6.0,
                DoubleStackDurationHours = 10.0,
                SlsBuildDurationHours = 6.0,
                SlsPartsPerBuild = 4,
                DepowderingDurationHours = 1.0,
                DepowderingPartsPerBatch = 8,
                HeatTreatmentDurationHours = 4.0,
                HeatTreatmentPartsPerBatch = 16,
                WireEdmDurationHours = 1.5,
                WireEdmPartsPerSession = 2,
                IsActive = true,
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                PartNumber = "SS-HOUSING-002",
                Name = "Stainless Steel Sensor Housing",
                Description = "Precision sensor housing for industrial applications",
                Material = "316L Stainless Steel",
                MaterialId = materialLookup.GetValueOrDefault("316L Stainless Steel"),
                ManufacturingApproach = "SLS/LPBF",
                Revision = "B",
                RevisionDate = DateTime.UtcNow.AddDays(-30),
                EstimatedWeightKg = 0.82,
                AllowStacking = false,
                PartsPerBuildSingle = 2,
                SlsBuildDurationHours = 8.0,
                SlsPartsPerBuild = 2,
                DepowderingDurationHours = 1.5,
                DepowderingPartsPerBatch = 4,
                HeatTreatmentDurationHours = 3.0,
                HeatTreatmentPartsPerBatch = 8,
                IsActive = true,
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                PartNumber = "TI-IMPELLER-003",
                Name = "Titanium Impeller",
                Description = "High-performance impeller for turbomachinery, requires full routing",
                Material = "Ti-6Al-4V Grade 5",
                MaterialId = materialLookup.GetValueOrDefault("Ti-6Al-4V Grade 5"),
                ManufacturingApproach = "SLS/LPBF + CNC Finish",
                Revision = "C",
                RevisionDate = DateTime.UtcNow.AddDays(-15),
                EstimatedWeightKg = 1.2,
                IsDefensePart = true,
                AllowStacking = false,
                PartsPerBuildSingle = 1,
                SlsBuildDurationHours = 12.0,
                SlsPartsPerBuild = 1,
                DepowderingDurationHours = 2.0,
                DepowderingPartsPerBatch = 2,
                HeatTreatmentDurationHours = 6.0,
                HeatTreatmentPartsPerBatch = 4,
                WireEdmDurationHours = 3.0,
                WireEdmPartsPerSession = 1,
                IsActive = true,
                CreatedBy = "System",
                LastModifiedBy = "System"
            },
            new()
            {
                PartNumber = "CNC-PLATE-004",
                Name = "Aluminum Adapter Plate",
                Description = "CNC-only machined adapter plate, no SLS required",
                Material = "AlSi10Mg",
                MaterialId = materialLookup.GetValueOrDefault("AlSi10Mg"),
                ManufacturingApproach = "CNC Machining",
                Revision = "A",
                RevisionDate = DateTime.UtcNow,
                EstimatedWeightKg = 0.35,
                AllowStacking = false,
                PartsPerBuildSingle = 1,
                IsActive = true,
                CreatedBy = "System",
                LastModifiedBy = "System"
            }
        };

        db.Parts.AddRange(parts);
        await db.SaveChangesAsync();

        // Now add stage requirements for each part
        var bracket = parts[0];
        var housing = parts[1];
        var impeller = parts[2];
        var plate = parts[3];

        var requirements = new List<PartStageRequirement>();

        // TI-BRACKET-001: SLS → Depowder → Heat Treat → Wire EDM → Surface Finish → QC → Ship
        requirements.AddRange(new[]
        {
            new PartStageRequirement { PartId = bracket.Id, ProductionStageId = slsPrinting.Id, ExecutionOrder = 1, EstimatedHours = 6.0, AssignedMachineId = "TI1", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = bracket.Id, ProductionStageId = depowdering.Id, ExecutionOrder = 2, EstimatedHours = 1.0, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = bracket.Id, ProductionStageId = heatTreatment.Id, ExecutionOrder = 3, EstimatedHours = 4.0, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = bracket.Id, ProductionStageId = wireEdm.Id, ExecutionOrder = 4, EstimatedHours = 1.5, AssignedMachineId = "EDM1", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = bracket.Id, ProductionStageId = surfaceFinishing.Id, ExecutionOrder = 5, EstimatedHours = 1.5, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = bracket.Id, ProductionStageId = qc.Id, ExecutionOrder = 6, EstimatedHours = 0.5, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = bracket.Id, ProductionStageId = shipping.Id, ExecutionOrder = 7, EstimatedHours = 0.5, CreatedBy = "System", LastModifiedBy = "System" }
        });

        // SS-HOUSING-002: SLS → Depowder → Heat Treat → Laser Engrave → QC → Ship
        requirements.AddRange(new[]
        {
            new PartStageRequirement { PartId = housing.Id, ProductionStageId = slsPrinting.Id, ExecutionOrder = 1, EstimatedHours = 8.0, AssignedMachineId = "TI2", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = housing.Id, ProductionStageId = depowdering.Id, ExecutionOrder = 2, EstimatedHours = 1.5, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = housing.Id, ProductionStageId = heatTreatment.Id, ExecutionOrder = 3, EstimatedHours = 3.0, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = housing.Id, ProductionStageId = laserEngraving.Id, ExecutionOrder = 4, EstimatedHours = 0.5, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = housing.Id, ProductionStageId = qc.Id, ExecutionOrder = 5, EstimatedHours = 0.5, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = housing.Id, ProductionStageId = shipping.Id, ExecutionOrder = 6, EstimatedHours = 0.5, CreatedBy = "System", LastModifiedBy = "System" }
        });

        // TI-IMPELLER-003: Full routing — SLS → Depowder → Heat Treat → Wire EDM → CNC → Laser Engrave → Surface Finish → QC → Ship
        requirements.AddRange(new[]
        {
            new PartStageRequirement { PartId = impeller.Id, ProductionStageId = slsPrinting.Id, ExecutionOrder = 1, EstimatedHours = 12.0, AssignedMachineId = "TI1", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = impeller.Id, ProductionStageId = depowdering.Id, ExecutionOrder = 2, EstimatedHours = 2.0, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = impeller.Id, ProductionStageId = heatTreatment.Id, ExecutionOrder = 3, EstimatedHours = 6.0, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = impeller.Id, ProductionStageId = wireEdm.Id, ExecutionOrder = 4, EstimatedHours = 3.0, AssignedMachineId = "EDM1", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = impeller.Id, ProductionStageId = cncMachining.Id, ExecutionOrder = 5, EstimatedHours = 4.0, AssignedMachineId = "CNC1", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = impeller.Id, ProductionStageId = laserEngraving.Id, ExecutionOrder = 6, EstimatedHours = 0.5, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = impeller.Id, ProductionStageId = surfaceFinishing.Id, ExecutionOrder = 7, EstimatedHours = 2.0, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = impeller.Id, ProductionStageId = qc.Id, ExecutionOrder = 8, EstimatedHours = 1.0, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = impeller.Id, ProductionStageId = shipping.Id, ExecutionOrder = 9, EstimatedHours = 0.5, CreatedBy = "System", LastModifiedBy = "System" }
        });

        // CNC-PLATE-004: CNC only → QC → Ship
        requirements.AddRange(new[]
        {
            new PartStageRequirement { PartId = plate.Id, ProductionStageId = cncMachining.Id, ExecutionOrder = 1, EstimatedHours = 2.0, AssignedMachineId = "CNC1", CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = plate.Id, ProductionStageId = qc.Id, ExecutionOrder = 2, EstimatedHours = 0.5, CreatedBy = "System", LastModifiedBy = "System" },
            new PartStageRequirement { PartId = plate.Id, ProductionStageId = shipping.Id, ExecutionOrder = 3, EstimatedHours = 0.5, CreatedBy = "System", LastModifiedBy = "System" }
        });

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
            }
        };

        db.InventoryItems.AddRange(items);
        await db.SaveChangesAsync();

        // Add lots for the powder materials
        var tiPowder = items[0];
        var ssPowder = items[1];
        var inPowder = items[2];

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
                Notes = "Initial powder stock from AP&C", TransactedAt = DateTime.UtcNow.AddDays(-45)
            },
            new()
            {
                InventoryItemId = tiPowder.Id, TransactionType = TransactionType.JobConsumption,
                Quantity = -20m, QuantityBefore = 100m, QuantityAfter = 80m,
                FromLocationId = powderStorage.Id, LotId = lots[0].Id,
                PerformedByUserId = "System", Reference = "Build BP-2026-001",
                Notes = "Consumed for titanium bracket build", TransactedAt = DateTime.UtcNow.AddDays(-30)
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
                PerformedByUserId = "System", Reference = "Build BP-2025-012",
                Notes = "Consumed for impeller prototype", TransactedAt = DateTime.UtcNow.AddDays(-60)
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
        if (parts.Count < 4) return;

        var bracket = parts.First(p => p.PartNumber == "TI-BRACKET-001");
        var housing = parts.First(p => p.PartNumber == "SS-HOUSING-002");
        var impeller = parts.First(p => p.PartNumber == "TI-IMPELLER-003");
        var plate = parts.First(p => p.PartNumber == "CNC-PLATE-004");

        var quotes = new List<Quote>
        {
            // Quote 1 — Accepted (converted to WO)
            new()
            {
                QuoteNumber = "QT-00001",
                CustomerName = "Apex Aerospace Inc.",
                CustomerEmail = "purchasing@apexaero.com",
                CustomerPhone = "480-555-1234",
                Status = QuoteStatus.Accepted,
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                ExpirationDate = DateTime.UtcNow.AddDays(60),
                TotalEstimatedCost = 18500.00m,
                QuotedPrice = 24050.00m,
                Markup = 30m,
                EstimatedLaborCost = 8200.00m,
                EstimatedMaterialCost = 6800.00m,
                EstimatedOverheadCost = 3500.00m,
                TargetMarginPct = 30m,
                RevisionNumber = 1,
                SentAt = DateTime.UtcNow.AddDays(-28),
                AcceptedAt = DateTime.UtcNow.AddDays(-20),
                CreatedBy = "admin",
                LastModifiedBy = "admin",
                Notes = "Titanium brackets for F-35 avionics mounting. ITAR controlled.",
                IsDefenseContract = true,
                ContractNumber = "FA8650-26-C-0042"
            },
            // Quote 2 — Sent, awaiting response
            new()
            {
                QuoteNumber = "QT-00002",
                CustomerName = "MedTech Solutions",
                CustomerEmail = "procurement@medtechsol.com",
                CustomerPhone = "612-555-9876",
                Status = QuoteStatus.Sent,
                CreatedDate = DateTime.UtcNow.AddDays(-7),
                ExpirationDate = DateTime.UtcNow.AddDays(23),
                TotalEstimatedCost = 4200.00m,
                QuotedPrice = 5460.00m,
                Markup = 30m,
                EstimatedLaborCost = 1800.00m,
                EstimatedMaterialCost = 1400.00m,
                EstimatedOverheadCost = 1000.00m,
                TargetMarginPct = 30m,
                RevisionNumber = 2,
                SentAt = DateTime.UtcNow.AddDays(-5),
                CreatedBy = "admin",
                LastModifiedBy = "admin",
                Notes = "Sensor housings for implantable medical devices. Needs biocompatibility cert."
            },
            // Quote 3 — Draft
            new()
            {
                QuoteNumber = "QT-00003",
                CustomerName = "TurboTech Engineering",
                CustomerEmail = "rfq@turbotech.com",
                CustomerPhone = "206-555-3344",
                Status = QuoteStatus.Draft,
                CreatedDate = DateTime.UtcNow.AddDays(-2),
                ExpirationDate = DateTime.UtcNow.AddDays(28),
                TotalEstimatedCost = 32000.00m,
                QuotedPrice = 0m,
                EstimatedLaborCost = 15000.00m,
                EstimatedMaterialCost = 10000.00m,
                EstimatedOverheadCost = 7000.00m,
                TargetMarginPct = 25m,
                RevisionNumber = 1,
                CreatedBy = "admin",
                LastModifiedBy = "admin",
                Notes = "Titanium impellers + adapter plates for turbocharger prototyping."
            }
        };

        db.Quotes.AddRange(quotes);
        await db.SaveChangesAsync();

        // Quote lines
        var lines = new List<QuoteLine>
        {
            // QT-00001 lines
            new()
            {
                QuoteId = quotes[0].Id, PartId = bracket.Id, Quantity = 20,
                EstimatedCostPerPart = 725.00m, QuotedPricePerPart = 942.50m,
                LaborMinutes = 210, SetupMinutes = 45, MaterialCostEach = 157.50m,
                Notes = "Ti-6Al-4V brackets, full SLS routing incl. heat treat"
            },
            new()
            {
                QuoteId = quotes[0].Id, PartId = plate.Id, Quantity = 20,
                EstimatedCostPerPart = 200.00m, QuotedPricePerPart = 260.00m,
                LaborMinutes = 60, SetupMinutes = 15, MaterialCostEach = 22.75m,
                Notes = "Aluminum adapter plates, CNC only"
            },
            // QT-00002 lines
            new()
            {
                QuoteId = quotes[1].Id, PartId = housing.Id, Quantity = 10,
                EstimatedCostPerPart = 420.00m, QuotedPricePerPart = 546.00m,
                LaborMinutes = 180, SetupMinutes = 30, MaterialCostEach = 65.60m,
                Notes = "316L SS sensor housings, passivation finish required"
            },
            // QT-00003 lines
            new()
            {
                QuoteId = quotes[2].Id, PartId = impeller.Id, Quantity = 5,
                EstimatedCostPerPart = 4800.00m, QuotedPricePerPart = 0m,
                LaborMinutes = 480, SetupMinutes = 90, MaterialCostEach = 420.00m,
                Notes = "Full routing: SLS → depowder → HT → EDM → CNC → engrave → finish → QC"
            },
            new()
            {
                QuoteId = quotes[2].Id, PartId = plate.Id, Quantity = 10,
                EstimatedCostPerPart = 800.00m, QuotedPricePerPart = 0m,
                LaborMinutes = 60, SetupMinutes = 15, MaterialCostEach = 22.75m,
                Notes = "Adapter plates for impeller assembly"
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

        if (parts.Count < 4 || stages.Count == 0) return;

        var bracket = parts.First(p => p.PartNumber == "TI-BRACKET-001");
        var housing = parts.First(p => p.PartNumber == "SS-HOUSING-002");
        var impeller = parts.First(p => p.PartNumber == "TI-IMPELLER-003");
        var plate = parts.First(p => p.PartNumber == "CNC-PLATE-004");
        var stageRequirements = await db.PartStageRequirements.ToListAsync();

        var ti1 = machines.FirstOrDefault(m => m.MachineId == "TI1");
        var ti2 = machines.FirstOrDefault(m => m.MachineId == "TI2");
        var edm1 = machines.FirstOrDefault(m => m.MachineId == "EDM1");
        var cnc1 = machines.FirstOrDefault(m => m.MachineId == "CNC1");
        var operator1 = users.FirstOrDefault(u => u.Username == "operator1");
        var operator2 = users.FirstOrDefault(u => u.Username == "operator2");
        var qcInspector = users.FirstOrDefault(u => u.Username == "qcinspector");

        var acceptedQuote = await db.Quotes.FirstOrDefaultAsync(q => q.QuoteNumber == "QT-00001");

        // ── WO-1: Released, from accepted quote (brackets + plates) ──
        var wo1 = new WorkOrder
        {
            OrderNumber = "WO-00001",
            CustomerName = "Apex Aerospace Inc.",
            CustomerPO = "APX-PO-2026-0187",
            CustomerEmail = "purchasing@apexaero.com",
            CustomerPhone = "480-555-1234",
            OrderDate = DateTime.UtcNow.AddDays(-18),
            DueDate = DateTime.UtcNow.AddDays(25),
            ShipByDate = DateTime.UtcNow.AddDays(22),
            PromisedDate = DateTime.UtcNow.AddDays(25),
            Status = WorkOrderStatus.InProgress,
            Priority = JobPriority.High,
            QuoteId = acceptedQuote?.Id,
            Notes = "ITAR: F-35 avionics mounting brackets. Export-controlled — no foreign nationals.",
            IsDefenseContract = true,
            ContractNumber = "FA8650-26-C-0042",
            ContractLineItem = "CLIN 0001",
            CreatedBy = "admin", LastModifiedBy = "admin",
            ApprovedBy = "admin", ApprovedDate = DateTime.UtcNow.AddDays(-17)
        };

        // ── WO-2: Released, standalone (sensor housings) ──
        var wo2 = new WorkOrder
        {
            OrderNumber = "WO-00002",
            CustomerName = "MedTech Solutions",
            CustomerPO = "MTS-2026-0044",
            CustomerEmail = "procurement@medtechsol.com",
            OrderDate = DateTime.UtcNow.AddDays(-12),
            DueDate = DateTime.UtcNow.AddDays(30),
            ShipByDate = DateTime.UtcNow.AddDays(28),
            Status = WorkOrderStatus.Released,
            Priority = JobPriority.Normal,
            Notes = "Biocompatible sensor housings for implant program. Passivation required per ASTM A967.",
            CreatedBy = "admin", LastModifiedBy = "admin",
            ApprovedBy = "admin", ApprovedDate = DateTime.UtcNow.AddDays(-11)
        };

        // ── WO-3: Draft (impeller prototypes) ──
        var wo3 = new WorkOrder
        {
            OrderNumber = "WO-00003",
            CustomerName = "TurboTech Engineering",
            CustomerPO = "TTE-RD-2026-009",
            CustomerEmail = "rfq@turbotech.com",
            OrderDate = DateTime.UtcNow.AddDays(-2),
            DueDate = DateTime.UtcNow.AddDays(60),
            Status = WorkOrderStatus.Draft,
            Priority = JobPriority.Normal,
            Notes = "R&D prototype impellers. Customer may add additional parts.",
            CreatedBy = "admin", LastModifiedBy = "admin"
        };

        db.WorkOrders.AddRange(wo1, wo2, wo3);
        await db.SaveChangesAsync();

        // ── Work Order Lines ──
        var wo1Line1 = new WorkOrderLine { WorkOrderId = wo1.Id, PartId = bracket.Id, Quantity = 20, Status = WorkOrderStatus.InProgress };
        var wo1Line2 = new WorkOrderLine { WorkOrderId = wo1.Id, PartId = plate.Id, Quantity = 20, Status = WorkOrderStatus.Released };
        var wo2Line1 = new WorkOrderLine { WorkOrderId = wo2.Id, PartId = housing.Id, Quantity = 10, Status = WorkOrderStatus.Released };
        var wo3Line1 = new WorkOrderLine { WorkOrderId = wo3.Id, PartId = impeller.Id, Quantity = 5, Status = WorkOrderStatus.Draft };
        var wo3Line2 = new WorkOrderLine { WorkOrderId = wo3.Id, PartId = plate.Id, Quantity = 10, Status = WorkOrderStatus.Draft };

        db.WorkOrderLines.AddRange(wo1Line1, wo1Line2, wo2Line1, wo3Line1, wo3Line2);
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

            // Get routing for this part
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

        // ── WO-1: Bracket jobs (in progress — first build completed SLS, doing depowder) ──
        var bracketJob1 = await CreateJobWithStagesAsync(
            bracket, 8, wo1Line1.Id, "TI1", "JOB-00001",
            JobStatus.InProgress, DateTime.UtcNow.AddDays(-10), 15.0,
            operator1?.Id, "admin");

        // Mark first 2 stages as completed for bracketJob1
        var bj1Stages = await db.StageExecutions.Where(s => s.JobId == bracketJob1.Id).OrderBy(s => s.SortOrder).ToListAsync();
        if (bj1Stages.Count >= 3)
        {
            // SLS Printing — completed
            bj1Stages[0].Status = StageExecutionStatus.Completed;
            bj1Stages[0].ActualStartAt = DateTime.UtcNow.AddDays(-10);
            bj1Stages[0].ActualEndAt = DateTime.UtcNow.AddDays(-9.5);
            bj1Stages[0].StartedAt = bj1Stages[0].ActualStartAt;
            bj1Stages[0].CompletedAt = bj1Stages[0].ActualEndAt;
            bj1Stages[0].ActualHours = 7.2;
            bj1Stages[0].OperatorUserId = operator1?.Id;
            bj1Stages[0].OperatorName = "Mike Johnson";

            // Depowdering — completed
            bj1Stages[1].Status = StageExecutionStatus.Completed;
            bj1Stages[1].ActualStartAt = DateTime.UtcNow.AddDays(-9);
            bj1Stages[1].ActualEndAt = DateTime.UtcNow.AddDays(-9).AddHours(1.2);
            bj1Stages[1].StartedAt = bj1Stages[1].ActualStartAt;
            bj1Stages[1].CompletedAt = bj1Stages[1].ActualEndAt;
            bj1Stages[1].ActualHours = 1.2;
            bj1Stages[1].OperatorUserId = operator1?.Id;
            bj1Stages[1].OperatorName = "Mike Johnson";

            // Heat Treatment — in progress
            bj1Stages[2].Status = StageExecutionStatus.InProgress;
            bj1Stages[2].ActualStartAt = DateTime.UtcNow.AddHours(-2);
            bj1Stages[2].StartedAt = bj1Stages[2].ActualStartAt;
            bj1Stages[2].OperatorUserId = operator1?.Id;
            bj1Stages[2].OperatorName = "Mike Johnson";
        }

        bracketJob1.Status = JobStatus.InProgress;
        bracketJob1.ActualStart = DateTime.UtcNow.AddDays(-10);
        bracketJob1.ProducedQuantity = 0; // not yet at final stage
        await db.SaveChangesAsync();

        // Second bracket batch — scheduled
        var bracketJob2 = await CreateJobWithStagesAsync(
            bracket, 12, wo1Line1.Id, "TI1", "JOB-00002",
            JobStatus.Scheduled, DateTime.UtcNow.AddDays(3), 15.0,
            operator1?.Id, "admin");

        // ── WO-1: Plate jobs (scheduled) ──
        var plateJob1 = await CreateJobWithStagesAsync(
            plate, 20, wo1Line2.Id, "CNC1", "JOB-00003",
            JobStatus.Scheduled, DateTime.UtcNow.AddDays(5), 3.0,
            operator2?.Id, "admin");

        // ── WO-2: Housing job (scheduled) ──
        var housingJob1 = await CreateJobWithStagesAsync(
            housing, 10, wo2Line1.Id, "TI2", "JOB-00004",
            JobStatus.Scheduled, DateTime.UtcNow.AddDays(2), 13.5,
            operator1?.Id, "admin");

        // ── Create some PartInstances for the in-progress bracket job ──
        var bracketInstances = new List<PartInstance>();
        for (int i = 1; i <= 8; i++)
        {
            bracketInstances.Add(new PartInstance
            {
                SerialNumber = $"SN-2026-{i:D5}",
                WorkOrderLineId = wo1Line1.Id,
                PartId = bracket.Id,
                CurrentStageId = bj1Stages.Count >= 3 ? bj1Stages[2].ProductionStageId : null,
                Status = PartInstanceStatus.InProcess,
                CreatedBy = "System"
            });
        }
        db.PartInstances.AddRange(bracketInstances);
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

        if (parts.Count < 4 || woLines.Count == 0) return;

        var bracket = parts.First(p => p.PartNumber == "TI-BRACKET-001");
        var impeller = parts.First(p => p.PartNumber == "TI-IMPELLER-003");
        var housing = parts.First(p => p.PartNumber == "SS-HOUSING-002");
        var ti1 = machines.FirstOrDefault(m => m.MachineId == "TI1");

        var bracketLine = woLines.FirstOrDefault(l => l.PartId == bracket.Id);
        var housingLine = woLines.FirstOrDefault(l => l.PartId == housing.Id);

        // ── Build Package 1: Mixed bracket+housing build — Ready status ──
        var bp1 = new BuildPackage
        {
            Name = "BP-2026-001 — Ti Bracket + SS Housing Mixed Build",
            Description = "Mixed build plate: 4x Ti brackets (top) + 2x SS housings (bottom). Split material zones not supported — this is a single-material Ti build with brackets only.",
            MachineId = ti1?.MachineId ?? "TI1",
            Status = BuildPackageStatus.Ready,
            Material = "Ti-6Al-4V Grade 5",
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            EstimatedDurationHours = 8.5,
            CurrentRevision = 1,
            BuildParameters = """{"layerThickness_um":30,"laserPower_W":280,"scanSpeed_mm_s":1200,"hatchDistance_um":100,"platformTemp_C":200}""",
            Notes = "First mixed-part build test for build plate flow validation.",
            CreatedBy = "admin", LastModifiedBy = "admin"
        };

        // ── Build Package 2: Completed bracket build ──
        var bp2 = new BuildPackage
        {
            Name = "BP-2026-002 — Ti Bracket Production Run",
            Description = "Full plate of titanium brackets for Apex Aerospace WO-00001.",
            MachineId = ti1?.MachineId ?? "TI1",
            Status = BuildPackageStatus.Completed,
            Material = "Ti-6Al-4V Grade 5",
            ScheduledDate = DateTime.UtcNow.AddDays(-12),
            EstimatedDurationHours = 6.0,
            CurrentRevision = 2,
            BuildParameters = """{"layerThickness_um":30,"laserPower_W":280,"scanSpeed_mm_s":1200,"hatchDistance_um":100,"platformTemp_C":200}""",
            Notes = "Completed successfully. Parts passed QC.",
            CreatedBy = "admin", LastModifiedBy = "admin"
        };

        db.BuildPackages.AddRange(bp1, bp2);
        await db.SaveChangesAsync();

        // ── Build Package Parts ──
        var bpParts = new List<BuildPackagePart>
        {
            // BP1 parts
            new()
            {
                BuildPackageId = bp1.Id, PartId = bracket.Id, Quantity = 4,
                WorkOrderLineId = bracketLine?.Id,
                Notes = "Top zone — standard orientation"
            },
            // BP2 parts (completed build)
            new()
            {
                BuildPackageId = bp2.Id, PartId = bracket.Id, Quantity = 8,
                WorkOrderLineId = bracketLine?.Id,
                Notes = "Full plate — double-stacked"
            }
        };

        db.BuildPackageParts.AddRange(bpParts);
        await db.SaveChangesAsync();

        // ── Build File Info for both packages ──
        var buildFiles = new List<BuildFileInfo>
        {
            new()
            {
                BuildPackageId = bp1.Id,
                FileName = "BP-2026-001_TiBracket_v3.cli",
                LayerCount = 2840,
                BuildHeightMm = 85.2m,
                EstimatedPrintTimeHours = 8.5m,
                EstimatedPowderKg = 4.2m,
                SlicerSoftware = "Materialise Magics",
                SlicerVersion = "27.0.3",
                ImportedBy = "admin",
                ImportedDate = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                BuildPackageId = bp2.Id,
                FileName = "BP-2026-002_TiBracket_prod.cli",
                LayerCount = 3200,
                BuildHeightMm = 96.0m,
                EstimatedPrintTimeHours = 6.0m,
                EstimatedPowderKg = 5.8m,
                PartPositionsJson = """[{"partId":"TI-BRACKET-001","x":20,"y":20,"z":0,"copies":4},{"partId":"TI-BRACKET-001","x":20,"y":20,"z":48,"copies":4}]""",
                SlicerSoftware = "Materialise Magics",
                SlicerVersion = "27.0.3",
                ImportedBy = "admin",
                ImportedDate = DateTime.UtcNow.AddDays(-14)
            }
        };

        db.BuildFileInfos.AddRange(buildFiles);
        await db.SaveChangesAsync();

        // ── Revisions for BP2 (showing history) ──
        var revisions = new List<BuildPackageRevision>
        {
            new()
            {
                BuildPackageId = bp2.Id, RevisionNumber = 1,
                RevisionDate = DateTime.UtcNow.AddDays(-15),
                ChangedBy = "admin",
                ChangeNotes = "Initial build layout — 4 brackets single stack",
                PartsSnapshotJson = """[{"partNumber":"TI-BRACKET-001","qty":4}]""",
                ParametersSnapshotJson = bp2.BuildParameters
            },
            new()
            {
                BuildPackageId = bp2.Id, RevisionNumber = 2,
                RevisionDate = DateTime.UtcNow.AddDays(-14),
                ChangedBy = "admin",
                ChangeNotes = "Doubled to 8 brackets with stacking — updated slice file",
                PartsSnapshotJson = """[{"partNumber":"TI-BRACKET-001","qty":8}]""",
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

        var bracket = parts.First(p => p.PartNumber == "TI-BRACKET-001");
        var housing = parts.First(p => p.PartNumber == "SS-HOUSING-002");
        var impeller = parts.First(p => p.PartNumber == "TI-IMPELLER-003");
        var qcInspector = users.FirstOrDefault(u => u.Username == "qcinspector");
        var bracketJob = jobs.FirstOrDefault(j => j.JobNumber == "JOB-00001");

        // ── NCR 1: In-process defect on bracket (open) ──
        var ncr1 = new NonConformanceReport
        {
            NcrNumber = "NCR-00001",
            JobId = bracketJob?.Id,
            PartId = bracket.Id,
            Type = NcrType.InProcess,
            Description = "Surface porosity detected on 2 of 8 brackets after depowdering. Pores visible on inner mounting face, estimated 0.3-0.5mm diameter. Possible powder contamination or insufficient laser energy in overhang zone.",
            QuantityAffected = "2",
            Severity = NcrSeverity.Major,
            Disposition = NcrDisposition.PendingReview,
            Status = NcrStatus.InReview,
            ReportedByUserId = qcInspector?.Id.ToString() ?? "1",
            ReportedAt = DateTime.UtcNow.AddDays(-8)
        };

        // ── NCR 2: Incoming material (closed) ──
        var ncr2 = new NonConformanceReport
        {
            NcrNumber = "NCR-00002",
            PartId = housing.Id,
            Type = NcrType.IncomingMaterial,
            Description = "316L powder lot SS316L-2025-018 particle size distribution out of spec. D50 measured at 52μm vs. 30±5μm specification. Lot rejected and quarantined.",
            QuantityAffected = "50 kg",
            Severity = NcrSeverity.Critical,
            Disposition = NcrDisposition.ReturnToVendor,
            Status = NcrStatus.Closed,
            ReportedByUserId = qcInspector?.Id.ToString() ?? "1",
            ReportedAt = DateTime.UtcNow.AddDays(-25),
            ClosedAt = DateTime.UtcNow.AddDays(-18)
        };

        // ── NCR 3: Minor dimensional issue (dispositioned) ──
        var ncr3 = new NonConformanceReport
        {
            NcrNumber = "NCR-00003",
            PartId = bracket.Id,
            Type = NcrType.InProcess,
            Description = "Bracket hole position 0.15mm out of tolerance on X-axis. Within customer UAI (Use As-Is) limits per drawing note 4.",
            QuantityAffected = "1",
            Severity = NcrSeverity.Minor,
            Disposition = NcrDisposition.UseAsIs,
            Status = NcrStatus.Dispositioned,
            ReportedByUserId = qcInspector?.Id.ToString() ?? "1",
            ReportedAt = DateTime.UtcNow.AddDays(-5)
        };

        db.NonConformanceReports.AddRange(ncr1, ncr2, ncr3);
        await db.SaveChangesAsync();

        // ── CAPAs ──
        var capas = new List<CorrectiveAction>
        {
            // CAPA for NCR-00001 (porosity)
            new()
            {
                CapaNumber = "CAPA-00001",
                Type = CapaType.Corrective,
                ProblemStatement = "Surface porosity on Ti-6Al-4V brackets traced to powder moisture absorption during extended storage.",
                RootCauseAnalysis = "Powder hopper left unsealed over weekend. Ambient humidity 65% RH caused moisture pickup in Ti64 powder.",
                ImmediateAction = "Quarantined remaining powder from lot TI64-2026-001. Switched to fresh lot TI64-2026-002.",
                LongTermAction = "Install desiccant-based humidity monitoring in powder storage. Add SOP for sealing hoppers after each shift.",
                PreventiveAction = "Add humidity sensor alarm to powder storage area. Require powder moisture check before each build.",
                OwnerId = qcInspector?.Id.ToString() ?? "1",
                DueDate = DateTime.UtcNow.AddDays(14),
                Status = CapaStatus.InProgress,
                CreatedAt = DateTime.UtcNow.AddDays(-7)
            },
            // CAPA for NCR-00002 (vendor material)
            new()
            {
                CapaNumber = "CAPA-00002",
                Type = CapaType.Preventive,
                ProblemStatement = "Incoming 316L powder failed particle size distribution. Vendor quality control gap.",
                RootCauseAnalysis = "Vendor sieving equipment calibration was overdue. Lot passed vendor QC with out-of-cal equipment.",
                ImmediateAction = "Returned lot to vendor. Issued corrective action request (CAR) to vendor.",
                LongTermAction = "Add incoming PSD check to receiving SOP for all metal powder lots.",
                PreventiveAction = "Require vendor calibration certificates with each shipment. Add vendor to quarterly audit schedule.",
                OwnerId = qcInspector?.Id.ToString() ?? "1",
                DueDate = DateTime.UtcNow.AddDays(-4),
                CompletedAt = DateTime.UtcNow.AddDays(-4),
                EffectivenessVerification = "Verified: next 3 lots from vendor all within spec. Vendor provided updated calibration records.",
                Status = CapaStatus.Closed,
                CreatedAt = DateTime.UtcNow.AddDays(-24)
            }
        };

        db.CorrectiveActions.AddRange(capas);
        await db.SaveChangesAsync();

        // Link CAPA to NCR
        ncr1.CorrectiveActionId = capas[0].Id;
        ncr2.CorrectiveActionId = capas[1].Id;
        await db.SaveChangesAsync();

        // ── Inspection Plans ──
        var inspPlans = new List<InspectionPlan>
        {
            new()
            {
                PartId = bracket.Id,
                Name = "TI-BRACKET-001 Standard Inspection",
                Revision = "A",
                IsDefault = true,
                Characteristics = new List<InspectionPlanCharacteristic>
                {
                    new() { Name = "Overall Length", DrawingCallout = "45.00 ±0.10", NominalValue = 45.00m, TolerancePlus = 0.10m, ToleranceMinus = 0.10m, InstrumentType = "Caliper", IsKeyCharacteristic = true, DisplayOrder = 1 },
                    new() { Name = "Mounting Hole Diameter", DrawingCallout = "6.00 +0.02/-0.00", NominalValue = 6.00m, TolerancePlus = 0.02m, ToleranceMinus = 0.00m, InstrumentType = "Pin Gauge", IsKeyCharacteristic = true, DisplayOrder = 2 },
                    new() { Name = "Hole Position X", DrawingCallout = "22.50 ±0.05", NominalValue = 22.50m, TolerancePlus = 0.05m, ToleranceMinus = 0.05m, InstrumentType = "CMM", IsKeyCharacteristic = true, DisplayOrder = 3 },
                    new() { Name = "Hole Position Y", DrawingCallout = "15.00 ±0.05", NominalValue = 15.00m, TolerancePlus = 0.05m, ToleranceMinus = 0.05m, InstrumentType = "CMM", IsKeyCharacteristic = true, DisplayOrder = 4 },
                    new() { Name = "Wall Thickness", DrawingCallout = "2.00 ±0.15", NominalValue = 2.00m, TolerancePlus = 0.15m, ToleranceMinus = 0.15m, InstrumentType = "Caliper", DisplayOrder = 5 },
                    new() { Name = "Surface Roughness (Ra)", DrawingCallout = "Ra ≤ 6.3μm", NominalValue = 4.0m, TolerancePlus = 2.3m, ToleranceMinus = 4.0m, InstrumentType = "Profilometer", DisplayOrder = 6 }
                }
            },
            new()
            {
                PartId = housing.Id,
                Name = "SS-HOUSING-002 Standard Inspection",
                Revision = "B",
                IsDefault = true,
                Characteristics = new List<InspectionPlanCharacteristic>
                {
                    new() { Name = "Outer Diameter", DrawingCallout = "25.00 ±0.05", NominalValue = 25.00m, TolerancePlus = 0.05m, ToleranceMinus = 0.05m, InstrumentType = "Micrometer", IsKeyCharacteristic = true, DisplayOrder = 1 },
                    new() { Name = "Inner Bore", DrawingCallout = "18.00 +0.02/-0.00", NominalValue = 18.00m, TolerancePlus = 0.02m, ToleranceMinus = 0.00m, InstrumentType = "Bore Gauge", IsKeyCharacteristic = true, DisplayOrder = 2 },
                    new() { Name = "Overall Height", DrawingCallout = "30.00 ±0.10", NominalValue = 30.00m, TolerancePlus = 0.10m, ToleranceMinus = 0.10m, InstrumentType = "Caliper", DisplayOrder = 3 },
                    new() { Name = "Thread M20x1.5 Go", DrawingCallout = "M20x1.5-6H", NominalValue = 20.00m, TolerancePlus = 0.10m, ToleranceMinus = 0.00m, InstrumentType = "Thread Gauge", IsKeyCharacteristic = true, DisplayOrder = 4 }
                }
            }
        };

        db.InspectionPlans.AddRange(inspPlans);
        await db.SaveChangesAsync();

        // ── QC Inspections (for completed stages) ──
        if (qcInspector != null && bracketJob != null)
        {
            var inspection = new QCInspection
            {
                JobId = bracketJob.Id,
                PartId = bracket.Id,
                InspectorUserId = qcInspector.Id,
                InspectionPlanId = inspPlans[0].Id,
                OverallResult = InspectionResult.Pass,
                OverallPass = true,
                Notes = "All 8 brackets from first build batch passed dimensional inspection. 2 brackets flagged for surface porosity — see NCR-00001.",
                InspectionDate = DateTime.UtcNow.AddDays(-7)
            };
            db.QCInspections.Add(inspection);
            await db.SaveChangesAsync();

            // ── Measurements for the inspection ──
            var measurements = new List<InspectionMeasurement>
            {
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Overall Length", ActualValue = 45.03m, Deviation = 0.03m, NominalValue = 45.00m, TolerancePlus = 0.10m, ToleranceMinus = 0.10m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Mounting Hole Diameter", ActualValue = 6.01m, Deviation = 0.01m, NominalValue = 6.00m, TolerancePlus = 0.02m, ToleranceMinus = 0.00m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Hole Position X", ActualValue = 22.48m, Deviation = -0.02m, NominalValue = 22.50m, TolerancePlus = 0.05m, ToleranceMinus = 0.05m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Hole Position Y", ActualValue = 15.02m, Deviation = 0.02m, NominalValue = 15.00m, TolerancePlus = 0.05m, ToleranceMinus = 0.05m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Wall Thickness", ActualValue = 2.08m, Deviation = 0.08m, NominalValue = 2.00m, TolerancePlus = 0.15m, ToleranceMinus = 0.15m, IsInSpec = true },
                new() { QcInspectionId = inspection.Id, CharacteristicName = "Surface Roughness (Ra)", ActualValue = 5.2m, Deviation = 1.2m, NominalValue = 4.0m, TolerancePlus = 2.3m, ToleranceMinus = 4.0m, IsInSpec = true }
            };
            db.InspectionMeasurements.AddRange(measurements);
            await db.SaveChangesAsync();
        }

        // ── SPC Data Points (historical bracket measurements) ──
        var spcData = new List<SpcDataPoint>();
        var rng = new Random(42); // Deterministic for reproducible test data
        for (int i = 0; i < 30; i++)
        {
            // Overall Length: nominal 45.00 ±0.10
            spcData.Add(new SpcDataPoint
            {
                PartId = bracket.Id,
                CharacteristicName = "Overall Length",
                MeasuredValue = 45.00m + (decimal)(rng.NextDouble() * 0.14 - 0.07),
                NominalValue = 45.00m,
                TolerancePlus = 0.10m,
                ToleranceMinus = 0.10m,
                RecordedAt = DateTime.UtcNow.AddDays(-30 + i)
            });

            // Mounting Hole Diameter: nominal 6.00 +0.02/-0.00
            spcData.Add(new SpcDataPoint
            {
                PartId = bracket.Id,
                CharacteristicName = "Mounting Hole Diameter",
                MeasuredValue = 6.00m + (decimal)(rng.NextDouble() * 0.018),
                NominalValue = 6.00m,
                TolerancePlus = 0.02m,
                ToleranceMinus = 0.00m,
                RecordedAt = DateTime.UtcNow.AddDays(-30 + i)
            });
        }

        db.SpcDataPoints.AddRange(spcData);
        await db.SaveChangesAsync();
    }
}
