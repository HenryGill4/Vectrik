namespace Opcentrix_V3.Services;

public class SpcService : ISpcService
{
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
