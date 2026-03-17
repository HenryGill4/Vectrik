namespace Opcentrix_V3.Services;

public interface ISpcService
{
    SpcCalculationResult Calculate(List<decimal> values, decimal nominal, decimal tolerancePlus, decimal toleranceMinus);
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
