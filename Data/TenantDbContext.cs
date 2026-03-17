using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Maintenance;

namespace Opcentrix_V3.Data;

public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options)
        : base(options)
    {
    }

    // Core Manufacturing
    public DbSet<Part> Parts { get; set; }
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

    // Production Tracking
    public DbSet<BuildJob> BuildJobs { get; set; }
    public DbSet<BuildJobPart> BuildJobParts { get; set; }
    public DbSet<StageExecution> StageExecutions { get; set; }
    public DbSet<DelayLog> DelayLogs { get; set; }

    // Build Planning
    public DbSet<BuildPackage> BuildPackages { get; set; }
    public DbSet<BuildPackagePart> BuildPackageParts { get; set; }
    public DbSet<BuildFileInfo> BuildFileInfos { get; set; }

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

    // Quoting
    public DbSet<QuoteRevision> QuoteRevisions { get; set; }
    public DbSet<RfqRequest> RfqRequests { get; set; }

    // Work Order Management
    public DbSet<WorkOrderComment> WorkOrderComments { get; set; }

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
        });

        // Machine
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasIndex(e => e.MachineId).IsUnique();
        });

        // Part
        modelBuilder.Entity<Part>(entity =>
        {
            entity.HasIndex(e => e.PartNumber);
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
        });

        // JobNote
        modelBuilder.Entity<JobNote>(entity =>
        {
            entity.HasOne(e => e.Job)
                .WithMany(e => e.JobNotes)
                .HasForeignKey(e => e.JobId);
        });

        // BuildJob
        modelBuilder.Entity<BuildJob>(entity =>
        {
            entity.HasKey(e => e.BuildId);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId);
        });

        // BuildJobPart
        modelBuilder.Entity<BuildJobPart>(entity =>
        {
            entity.HasOne(e => e.BuildJob)
                .WithMany(e => e.Parts)
                .HasForeignKey(e => e.BuildJobId)
                .HasPrincipalKey(e => e.BuildId);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);
        });

        // BuildPackage
        modelBuilder.Entity<BuildPackage>(entity =>
        {
            entity.HasOne(e => e.ScheduledJob)
                .WithMany()
                .HasForeignKey(e => e.ScheduledJobId);
        });

        // BuildPackagePart
        modelBuilder.Entity<BuildPackagePart>(entity =>
        {
            entity.HasOne(e => e.BuildPackage)
                .WithMany(e => e.Parts)
                .HasForeignKey(e => e.BuildPackageId);
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId);
            entity.HasOne(e => e.WorkOrderLine)
                .WithMany()
                .HasForeignKey(e => e.WorkOrderLineId);
        });

        // BuildFileInfo
        modelBuilder.Entity<BuildFileInfo>(entity =>
        {
            entity.HasOne(e => e.BuildPackage)
                .WithOne(e => e.BuildFileInfo)
                .HasForeignKey<BuildFileInfo>(e => e.BuildPackageId);
        });

        // PartInstance
        modelBuilder.Entity<PartInstance>(entity =>
        {
            entity.HasIndex(e => e.SerialNumber).IsUnique();
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
            entity.HasOne(e => e.BuildJob)
                .WithMany()
                .HasForeignKey(e => e.BuildJobId)
                .HasPrincipalKey(e => e.BuildId);
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
            entity.HasOne(e => e.BuildJob)
                .WithMany(e => e.Delays)
                .HasForeignKey(e => e.BuildJobId)
                .HasPrincipalKey(e => e.BuildId);
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
    }
}
