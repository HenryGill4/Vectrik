using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class OperatingShift
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    [Required, MaxLength(50)]
    public string DaysOfWeek { get; set; } = "Mon,Tue,Wed,Thu,Fri";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
