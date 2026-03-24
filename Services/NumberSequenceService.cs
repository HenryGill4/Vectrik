using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class NumberSequenceService : INumberSequenceService
{
    private readonly TenantDbContext _db;

    // Maps entity types to their setting key prefixes
    private static readonly Dictionary<string, (string PrefixKey, string DigitsKey, string CounterKey, string DefaultPrefix, int DefaultDigits)> _entityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WorkOrder"] = ("numbering.wo_prefix", "numbering.wo_digits", "numbering.wo_counter", "WO", 5),
        ["Quote"] = ("numbering.quote_prefix", "numbering.quote_digits", "numbering.quote_counter", "QT", 5),
        ["Shipment"] = ("numbering.shipment_prefix", "numbering.shipment_digits", "numbering.shipment_counter", "SHP", 5),
        ["NCR"] = ("numbering.ncr_prefix", "numbering.ncr_digits", "numbering.ncr_counter", "NCR", 5),
        ["PO"] = ("numbering.po_prefix", "numbering.po_digits", "numbering.po_counter", "PO", 5),
        ["Part"] = ("numbering.part_prefix", "numbering.part_digits", "numbering.part_counter", "PT", 5),
        ["Job"] = ("numbering.job_prefix", "numbering.job_digits", "numbering.job_counter", "JOB", 5),
        ["CAPA"] = ("numbering.capa_prefix", "numbering.capa_digits", "numbering.capa_counter", "CAPA", 5),
        ["BuildPlate"] = ("numbering.bp_prefix", "numbering.bp_digits", "numbering.bp_counter", "BP", 5),
    };

    public NumberSequenceService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<string> NextAsync(string entityType)
    {
        if (!_entityMap.TryGetValue(entityType, out var config))
            throw new ArgumentException($"Unknown entity type for number sequence: {entityType}");

        var prefix = await GetSettingValueAsync(config.PrefixKey) ?? config.DefaultPrefix;
        var digitsStr = await GetSettingValueAsync(config.DigitsKey);
        var digits = int.TryParse(digitsStr, out var d) ? d : config.DefaultDigits;

        // Get and increment counter atomically
        var counterSetting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == config.CounterKey);

        int nextVal;
        if (counterSetting == null)
        {
            nextVal = 1;
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = config.CounterKey,
                Value = "1",
                Category = "Numbering",
                Description = $"Last used counter for {entityType} numbers",
                LastModifiedBy = "System"
            });
        }
        else
        {
            nextVal = int.TryParse(counterSetting.Value, out var current) ? current + 1 : 1;
            counterSetting.Value = nextVal.ToString();
            counterSetting.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var separator = await GetSettingValueAsync("numbering.separator") ?? "-";
        return $"{prefix}{separator}{nextVal.ToString().PadLeft(digits, '0')}";
    }

    private async Task<string?> GetSettingValueAsync(string key)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }
}
