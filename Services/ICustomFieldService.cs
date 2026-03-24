using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface ICustomFieldService
{
    Task<CustomFieldConfig?> GetConfigAsync(string entityType);
    Task<List<CustomFieldDefinition>> GetFieldDefinitionsAsync(string entityType);
    Task SaveConfigAsync(string entityType, List<CustomFieldDefinition> fields, string modifiedBy);
    Dictionary<string, string> ValidateValues(List<CustomFieldDefinition> definitions, Dictionary<string, string> values);
}
