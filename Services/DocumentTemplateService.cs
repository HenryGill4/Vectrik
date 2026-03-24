using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class DocumentTemplateService : IDocumentTemplateService
{
    private static readonly Regex MergeFieldRegex = new(@"\{\{(.+?)\}\}", RegexOptions.Compiled);
    private readonly TenantDbContext _db;

    public DocumentTemplateService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<DocumentTemplate>> GetTemplatesAsync(string entityType)
    {
        return await _db.DocumentTemplates
            .Where(t => t.EntityType == entityType)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<DocumentTemplate?> GetDefaultTemplateAsync(string entityType)
    {
        return await _db.DocumentTemplates
            .Where(t => t.EntityType == entityType && t.IsDefault)
            .FirstOrDefaultAsync();
    }

    public async Task<DocumentTemplate?> GetByIdAsync(int id)
    {
        return await _db.DocumentTemplates.FindAsync(id);
    }

    public async Task SaveAsync(DocumentTemplate template)
    {
        if (template.Id == 0)
        {
            _db.DocumentTemplates.Add(template);
        }
        else
        {
            _db.DocumentTemplates.Update(template);
        }

        // If this template is set as default, unset other defaults for the same entity type
        if (template.IsDefault)
        {
            var others = await _db.DocumentTemplates
                .Where(t => t.EntityType == template.EntityType && t.Id != template.Id && t.IsDefault)
                .ToListAsync();

            foreach (var other in others)
                other.IsDefault = false;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var template = await _db.DocumentTemplates.FindAsync(id);
        if (template != null)
        {
            _db.DocumentTemplates.Remove(template);
            await _db.SaveChangesAsync();
        }
    }

    public string RenderHtml(DocumentTemplate template, Dictionary<string, string> mergeFields)
    {
        var html = template.TemplateHtml ?? string.Empty;

        // Replace {{FieldName}} with values
        html = MergeFieldRegex.Replace(html, match =>
        {
            var fieldName = match.Groups[1].Value.Trim();
            return mergeFields.TryGetValue(fieldName, out var value) ? value : string.Empty;
        });

        // Build full document with header/footer
        var header = template.HeaderHtml ?? string.Empty;
        var footer = template.FooterHtml ?? string.Empty;
        var css = template.CssOverrides ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(header) || !string.IsNullOrWhiteSpace(footer) || !string.IsNullOrWhiteSpace(css))
        {
            html = $"<style>{css}</style>{header}{html}{footer}";
        }

        return html;
    }

    }
