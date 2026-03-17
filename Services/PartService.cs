using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class PartService : IPartService
{
    private readonly TenantDbContext _db;

    public PartService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<Part>> GetAllPartsAsync(bool activeOnly = true)
    {
        var query = _db.Parts.AsQueryable();
        if (activeOnly)
            query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.PartNumber).ToListAsync();
    }

    public async Task<Part?> GetPartByIdAsync(int id)
    {
        return await _db.Parts
            .Include(p => p.StageRequirements)
                .ThenInclude(sr => sr.ProductionStage)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Part?> GetPartByNumberAsync(string partNumber)
    {
        return await _db.Parts
            .Include(p => p.StageRequirements)
            .FirstOrDefaultAsync(p => p.PartNumber == partNumber);
    }

    public async Task<Part> CreatePartAsync(Part part)
    {
        part.CreatedDate = DateTime.UtcNow;
        part.LastModifiedDate = DateTime.UtcNow;

        _db.Parts.Add(part);
        await _db.SaveChangesAsync();
        return part;
    }

    public async Task<Part> UpdatePartAsync(Part part)
    {
        part.LastModifiedDate = DateTime.UtcNow;
        _db.Parts.Update(part);
        await _db.SaveChangesAsync();
        return part;
    }

    public async Task DeletePartAsync(int id)
    {
        var part = await _db.Parts.FindAsync(id);
        if (part == null) throw new InvalidOperationException("Part not found.");
        part.IsActive = false;
        part.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<List<PartStageRequirement>> GetStageRequirementsAsync(int partId)
    {
        return await _db.PartStageRequirements
            .Include(r => r.ProductionStage)
            .Where(r => r.PartId == partId && r.IsActive)
            .OrderBy(r => r.ExecutionOrder)
            .ToListAsync();
    }

    public async Task<PartStageRequirement> AddStageRequirementAsync(PartStageRequirement requirement)
    {
        requirement.CreatedDate = DateTime.UtcNow;
        requirement.LastModifiedDate = DateTime.UtcNow;

        _db.PartStageRequirements.Add(requirement);
        await _db.SaveChangesAsync();
        return requirement;
    }

    public async Task<PartStageRequirement> UpdateStageRequirementAsync(PartStageRequirement requirement)
    {
        requirement.LastModifiedDate = DateTime.UtcNow;
        _db.PartStageRequirements.Update(requirement);
        await _db.SaveChangesAsync();
        return requirement;
    }

    public async Task RemoveStageRequirementAsync(int requirementId)
    {
        var req = await _db.PartStageRequirements.FindAsync(requirementId);
        if (req == null) throw new InvalidOperationException("Stage requirement not found.");
        req.IsActive = false;
        req.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public Task<List<string>> ValidatePartAsync(Part part)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(part.PartNumber))
            errors.Add("Part number is required.");
        if (string.IsNullOrWhiteSpace(part.Name))
            errors.Add("Part name is required.");

        errors.AddRange(part.ValidateStackingConfiguration());

        return Task.FromResult(errors);
    }

    // PDM Extensions

    public async Task<Part?> GetPartDetailAsync(int id)
    {
        return await _db.Parts
            .Include(p => p.StageRequirements)
                .ThenInclude(sr => sr.ProductionStage)
            .Include(p => p.Drawings)
            .Include(p => p.RevisionHistory.OrderByDescending(r => r.RevisionDate))
            .Include(p => p.Notes.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.CreatedDate))
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PartRevisionHistory> BumpRevisionAsync(int partId, string newRevision, string changeDescription, string createdBy)
    {
        var part = await _db.Parts
            .Include(p => p.StageRequirements)
            .FirstOrDefaultAsync(p => p.Id == partId)
            ?? throw new InvalidOperationException("Part not found.");

        var routingSnapshot = System.Text.Json.JsonSerializer.Serialize(
            part.StageRequirements.Select(sr => new { sr.ProductionStageId, sr.ExecutionOrder, sr.IsRequired }).ToList());

        var history = new PartRevisionHistory
        {
            PartId = partId,
            Revision = newRevision,
            PreviousRevision = part.Revision,
            ChangeDescription = changeDescription,
            RawMaterialSpec = part.RawMaterialSpec,
            DrawingNumber = part.DrawingNumber,
            RoutingSnapshot = routingSnapshot,
            RevisionDate = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        part.Revision = newRevision;
        part.RevisionDate = DateTime.UtcNow;
        part.LastModifiedDate = DateTime.UtcNow;
        part.LastModifiedBy = createdBy;

        _db.PartRevisionHistories.Add(history);
        await _db.SaveChangesAsync();
        return history;
    }

    public async Task<List<Part>> SearchPartsAsync(string searchTerm, bool activeOnly = true)
    {
        var query = _db.Parts.AsQueryable();
        if (activeOnly)
            query = query.Where(p => p.IsActive);

        var term = searchTerm.ToLower();
        query = query.Where(p =>
            p.PartNumber.ToLower().Contains(term) ||
            p.Name.ToLower().Contains(term) ||
            (p.CustomerPartNumber != null && p.CustomerPartNumber.ToLower().Contains(term)) ||
            (p.DrawingNumber != null && p.DrawingNumber.ToLower().Contains(term)) ||
            (p.Description != null && p.Description.ToLower().Contains(term)));

        return await query.OrderBy(p => p.PartNumber).Take(50).ToListAsync();
    }

    public async Task<List<PartNote>> GetNotesAsync(int partId)
    {
        return await _db.PartNotes
            .Where(n => n.PartId == partId)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedDate)
            .ToListAsync();
    }

    public async Task<PartNote> AddNoteAsync(PartNote note)
    {
        note.CreatedDate = DateTime.UtcNow;
        _db.PartNotes.Add(note);
        await _db.SaveChangesAsync();
        return note;
    }

    public async Task<PartNote> UpdateNoteAsync(PartNote note)
    {
        note.LastModifiedDate = DateTime.UtcNow;
        _db.PartNotes.Update(note);
        await _db.SaveChangesAsync();
        return note;
    }

    public async Task DeleteNoteAsync(int noteId)
    {
        var note = await _db.PartNotes.FindAsync(noteId);
        if (note == null) throw new InvalidOperationException("Note not found.");
        _db.PartNotes.Remove(note);
        await _db.SaveChangesAsync();
    }
}
