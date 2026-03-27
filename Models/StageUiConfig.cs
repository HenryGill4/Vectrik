namespace Vectrik.Models;

/// <summary>
/// Per-stage configuration for the operator shop floor UI.
/// Stored as JSON in ProductionStage.StageUiConfigJson.
/// All defaults are set to preserve existing behavior — admins opt-in to restrictions.
/// </summary>
public class StageUiConfig
{
    // ── Visible Sections ──
    // Controls which cards/panels appear on the operator stage page

    /// <summary>Show work instruction banner at top of stage page.</summary>
    public bool ShowWorkInstructions { get; set; } = true;

    /// <summary>Show sign-off checklist panel for each active execution.</summary>
    public bool ShowSignOffChecklist { get; set; } = true;

    /// <summary>Show live machine status card (requires MachineSyncService).</summary>
    public bool ShowMachineStatus { get; set; } = false;

    /// <summary>Show part context panel (drawing, specs, WO info).</summary>
    public bool ShowPartContext { get; set; } = true;

    /// <summary>Show notes from previous production stages.</summary>
    public bool ShowPreviousStageNotes { get; set; } = false;

    /// <summary>Show elapsed/remaining timer on active work.</summary>
    public bool ShowTimer { get; set; } = true;

    /// <summary>Show material lot tracking fields.</summary>
    public bool ShowMaterialLotTracking { get; set; } = false;

    /// <summary>Show photo capture button for operators.</summary>
    public bool ShowPhotoCapture { get; set; } = false;

    /// <summary>Show the History tab on the stage page.</summary>
    public bool ShowHistory { get; set; } = true;

    // ── Required Before Completion ──
    // Operators cannot complete work unless these conditions are met

    /// <summary>All sign-off checklist items must be signed before completion.</summary>
    public bool RequireChecklist { get; set; } = false;

    /// <summary>Completion notes field must be filled before completing.</summary>
    public bool RequireNotes { get; set; } = false;

    /// <summary>At least one photo must be captured before completing.</summary>
    public bool RequirePhoto { get; set; } = false;

    /// <summary>Quality check must be performed before completing.</summary>
    public bool RequireQualityCheck { get; set; } = true;

    /// <summary>All custom form fields marked as required must be filled.</summary>
    public bool RequireAllCustomFields { get; set; } = false;

    // ── Operator Actions ──
    // Controls which action buttons operators can see and use

    /// <summary>Allow operators to pause work and log a delay reason.</summary>
    public bool AllowPause { get; set; } = true;

    /// <summary>Allow operators to fail/reject work and create an NCR.</summary>
    public bool AllowFail { get; set; } = true;

    /// <summary>Allow batch completion for build-level stages.</summary>
    public bool AllowBatchComplete { get; set; } = true;

    /// <summary>Allow operators to transfer work to another operator.</summary>
    public bool AllowTransfer { get; set; } = false;

    /// <summary>Allow operators to log delays without pausing.</summary>
    public bool AllowLogDelay { get; set; } = true;

    /// <summary>Allow operators to skip this stage entirely.</summary>
    public bool AllowSkip { get; set; } = false;

    // ── Workflow ──

    /// <summary>Automatically load the next queue item after completing the current one.</summary>
    public bool AutoAdvanceToNext { get; set; } = false;
}
