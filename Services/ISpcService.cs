using Vectrik.Models;

namespace Vectrik.Services;

public interface ISpcService
{
    SpcCalculationResult Calculate(List<decimal> values, decimal nominal, decimal tolerancePlus, decimal toleranceMinus);
    Task<List<Part>> GetActivePartsWithSpcDataAsync();
    Task<List<string>> GetCharacteristicsForPartAsync(int partId);
    Task<List<SpcDataPoint>> GetDataPointsAsync(int partId, string characteristic, int count);
}

public record SpcCalculationResult(
    decimal Mean,
    decimal StdDev,
    decimal Ucl,
    decimal Lcl,
    decimal Usl,
    decimal Lsl,
    decimal Cp,
    decimal Cpk,
    bool HasOutOfControl);
