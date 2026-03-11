using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services.MachineProviders;

public class MockMachineProvider : IMachineProvider
{
    private readonly Random _random = new();

    public string ProviderType => "Mock";

    public Task<MachineStateRecord> GetCurrentStateAsync(string machineId)
    {
        var statuses = new[] { MachineStatus.Idle, MachineStatus.Building, MachineStatus.Preheating, MachineStatus.Cooling };
        var status = statuses[_random.Next(statuses.Length)];

        var isBuilding = status == MachineStatus.Building;
        var totalLayers = isBuilding ? _random.Next(1000, 3500) : (int?)null;
        var currentLayer = isBuilding ? _random.Next(1, totalLayers!.Value) : (int?)null;
        var progress = isBuilding && totalLayers > 0 ? Math.Round((double)currentLayer!.Value / totalLayers.Value * 100, 1) : (double?)null;

        var record = new MachineStateRecord
        {
            MachineId = machineId,
            Timestamp = DateTime.UtcNow,
            Status = status.ToString(),
            BuildProgress = progress,
            CurrentLayer = currentLayer,
            TotalLayers = totalLayers,
            BedTemperature = isBuilding ? Math.Round(180 + _random.NextDouble() * 40, 1) : null,
            ChamberTemperature = isBuilding ? Math.Round(35 + _random.NextDouble() * 10, 1) : null,
            LaserPower = isBuilding ? Math.Round(200 + _random.NextDouble() * 200, 1) : null,
            GasFlow = isBuilding ? Math.Round(2.0 + _random.NextDouble() * 3.0, 1) : null,
            OxygenLevel = isBuilding ? Math.Round(0.01 + _random.NextDouble() * 0.09, 3) : null,
            HumidityPercent = Math.Round(20 + _random.NextDouble() * 30, 1),
            IsConnected = true
        };

        return Task.FromResult(record);
    }

    public Task<bool> TestConnectionAsync(string machineId)
    {
        return Task.FromResult(true);
    }
}
