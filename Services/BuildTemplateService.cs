using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class BuildTemplateService : IBuildTemplateService
{
    private readonly TenantDbContext _db;

    public BuildTemplateService(TenantDbContext db)
    {
        _db = db;
    }

    // ── CRUD ──────────────────────────────────────────────────

    public async Task<List<BuildTemplate>> GetAllAsync(BuildTemplateStatus? statusFilter = null)
    {
        var query = _db.BuildTemplates
            .Include(t => t.Parts).ThenInclude(p => p.Part)
            .Include(t => t.Material)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(t => t.Status == statusFilter.Value);

        return await query.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<BuildTemplate?> GetByIdAsync(int id)
    {
        return await _db.BuildTemplates
            .Include(t => t.Parts).ThenInclude(p => p.Part)
            .Include(t => t.Material)
            .Include(t => t.SourceBuildPackage)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<BuildTemplate> CreateAsync(BuildTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        template.CreatedDate = DateTime.UtcNow;
        template.LastModifiedDate = DateTime.UtcNow;

        _db.BuildTemplates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task<BuildTemplate> UpdateAsync(BuildTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        template.LastModifiedDate = DateTime.UtcNow;

        _db.BuildTemplates.Update(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task ArchiveAsync(int templateId)
    {
        var template = await _db.BuildTemplates.FindAsync(templateId)
            ?? throw new InvalidOperationException($"BuildTemplate {templateId} not found.");

        template.Status = BuildTemplateStatus.Archived;
        template.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Template Parts ────────────────────────────────────────

    public async Task<BuildTemplatePart> AddPartAsync(
        int templateId, int partId, int quantity, int stackLevel = 1, string? positionNotes = null)
    {
        var template = await _db.BuildTemplates.FindAsync(templateId)
            ?? throw new InvalidOperationException($"BuildTemplate {templateId} not found.");

        if (template.Status == BuildTemplateStatus.Archived)
            throw new InvalidOperationException("Cannot add parts to an archived template.");

        var part = await _db.Parts.FindAsync(partId)
            ?? throw new InvalidOperationException($"Part {partId} not found.");

        var templatePart = new BuildTemplatePart
        {
            BuildTemplateId = templateId,
            PartId = partId,
            Quantity = quantity,
            StackLevel = stackLevel,
            PositionNotes = positionNotes
        };

        _db.BuildTemplateParts.Add(templatePart);

        // Adding a part invalidates certification
        if (template.Status == BuildTemplateStatus.Certified)
            template.NeedsRecertification = true;

        template.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return templatePart;
    }

    public async Task<BuildTemplatePart> UpdatePartAsync(
        int templatePartId, int quantity, int stackLevel, string? positionNotes = null)
    {
        var templatePart = await _db.BuildTemplateParts
            .Include(tp => tp.BuildTemplate)
            .FirstOrDefaultAsync(tp => tp.Id == templatePartId)
            ?? throw new InvalidOperationException($"BuildTemplatePart {templatePartId} not found.");

        if (templatePart.BuildTemplate.Status == BuildTemplateStatus.Archived)
            throw new InvalidOperationException("Cannot modify parts on an archived template.");

        templatePart.Quantity = quantity;
        templatePart.StackLevel = stackLevel;
        templatePart.PositionNotes = positionNotes;

        // Modifying a part invalidates certification
        if (templatePart.BuildTemplate.Status == BuildTemplateStatus.Certified)
            templatePart.BuildTemplate.NeedsRecertification = true;

        templatePart.BuildTemplate.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return templatePart;
    }

    public async Task RemovePartAsync(int templatePartId)
    {
        var templatePart = await _db.BuildTemplateParts
            .Include(tp => tp.BuildTemplate)
            .FirstOrDefaultAsync(tp => tp.Id == templatePartId)
            ?? throw new InvalidOperationException($"BuildTemplatePart {templatePartId} not found.");

        if (templatePart.BuildTemplate.Status == BuildTemplateStatus.Archived)
            throw new InvalidOperationException("Cannot remove parts from an archived template.");

        // Removing a part invalidates certification
        if (templatePart.BuildTemplate.Status == BuildTemplateStatus.Certified)
            templatePart.BuildTemplate.NeedsRecertification = true;

        templatePart.BuildTemplate.LastModifiedDate = DateTime.UtcNow;
        _db.BuildTemplateParts.Remove(templatePart);
        await _db.SaveChangesAsync();
    }

    // ── Certification ─────────────────────────────────────────

    public async Task<BuildTemplate> CertifyAsync(int templateId, string certifiedBy)
    {
        if (string.IsNullOrWhiteSpace(certifiedBy))
            throw new ArgumentException("CertifiedBy is required.", nameof(certifiedBy));

        var template = await _db.BuildTemplates
            .Include(t => t.Parts).ThenInclude(p => p.Part)
            .FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new InvalidOperationException($"BuildTemplate {templateId} not found.");

        if (!template.Parts.Any())
            throw new InvalidOperationException("Cannot certify a template with no parts.");

        if (template.EstimatedDurationHours <= 0)
            throw new InvalidOperationException("Cannot certify a template without a valid estimated duration.");

        var parts = template.Parts.Select(tp => tp.Part).ToList();
        template.PartVersionHash = ComputePartVersionHash(parts);
        template.Status = BuildTemplateStatus.Certified;
        template.CertifiedBy = certifiedBy;
        template.CertifiedDate = DateTime.UtcNow;
        template.NeedsRecertification = false;
        template.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return template;
    }

    public async Task<BuildTemplate> RecertifyAsync(int templateId, string certifiedBy)
    {
        if (string.IsNullOrWhiteSpace(certifiedBy))
            throw new ArgumentException("CertifiedBy is required.", nameof(certifiedBy));

        var template = await _db.BuildTemplates
            .Include(t => t.Parts).ThenInclude(p => p.Part)
            .FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new InvalidOperationException($"BuildTemplate {templateId} not found.");

        if (!template.Parts.Any())
            throw new InvalidOperationException("Cannot recertify a template with no parts.");

        if (template.EstimatedDurationHours <= 0)
            throw new InvalidOperationException("Cannot recertify a template without a valid estimated duration.");

        var parts = template.Parts.Select(tp => tp.Part).ToList();
        template.PartVersionHash = ComputePartVersionHash(parts);
        template.CertifiedBy = certifiedBy;
        template.CertifiedDate = DateTime.UtcNow;
        template.NeedsRecertification = false;
        template.Status = BuildTemplateStatus.Certified;
        template.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return template;
    }

    // ── Instantiation ─────────────────────────────────────────

    public async Task<BuildPackage> InstantiateAsync(
        int templateId, string machineId, string createdBy, int? workOrderLineId = null)
    {
        if (string.IsNullOrWhiteSpace(machineId))
            throw new ArgumentException("MachineId is required.", nameof(machineId));

        var template = await _db.BuildTemplates
            .Include(t => t.Parts).ThenInclude(p => p.Part)
            .Include(t => t.Material)
            .FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new InvalidOperationException($"BuildTemplate {templateId} not found.");

        if (!template.IsCertified)
            throw new InvalidOperationException("Cannot instantiate a template that is not certified or needs recertification.");

        var buildPackage = new BuildPackage
        {
            Name = $"{template.Name} — {DateTime.UtcNow:MMdd-HHmm}",
            MachineId = machineId,
            Status = BuildPackageStatus.Sliced,
            IsSlicerDataEntered = true,
            EstimatedDurationHours = template.EstimatedDurationHours,
            Material = template.Material?.Name,
            BuildParameters = template.BuildParameters,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _db.BuildPackages.Add(buildPackage);
        await _db.SaveChangesAsync();

        var woLineLinked = false;
        foreach (var tp in template.Parts)
        {
            var buildPart = new BuildPackagePart
            {
                BuildPackageId = buildPackage.Id,
                PartId = tp.PartId,
                Quantity = tp.Quantity,
                StackLevel = tp.StackLevel,
                SlicerNotes = tp.PositionNotes
            };

            // Link the first matching part to the WO line
            if (workOrderLineId.HasValue && !woLineLinked)
            {
                buildPart.WorkOrderLineId = workOrderLineId.Value;
                woLineLinked = true;
            }

            _db.BuildPackageParts.Add(buildPart);
        }

        template.UseCount++;
        template.LastUsedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return buildPackage;
    }

    // ── Create from Build ─────────────────────────────────────

    public async Task<BuildTemplate> CreateFromBuildPackageAsync(int buildPackageId, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy is required.", nameof(createdBy));

        var build = await _db.BuildPackages
            .Include(b => b.Parts).ThenInclude(p => p.Part)
            .FirstOrDefaultAsync(b => b.Id == buildPackageId)
            ?? throw new InvalidOperationException($"BuildPackage {buildPackageId} not found.");

        if (build.Status != BuildPackageStatus.Completed)
            throw new InvalidOperationException("Can only create a template from a completed build.");

        var template = new BuildTemplate
        {
            Name = $"Template from {build.Name}",
            EstimatedDurationHours = build.EstimatedDurationHours ?? 0,
            Material = build.Parts.FirstOrDefault()?.Part?.MaterialEntity,
            MaterialId = build.Parts.FirstOrDefault()?.Part?.MaterialId,
            BuildParameters = build.BuildParameters,
            SourceBuildPackageId = buildPackageId,
            Status = BuildTemplateStatus.Draft,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _db.BuildTemplates.Add(template);
        await _db.SaveChangesAsync();

        foreach (var bp in build.Parts)
        {
            _db.BuildTemplateParts.Add(new BuildTemplatePart
            {
                BuildTemplateId = template.Id,
                PartId = bp.PartId,
                Quantity = bp.Quantity,
                StackLevel = bp.StackLevel,
                PositionNotes = bp.SlicerNotes
            });
        }

        await _db.SaveChangesAsync();
        return template;
    }

    // ── Part Lookup ───────────────────────────────────────────

    public async Task<List<BuildTemplate>> GetTemplatesForPartAsync(int partId, bool certifiedOnly = true)
    {
        var query = _db.BuildTemplates
            .Include(t => t.Parts).ThenInclude(p => p.Part)
            .Include(t => t.Material)
            .Where(t => t.Parts.Any(p => p.PartId == partId));

        if (certifiedOnly)
            query = query.Where(t => t.Status == BuildTemplateStatus.Certified && !t.NeedsRecertification);

        return await query.OrderByDescending(t => t.UseCount).ToListAsync();
    }

    public async Task<List<BuildTemplate>> GetTemplatesNeedingRecertificationAsync()
    {
        return await _db.BuildTemplates
            .Include(t => t.Parts).ThenInclude(p => p.Part)
            .Where(t => t.NeedsRecertification && t.Status == BuildTemplateStatus.Certified)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    // ── Invalidation ──────────────────────────────────────────

    public async Task InvalidateTemplatesForPartAsync(int partId)
    {
        var templates = await _db.BuildTemplates
            .Where(t => t.Status == BuildTemplateStatus.Certified
                        && t.Parts.Any(p => p.PartId == partId))
            .ToListAsync();

        foreach (var template in templates)
        {
            template.NeedsRecertification = true;
            template.LastModifiedDate = DateTime.UtcNow;
        }

        if (templates.Count > 0)
            await _db.SaveChangesAsync();
    }

    // ── Hash ──────────────────────────────────────────────────

    public string ComputePartVersionHash(IEnumerable<Part> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var input = string.Join("|", parts
            .OrderBy(p => p.Id)
            .Select(p => $"{p.Id}:{p.LastModifiedDate.Ticks}"));

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..32];
    }
}
