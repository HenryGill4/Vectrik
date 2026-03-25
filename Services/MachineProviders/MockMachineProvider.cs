using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services.MachineProviders;

public class MockMachineProvider : IMachineProvider
{
    private readonly TenantDbContext _db;
    private readonly Random _random = new();

    public MockMachineProvider(TenantDbContext db)
    {
        _db = db;
    }

    public string ProviderType => "Mock";

    public async Task<MachineStateRecord> GetCurrentStateAsync(string machineId)
    {
        // Look for a currently-printing build on this machine
        var printingBuild = await _db.MachinePrograms
            .Include(mp => mp.Machine)
            .FirstOrDefaultAsync(mp =>
                mp.Machine != null &&
                mp.Machine.MachineId == machineId &&
                mp.ScheduleStatus == ProgramScheduleStatus.Printing);

        if (printingBuild is not null)
            return BuildPrintingState(machineId, printingBuild);

        // Check for a post-print (cooling) build
        var postPrintBuild = await _db.MachinePrograms
            .Include(mp => mp.Machine)
            .FirstOrDefaultAsync(mp =>
                mp.Machine != null &&
                mp.Machine.MachineId == machineId &&
                mp.ScheduleStatus == ProgramScheduleStatus.PostPrint);

        if (postPrintBuild is not null)
            return BuildCoolingState(machineId, postPrintBuild);

        // No active build — idle
        return BuildIdleState(machineId);
    }

    public Task<bool> TestConnectionAsync(string machineId)
    {
        return Task.FromResult(true);
    }

    private MachineStateRecord BuildPrintingState(string machineId, MachineProgram build)
    {
        var now = DateTime.UtcNow;
        var elapsedFraction = 0.0;

        if (build.PrintStartedAt.HasValue && build.EstimatedPrintHours is > 0)
        {
            var elapsed = (now - build.PrintStartedAt.Value).TotalHours;
            elapsedFraction = Math.Clamp(elapsed / build.EstimatedPrintHours.Value, 0.0, 1.0);
        }

        var totalLayers = build.LayerCount ?? 2000;
        var currentLayer = (int)(elapsedFraction * totalLayers);
        currentLayer = Math.Clamp(currentLayer, 0, totalLayers);
        var progress = Math.Round(elapsedFraction * 100, 1);

        return new MachineStateRecord
        {
            MachineId = machineId,
            Timestamp = now,
            Status = "Building",
            BuildProgress = progress,
            CurrentLayer = currentLayer,
            TotalLayers = totalLayers,
            BedTemperature = Math.Round(180 + 15 * Math.Min(1, elapsedFraction * 5) + (_random.NextDouble() - 0.5) * 2, 1),
            ChamberTemperature = Math.Round(35 + 8 * Math.Min(1, elapsedFraction * 3) + (_random.NextDouble() - 0.5) * 1, 1),
            OxygenLevel = Math.Round(0.03 + (_random.NextDouble() - 0.5) * 0.02, 3),
            LaserPower = Math.Round(280 + _random.NextDouble() * 60, 1),
            GasFlow = Math.Round(3.5 + (_random.NextDouble() - 0.5) * 0.5, 1),
            HumidityPercent = Math.Round(24 + (_random.NextDouble() - 0.5) * 4, 1),
            IsConnected = true
        };
    }

    private MachineStateRecord BuildCoolingState(string machineId, MachineProgram build)
    {
        var now = DateTime.UtcNow;

        // Decay factor: temps drop over time after print completes
        var hoursSincePrintEnd = 0.0;
        if (build.PrintStartedAt.HasValue && build.EstimatedPrintHours is > 0)
        {
            var printEnd = build.PrintStartedAt.Value.AddHours(build.EstimatedPrintHours.Value);
            hoursSincePrintEnd = Math.Max(0, (now - printEnd).TotalHours);
        }

        var decay = Math.Exp(-0.3 * hoursSincePrintEnd); // exponential decay

        return new MachineStateRecord
        {
            MachineId = machineId,
            Timestamp = now,
            Status = "Cooling",
            BuildProgress = null,
            CurrentLayer = null,
            TotalLayers = null,
            BedTemperature = Math.Round(180 * decay + 25 * (1 - decay) + (_random.NextDouble() - 0.5) * 1, 1),
            ChamberTemperature = Math.Round(43 * decay + 22 * (1 - decay) + (_random.NextDouble() - 0.5) * 0.5, 1),
            OxygenLevel = null,
            LaserPower = null,
            GasFlow = null,
            HumidityPercent = Math.Round(24 + (_random.NextDouble() - 0.5) * 4, 1),
            IsConnected = true
        };
    }

    private MachineStateRecord BuildIdleState(string machineId)
    {
        return new MachineStateRecord
        {
            MachineId = machineId,
            Timestamp = DateTime.UtcNow,
            Status = "Idle",
            BuildProgress = null,
            CurrentLayer = null,
            TotalLayers = null,
            BedTemperature = Math.Round(25 + (_random.NextDouble() - 0.5) * 2, 1),
            ChamberTemperature = Math.Round(22 + (_random.NextDouble() - 0.5) * 1, 1),
            OxygenLevel = null,
            LaserPower = null,
            GasFlow = null,
            HumidityPercent = Math.Round(24 + (_random.NextDouble() - 0.5) * 4, 1),
            IsConnected = true
        };
    }
}
