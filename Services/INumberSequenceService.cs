namespace Vectrik.Services;

public interface INumberSequenceService
{
    /// <summary>
    /// Generates the next sequential number for the given entity type.
    /// Reads prefix and digit count from SystemSettings (e.g. numbering.wo_prefix, numbering.wo_digits).
    /// </summary>
    Task<string> NextAsync(string entityType);
}
