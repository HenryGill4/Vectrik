using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class MachineConnectionSettings
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string MachineId { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ProviderType { get; set; } = "Mock";

    [MaxLength(200)]
    public string? EndpointUrl { get; set; }

    public bool IsEnabled { get; set; }

    public int PollIntervalSeconds { get; set; } = 5;

    public string? ConfigJson { get; set; }

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
