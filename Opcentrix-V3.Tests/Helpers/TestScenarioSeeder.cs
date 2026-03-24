using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Tests.Helpers;

/// <summary>
/// Provides realistic seeding scenarios for testing the program-scheduler integration.
/// Each scenario seeds: Machines, ProductionStages, Parts, ManufacturingProcesses, 
/// ProcessStages with linked MachinePrograms, WorkOrders, and related entities.
/// </summary>
public static class TestScenarioSeeder
{
    // Fixed reference date for deterministic tests
    public static readonly DateTime BaseDate = new(2025, 7, 7, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Seeds a complete SLS additive manufacturing scenario with:
    /// - EOS M4 SLS printer with build plate programs
    /// - Depowder and Heat Treatment post-processing machines
    /// - Ti-6Al-4V aerospace part with full manufacturing process
    /// - BuildPlate program linked to print stage
    /// - WorkOrder ready for scheduling
    /// </summary>
    public static async Task<SlsScenarioResult> SeedSlsScenarioAsync(TenantDbContext db)
    {
        // ══════════════════════════════════════════════════════════
        // Machines
        // ══════════════════════════════════════════════════════════
        var slsMachine = new Machine
        {
            MachineId = "M4-1",
            Name = "EOS M4 Onyx #1",
            MachineType = "SLS",
            MachineModel = "EOS M 400-4",
            Department = "SLS",
            BuildLengthMm = 450,
            BuildWidthMm = 450,
            BuildHeightMm = 400,
            BuildPlateCapacity = 2,
            AutoChangeoverEnabled = true,
            ChangeoverMinutes = 30,
            LaserCount = 6,
            MaxLaserPowerWatts = 1000,
            HourlyRate = 200.00m,
            IsActive = true,
            IsAvailableForScheduling = true,
            IsAdditiveMachine = true,
            Priority = 1,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var depowderMachine = new Machine
        {
            MachineId = "INC1",
            Name = "Incineris Depowder",
            MachineType = "Depowder",
            MachineModel = "Incineris",
            Department = "Post-Process",
            BuildPlateCapacity = 1,
            HourlyRate = 85.00m,
            IsActive = true,
            IsAvailableForScheduling = true,
            Priority = 2,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var heatTreatMachine = new Machine
        {
            MachineId = "HT1",
            Name = "Vacuum Furnace #1",
            MachineType = "Heat-Treat",
            MachineModel = "TAV H4",
            Department = "Post-Process",
            BuildPlateCapacity = 1,
            HourlyRate = 75.00m,
            IsActive = true,
            IsAvailableForScheduling = true,
            Priority = 3,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        db.Machines.AddRange(slsMachine, depowderMachine, heatTreatMachine);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Production Stages (catalog entries)
        // ══════════════════════════════════════════════════════════
        var printStage = new ProductionStage
        {
            Name = "SLS/LPBF Printing",
            StageSlug = "sls-printing",
            Department = "SLS",
            DefaultDurationHours = 8.0,
            HasBuiltInPage = true,
            DefaultHourlyRate = 225.00m,
            DefaultSetupMinutes = 60,
            DisplayOrder = 1,
            StageIcon = "🖨️",
            StageColor = "#3B82F6",
            RequiresMachineAssignment = true,
            DefaultMachineId = slsMachine.MachineId,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var depowderStage = new ProductionStage
        {
            Name = "Depowdering",
            StageSlug = "depowdering",
            Department = "Post-Process",
            DefaultDurationHours = 1.0,
            HasBuiltInPage = true,
            DefaultHourlyRate = 55.00m,
            DefaultSetupMinutes = 10,
            DisplayOrder = 2,
            StageIcon = "💨",
            StageColor = "#F59E0B",
            RequiresMachineAssignment = true,
            DefaultMachineId = depowderMachine.MachineId,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var heatTreatStage = new ProductionStage
        {
            Name = "Heat Treatment",
            StageSlug = "heat-treatment",
            Department = "Post-Process",
            DefaultDurationHours = 4.0,
            HasBuiltInPage = true,
            DefaultHourlyRate = 65.00m,
            DefaultSetupMinutes = 20,
            DisplayOrder = 3,
            StageIcon = "🔥",
            StageColor = "#EF4444",
            RequiresMachineAssignment = true,
            DefaultMachineId = heatTreatMachine.MachineId,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        db.ProductionStages.AddRange(printStage, depowderStage, heatTreatStage);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Material
        // ══════════════════════════════════════════════════════════
        var material = new Material
        {
            Name = "Ti-6Al-4V Grade 5",
            Category = "Titanium",
            Density = 4.43,
            CostPerKg = 185.00m,
            IsActive = true
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Part: Aerospace Bracket
        // ══════════════════════════════════════════════════════════
        var part = new Part
        {
            PartNumber = "AERO-BRACKET-001",
            Name = "Aerospace Mounting Bracket",
            Description = "Titanium bracket for aircraft engine mounting",
            Material = "Ti-6Al-4V Grade 5",
            MaterialId = material.Id,
            CustomerPartNumber = "CUST-BR-2025",
            DrawingNumber = "DWG-BR-001",
            Revision = "A",
            RevisionDate = DateTime.UtcNow.AddDays(-30),
            EstimatedWeightKg = 0.45,
            IsDefensePart = true,
            ItarClassification = ItarClassification.ITAR,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.Parts.Add(part);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Manufacturing Process
        // ══════════════════════════════════════════════════════════
        var process = new ManufacturingProcess
        {
            PartId = part.Id,
            Name = "AERO-BRACKET SLS Process",
            Description = "Full additive manufacturing process for aerospace bracket",
            DefaultBatchCapacity = 60,
            IsActive = true,
            Version = 1,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ManufacturingProcesses.Add(process);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Process Stages (linked to manufacturing process)
        // ══════════════════════════════════════════════════════════
        var processPrintStage = new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = printStage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Build,
            DurationFromBuildConfig = true,  // Duration comes from slicer/build program
            SetupDurationMode = DurationMode.PerBuild,
            SetupTimeMinutes = 45,
            RunDurationMode = DurationMode.PerBuild,
            AssignedMachineId = slsMachine.Id,
            RequiresSpecificMachine = false,
            IsRequired = true,
            IsBlocking = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var processDepowderStage = new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = depowderStage.Id,
            ExecutionOrder = 2,
            ProcessingLevel = ProcessingLevel.Build,
            DurationFromBuildConfig = false,
            SetupDurationMode = DurationMode.PerBuild,
            SetupTimeMinutes = 15,
            RunDurationMode = DurationMode.PerBuild,
            RunTimeMinutes = 72,  // 1.2 hours for depowdering
            AssignedMachineId = depowderMachine.Id,
            IsRequired = true,
            IsBlocking = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var processHeatTreatStage = new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = heatTreatStage.Id,
            ExecutionOrder = 3,
            ProcessingLevel = ProcessingLevel.Build,
            DurationFromBuildConfig = false,
            SetupDurationMode = DurationMode.PerBuild,
            SetupTimeMinutes = 30,
            RunDurationMode = DurationMode.PerBuild,
            RunTimeMinutes = 240,  // 4 hours heat treatment cycle
            AssignedMachineId = heatTreatMachine.Id,
            IsRequired = true,
            IsBlocking = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        db.ProcessStages.AddRange(processPrintStage, processDepowderStage, processHeatTreatStage);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Machine Program (BuildPlate type for SLS printing)
        // ══════════════════════════════════════════════════════════
        var printProgram = new MachineProgram
        {
            PartId = part.Id,
            MachineId = slsMachine.Id,
            ProcessStageId = processPrintStage.Id,
            ProgramNumber = "BP-AERO-001",
            Name = "Bracket Build Plate - 24 parts",
            ProgramType = ProgramType.BuildPlate,
            Description = "Optimized build layout for 24 aerospace brackets",
            Version = 1,
            Status = ProgramStatus.Active,
            // BuildPlate-specific fields
            EstimatedPrintHours = 18.5,  // From slicer output
            BuildHeightMm = 85.0,
            // EMA learning data (from previous runs)
            ActualAverageDurationMinutes = 1095,  // ~18.25 hours learned
            ActualSampleCount = 3,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.MachinePrograms.Add(printProgram);
        await db.SaveChangesAsync();

        // Link process stage to program
        processPrintStage.MachineProgramId = printProgram.Id;
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Work Order
        // ══════════════════════════════════════════════════════════
        var workOrder = new WorkOrder
        {
            OrderNumber = "WO-2025-001",
            CustomerName = "Aerospace Dynamics Inc.",
            CustomerPO = "PO-ADI-2025-0847",
            CustomerEmail = "orders@aerospacedynamics.com",
            OrderDate = DateTime.UtcNow.AddDays(-5),
            DueDate = DateTime.UtcNow.AddDays(21),
            ShipByDate = DateTime.UtcNow.AddDays(19),
            Status = WorkOrderStatus.InProgress,
            Priority = JobPriority.High,
            IsDefenseContract = true,
            ContractNumber = "FA8622-25-C-1234",
            ContractLineItem = "CLIN-001",
            Notes = "Priority aerospace contract - expedite",
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync();

        var woLine = new WorkOrderLine
        {
            WorkOrderId = workOrder.Id,
            PartId = part.Id,
            Quantity = 48,  // 2 full build plates
            Status = WorkOrderStatus.InProgress
        };
        db.Set<WorkOrderLine>().Add(woLine);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Operating Shift (enables scheduling)
        // ══════════════════════════════════════════════════════════
        var shift = new OperatingShift
        {
            Name = "24/7 Operations",
            StartTime = TimeSpan.Zero,
            EndTime = TimeSpan.FromHours(24),
            DaysOfWeek = "Mon,Tue,Wed,Thu,Fri,Sat,Sun",
            IsActive = true
        };
        db.OperatingShifts.Add(shift);
        await db.SaveChangesAsync();

        return new SlsScenarioResult(
            SlsMachine: slsMachine,
            DepowderMachine: depowderMachine,
            HeatTreatMachine: heatTreatMachine,
            Part: part,
            Process: process,
            PrintStage: processPrintStage,
            DepowderStage: processDepowderStage,
            HeatTreatStage: processHeatTreatStage,
            PrintProgram: printProgram,
            WorkOrder: workOrder,
            WorkOrderLine: woLine
        );
    }

    /// <summary>
    /// Seeds a complete CNC machining scenario with:
    /// - Haas VF-2 CNC mill with Standard programs
    /// - Aluminum machined part with manufacturing process
    /// - Standard program with setup/run/cycle times
    /// - EMA learning data from previous runs
    /// </summary>
    public static async Task<CncScenarioResult> SeedCncScenarioAsync(TenantDbContext db)
    {
        // ══════════════════════════════════════════════════════════
        // Machines
        // ══════════════════════════════════════════════════════════
        var cncMachine = new Machine
        {
            MachineId = "CNC1",
            Name = "Haas VF-2",
            MachineType = "CNC",
            MachineModel = "Haas VF-2",
            Department = "Machining",
            HourlyRate = 95.00m,
            IsActive = true,
            IsAvailableForScheduling = true,
            Priority = 4,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var cncMachine2 = new Machine
        {
            MachineId = "CNC2",
            Name = "Haas VF-2SS #2",
            MachineType = "CNC",
            MachineModel = "Haas VF-2SS",
            Department = "Machining",
            HourlyRate = 95.00m,
            IsActive = true,
            IsAvailableForScheduling = true,
            Priority = 4,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var qcStation = new Machine
        {
            MachineId = "QC1",
            Name = "QC Inspection Station",
            MachineType = "QC",
            Department = "Quality",
            HourlyRate = 75.00m,
            IsActive = true,
            IsAvailableForScheduling = true,
            Priority = 8,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        db.Machines.AddRange(cncMachine, cncMachine2, qcStation);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Production Stages
        // ══════════════════════════════════════════════════════════
        var cncStage = new ProductionStage
        {
            Name = "CNC Machining",
            StageSlug = "cnc-machining",
            Department = "Machining",
            DefaultDurationHours = 0.5,
            HasBuiltInPage = true,
            DefaultHourlyRate = 95.00m,
            DefaultSetupMinutes = 30,
            DisplayOrder = 5,
            StageIcon = "⚙️",
            StageColor = "#06B6D4",
            RequiresMachineAssignment = true,
            DefaultMachineId = cncMachine.MachineId,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var qcStage = new ProductionStage
        {
            Name = "Quality Control",
            StageSlug = "qc",
            Department = "Quality",
            DefaultDurationHours = 0.083,
            HasBuiltInPage = true,
            DefaultHourlyRate = 75.00m,
            DefaultSetupMinutes = 15,
            DisplayOrder = 8,
            StageIcon = "✅",
            StageColor = "#14B8A6",
            RequiresQualityCheck = true,
            RequiresMachineAssignment = true,
            DefaultMachineId = qcStation.MachineId,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        db.ProductionStages.AddRange(cncStage, qcStage);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Material
        // ══════════════════════════════════════════════════════════
        var material = new Material
        {
            Name = "Aluminum 6061-T6",
            Category = "Aluminum",
            Density = 2.70,
            CostPerKg = 8.50m,
            IsActive = true
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Part: CNC Machined Housing
        // ══════════════════════════════════════════════════════════
        var part = new Part
        {
            PartNumber = "HOUSING-AL-001",
            Name = "Precision Aluminum Housing",
            Description = "CNC machined aluminum housing for electronics enclosure",
            Material = "Aluminum 6061-T6",
            MaterialId = material.Id,
            CustomerPartNumber = "CUST-HSG-001",
            DrawingNumber = "DWG-HSG-001",
            Revision = "B",
            RevisionDate = DateTime.UtcNow.AddDays(-60),
            EstimatedWeightKg = 0.82,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.Parts.Add(part);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Manufacturing Process
        // ══════════════════════════════════════════════════════════
        var process = new ManufacturingProcess
        {
            PartId = part.Id,
            Name = "Housing CNC Process",
            Description = "CNC machining process for precision aluminum housing",
            DefaultBatchCapacity = 24,
            IsActive = true,
            Version = 1,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ManufacturingProcesses.Add(process);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Process Stages
        // ══════════════════════════════════════════════════════════
        var processCncStage = new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = cncStage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Part,
            DurationFromBuildConfig = false,
            SetupDurationMode = DurationMode.PerBatch,
            SetupTimeMinutes = 25,
            RunDurationMode = DurationMode.PerPart,
            RunTimeMinutes = 18,  // 18 min per part
            AssignedMachineId = cncMachine.Id,
            PreferredMachineIds = $"{cncMachine.Id},{cncMachine2.Id}",
            IsRequired = true,
            IsBlocking = true,
            ProgramSetupRequired = false,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var processQcStage = new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = qcStage.Id,
            ExecutionOrder = 2,
            ProcessingLevel = ProcessingLevel.Batch,
            DurationFromBuildConfig = false,
            SetupDurationMode = DurationMode.PerBatch,
            SetupTimeMinutes = 10,
            RunDurationMode = DurationMode.PerPart,
            RunTimeMinutes = 5,  // 5 min per part inspection
            AssignedMachineId = qcStation.Id,
            RequiresQualityCheck = true,
            IsRequired = true,
            IsBlocking = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        db.ProcessStages.AddRange(processCncStage, processQcStage);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Machine Program (Standard type for CNC)
        // ══════════════════════════════════════════════════════════
        var cncProgram = new MachineProgram
        {
            PartId = part.Id,
            MachineId = cncMachine.Id,
            ProcessStageId = processCncStage.Id,
            ProgramNumber = "O1001",
            Name = "Housing Op10 - Rough/Finish",
            ProgramType = ProgramType.Standard,
            Description = "Complete CNC program for aluminum housing",
            Version = 3,
            Status = ProgramStatus.Active,
            // Standard program timing
            SetupTimeMinutes = 22,
            RunTimeMinutes = 16,
            CycleTimeMinutes = 17,  // Includes load/unload
            // EMA learning data
            ActualAverageDurationMinutes = 16.8,  // Learned from 12 runs
            ActualSampleCount = 12,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.MachinePrograms.Add(cncProgram);
        await db.SaveChangesAsync();

        // Link process stage to program
        processCncStage.MachineProgramId = cncProgram.Id;
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Work Order
        // ══════════════════════════════════════════════════════════
        var workOrder = new WorkOrder
        {
            OrderNumber = "WO-2025-002",
            CustomerName = "TechCorp Electronics",
            CustomerPO = "TCE-2025-0392",
            CustomerEmail = "purchasing@techcorp.com",
            OrderDate = DateTime.UtcNow.AddDays(-3),
            DueDate = DateTime.UtcNow.AddDays(14),
            Status = WorkOrderStatus.InProgress,
            Priority = JobPriority.Normal,
            Notes = "Standard production order",
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync();

        var woLine = new WorkOrderLine
        {
            WorkOrderId = workOrder.Id,
            PartId = part.Id,
            Quantity = 50,
            Status = WorkOrderStatus.InProgress
        };
        db.Set<WorkOrderLine>().Add(woLine);
        await db.SaveChangesAsync();

        // ══════════════════════════════════════════════════════════
        // Operating Shift
        // ══════════════════════════════════════════════════════════
        if (!db.OperatingShifts.Any())
        {
            var shift = new OperatingShift
            {
                Name = "Day Shift",
                StartTime = TimeSpan.FromHours(8),
                EndTime = TimeSpan.FromHours(16),
                DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
                IsActive = true
            };
            db.OperatingShifts.Add(shift);
            await db.SaveChangesAsync();
        }

        return new CncScenarioResult(
            CncMachine: cncMachine,
            CncMachine2: cncMachine2,
            QcStation: qcStation,
            Part: part,
            Process: process,
            CncStage: processCncStage,
            QcStage: processQcStage,
            CncProgram: cncProgram,
            WorkOrder: workOrder,
            WorkOrderLine: woLine
        );
    }

    /// <summary>
    /// Seeds a multi-part scenario with shared machines and multiple programs:
    /// - SLS + CNC workflow (additive then subtractive)
    /// - Multiple parts sharing the same machines
    /// - Programs with varying levels of EMA learning
    /// </summary>
    public static async Task<MultiPartScenarioResult> SeedMultiPartScenarioAsync(TenantDbContext db)
    {
        // Seed base machines
        var slsResult = await SeedSlsScenarioAsync(db);
        var cncResult = await SeedCncScenarioAsync(db);

        // Add a second SLS part that shares the same machines
        var part2 = new Part
        {
            PartNumber = "AERO-CLIP-002",
            Name = "Aerospace Retention Clip",
            Description = "Small titanium clip for harness retention",
            Material = "Ti-6Al-4V Grade 5",
            CustomerPartNumber = "CUST-CL-2025",
            DrawingNumber = "DWG-CL-002",
            Revision = "A",
            EstimatedWeightKg = 0.08,
            IsDefensePart = true,
            ItarClassification = ItarClassification.ITAR,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.Parts.Add(part2);
        await db.SaveChangesAsync();

        // Process for second part
        var process2 = new ManufacturingProcess
        {
            PartId = part2.Id,
            Name = "AERO-CLIP SLS Process",
            Description = "Additive process for small titanium clips",
            DefaultBatchCapacity = 200,
            IsActive = true,
            Version = 1,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ManufacturingProcesses.Add(process2);
        await db.SaveChangesAsync();

        // Get production stage IDs
        var printProdStage = db.ProductionStages.First(s => s.StageSlug == "sls-printing");
        var depowderProdStage = db.ProductionStages.First(s => s.StageSlug == "depowdering");

        var print2Stage = new ProcessStage
        {
            ManufacturingProcessId = process2.Id,
            ProductionStageId = printProdStage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Build,
            DurationFromBuildConfig = true,
            SetupDurationMode = DurationMode.PerBuild,
            SetupTimeMinutes = 45,
            AssignedMachineId = slsResult.SlsMachine.Id,
            IsRequired = true,
            IsBlocking = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        var depowder2Stage = new ProcessStage
        {
            ManufacturingProcessId = process2.Id,
            ProductionStageId = depowderProdStage.Id,
            ExecutionOrder = 2,
            ProcessingLevel = ProcessingLevel.Build,
            SetupTimeMinutes = 10,
            RunTimeMinutes = 45,  // Smaller parts = faster depowder
            AssignedMachineId = slsResult.DepowderMachine.Id,
            IsRequired = true,
            IsBlocking = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };

        db.ProcessStages.AddRange(print2Stage, depowder2Stage);
        await db.SaveChangesAsync();

        // Build plate program for clips (high density, shorter print)
        var clipProgram = new MachineProgram
        {
            PartId = part2.Id,
            MachineId = slsResult.SlsMachine.Id,
            ProcessStageId = print2Stage.Id,
            ProgramNumber = "BP-CLIP-001",
            Name = "Clip Build Plate - 180 parts",
            ProgramType = ProgramType.BuildPlate,
            Description = "High-density build layout for retention clips",
            Version = 1,
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 12.5,
            BuildHeightMm = 35.0,
            // No EMA data yet (new program)
            ActualAverageDurationMinutes = null,
            ActualSampleCount = 0,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.MachinePrograms.Add(clipProgram);
        await db.SaveChangesAsync();

        print2Stage.MachineProgramId = clipProgram.Id;
        await db.SaveChangesAsync();

        // Add work order for clips
        var woClip = new WorkOrder
        {
            OrderNumber = "WO-2025-003",
            CustomerName = "Aerospace Dynamics Inc.",
            CustomerPO = "PO-ADI-2025-0848",
            OrderDate = DateTime.UtcNow.AddDays(-2),
            DueDate = DateTime.UtcNow.AddDays(28),
            Status = WorkOrderStatus.InProgress,
            Priority = JobPriority.Normal,
            IsDefenseContract = true,
            ContractNumber = "FA8622-25-C-1234",
            ContractLineItem = "CLIN-002",
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.WorkOrders.Add(woClip);
        await db.SaveChangesAsync();

        var woClipLine = new WorkOrderLine
        {
            WorkOrderId = woClip.Id,
            PartId = part2.Id,
            Quantity = 360,  // 2 full build plates
            Status = WorkOrderStatus.InProgress
        };
        db.Set<WorkOrderLine>().Add(woClipLine);
        await db.SaveChangesAsync();

        return new MultiPartScenarioResult(
            SlsScenario: slsResult,
            CncScenario: cncResult,
            SecondPart: part2,
            SecondProcess: process2,
            SecondPrintStage: print2Stage,
            SecondDepowderStage: depowder2Stage,
            ClipProgram: clipProgram,
            ClipWorkOrder: woClip,
            ClipWorkOrderLine: woClipLine
        );
    }

    /// <summary>
    /// Seeds a scenario specifically for testing program duration priority chain:
    /// - Program with EMA data (should be preferred)
    /// - Program with only estimated times
    /// - ProcessStage with fallback times
    /// </summary>
    public static async Task<DurationPriorityScenarioResult> SeedDurationPriorityScenarioAsync(TenantDbContext db)
    {
        // Machine
        var machine = new Machine
        {
            MachineId = "TEST-1",
            Name = "Test Machine",
            MachineType = "CNC",
            IsActive = true,
            IsAvailableForScheduling = true,
            HourlyRate = 100.00m,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        // Production stage with default duration
        var prodStage = new ProductionStage
        {
            Name = "Test Stage",
            StageSlug = "test-stage",
            Department = "Test",
            DefaultDurationHours = 2.0,  // Fallback: 2 hours
            DefaultSetupMinutes = 30,
            DefaultHourlyRate = 100.00m,
            DisplayOrder = 1,
            RequiresMachineAssignment = true,
            DefaultMachineId = machine.MachineId,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ProductionStages.Add(prodStage);
        await db.SaveChangesAsync();

        // Part with EMA program
        var partWithEma = new Part
        {
            PartNumber = "EMA-TEST-001",
            Name = "Part with EMA Program",
            Material = "Steel",
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.Parts.Add(partWithEma);
        await db.SaveChangesAsync();

        var processEma = new ManufacturingProcess
        {
            PartId = partWithEma.Id,
            Name = "EMA Test Process",
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ManufacturingProcesses.Add(processEma);
        await db.SaveChangesAsync();

        var stageEma = new ProcessStage
        {
            ManufacturingProcessId = processEma.Id,
            ProductionStageId = prodStage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Part,
            SetupTimeMinutes = 20,  // Stage fallback: 20 min
            RunTimeMinutes = 45,    // Stage fallback: 45 min
            AssignedMachineId = machine.Id,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ProcessStages.Add(stageEma);
        await db.SaveChangesAsync();

        // Program with EMA data (should be used)
        var programWithEma = new MachineProgram
        {
            PartId = partWithEma.Id,
            MachineId = machine.Id,
            ProcessStageId = stageEma.Id,
            ProgramNumber = "EMA-001",
            Name = "Program with EMA",
            ProgramType = ProgramType.Standard,
            Version = 1,
            Status = ProgramStatus.Active,
            SetupTimeMinutes = 15,
            RunTimeMinutes = 35,
            CycleTimeMinutes = 38,
            // EMA: 32 min learned from 8 runs (should override RunTimeMinutes)
            ActualAverageDurationMinutes = 32,
            ActualSampleCount = 8,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.MachinePrograms.Add(programWithEma);
        await db.SaveChangesAsync();

        stageEma.MachineProgramId = programWithEma.Id;
        await db.SaveChangesAsync();

        // Part without EMA (uses program estimates)
        var partNoEma = new Part
        {
            PartNumber = "NO-EMA-001",
            Name = "Part without EMA",
            Material = "Steel",
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.Parts.Add(partNoEma);
        await db.SaveChangesAsync();

        var processNoEma = new ManufacturingProcess
        {
            PartId = partNoEma.Id,
            Name = "No-EMA Test Process",
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ManufacturingProcesses.Add(processNoEma);
        await db.SaveChangesAsync();

        var stageNoEma = new ProcessStage
        {
            ManufacturingProcessId = processNoEma.Id,
            ProductionStageId = prodStage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Part,
            SetupTimeMinutes = 20,
            RunTimeMinutes = 45,
            AssignedMachineId = machine.Id,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ProcessStages.Add(stageNoEma);
        await db.SaveChangesAsync();

        // Program without EMA (no ActualAverageDurationMinutes)
        var programNoEma = new MachineProgram
        {
            PartId = partNoEma.Id,
            MachineId = machine.Id,
            ProcessStageId = stageNoEma.Id,
            ProgramNumber = "NO-EMA-001",
            Name = "Program without EMA",
            ProgramType = ProgramType.Standard,
            Version = 1,
            Status = ProgramStatus.Active,
            SetupTimeMinutes = 18,
            RunTimeMinutes = 42,
            CycleTimeMinutes = 45,
            ActualAverageDurationMinutes = null,  // No EMA
            ActualSampleCount = 0,
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.MachinePrograms.Add(programNoEma);
        await db.SaveChangesAsync();

        stageNoEma.MachineProgramId = programNoEma.Id;
        await db.SaveChangesAsync();

        // Part with no program (uses stage defaults)
        var partNoProgram = new Part
        {
            PartNumber = "NO-PROG-001",
            Name = "Part without Program",
            Material = "Steel",
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.Parts.Add(partNoProgram);
        await db.SaveChangesAsync();

        var processNoProgram = new ManufacturingProcess
        {
            PartId = partNoProgram.Id,
            Name = "No-Program Test Process",
            IsActive = true,
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ManufacturingProcesses.Add(processNoProgram);
        await db.SaveChangesAsync();

        var stageNoProgram = new ProcessStage
        {
            ManufacturingProcessId = processNoProgram.Id,
            ProductionStageId = prodStage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Part,
            SetupTimeMinutes = 25,
            RunTimeMinutes = 50,  // Should be used (no program)
            AssignedMachineId = machine.Id,
            MachineProgramId = null,  // No program linked
            CreatedBy = "Seed",
            LastModifiedBy = "Seed"
        };
        db.ProcessStages.Add(stageNoProgram);
        await db.SaveChangesAsync();

        // Operating shift
        if (!db.OperatingShifts.Any())
        {
            db.OperatingShifts.Add(new OperatingShift
            {
                Name = "Day Shift",
                StartTime = TimeSpan.FromHours(8),
                EndTime = TimeSpan.FromHours(16),
                DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        return new DurationPriorityScenarioResult(
            Machine: machine,
            ProductionStage: prodStage,
            PartWithEma: partWithEma,
            ProcessWithEma: processEma,
            StageWithEma: stageEma,
            ProgramWithEma: programWithEma,
            PartNoEma: partNoEma,
            ProcessNoEma: processNoEma,
            StageNoEma: stageNoEma,
            ProgramNoEma: programNoEma,
            PartNoProgram: partNoProgram,
            ProcessNoProgram: processNoProgram,
            StageNoProgram: stageNoProgram
        );
    }
}

// ══════════════════════════════════════════════════════════
// Result Records
// ══════════════════════════════════════════════════════════

public record SlsScenarioResult(
    Machine SlsMachine,
    Machine DepowderMachine,
    Machine HeatTreatMachine,
    Part Part,
    ManufacturingProcess Process,
    ProcessStage PrintStage,
    ProcessStage DepowderStage,
    ProcessStage HeatTreatStage,
    MachineProgram PrintProgram,
    WorkOrder WorkOrder,
    WorkOrderLine WorkOrderLine
);

public record CncScenarioResult(
    Machine CncMachine,
    Machine CncMachine2,
    Machine QcStation,
    Part Part,
    ManufacturingProcess Process,
    ProcessStage CncStage,
    ProcessStage QcStage,
    MachineProgram CncProgram,
    WorkOrder WorkOrder,
    WorkOrderLine WorkOrderLine
);

public record MultiPartScenarioResult(
    SlsScenarioResult SlsScenario,
    CncScenarioResult CncScenario,
    Part SecondPart,
    ManufacturingProcess SecondProcess,
    ProcessStage SecondPrintStage,
    ProcessStage SecondDepowderStage,
    MachineProgram ClipProgram,
    WorkOrder ClipWorkOrder,
    WorkOrderLine ClipWorkOrderLine
);

public record DurationPriorityScenarioResult(
    Machine Machine,
    ProductionStage ProductionStage,
    Part PartWithEma,
    ManufacturingProcess ProcessWithEma,
    ProcessStage StageWithEma,
    MachineProgram ProgramWithEma,
    Part PartNoEma,
    ManufacturingProcess ProcessNoEma,
    ProcessStage StageNoEma,
    MachineProgram ProgramNoEma,
    Part PartNoProgram,
    ManufacturingProcess ProcessNoProgram,
    ProcessStage StageNoProgram
);
