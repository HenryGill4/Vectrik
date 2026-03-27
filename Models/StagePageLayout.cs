using System.Text.Json;

namespace Vectrik.Models;

/// <summary>
/// Defines the layout of widgets on an operator's stage page.
/// Stored as JSON in ProductionStage.PageLayoutJson.
/// </summary>
public class StagePageLayout
{
    /// <summary>Layout columns: 1, 2, or 3.</summary>
    public int Columns { get; set; } = 1;

    /// <summary>Ordered list of widgets to display on the page.</summary>
    public List<LayoutWidget> Widgets { get; set; } = new()
    {
        new() { Type = "queue", Title = "Queue", Column = 1, Size = "full" },
        new() { Type = "active-work", Title = "Active Work", Column = 1, Size = "full" },
        new() { Type = "custom-form", Title = "Stage Form", Column = 1, Size = "full" },
        new() { Type = "history", Title = "History", Column = 1, Size = "full" },
    };

    /// <summary>Default layout with all standard widgets.</summary>
    public static StagePageLayout Default => new();
}

/// <summary>
/// A single widget in the stage page layout.
/// </summary>
public class LayoutWidget
{
    /// <summary>
    /// Widget type: queue, active-work, custom-form, work-instructions, machine-status,
    /// part-context, timer, checklist, history, shift-info, notes
    /// </summary>
    public string Type { get; set; } = "queue";

    /// <summary>Display title for the widget header.</summary>
    public string Title { get; set; } = "";

    /// <summary>Which column (1-based) this widget appears in.</summary>
    public int Column { get; set; } = 1;

    /// <summary>Widget size: "small", "medium", "large", "full".</summary>
    public string Size { get; set; } = "full";

    /// <summary>Whether this widget is collapsed by default.</summary>
    public bool CollapsedByDefault { get; set; }

    /// <summary>Widget-specific settings as JSON.</summary>
    public string? SettingsJson { get; set; }
}
