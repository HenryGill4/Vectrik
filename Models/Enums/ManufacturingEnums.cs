namespace Opcentrix_V3.Models.Enums;

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

public enum BuildJobStatus
{
    Pending, Preheating, Building, Cooling, Completed, Failed, Cancelled
}

public enum BuildPackageStatus
{
    Draft,          // Parts being assembled onto plate
    Sliced,         // Slicer data entered (duration, actual counts)
    Ready,          // Approved for scheduling
    Scheduled,      // Assigned to machine + time slot
    Printing,       // Actively printing on machine
    PostPrint,      // Plate off printer, going through post-print stages
    Completed,      // All parts released as PartInstances
    Cancelled
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

public enum BuildTemplateStatus
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
