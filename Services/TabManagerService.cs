namespace Opcentrix_V3.Services;

/// <summary>
/// Represents a single open tab in the MDI workspace.
/// </summary>
public sealed record TabInfo
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public required string Icon { get; set; }
    public required string Url { get; set; }

    /// <summary>
    /// Whether this tab is pinned (cannot be closed by bulk-close operations).
    /// The Dashboard tab is pinned by default; users can pin/unpin others.
    /// </summary>
    public bool Pinned { get; set; }

    /// <summary>
    /// Whether this tab has unsaved or in-progress work (e.g., a WO being edited).
    /// Pages set this via <see cref="TabManagerService.MarkDirty"/>/<see cref="TabManagerService.MarkClean"/>.
    /// </summary>
    public bool HasUnsavedWork { get; set; }
}

/// <summary>
/// Manages open tabs within the Blazor circuit. Scoped per-user session.
/// Tab state lives in memory — switching tabs preserves the browser URL
/// while rendering the active tab's content via the Blazor router.
/// </summary>
public sealed class TabManagerService
{
    private readonly List<TabInfo> _tabs = [];
    private string _activeTabId = string.Empty;

    /// <summary>Fires when any tab is opened, closed, activated, or renamed.</summary>
    public event Action? OnChanged;

    /// <summary>All currently open tabs in order.</summary>
    public IReadOnlyList<TabInfo> Tabs => _tabs;

    /// <summary>The currently active tab ID.</summary>
    public string ActiveTabId => _activeTabId;

    /// <summary>The currently active tab, or null if none.</summary>
    public TabInfo? ActiveTab => _tabs.Find(t => t.Id == _activeTabId);

    /// <summary>
    /// Opens a new tab or activates an existing one with the same URL.
    /// Returns the tab that was opened/activated.
    /// </summary>
    public TabInfo Open(string title, string icon, string url, bool pinned = false)
    {
        // Normalize the URL for comparison (strip leading slash)
        var normalizedUrl = NormalizeUrl(url);

        // If a tab with this URL already exists, just activate it
        var existing = _tabs.Find(t => NormalizeUrl(t.Url) == normalizedUrl);
        if (existing is not null)
        {
            Activate(existing.Id);
            return existing;
        }

        var tab = new TabInfo
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Title = title,
            Icon = icon,
            Url = url,
            Pinned = pinned
        };

        _tabs.Add(tab);
        _activeTabId = tab.Id;
        OnChanged?.Invoke();
        return tab;
    }

    /// <summary>
    /// Activates an existing tab by ID.
    /// </summary>
    public void Activate(string tabId)
    {
        if (_activeTabId == tabId) return;
        if (_tabs.Exists(t => t.Id == tabId))
        {
            _activeTabId = tabId;
            OnChanged?.Invoke();
        }
    }

    /// <summary>
    /// Closes a tab. If the closed tab was active, activates the nearest neighbor.
    /// Pinned tabs cannot be closed.
    /// </summary>
    public void Close(string tabId)
    {
        var tab = _tabs.Find(t => t.Id == tabId);
        if (tab is null || tab.Pinned) return;

        var index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);

        // If we closed the active tab, activate the nearest remaining
        if (_activeTabId == tabId && _tabs.Count > 0)
        {
            var nextIndex = Math.Min(index, _tabs.Count - 1);
            _activeTabId = _tabs[nextIndex].Id;
        }
        else if (_tabs.Count == 0)
        {
            _activeTabId = string.Empty;
        }

        OnChanged?.Invoke();
    }

    /// <summary>
    /// Closes all tabs except the specified one (and any pinned tabs).
    /// </summary>
    public void CloseOthers(string keepTabId)
    {
        _tabs.RemoveAll(t => t.Id != keepTabId && !t.Pinned);

        if (!_tabs.Exists(t => t.Id == _activeTabId) && _tabs.Count > 0)
            _activeTabId = _tabs[^1].Id;

        OnChanged?.Invoke();
    }

    /// <summary>
    /// Closes all non-pinned tabs.
    /// </summary>
    public void CloseAll()
    {
        _tabs.RemoveAll(t => !t.Pinned);

        if (!_tabs.Exists(t => t.Id == _activeTabId) && _tabs.Count > 0)
            _activeTabId = _tabs[^1].Id;
        else if (_tabs.Count == 0)
            _activeTabId = string.Empty;

        OnChanged?.Invoke();
    }

    /// <summary>
    /// Closes all tabs except pinned ones (user-pinned and the Dashboard).
    /// </summary>
    public void CloseUnpinned()
    {
        _tabs.RemoveAll(t => !t.Pinned);
        ActivateNearestAfterBulkClose();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Closes tabs that have no unsaved work and are not pinned.
    /// Tabs with <see cref="TabInfo.HasUnsavedWork"/> = true are kept.
    /// </summary>
    public void CloseSaved()
    {
        _tabs.RemoveAll(t => !t.Pinned && !t.HasUnsavedWork);
        ActivateNearestAfterBulkClose();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Toggles the pinned state on a tab. The home tab cannot be unpinned.
    /// </summary>
    public void TogglePin(string tabId)
    {
        var tab = _tabs.Find(t => t.Id == tabId);
        if (tab is null) return;

        // Don't allow unpinning the home/dashboard tab
        if (tab.Pinned && NormalizeUrl(tab.Url) == "")
            return;

        tab.Pinned = !tab.Pinned;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Marks a tab (by URL) as having unsaved/in-progress work.
    /// Pages call this when a form is dirty or a long operation is running.
    /// </summary>
    public void MarkDirty(string url)
    {
        var normalized = NormalizeUrl(url);
        var tab = _tabs.Find(t => NormalizeUrl(t.Url) == normalized);
        if (tab is not null && !tab.HasUnsavedWork)
        {
            tab.HasUnsavedWork = true;
            OnChanged?.Invoke();
        }
    }

    /// <summary>
    /// Clears the unsaved-work flag on a tab (by URL).
    /// Pages call this when a save completes or the form is reset.
    /// </summary>
    public void MarkClean(string url)
    {
        var normalized = NormalizeUrl(url);
        var tab = _tabs.Find(t => NormalizeUrl(t.Url) == normalized);
        if (tab is not null && tab.HasUnsavedWork)
        {
            tab.HasUnsavedWork = false;
            OnChanged?.Invoke();
        }
    }

    /// <summary>
    /// Updates the title/icon of an existing tab (e.g., after data loads).
    /// </summary>
    public void UpdateTab(string tabId, string? newTitle = null, string? newIcon = null)
    {
        var tab = _tabs.Find(t => t.Id == tabId);
        if (tab is null) return;

        if (newTitle is not null) tab.Title = newTitle;
        if (newIcon is not null) tab.Icon = newIcon;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Ensures the home/dashboard tab exists. Called on layout init.
    /// </summary>
    public void EnsureHomeTab()
    {
        if (_tabs.Count != 0) return;
        Open("Dashboard", "📊", "/", pinned: true);
    }

    /// <summary>
    /// Moves a tab by dragging (reorder).
    /// </summary>
    public void MoveTab(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _tabs.Count) return;
        if (toIndex < 0 || toIndex >= _tabs.Count) return;
        if (fromIndex == toIndex) return;

        var tab = _tabs[fromIndex];
        _tabs.RemoveAt(fromIndex);
        _tabs.Insert(toIndex, tab);
        OnChanged?.Invoke();
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = url.TrimStart('/').ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? "" : trimmed;
    }

    private void ActivateNearestAfterBulkClose()
    {
        if (!_tabs.Exists(t => t.Id == _activeTabId) && _tabs.Count > 0)
            _activeTabId = _tabs[^1].Id;
        else if (_tabs.Count == 0)
            _activeTabId = string.Empty;
    }
}
