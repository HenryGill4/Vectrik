using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class SpcService : ISpcService
{
    private readonly TenantDbContext _db;

    public SpcService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<Part>> GetActivePartsWithSpcDataAsync()
    {
        var partIds = await _db.SpcDataPoints.Select(d => d.PartId).Distinct().ToListAsync();
        return await _db.Parts.Where(p => p.IsActive && partIds.Contains(p.Id))
            .OrderBy(p => p.PartNumber).ToListAsync();
    }

    public async Task<List<string>> GetCharacteristicsForPartAsync(int partId)
    {
        return await _db.SpcDataPoints
            .Where(d => d.PartId == partId)
            .Select(d => d.CharacteristicName)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task<List<SpcDataPoint>> GetDataPointsAsync(int partId, string characteristic, int count)
    {
        return await _db.SpcDataPoints
            .Where(d => d.PartId == partId && d.CharacteristicName == characteristic)
            .OrderByDescending(d => d.RecordedAt)
            .Take(count)
            .OrderBy(d => d.RecordedAt)
            .ToListAsync();
    }

    public SpcCalculationResult Calculate(List<decimal> values, decimal nominal, decimal tolerancePlus, decimal toleranceMinus)
    {
        if (values.Count < 2)
        {
            return new SpcCalculationResult(
                nominal, 0, nominal, nominal,
                nominal + tolerancePlus, nominal - toleranceMinus,
                0, 0, false);
        }

        decimal mean = values.Average();
        decimal sumSquares = values.Sum(v => (v - mean) * (v - mean));
        decimal stdDev = (decimal)Math.Sqrt((double)(sumSquares / (values.Count - 1)));

        decimal ucl = mean + 3 * stdDev;
        decimal lcl = mean - 3 * stdDev;
        decimal usl = nominal + tolerancePlus;
        decimal lsl = nominal - toleranceMinus;

        decimal cp = 0;
        decimal cpk = 0;

        if (stdDev > 0)
        {
            cp = (usl - lsl) / (6 * stdDev);
            decimal cpupper = (usl - mean) / (3 * stdDev);
            decimal cplower = (mean - lsl) / (3 * stdDev);
            cpk = Math.Min(cpupper, cplower);
        }

        bool hasOoc = values.Any(v => v > ucl || v < lcl);

        return new SpcCalculationResult(
            Math.Round(mean, 4),
            Math.Round(stdDev, 4),
            Math.Round(ucl, 4),
            Math.Round(lcl, 4),
            Math.Round(usl, 4),
            Math.Round(lsl, 4),
            Math.Round(cp, 3),
            Math.Round(cpk, 3),
            hasOoc);
    }
}
