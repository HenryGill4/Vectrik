using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class UserSettings
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [MaxLength(20)]
    public string Theme { get; set; } = "dark";

    public string? DashboardLayout { get; set; }

    [MaxLength(50)]
    public string? DefaultView { get; set; }

    public bool NotificationsEnabled { get; set; } = true;

    public bool DebugFabEnabled { get; set; }

    public virtual User User { get; set; } = null!;
}
