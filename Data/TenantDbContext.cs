using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vectrik.Models;
using Vectrik.Models.Maintenance;

namespace Vectrik.Data;

public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    // Core Manufacturing
    public DbSet<Part> Parts { get; set; }
    public DbSet<PartAdditiveBuildConfig> PartAdditiveBuildConfigs { get; set; }
    public DbSet<ManufacturingApproach> ManufacturingApproaches { get; set; }
    public DbSet<Job> Jobs { get; set; }
    public DbSet<ProductionStage> ProductionStages { get; set; }
    public DbSet<PartStageRequirement> PartStageRequirements { get; set; }

    // Work Orders & Quotes
    public DbSet<WorkOrder> WorkOrders { get; set; }
    public DbSet<WorkOrderLine> WorkOrderLines { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<QuoteLine> QuoteLines { get; set; }

    // Serial Tracking
    public DbSet<PartInstance> PartInstances { get; set; }
    public DbSet<PartInstanceStageLog> PartInstanceStageLogs { get; set; }

    // Scheduling
    public DbSet<Machine> Machines { get; set; }
    public DbSet<OperatingShift> OperatingShifts { get; set; }
    public DbSet<MachineShiftAssignment> MachineShiftAssignments { get; set; }
    public DbSet<UserShiftAssignment> UserShiftAssignments { get; set; }

    // Production Tracking
    public DbSet<StageExecution> StageExecutions { get; set; }
    public DbSet<DelayLog> DelayLogs { get; set; }

    // Quality
    public DbSet<QCInspection> QCInspections { get; set; }
    public DbSet<QCChecklistItem> QCChecklistItems { get; set; }

    // Notes & Logging
    public DbSet<JobNote> JobNotes { get; set; }

    // Infrastructure
    public DbSet<User> Users { get; set; }
    public DbSet<UserSettings> UserSettings { get; set; }
    public DbSet<Material> Materials { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }

    // Machine Integration
    public DbSet<MachineConnectionSettings> MachineConnectionSettings { get; set; }
    public DbSet<MachineStateRecord> MachineStateRecords { get; set; }

    // Maintenance
    public DbSet<MachineComponent> MachineComponents { get; set; }
    public DbSet<MaintenanceRule> MaintenanceRules { get; set; }
    public DbSet<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; }
    public DbSet<MaintenanceActionLog> MaintenanceActionLogs { get; set; }

    // Customization Foundation
    public DbSet<CustomFieldConfig> CustomFieldConfigs { get; set; }
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    public DbSet<WorkflowInstance> WorkflowInstances { get; set; }
    public DbSet<DocumentTemplate> DocumentTemplates { get; set; }

    // Parts / PDM
    public DbSet<PartDrawing> PartDrawings { get; set; }
    public DbSet<PartRevisionHistory> PartRevisionHistories { get; set; }
    public DbSet<PartNote> PartNotes { get; set; }
    public DbSet<PartBomItem> PartBomItems { get; set; }

    // Quoting
    public DbSet<QuoteRevision> QuoteRevisions { get; set; }
    public DbSet<RfqRequest> RfqRequests { get; set; }

    // Work Order Management
    public DbSet<WorkOrderComment> WorkOrderComments { get; set; }

    // Inventory Control (Module 06)
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<StockLocation> StockLocations { get; set; }
    public DbSet<InventoryLot> InventoryLots { get; set; }
    public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
    public DbSet<MaterialRequest> MaterialRequests { get; set; }

    // Quality Systems (Module 05)
    public DbSet<InspectionPlan> InspectionPlans { get; set; }
    public DbSet<InspectionPlanCharacteristic> InspectionPlanCharacteristics { get; set; }
    public DbSet<InspectionMeasurement> InspectionMeasurements { get; set; }
    public DbSet<NonConformanceReport> NonConformanceReports { get; set; }
    public DbSet<CorrectiveAction> CorrectiveActions { get; set; }
    public DbSet<SpcDataPoint> SpcDataPoints { get; set; }

    // Reporting & Analytics (Module 07)
    public DbSet<DashboardLayout> DashboardLayouts { get; set; }
    public DbSet<SavedReport> SavedReports { get; set; }

    // Visual Work Instructions (Module 03)
    public DbSet<WorkInstruction> WorkInstructions { get; set; }
    public DbSet<WorkInstructionStep> WorkInstructionSteps { get; set; }
    public DbSet<WorkInstructionMedia> WorkInstructionMedia { get; set; }
    public DbSet<WorkInstructionRevision> WorkInstructionRevisions { get; set; }
    public DbSet<OperatorFeedback> OperatorFeedback { get; set; }

    // Operator Roles (Phase 5)
    public DbSet<OperatorRole> OperatorRoles { get; set; }
    public DbSet<UserOperatorRole> UserOperatorRoles { get; set; }

    // External Operations (Phase 6)
    public DbSet<ExternalOperation> ExternalOperations { get; set; }

    // Build Templates (Scheduler Overhaul Phase A)
    public DbSet<BuildTemplate> BuildTemplates { get; set; }
    public DbSet<BuildTemplatePart> BuildTemplateParts { get; set; }
    public DbSet<BuildTemplateRevision> BuildTemplateRevisions { get; set; }

    // Certified Layouts (Quadrant & Half plate compositions)
    public DbSet<CertifiedLayout> CertifiedLayouts { get; set; }
    public DbSet<CertifiedLayoutRevision> CertifiedLayoutRevisions { get; set; }

    // Dev Issue Tracking
    public DbSet<DevIssue> DevIssues { get; set; }

    // Manufacturing Process Redesign
    public DbSet<ManufacturingProcess> ManufacturingProcesses { get; set; }
    public DbSet<ProcessStage> ProcessStages { get; set; }
    public DbSet<ProductionBatch> ProductionBatches { get; set; }
    public DbSet<BatchPartAssignment> BatchPartAssignments { get; set; }

    // Machine Programs
    public DbSet<MachineProgram> MachinePrograms { get; set; }
    public DbSet<MachineProgramFile> MachineProgramFiles { get; set; }
    public DbSet<ProgramToolingItem> ProgramToolingItems { get; set; }
    public DbSet<ProgramFeedback> ProgramFeedbacks { get; set; }
    public DbSet<MachineProgramAssignment> MachineProgramAssignments { get; set; }
    public DbSet<ProgramPart> ProgramParts { get; set; }
    public DbSet<ProgramRevision> ProgramRevisions { get; set; }

    // Operation Cost Profiles
    public DbSet<StageCostProfile> StageCostProfiles { get; set; }

    // Part Pricing
    public DbSet<PartPricing> PartPricings { get; set; }

    // Smart Pricing
    public DbSet<PartSignature> PartSignatures { get; set; }

    // Customer Pricing
    public DbSet<Customer> Customers { get; set; }
    public DbSet<CustomerPricingRule> CustomerPricingRules { get; set; }
    public DbSet<PricingContract> PricingContracts { get; set; }

    // Shipping
    public DbSet<Shipment> Shipments { get; set; }
    public DbSet<ShipmentLine> ShipmentLines { get; set; }

    // Setup Dispatch System
    public DbSet<SetupDispatch> SetupDispatches { get; set; }
    public DbSet<SetupHistory> SetupHistories { get; set; }
    public DbSet<OperatorSetupProfile> OperatorSetupProfiles { get; set; }
    public DbSet<DispatchConfiguration> DispatchConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasOne(e => e.Settings)
                .WithOne(e => e.User)
                .HasForeignKey<UserSettings>(e => e.UserId);
        });

        // ProductionStage
        modelBuilder.Entity<ProductionStage>(entity =>
        {
            entity.HasIndex(e => e.StageSlug).IsUnique();
            entity.HasOne(e => e.RequiredOperatorRole)
                .WithMany()
                .HasForeignKey(e => e.RequiredOperatorRoleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // OperatorRole
        modelBuilder.Entity<OperatorRole>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // UserOperatorRole
        modelBuilder.Entity<UserOperatorRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.OperatorRoleId });
            entity.HasOne(e => e.User)
                .WithMany(u => u.OperatorRoles)
                .HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.OperatorRole)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.OperatorRoleId);
        });

        // Machine
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasIndex(e => e.MachineId).IsUnique();
        });

        // BuildTemplateRevision
        modelBuilder.Entity<BuildTemplateRevision>(entity =>
        {
            entity.HasOne(e => e.BuildTemplate)
                .WithMany(e => e.Revisions)
                .HasForeignKey(e => e.BuildTemplateId);
        });

        // Part
        modelBuilder.Entity<Part>(entity =>
        {
            entity.HasIndex(e => e.PartNumber);
            entity.HasOne(p => p.MaterialEntity)
                .WithMany()
                .HasForeignKey(p => p.MaterialId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(p => p.ManufacturingApproach)
                .WithMany()
                .HasForeignKey(p => p.ManufacturingApproachId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ManufacturingApproach
        modelBuilder.Entity<ManufacturingApproach>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // PartAdditiveBuildConfig
        modelBuilder.Entity<PartAdditiveBuildConfig>(entity =>
        {
            entity.HasIndex(e => e.PartId).IsUnique();
            entity.HasOne(e => e.Part)
                .WithOne(e => e.AdditiveBuildConfig)
                .HasForeignKey<PartAdditiveBuildConfig>(e => e.PartId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PartDrawing
        modelBuilder.Entity<PartDrawing>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany(e => e.Drawings)
                .HasForeignKey(e => e.PartId);
        });

        // PartRevisionHistory
        modelBuilder.Entity<PartRevisionHistory>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany(e => e.RevisionHistory)
                .HasForeignKey(e => e.PartId);
        });

        // PartNote
        modelBuilder.Entity<PartNote>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany(e => e.Notes)
                .HasForeignKey(e => e.PartId);
        });

        // PartBomItem
        modelBuilder.Entity<PartBomItem>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany(e => e.BomItems)
                .HasForeignKey(e => e.PartId);
            entity.HasOne(e => e.InventoryItem)
                .WithMany()
                .HasForeignKey(e => e.InventoryItemId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ChildPart)
                .WithMany()
                .HasForeignKey(e => e.ChildPartId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PartStageRequirement
        modelBuilder.Entity<PartStageRequirement>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany(e => e.StageRequirements)
                .HasForeignKey(e => e.PartId);
            entity.HasOne(e => e.ProductionStage)
                .WithMany(e => e.PartStageRequirements)
                .HasForeignKey(e => e.ProductionStageId);
        });

        // WorkOrder
        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasOne(e => e.Quote)
                .WithOne()
                .HasForeignKey<WorkOrder>(e => e.QuoteId);
            entity.HasOne(e => e.WorkflowInstance)
                .WithMany()
                .HasForeignKey(e => e.WorkflowInstanceId);
        });

        // WorkOrderComment
        modelBuilder.Entity<WorkOrderComment>(entity =>
        {
            entity.HasOne(e => e.WorkOrder)
                .WithMany(e => e.Comments)
                .HasForeignKey(e => e.WorkOrderId);
            entity.HasOne(e => e.AuthorUser)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId);
            entity.HasOne(e => e.ParentComment)
                .WithMany(e => e.Replies)
                .HasForeignKey(e => e.ParentCommentId);
        });

        // WorkOrderLine
        modelBuilder.Entity<WorkOrderLine>(entity =>
        {
            entity.HasOne(e => e.WorkOrder)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.WorkOrderId);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);
        });

        // Quote
        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasIndex(e => e.QuoteNumber).IsUnique();
        });

        // QuoteLine
        modelBuilder.Entity<QuoteLine>(entity =>
        {
            entity.HasOne(e => e.Quote)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.QuoteId);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);
        });

        // QuoteRevision
        modelBuilder.Entity<QuoteRevision>(entity =>
        {
            entity.HasOne(e => e.Quote)
                .WithMany(e => e.Revisions)
                .HasForeignKey(e => e.QuoteId);
        });

        // RfqRequest
        modelBuilder.Entity<RfqRequest>(entity =>
        {
            entity.HasOne(e => e.ConvertedQuote)
                .WithMany()
                .HasForeignKey(e => e.ConvertedQuoteId);
        });

        // Job
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany(e => e.Jobs)
                .HasForeignKey(e => e.PartId);
            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.PredecessorJob)
                .WithMany()
                .HasForeignKey(e => e.PredecessorJobId);
            entity.HasOne(e => e.OperatorUser)
                .WithMany()
                .HasForeignKey(e => e.OperatorUserId);
            entity.HasOne(e => e.WorkOrderLine)
                .WithMany(e => e.Jobs)
                .HasForeignKey(e => e.WorkOrderLineId);
        });

        // StageExecution
        modelBuilder.Entity<StageExecution>(entity =>
        {
            entity.HasOne(e => e.Job)
                .WithMany(e => e.Stages)
                .HasForeignKey(e => e.JobId);
            entity.HasOne(e => e.ProductionStage)
                .WithMany(e => e.StageExecutions)
                .HasForeignKey(e => e.ProductionStageId);
            entity.HasOne(e => e.Operator)
                .WithMany()
                .HasForeignKey(e => e.OperatorUserId);
            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId);
            entity.HasMany(e => e.DelayLogs)
                .WithOne(e => e.StageExecution)
                .HasForeignKey(e => e.StageExecutionId);
            entity.HasOne(e => e.ExternalOperation)
                .WithOne(e => e.StageExecution)
                .HasForeignKey<ExternalOperation>(e => e.StageExecutionId);
        });

        // JobNote
        modelBuilder.Entity<JobNote>(entity =>
        {
            entity.HasOne(e => e.Job)
                .WithMany(e => e.JobNotes)
                .HasForeignKey(e => e.JobId);
        });

        // PartInstance
        modelBuilder.Entity<PartInstance>(entity =>
        {
            entity.HasIndex(e => e.SerialNumber)
                .IsUnique()
                .HasFilter("\"SerialNumber\" IS NOT NULL");
            entity.HasIndex(e => e.TemporaryTrackingId);
            entity.HasOne(e => e.WorkOrderLine)
                .WithMany(e => e.PartInstances)
                .HasForeignKey(e => e.WorkOrderLineId);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);
            entity.HasOne(e => e.CurrentStage)
                .WithMany()
                .HasForeignKey(e => e.CurrentStageId);
        });

        // PartInstanceStageLog
        modelBuilder.Entity<PartInstanceStageLog>(entity =>
        {
            entity.HasOne(e => e.PartInstance)
                .WithMany(e => e.StageLogs)
                .HasForeignKey(e => e.PartInstanceId);
            entity.HasOne(e => e.ProductionStage)
                .WithMany()
                .HasForeignKey(e => e.ProductionStageId);
        });

        // QCInspection
        modelBuilder.Entity<QCInspection>(entity =>
        {
            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);
            entity.HasOne(e => e.PartInstance)
                .WithMany(e => e.Inspections)
                .HasForeignKey(e => e.PartInstanceId);
            entity.HasOne(e => e.Inspector)
                .WithMany()
                .HasForeignKey(e => e.InspectorUserId);
        });

        // QCChecklistItem
        modelBuilder.Entity<QCChecklistItem>(entity =>
        {
            entity.HasOne(e => e.QCInspection)
                .WithMany(e => e.ChecklistItems)
                .HasForeignKey(e => e.QCInspectionId);
        });

        // DelayLog
        modelBuilder.Entity<DelayLog>(entity =>
        {
            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId);
        });

        // SystemSetting
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // MachineConnectionSettings
        modelBuilder.Entity<MachineConnectionSettings>(entity =>
        {
            entity.HasIndex(e => e.MachineId).IsUnique();
        });

        // MachineComponent
        modelBuilder.Entity<MachineComponent>(entity =>
        {
            entity.HasOne(e => e.Machine)
                .WithMany(e => e.Components)
                .HasForeignKey(e => e.MachineId)
                .HasPrincipalKey(e => e.MachineId);
        });

        // MaintenanceRule
        modelBuilder.Entity<MaintenanceRule>(entity =>
        {
            entity.HasOne(e => e.MachineComponent)
                .WithMany(e => e.MaintenanceRules)
                .HasForeignKey(e => e.MachineComponentId);
        });

        // MaintenanceWorkOrder
        modelBuilder.Entity<MaintenanceWorkOrder>(entity =>
        {
            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .HasPrincipalKey(e => e.MachineId);
            entity.HasOne(e => e.MachineComponent)
                .WithMany()
                .HasForeignKey(e => e.MachineComponentId);
            entity.HasOne(e => e.MaintenanceRule)
                .WithMany()
                .HasForeignKey(e => e.MaintenanceRuleId);
            entity.HasOne(e => e.AssignedTechnician)
                .WithMany()
                .HasForeignKey(e => e.AssignedTechnicianUserId);
        });

        // MaintenanceActionLog
        modelBuilder.Entity<MaintenanceActionLog>(entity =>
        {
            entity.HasOne(e => e.MaintenanceRule)
                .WithMany()
                .HasForeignKey(e => e.MaintenanceRuleId);
        });

        // CustomFieldConfig
        modelBuilder.Entity<CustomFieldConfig>(entity =>
        {
            entity.HasIndex(e => e.EntityType).IsUnique();
        });

        // WorkflowDefinition
        modelBuilder.Entity<WorkflowDefinition>(entity =>
        {
            entity.HasMany(e => e.Steps)
                .WithOne(e => e.WorkflowDefinition)
                .HasForeignKey(e => e.WorkflowDefinitionId);
        });

        // WorkflowInstance
        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionId);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        // DocumentTemplate
        modelBuilder.Entity<DocumentTemplate>(entity =>
        {
            entity.HasIndex(e => new { e.EntityType, e.IsDefault });
        });

        // InventoryItem
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasIndex(e => e.ItemNumber).IsUnique();
            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId);
        });

        // StockLocation
        modelBuilder.Entity<StockLocation>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // InventoryLot
        modelBuilder.Entity<InventoryLot>(entity =>
        {
            entity.HasOne(e => e.InventoryItem)
                .WithMany(e => e.Lots)
                .HasForeignKey(e => e.InventoryItemId);
            entity.HasOne(e => e.Location)
                .WithMany()
                .HasForeignKey(e => e.StockLocationId);
        });

        // InventoryTransaction
        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.HasOne(e => e.InventoryItem)
                .WithMany(e => e.Transactions)
                .HasForeignKey(e => e.InventoryItemId);
            entity.HasOne(e => e.Lot)
                .WithMany()
                .HasForeignKey(e => e.LotId);
            entity.HasIndex(e => e.TransactedAt);
        });

        // MaterialRequest
        modelBuilder.Entity<MaterialRequest>(entity =>
        {
            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId);
            entity.HasOne(e => e.InventoryItem)
                .WithMany()
                .HasForeignKey(e => e.InventoryItemId);
            entity.HasOne(e => e.IssuedFromLot)
                .WithMany()
                .HasForeignKey(e => e.LotId);
        });

        // InspectionPlan
        modelBuilder.Entity<InspectionPlan>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany(e => e.InspectionPlans)
                .HasForeignKey(e => e.PartId);
        });

        // InspectionPlanCharacteristic
        modelBuilder.Entity<InspectionPlanCharacteristic>(entity =>
        {
            entity.HasOne(e => e.InspectionPlan)
                .WithMany(e => e.Characteristics)
                .HasForeignKey(e => e.InspectionPlanId);
        });

        // InspectionMeasurement
        modelBuilder.Entity<InspectionMeasurement>(entity =>
        {
            entity.HasOne(e => e.Inspection)
                .WithMany(e => e.Measurements)
                .HasForeignKey(e => e.QcInspectionId);
        });

        // NonConformanceReport
        modelBuilder.Entity<NonConformanceReport>(entity =>
        {
            entity.HasIndex(e => e.NcrNumber).IsUnique();
            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);
            entity.HasOne(e => e.PartInstance)
                .WithMany()
                .HasForeignKey(e => e.PartInstanceId);
            entity.HasOne(e => e.CorrectiveAction)
                .WithMany()
                .HasForeignKey(e => e.CorrectiveActionId);
        });

        // CorrectiveAction
        modelBuilder.Entity<CorrectiveAction>(entity =>
        {
            entity.HasIndex(e => e.CapaNumber).IsUnique();
        });

        // SpcDataPoint
        modelBuilder.Entity<SpcDataPoint>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);
            entity.HasIndex(e => new { e.PartId, e.CharacteristicName });
        });

        // WorkInstruction
        modelBuilder.Entity<WorkInstruction>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ProductionStage)
                .WithMany()
                .HasForeignKey(e => e.ProductionStageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Steps)
                .WithOne(s => s.WorkInstruction)
                .HasForeignKey(s => s.WorkInstructionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Revisions)
                .WithOne(r => r.WorkInstruction)
                .HasForeignKey(r => r.WorkInstructionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.PartId, e.ProductionStageId }).IsUnique();
        });

        // WorkInstructionStep
        modelBuilder.Entity<WorkInstructionStep>(entity =>
        {
            entity.HasMany(e => e.Media)
                .WithOne(m => m.Step)
                .HasForeignKey(m => m.WorkInstructionStepId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Feedback)
                .WithOne(f => f.Step)
                .HasForeignKey(f => f.WorkInstructionStepId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BuildTemplate
        modelBuilder.Entity<BuildTemplate>(entity =>
        {
            entity.HasMany(e => e.Parts)
                .WithOne(e => e.BuildTemplate)
                .HasForeignKey(e => e.BuildTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Status);
        });

        // BuildTemplatePart
        modelBuilder.Entity<BuildTemplatePart>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);

            entity.HasIndex(e => new { e.BuildTemplateId, e.PartId });
        });

        // ── Certified Layouts ────────────────────────────────────

        modelBuilder.Entity<CertifiedLayout>(entity =>
        {
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);

            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.PartId);
        });

        modelBuilder.Entity<CertifiedLayoutRevision>(entity =>
        {
            entity.HasOne(e => e.CertifiedLayout)
                .WithMany(e => e.Revisions)
                .HasForeignKey(e => e.CertifiedLayoutId);
        });

        // ProgramPart → CertifiedLayout (optional FK)
        modelBuilder.Entity<ProgramPart>(entity =>
        {
            entity.HasOne(e => e.CertifiedLayout)
                .WithMany()
                .HasForeignKey(e => e.CertifiedLayoutId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Manufacturing Process Redesign ───────────────────────

        // ManufacturingProcess (1:1 with Part)
        modelBuilder.Entity<ManufacturingProcess>(entity =>
        {
            entity.HasIndex(e => e.PartId).IsUnique();
            entity.HasOne(e => e.Part)
                .WithOne(e => e.ManufacturingProcess)
                .HasForeignKey<ManufacturingProcess>(e => e.PartId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ManufacturingApproach)
                .WithMany()
                .HasForeignKey(e => e.ManufacturingApproachId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.PlateReleaseStage)
                .WithMany()
                .HasForeignKey(e => e.PlateReleaseStageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ProcessStage (many per ManufacturingProcess, references ProductionStage catalog)
        modelBuilder.Entity<ProcessStage>(entity =>
        {
            entity.HasOne(e => e.ManufacturingProcess)
                .WithMany(e => e.Stages)
                .HasForeignKey(e => e.ManufacturingProcessId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ProductionStage)
                .WithMany()
                .HasForeignKey(e => e.ProductionStageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssignedMachine)
                .WithMany()
                .HasForeignKey(e => e.AssignedMachineId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.ManufacturingProcessId, e.ExecutionOrder });
        });

        // ProductionBatch
        modelBuilder.Entity<ProductionBatch>(entity =>
        {
            entity.HasIndex(e => e.BatchNumber).IsUnique();
            entity.HasOne(e => e.CurrentProcessStage)
                .WithMany()
                .HasForeignKey(e => e.CurrentProcessStageId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.AssignedMachine)
                .WithMany()
                .HasForeignKey(e => e.AssignedMachineId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.StageExecution)
                .WithMany()
                .HasForeignKey(e => e.StageExecutionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // BatchPartAssignment (immutable history)
        modelBuilder.Entity<BatchPartAssignment>(entity =>
        {
            entity.HasIndex(e => new { e.PartInstanceId, e.Timestamp });
            entity.HasOne(e => e.ProductionBatch)
                .WithMany(e => e.PartAssignments)
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PartInstance)
                .WithMany()
                .HasForeignKey(e => e.PartInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AtProcessStage)
                .WithMany()
                .HasForeignKey(e => e.AtProcessStageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PartInstance — CurrentBatch FK
        modelBuilder.Entity<PartInstance>(entity =>
        {
            entity.HasOne(e => e.CurrentBatch)
                .WithMany()
                .HasForeignKey(e => e.CurrentBatchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Job — ProductionBatch and ManufacturingProcess FKs
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ManufacturingProcess)
                .WithMany()
                .HasForeignKey(e => e.ManufacturingProcessId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // StageExecution — ProductionBatch and ProcessStage FKs
        modelBuilder.Entity<StageExecution>(entity =>
        {
            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ProcessStage)
                .WithMany()
                .HasForeignKey(e => e.ProcessStageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Machine Programs ─────────────────────────────────────

        // MachineProgram
        modelBuilder.Entity<MachineProgram>(entity =>
        {
            entity.HasIndex(e => e.ProgramNumber);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ProcessStage)
                .WithMany()
                .HasForeignKey(e => e.ProcessStageId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.WorkInstruction)
                .WithMany()
                .HasForeignKey(e => e.WorkInstructionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.DepowderProgram)
                .WithMany()
                .HasForeignKey(e => e.DepowderProgramId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.EdmProgram)
                .WithMany()
                .HasForeignKey(e => e.EdmProgramId)
                .OnDelete(DeleteBehavior.SetNull);

            // Scheduling lifecycle navigation (replaces BuildPackage)
            entity.HasOne(e => e.PredecessorProgram)
                .WithMany()
                .HasForeignKey(e => e.PredecessorProgramId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.SourceProgram)
                .WithMany()
                .HasForeignKey(e => e.SourceProgramId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ScheduledJob)
                .WithMany()
                .HasForeignKey(e => e.ScheduledJobId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.SourceTemplate)
                .WithMany()
                .HasForeignKey(e => e.SourceTemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ScheduleStatus);
            entity.HasIndex(e => e.ScheduledDate);
        });

        // ProgramPart (multi-part nesting on build plate programs)
        modelBuilder.Entity<ProgramPart>(entity =>
        {
            entity.HasOne(e => e.MachineProgram)
                .WithMany(e => e.ProgramParts)
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.WorkOrderLine)
                .WithMany(e => e.ProgramParts)
                .HasForeignKey(e => e.WorkOrderLineId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.MachineProgramId, e.PartId });
        });

        // MachineProgramFile
        modelBuilder.Entity<MachineProgramFile>(entity =>
        {
            entity.HasOne(e => e.MachineProgram)
                .WithMany(e => e.Files)
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ProgramToolingItem
        modelBuilder.Entity<ProgramToolingItem>(entity =>
        {
            entity.HasOne(e => e.MachineProgram)
                .WithMany(e => e.ToolingItems)
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.MachineComponent)
                .WithMany()
                .HasForeignKey(e => e.MachineComponentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.MachineProgramId, e.ToolPosition });
        });

        // ProgramFeedback
        modelBuilder.Entity<ProgramFeedback>(entity =>
        {
            entity.HasOne(e => e.MachineProgram)
                .WithMany(e => e.Feedbacks)
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.StageExecution)
                .WithMany()
                .HasForeignKey(e => e.StageExecutionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.MachineProgramId, e.Status });
        });

        // MachineShiftAssignment (many-to-many join)
        modelBuilder.Entity<MachineShiftAssignment>(entity =>
        {
            entity.HasKey(e => new { e.MachineId, e.OperatingShiftId });
            entity.HasOne(e => e.Machine)
                .WithMany(e => e.ShiftAssignments)
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.OperatingShift)
                .WithMany(e => e.MachineAssignments)
                .HasForeignKey(e => e.OperatingShiftId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserShiftAssignment (many-to-many join)
        modelBuilder.Entity<UserShiftAssignment>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.OperatingShiftId });
            entity.HasOne(e => e.User)
                .WithMany(e => e.ShiftAssignments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.OperatingShift)
                .WithMany(e => e.UserAssignments)
                .HasForeignKey(e => e.OperatingShiftId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MachineProgramAssignment (many-to-many join)
        modelBuilder.Entity<MachineProgramAssignment>(entity =>
        {
            entity.HasOne(e => e.MachineProgram)
                .WithMany(e => e.MachineAssignments)
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Machine)
                .WithMany(e => e.ProgramAssignments)
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.MachineProgramId, e.MachineId }).IsUnique();
        });

        // ProgramRevision
        modelBuilder.Entity<ProgramRevision>(entity =>
        {
            entity.HasOne(e => e.MachineProgram)
                .WithMany()
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.MachineProgramId, e.RevisionNumber });
        });

        // ProcessStage — MachineProgram FK
        modelBuilder.Entity<ProcessStage>(entity =>
        {
            entity.HasOne(e => e.MachineProgram)
                .WithMany()
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // StageExecution — MachineProgram FK (records which program version was used)
        modelBuilder.Entity<StageExecution>(entity =>
        {
            entity.HasOne(e => e.MachineProgram)
                .WithMany()
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // StageCostProfile — 1:1 with ProductionStage
        modelBuilder.Entity<StageCostProfile>(entity =>
        {
            entity.HasIndex(e => e.ProductionStageId).IsUnique();
            entity.HasOne(e => e.ProductionStage)
                .WithOne()
                .HasForeignKey<StageCostProfile>(e => e.ProductionStageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PartPricing — 1:1 with Part
        modelBuilder.Entity<PartPricing>(entity =>
        {
            entity.HasIndex(e => e.PartId).IsUnique();
            entity.HasOne(e => e.Part)
                .WithOne(e => e.Pricing)
                .HasForeignKey<PartPricing>(e => e.PartId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PartSignature — 1:1 with Part (smart pricing feature vectors)
        modelBuilder.Entity<PartSignature>(entity =>
        {
            entity.HasIndex(e => e.PartId).IsUnique();
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Customer Pricing ─────────────────────────────────────

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Code).IsUnique().HasFilter("\"Code\" IS NOT NULL");
        });

        modelBuilder.Entity<CustomerPricingRule>(entity =>
        {
            entity.HasIndex(e => new { e.CustomerId, e.PartId });
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.PricingRules)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PricingContract>(entity =>
        {
            entity.HasIndex(e => e.ContractNumber).IsUnique();
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Contracts)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.PricingContract)
                .WithMany()
                .HasForeignKey(e => e.PricingContractId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ConvertedWorkOrder)
                .WithMany()
                .HasForeignKey(e => e.ConvertedWorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Setup Dispatch System ────────────────────────────────

        // Machine — CurrentProgram FK
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasOne(e => e.CurrentProgram)
                .WithMany()
                .HasForeignKey(e => e.CurrentProgramId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // SetupDispatch
        modelBuilder.Entity<SetupDispatch>(entity =>
        {
            entity.HasIndex(e => e.DispatchNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.MachineId, e.Status });

            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.MachineProgram)
                .WithMany()
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.StageExecution)
                .WithMany()
                .HasForeignKey(e => e.StageExecutionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.AssignedOperator)
                .WithMany()
                .HasForeignKey(e => e.AssignedOperatorId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.RequestedByUser)
                .WithMany()
                .HasForeignKey(e => e.RequestedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.WorkInstruction)
                .WithMany()
                .HasForeignKey(e => e.WorkInstructionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.PredecessorDispatch)
                .WithMany()
                .HasForeignKey(e => e.PredecessorDispatchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.TargetRole)
                .WithMany()
                .HasForeignKey(e => e.TargetRoleId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.TargetRoleId);
        });

        // StageExecution — SetupDispatch FK
        modelBuilder.Entity<StageExecution>(entity =>
        {
            entity.HasOne(e => e.SetupDispatch)
                .WithMany()
                .HasForeignKey(e => e.SetupDispatchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // SetupHistory (immutable ledger)
        modelBuilder.Entity<SetupHistory>(entity =>
        {
            entity.HasIndex(e => new { e.MachineId, e.CompletedAt });
            entity.HasIndex(e => new { e.OperatorUserId, e.MachineId });

            entity.HasOne(e => e.SetupDispatch)
                .WithMany()
                .HasForeignKey(e => e.SetupDispatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.MachineProgram)
                .WithMany()
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Operator)
                .WithMany()
                .HasForeignKey(e => e.OperatorUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Shift)
                .WithMany()
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // OperatorSetupProfile
        modelBuilder.Entity<OperatorSetupProfile>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.MachineId, e.MachineProgramId }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.MachineProgram)
                .WithMany()
                .HasForeignKey(e => e.MachineProgramId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // DispatchConfiguration
        modelBuilder.Entity<DispatchConfiguration>(entity =>
        {
            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ProductionStage)
                .WithMany()
                .HasForeignKey(e => e.ProductionStageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Shipping ─────────────────────────────────────────────

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasIndex(e => e.ShipmentNumber).IsUnique();
            entity.HasOne(e => e.WorkOrder)
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShipmentLine>(entity =>
        {
            entity.HasOne(e => e.Shipment)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.WorkOrderLine)
                .WithMany()
                .HasForeignKey(e => e.WorkOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
