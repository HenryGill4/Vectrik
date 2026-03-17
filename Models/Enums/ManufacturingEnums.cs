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
    Draft, Ready, Scheduled, InProgress, Completed, Cancelled
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
