using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IDocumentTemplateService
{
    Task<List<DocumentTemplate>> GetTemplatesAsync(string entityType);
    Task<DocumentTemplate?> GetDefaultTemplateAsync(string entityType);
    Task<DocumentTemplate?> GetByIdAsync(int id);
    Task SaveAsync(DocumentTemplate template);
    Task DeleteAsync(int id);

    /// <summary>
    /// Renders a template by replacing {{MergeField}} placeholders with values from the data dictionary.
    /// </summary>
    string RenderHtml(DocumentTemplate template, Dictionary<string, string> mergeFields);
}
