using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class SerialNumberService : ISerialNumberService
{
    private readonly TenantDbContext _db;

    public SerialNumberService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateSerialNumberAsync()
    {
        var prefix = "SN";
        var year = DateTime.UtcNow.Year;

        // Check SystemSettings for custom prefix
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "SerialNumberPrefix");
        if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
            prefix = setting.Value;

        var pattern = $"{prefix}-{year}-";

        var lastSerial = await _db.PartInstances
            .Where(p => p.SerialNumber != null && p.SerialNumber.StartsWith(pattern))
            .OrderByDescending(p => p.SerialNumber)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (lastSerial?.SerialNumber != null)
        {
            var suffix = lastSerial.SerialNumber.Replace(pattern, "");
            if (int.TryParse(suffix, out var lastNum))
                nextNumber = lastNum + 1;
        }

        return $"{pattern}{nextNumber:D5}";
    }

    public async Task<List<PartInstance>> AssignSerialNumbersAsync(int workOrderLineId, int partId, int quantity, string createdBy)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        var instances = new List<PartInstance>();

        for (int i = 0; i < quantity; i++)
        {
            var serialNumber = await GenerateSerialNumberAsync();

            var instance = new PartInstance
            {
                SerialNumber = serialNumber,
                TemporaryTrackingId = serialNumber, // When serial is assigned immediately, tracking ID mirrors it
                IsSerialAssigned = true,
                WorkOrderLineId = workOrderLineId,
                PartId = partId,
                Status = PartInstanceStatus.InProcess,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = createdBy,
                LastModifiedDate = DateTime.UtcNow
            };

            _db.PartInstances.Add(instance);
            await _db.SaveChangesAsync(); // Save each to ensure unique serial generation

            instances.Add(instance);
        }

        return instances;
    }

    public async Task<PartInstance?> GetBySerialNumberAsync(string serialNumber)
    {
        return await _db.PartInstances
            .Include(p => p.Part)
            .Include(p => p.WorkOrderLine)
                .ThenInclude(wl => wl.WorkOrder)
            .Include(p => p.CurrentStage)
            .Include(p => p.StageLogs)
                .ThenInclude(sl => sl.ProductionStage)
            .Include(p => p.Inspections)
            .FirstOrDefaultAsync(p => p.SerialNumber == serialNumber);
    }

    public async Task<List<PartInstance>> GetByWorkOrderLineAsync(int workOrderLineId)
    {
        return await _db.PartInstances
            .Include(p => p.Part)
            .Include(p => p.CurrentStage)
            .Where(p => p.WorkOrderLineId == workOrderLineId)
            .OrderBy(p => p.SerialNumber ?? p.TemporaryTrackingId)
            .ToListAsync();
    }

    public async Task<PartInstance> UpdateStatusAsync(int partInstanceId, PartInstanceStatus status)
    {
        var instance = await _db.PartInstances.FindAsync(partInstanceId);
        if (instance == null) throw new InvalidOperationException("Part instance not found.");

        instance.Status = status;
        instance.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return instance;
    }

    public async Task<PartInstance> MoveToStageAsync(int partInstanceId, int stageId)
    {
        var instance = await _db.PartInstances
            .Include(p => p.StageLogs)
            .FirstOrDefaultAsync(p => p.Id == partInstanceId);

        if (instance == null) throw new InvalidOperationException("Part instance not found.");

        // Complete the current stage log if one is active
        var activeLog = instance.StageLogs
            .FirstOrDefault(l => l.CompletedAt == null);
        if (activeLog != null)
            activeLog.CompletedAt = DateTime.UtcNow;

        // Create new stage log
        var stageLog = new PartInstanceStageLog
        {
            PartInstanceId = partInstanceId,
            ProductionStageId = stageId,
            StartedAt = DateTime.UtcNow
        };

        _db.PartInstanceStageLogs.Add(stageLog);
        instance.CurrentStageId = stageId;
        instance.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return instance;
    }

    public Task<string> GenerateTemporaryTrackingIdAsync(int programId, int index)
    {
        return Task.FromResult($"TMP-{programId}-{index:D4}");
    }

    public async Task<PartInstance> AssignOfficialSerialAsync(int partInstanceId)
    {
        var instance = await _db.PartInstances.FindAsync(partInstanceId)
            ?? throw new InvalidOperationException("Part instance not found.");

        if (instance.IsSerialAssigned)
            throw new InvalidOperationException(
                $"Part instance {partInstanceId} already has an official serial: {instance.SerialNumber}");

        var serialNumber = await GenerateSerialNumberAsync();
        instance.SerialNumber = serialNumber;
        instance.IsSerialAssigned = true;
        instance.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return instance;
    }

    public async Task<List<string>> GenerateSerialRangeAsync(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var serials = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            serials.Add(await GenerateSerialNumberAsync());
        }

        return serials;
    }
}
