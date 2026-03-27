using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;

namespace Vectrik.Services;

public class CustomFieldService : ICustomFieldService
{
    private readonly TenantDbContext _db;

    public CustomFieldService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<CustomFieldConfig?> GetConfigAsync(string entityType)
    {
        return await _db.CustomFieldConfigs
            .FirstOrDefaultAsync(c => c.EntityType == entityType);
    }

    public async Task<List<CustomFieldDefinition>> GetFieldDefinitionsAsync(string entityType)
    {
        var config = await GetConfigAsync(entityType);
        if (config == null || string.IsNullOrWhiteSpace(config.FieldDefinitionsJson))
            return new List<CustomFieldDefinition>();

        return JsonSerializer.Deserialize<List<CustomFieldDefinition>>(config.FieldDefinitionsJson)
            ?? new List<CustomFieldDefinition>();
    }

    public async Task SaveConfigAsync(string entityType, List<CustomFieldDefinition> fields, string modifiedBy)
    {
        var config = await _db.CustomFieldConfigs
            .FirstOrDefaultAsync(c => c.EntityType == entityType);

        var json = JsonSerializer.Serialize(fields);

        if (config == null)
        {
            config = new CustomFieldConfig
            {
                EntityType = entityType,
                FieldDefinitionsJson = json,
                LastModifiedBy = modifiedBy,
                LastModifiedDate = DateTime.UtcNow
            };
            _db.CustomFieldConfigs.Add(config);
        }
        else
        {
            config.FieldDefinitionsJson = json;
            config.LastModifiedBy = modifiedBy;
            config.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public Dictionary<string, string> ValidateValues(List<CustomFieldDefinition> definitions, Dictionary<string, string> values)
    {
        var errors = new Dictionary<string, string>();

        foreach (var field in definitions)
        {
            values.TryGetValue(field.Name, out var value);
            var hasValue = !string.IsNullOrWhiteSpace(value);

            if (field.IsRequired && !hasValue)
            {
                errors[field.Name] = $"{field.Label} is required.";
                continue;
            }

            if (!hasValue) continue;

            if (field.FieldType is "number" or "decimal" && double.TryParse(value, out var numVal))
            {
                if (field.MinValue.HasValue && numVal < field.MinValue.Value)
                    errors[field.Name] = $"{field.Label} must be at least {field.MinValue.Value}.";
                else if (field.MaxValue.HasValue && numVal > field.MaxValue.Value)
                    errors[field.Name] = $"{field.Label} must be at most {field.MaxValue.Value}.";
            }

            if (!string.IsNullOrEmpty(field.ValidationRegex) && !Regex.IsMatch(value!, field.ValidationRegex))
            {
                errors[field.Name] = $"{field.Label} does not match the required format.";
            }
        }

        return errors;
    }
}
