using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class DevIssue
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DevIssueType Type { get; set; }
    public DevIssuePriority Priority { get; set; } = DevIssuePriority.Medium;
    public DevIssueStatus Status { get; set; } = DevIssueStatus.Open;

    /// <summary>System area this issue belongs to (e.g. Scheduler, Parts, WorkOrders).</summary>
    [MaxLength(50)]
    public string SystemArea { get; set; } = string.Empty;

    /// <summary>What happened / current behavior.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Expected behavior / desired behavior.</summary>
    public string Expected { get; set; } = string.Empty;

    /// <summary>Steps to reproduce (bugs) or affected element (styling).</summary>
    public string StepsOrElement { get; set; } = string.Empty;

    /// <summary>Comma-separated file paths relevant to this issue.</summary>
    public string RelatedFiles { get; set; } = string.Empty;

    /// <summary>How do we know it's fixed? Testable criteria.</summary>
    public string AcceptanceCriteria { get; set; } = string.Empty;

    /// <summary>Page URL captured at report time.</summary>
    [MaxLength(500)]
    public string AffectedPage { get; set; } = string.Empty;

    /// <summary>Auto-captured: viewport size, theme, breakpoint, UA.</summary>
    [MaxLength(500)]
    public string EnvironmentContext { get; set; } = string.Empty;

    /// <summary>Notes on what was done to fix the issue.</summary>
    public string Resolution { get; set; } = string.Empty;

    // ── Scheduling Issue fields ──

    /// <summary>Machine name/identifier for scheduling issues (e.g. "EOS M4 Onyx #1").</summary>
    [MaxLength(200)]
    public string? MachineContext { get; set; }

    /// <summary>
    /// Auto-captured JSON snapshot of scheduling diagnostics at report time.
    /// Contains machine state, executions, conflicts, build packages, and timeline data.
    /// </summary>
    public string? ScheduleSnapshotJson { get; set; }

    /// <summary>Manual sort order for queue prioritization.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public enum DevIssueType
{
    Bug,
    Change,
    Styling,
    SchedulingIssue
}

public enum DevIssuePriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum DevIssueStatus
{
    Open,
    InProgress,
    Fixed,
    Verified,
    WontFix
}
