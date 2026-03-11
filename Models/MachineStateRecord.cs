using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class MachineStateRecord
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string MachineId { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    public double? BuildProgress { get; set; }
    public int? CurrentLayer { get; set; }
    public int? TotalLayers { get; set; }
    public double? BedTemperature { get; set; }
    public double? ChamberTemperature { get; set; }
    public double? LaserPower { get; set; }
    public double? GasFlow { get; set; }
    public double? OxygenLevel { get; set; }
    public double? HumidityPercent { get; set; }
    public bool IsConnected { get; set; }
    public string? RawDataJson { get; set; }
}
