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

    // ── Slicer Metadata ───────────────────────────────────────

    public async Task<BuildTemplate> UpdateSlicerMetadataAsync(
        int templateId, string? fileName, int? layerCount,
        double? buildHeightMm, double? estimatedPowderKg, string? partPositionsJson,
        string? slicerSoftware, string? slicerVersion)
    {
        var template = await _db.BuildTemplates.FindAsync(templateId)
            ?? throw new InvalidOperationException($"BuildTemplate {templateId} not found.");

        if (template.Status == BuildTemplateStatus.Archived)
            throw new InvalidOperationException("Cannot modify an archived build file.");

        template.FileName = fileName;
        template.LayerCount = layerCount;
        template.BuildHeightMm = buildHeightMm;
        template.EstimatedPowderKg = estimatedPowderKg;
        template.PartPositionsJson = partPositionsJson;
        template.SlicerSoftware = slicerSoftware;
        template.SlicerVersion = slicerVersion;

        // Modifying slicer data invalidates certification
        if (template.Status == BuildTemplateStatus.Certified)
            template.NeedsRecertification = true;

        template.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return template;
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

    public async Task<BuildTemplate> RecertifyAsync(int templateId, string certifiedBy, string? changeNotes = null)
    {
        if (string.IsNullOrWhiteSpace(certifiedBy))
            throw new ArgumentException("CertifiedBy is required.", nameof(certifiedBy));

        var template = await _db.BuildTemplates
            .Include(t => t.Parts).ThenInclude(p => p.Part)
            .Include(t => t.Revisions)
            .FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new InvalidOperationException($"BuildTemplate {templateId} not found.");

        if (!template.Parts.Any())
            throw new InvalidOperationException("Cannot recertify a template with no parts.");

        if (template.EstimatedDurationHours <= 0)
            throw new InvalidOperationException("Cannot recertify a template without a valid estimated duration.");

        // Create a revision snapshot of the previous state before recertifying
        var nextRevision = template.Revisions.Count > 0
            ? template.Revisions.Max(r => r.RevisionNumber) + 1
            : 1;

        var partsSnapshot = System.Text.Json.JsonSerializer.Serialize(
            template.Parts.Select(p => new { p.PartId, p.Quantity, p.StackLevel, p.PositionNotes }));

        var slicerSnapshot = System.Text.Json.JsonSerializer.Serialize(new
        {
            template.FileName, template.LayerCount, template.BuildHeightMm,
            template.EstimatedPowderKg, template.EstimatedDurationHours,
            template.SlicerSoftware, template.SlicerVersion
        });

        _db.BuildTemplateRevisions.Add(new BuildTemplateRevision
        {
            BuildTemplateId = template.Id,
            RevisionNumber = nextRevision,
            ChangedBy = certifiedBy,
            ChangeNotes = changeNotes,
            PartsSnapshotJson = partsSnapshot,
            ParametersSnapshotJson = template.BuildParameters,
            SlicerMetadataSnapshotJson = slicerSnapshot,
            RevisionDate = DateTime.UtcNow
        });

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

    // ── Revisions ─────────────────────────────────────────────

    public async Task<List<BuildTemplateRevision>> GetRevisionsAsync(int templateId)
    {
        return await _db.BuildTemplateRevisions
            .Where(r => r.BuildTemplateId == templateId)
            .OrderByDescending(r => r.RevisionNumber)
            .ToListAsync();
    }

    // ── Instantiation ──────────────────────────────────────────

    public async Task<MachineProgram> InstantiateAsync(int templateId, int machineId, string createdBy, int? workOrderLineId = null)
    {
        var template = await _db.BuildTemplates
            .Include(t => t.Parts)
            .FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new InvalidOperationException($"BuildTemplate {templateId} not found.");

        var program = new MachineProgram
        {
            ProgramType = ProgramType.BuildPlate,
            MachineId = machineId,
            ProgramNumber = $"BT-{templateId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Name = template.Name,
            Description = $"Instantiated from template '{template.Name}' (ID {templateId})",
            Version = 1,
            Status = ProgramStatus.Active,
            MaterialId = template.MaterialId,
            LayerCount = template.LayerCount,
            BuildHeightMm = template.BuildHeightMm,
            EstimatedPrintHours = template.EstimatedDurationHours,
            EstimatedPowderKg = template.EstimatedPowderKg,
            SlicerFileName = template.FileName,
            SlicerSoftware = template.SlicerSoftware,
            SlicerVersion = template.SlicerVersion,
            PartPositionsJson = template.PartPositionsJson,
            ScheduleStatus = ProgramScheduleStatus.Ready,
            CreatedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        // Copy template parts to program parts
        foreach (var tp in template.Parts)
        {
            _db.ProgramParts.Add(new ProgramPart
            {
                MachineProgramId = program.Id,
                PartId = tp.PartId,
                Quantity = tp.Quantity,
                StackLevel = tp.StackLevel,
                PositionNotes = tp.PositionNotes,
                WorkOrderLineId = workOrderLineId
            });
        }

        // Update template usage tracking
        template.UseCount++;
        template.LastUsedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return program;
    }
}
