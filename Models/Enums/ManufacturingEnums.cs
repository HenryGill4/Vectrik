namespace Vectrik.Models.Enums;

public enum JobStatus
{
    Draft, Scheduled, InProgress, Paused, Completed, Cancelled
}

public enum JobPriority
{
    Low, Normal, High, Rush, Emergency
}

public enum WorkOrderStatus
{
    Draft, Released, InProgress, Complete, Cancelled, OnHold
}

public enum QuoteStatus
{
    Draft, Sent, Accepted, Rejected, Expired
}

public enum StageExecutionStatus
{
    NotStarted, InProgress, Paused, Completed, Skipped, Failed
}

public enum PartInstanceStatus
{
    InProcess, Passed, Failed, Scrapped, Shipped
}

public enum MaintenanceTriggerType
{
    HoursRun, BuildsCompleted, DateInterval, CustomMeter
}

public enum MaintenanceSeverity
{
    Info, Warning, Critical
}

public enum MaintenanceWorkOrderType
{
    Preventive, Corrective, Emergency, Inspection, Calibration, Upgrade
}

public enum MaintenanceWorkOrderPriority
{
    Low, Normal, High, Critical, Emergency
}

public enum MaintenanceWorkOrderStatus
{
    Open, Assigned, InProgress, Completed, Cancelled, OnHold, WaitingForParts
}

public enum MachineStatus
{
    Idle, Running, Building, Preheating, Cooling, Maintenance, Error, Offline, Setup
}

public enum ItarClassification
{
    None, ITAR, EAR, CUI
}

public enum StageExecutionAction
{
    Start, Pause, Resume, Complete, Fail, Skip
}

public enum DelayCategory
{
    Material, Machine, Operator, Quality, WaitingForInspection, Other
}

// Inventory Control (Module 06)
public enum InventoryItemType
{
    RawMaterial, Consumable, CuttingTool, Fixture, FinishedGood, WIP
}

public enum LocationType
{
    Warehouse, ShopFloor, Quarantine, Receiving, Shipping, CuttingToolCrib
}

public enum LotStatus
{
    Quarantine, Available, Depleted, Rejected
}

public enum TransactionType
{
    Receipt, JobConsumption, JobReturn, Adjustment, Transfer, Scrap, CustomerReturn, CycleCount
}

public enum MaterialRequestStatus
{
    Pending, PartiallyFulfilled, Fulfilled, Cancelled
}

public enum BomItemType
{
    RawMaterial,
    InventoryItem,
    SubPart
}

public enum BuildTemplateStatus
{
    Draft,
    Certified,
    Archived
}

public enum LayoutSize
{
    Quadrant,
    Half
}

public enum CertifiedLayoutStatus
{
    Draft,
    Certified,
    Archived
}

// Quality Systems (Module 05)
public enum InspectionResult
{
    Pending, Pass, Fail, Conditional
}

public enum NcrType
{
    InProcess, IncomingMaterial, CustomerReturn, Audit
}

public enum NcrSeverity
{
    Minor, Major, Critical
}

public enum NcrDisposition
{
    PendingReview, Rework, Scrap, UseAsIs, ReturnToVendor
}

public enum NcrStatus
{
    Open, InReview, Dispositioned, Closed
}

public enum CapaType
{
    Corrective, Preventive
}

public enum CapaStatus
{
    Open, InProgress, PendingVerification, Closed
}

public enum MediaType
{
    Image, Video, PDF, Model3D
}

public enum FeedbackType
{
    Confusing, IncorrectInfo, SafetyConcern, Suggestion, Typo
}

public enum FeedbackStatus
{
    New, Acknowledged, Resolved, WontFix
}

// Manufacturing Process Redesign
public enum ProcessingLevel { Build, Batch, Part }

public enum DurationMode { None, PerBuild, PerBatch, PerPart }

public enum BatchStatus { Open, Sealed, InProcess, Completed, Dissolved }

public enum BatchAssignmentAction { Assigned, Removed }

public enum JobScope { Build, Batch, Part }

// Machine Programs
public enum ProgramStatus { Draft, Active, Superseded, Archived }

/// <summary>
/// Schedule lifecycle status for BuildPlate programs.
/// Replaces BuildPackageStatus for program-based SLS scheduling.
/// </summary>
public enum ProgramScheduleStatus
{
    /// <summary>Not scheduled — program is a template/source file.</summary>
    None,
    /// <summary>Ready for scheduling — has parts, slicer data entered.</summary>
    Ready,
    /// <summary>Assigned to machine + time slot.</summary>
    Scheduled,
    /// <summary>Actively printing on machine.</summary>
    Printing,
    /// <summary>Plate off printer, going through post-print stages (depowder, EDM).</summary>
    PostPrint,
    /// <summary>All parts released as PartInstances.</summary>
    Completed,
    /// <summary>Schedule cancelled.</summary>
    Cancelled
}

/// <summary>
/// Distinguishes standard machine programs (CNC, EDM, etc.) from
/// SLS build plate programs that manage multi-part plate nesting and scheduling.
/// </summary>
public enum ProgramType { Standard, BuildPlate }

// Program Feedback (from operators on machine programs)
public enum ProgramFeedbackCategory
{
    ToolingIssue, ParameterAdjustment, SafetyConcern, SetupDifficulty,
    QualityIssue, CycleTimeDeviation, Suggestion, Other
}

public enum ProgramFeedbackSeverity { Low, Medium, High, Critical }

public enum ProgramFeedbackStatus { New, Acknowledged, InReview, Resolved, WontFix }

/// <summary>Purpose tag for build variations in scheduling.</summary>
public enum BuildPurpose { Weekday, Weekend, ChangeoverBackup, DemandFill, Custom }

public enum ShipmentStatus
{
    Preparing,
    Shipped,
    Delivered
}
