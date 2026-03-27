namespace Vectrik.Services;

public interface IOeeService
{
    Task<OeeData> GetMachineOeeAsync(int machineId, DateTime from, DateTime to);
    Task<OeeData> GetOverallOeeAsync(DateTime from, DateTime to);
    Task<List<OeeData>> GetAllMachineOeeAsync(DateTime from, DateTime to);
}

public record OeeData(
    int MachineId,
    string MachineName,
    decimal Availability,
    decimal Performance,
    decimal Quality,
    decimal Oee
);
