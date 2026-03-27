using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class OeeService : IOeeService
{
    private readonly TenantDbContext _db;

    public OeeService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<OeeData> GetMachineOeeAsync(int machineId, DateTime from, DateTime to)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == machineId);
        if (machine == null)
            return new OeeData(machineId, "Unknown", 0, 0, 0, 0);

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

        // Calculate scheduled hours from shifts
        double scheduledHours = 0;
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var dayName = day.DayOfWeek.ToString()[..3];
            foreach (var shift in shifts)
            {
                if (shift.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
                    scheduledHours += (shift.EndTime - shift.StartTime).TotalHours;
            }
        }

        if (scheduledHours <= 0)
            return new OeeData(machineId, machine.Name, 0, 0, 0, 0);

        // Get completed stage executions for this machine in the period
        var executions = await _db.StageExecutions
            .Include(e => e.DelayLogs)
            .Where(e => e.MachineId == machineId
                && e.Status == StageExecutionStatus.Completed
                && e.ActualStartAt >= from && e.ActualEndAt <= to)
            .ToListAsync();

        // Availability = (Scheduled - Downtime) / Scheduled
        double downtimeHours = executions
            .SelectMany(e => e.DelayLogs)
            .Sum(d => d.DelayMinutes / 60.0);
        var availability = (decimal)((scheduledHours - downtimeHours) / scheduledHours);

        // Performance = Actual output time / Planned run time
        double totalEstimated = executions.Sum(e => e.EstimatedHours ?? 0);
        double totalActual = executions.Sum(e => e.ActualHours ?? 0);
        var performance = totalActual > 0 ? (decimal)(totalEstimated / totalActual) : 0;

        // Quality = from QC inspections
        var jobIds = executions.Where(e => e.JobId.HasValue).Select(e => e.JobId!.Value).Distinct().ToList();
        int totalInspected = 0;
        int totalPassed = 0;
        if (jobIds.Count > 0)
        {
            var inspections = await _db.QCInspections
                .Where(i => i.JobId.HasValue && jobIds.Contains(i.JobId.Value))
                .ToListAsync();
            totalInspected = inspections.Count;
            totalPassed = inspections.Count(i => i.OverallPass);
        }
        var quality = totalInspected > 0 ? (decimal)totalPassed / totalInspected : 1.0m;

        // Clamp to 0-1
        availability = Math.Clamp(availability, 0, 1);
        performance = Math.Clamp(performance, 0, 1);
        quality = Math.Clamp(quality, 0, 1);

        var oee = availability * performance * quality;

        return new OeeData(
            machineId,
            machine.Name,
            Math.Round(availability * 100, 1),
            Math.Round(performance * 100, 1),
            Math.Round(quality * 100, 1),
            Math.Round(oee * 100, 1)
        );
    }

    public async Task<OeeData> GetOverallOeeAsync(DateTime from, DateTime to)
    {
        var machines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .ToListAsync();

        if (machines.Count == 0)
            return new OeeData(0, "Overall", 0, 0, 0, 0);

        var allOee = new List<OeeData>();
        foreach (var m in machines)
        {
            allOee.Add(await GetMachineOeeAsync(m.Id, from, to));
        }

        var avgAvailability = allOee.Average(o => o.Availability);
        var avgPerformance = allOee.Average(o => o.Performance);
        var avgQuality = allOee.Average(o => o.Quality);
        var avgOee = allOee.Average(o => o.Oee);

        return new OeeData(0, "Overall",
            Math.Round(avgAvailability, 1),
            Math.Round(avgPerformance, 1),
            Math.Round(avgQuality, 1),
            Math.Round(avgOee, 1));
    }

    public async Task<List<OeeData>> GetAllMachineOeeAsync(DateTime from, DateTime to)
    {
        var machines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .ToListAsync();

        var result = new List<OeeData>();
        foreach (var m in machines)
        {
            result.Add(await GetMachineOeeAsync(m.Id, from, to));
        }

        return result.OrderByDescending(o => o.Oee).ToList();
    }
}
