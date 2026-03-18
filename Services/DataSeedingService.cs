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
        await SeedProductionStagesAsync(tenantDb);
        await SeedMachinesAsync(tenantDb);
        await SeedMaterialsAsync(tenantDb);
        await SeedOperatingShiftsAsync(tenantDb);
        await SeedSystemSettingsAsync(tenantDb);
        await SeedDefaultAdminUserAsync(tenantDb);
        await SeedTestUsersAsync(tenantDb);
        await SeedTestPartsAsync(tenantDb);
    }

    private static async Task SeedProductionStagesAsync(TenantDbContext db)
    {
        if (await db.ProductionStages.AnyAsync()) return;

        var stages = new List<ProductionStage>
        {
            new()
            {
                Name = "SLS/LPBF Printing", StageSlug = "sls-printing", Department = "SLS",
                DefaultDurationHours = 8.0, IsBatchStage = true, HasBuiltInPage = true,
                DisplayOrder = 1, StageIcon = "fas fa-print", StageColor = "#3B82F6",
                RequiresMachineAssignment = true, RequiresQualityCheck = false,
                CreatedBy = "System", LastModifiedBy = "System"
            },
            new()
            {
                Name = "Depowdering", StageSlug = "depowdering", Department = "SLS",
                DefaultDurationHours = 1.0, IsBatchStage = true, HasBuiltInPage = true,
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
                DefaultDurationHours = 2.0, IsBatchStage = false, HasBuiltInPage = true,
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

        var parts = new List<Part>
        {
            new()
            {
                PartNumber = "TI-BRACKET-001",
                Name = "Titanium Mounting Bracket",
                Description = "SLS-printed titanium bracket for aerospace mounting application",
                Material = "Ti-6Al-4V Grade 5",
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
}
