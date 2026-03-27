using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models.Maintenance;

public class MachineComponent
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string MachineId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? PartNumber { get; set; }

    public double? CurrentHours { get; set; }
    public int? CurrentBuilds { get; set; }

    public DateTime? LastReplacedDate { get; set; }
    public DateTime? InstallDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Machine Machine { get; set; } = null!;
    public virtual ICollection<MaintenanceRule> MaintenanceRules { get; set; } = new List<MaintenanceRule>();
}
