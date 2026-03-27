using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class DashboardLayout
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string LayoutName { get; set; } = "My Dashboard";

    public bool IsDefault { get; set; }

    public string WidgetsJson { get; set; } = "[]";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
