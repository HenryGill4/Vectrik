using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class CertifiedLayoutService : ICertifiedLayoutService
{
    private readonly TenantDbContext _db;

    // Valid adjacent slot pairs for half layouts
    private static readonly int[][] ValidHalfPairs =
    [
        [0, 1], // Top row
        [2, 3], // Bottom row
        [0, 2], // Left column
        [1, 3], // Right column
    ];

    public CertifiedLayoutService(TenantDbContext db)
    {
        _db = db;
    }

    // ── CRUD ──────────────────────────────────────────────────

    public async Task<List<CertifiedLayout>> GetAllAsync(CertifiedLayoutStatus? status = null, LayoutSize? size = null)
    {
        var query = _db.CertifiedLayouts
            .Include(l => l.Part)
            .Include(l => l.Material)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);

        if (size.HasValue)
            query = query.Where(l => l.Size == size.Value);

        return await query.OrderBy(l => l.Name).ToListAsync();
    }

    public async Task<CertifiedLayout?> GetByIdAsync(int id)
    {
        return await _db.CertifiedLayouts
            .Include(l => l.Part)
            .Include(l => l.Material)
            .Include(l => l.Revisions)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<CertifiedLayout> CreateAsync(CertifiedLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        layout.CreatedDate = DateTime.UtcNow;
        layout.LastModifiedDate = DateTime.UtcNow;

        _db.CertifiedLayouts.Add(layout);
        await _db.SaveChangesAsync();
        return layout;
    }

    public async Task<CertifiedLayout> UpdateAsync(CertifiedLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        // Modifying a certified layout invalidates certification
        if (layout.Status == CertifiedLayoutStatus.Certified)
            layout.NeedsRecertification = true;

        layout.LastModifiedDate = DateTime.UtcNow;

        _db.CertifiedLayouts.Update(layout);
        await _db.SaveChangesAsync();
        return layout;
    }

    public async Task ArchiveAsync(int layoutId)
    {
        var layout = await _db.CertifiedLayouts.FindAsync(layoutId)
            ?? throw new InvalidOperationException($"CertifiedLayout {layoutId} not found.");

        layout.Status = CertifiedLayoutStatus.Archived;
        layout.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Certification ────────────────────────────────────────

    public async Task<CertifiedLayout> CertifyAsync(int layoutId, string certifiedBy)
    {
        if (string.IsNullOrWhiteSpace(certifiedBy))
            throw new ArgumentException("CertifiedBy is required.", nameof(certifiedBy));

        var layout = await _db.CertifiedLayouts
            .Include(l => l.Part)
            .FirstOrDefaultAsync(l => l.Id == layoutId)
            ?? throw new InvalidOperationException($"CertifiedLayout {layoutId} not found.");

        if (layout.Part == null)
            throw new InvalidOperationException("Cannot certify a layout with no Part.");

        if (layout.Positions < 1)
            throw new InvalidOperationException("Cannot certify a layout with zero positions.");

        layout.PartVersionHash = ComputePartVersionHash(layout.Part);
        layout.Status = CertifiedLayoutStatus.Certified;
        layout.CertifiedBy = certifiedBy;
        layout.CertifiedDate = DateTime.UtcNow;
        layout.NeedsRecertification = false;
        layout.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return layout;
    }

    public async Task<CertifiedLayout> RecertifyAsync(int layoutId, string certifiedBy, string? changeNotes = null)
    {
        if (string.IsNullOrWhiteSpace(certifiedBy))
            throw new ArgumentException("CertifiedBy is required.", nameof(certifiedBy));

        var layout = await _db.CertifiedLayouts
            .Include(l => l.Part)
            .Include(l => l.Revisions)
            .FirstOrDefaultAsync(l => l.Id == layoutId)
            ?? throw new InvalidOperationException($"CertifiedLayout {layoutId} not found.");

        if (layout.Part == null)
            throw new InvalidOperationException("Cannot recertify a layout with no Part.");

        // Create revision snapshot of the previous state
        var nextRevision = layout.Revisions.Count > 0
            ? layout.Revisions.Max(r => r.RevisionNumber) + 1
            : 1;

        var snapshot = JsonSerializer.Serialize(new
        {
            layout.PartId,
            layout.Positions,
            layout.StackLevel,
            layout.Size,
            layout.Notes,
            layout.MaterialId,
            layout.PartVersionHash
        });

        _db.CertifiedLayoutRevisions.Add(new CertifiedLayoutRevision
        {
            CertifiedLayoutId = layout.Id,
            RevisionNumber = nextRevision,
            ChangedBy = certifiedBy,
            ChangeNotes = changeNotes,
            PreviousPartId = layout.PartId,
            PreviousPositions = layout.Positions,
            PreviousStackLevel = layout.StackLevel,
            PreviousNotes = layout.Notes,
            SnapshotJson = snapshot,
            RevisionDate = DateTime.UtcNow
        });

        layout.PartVersionHash = ComputePartVersionHash(layout.Part);
        layout.CertifiedBy = certifiedBy;
        layout.CertifiedDate = DateTime.UtcNow;
        layout.NeedsRecertification = false;
        layout.Status = CertifiedLayoutStatus.Certified;
        layout.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return layout;
    }

    // ── Queries ──────────────────────────────────────────────

    public async Task<List<CertifiedLayout>> GetCertifiedAsync(LayoutSize? size = null, int? materialId = null)
    {
        var query = _db.CertifiedLayouts
            .Include(l => l.Part)
            .Include(l => l.Material)
            .Where(l => l.Status == CertifiedLayoutStatus.Certified && !l.NeedsRecertification);

        if (size.HasValue)
            query = query.Where(l => l.Size == size.Value);

        if (materialId.HasValue)
            query = query.Where(l => l.MaterialId == materialId.Value || l.MaterialId == null);

        return await query.OrderBy(l => l.Name).ToListAsync();
    }

    public async Task<List<CertifiedLayout>> GetCertifiedForPartAsync(int partId)
    {
        return await _db.CertifiedLayouts
            .Include(l => l.Part)
            .Include(l => l.Material)
            .Where(l => l.PartId == partId
                && l.Status == CertifiedLayoutStatus.Certified
                && !l.NeedsRecertification)
            .OrderByDescending(l => l.UseCount)
            .ToListAsync();
    }

    public async Task<List<CertifiedLayout>> GetLayoutsNeedingRecertificationAsync()
    {
        return await _db.CertifiedLayouts
            .Include(l => l.Part)
            .Where(l => l.NeedsRecertification && l.Status == CertifiedLayoutStatus.Certified)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    // ── Invalidation ─────────────────────────────────────────

    public async Task InvalidateLayoutsForPartAsync(int partId)
    {
        var layouts = await _db.CertifiedLayouts
            .Where(l => l.Status == CertifiedLayoutStatus.Certified
                && l.PartId == partId)
            .ToListAsync();

        foreach (var layout in layouts)
        {
            layout.NeedsRecertification = true;
            layout.LastModifiedDate = DateTime.UtcNow;
        }

        if (layouts.Count > 0)
            await _db.SaveChangesAsync();
    }

    // ── Plate Composition Validation ─────────────────────────

    public async Task<List<string>> ValidatePlateCompositionAsync(List<PlateSlotAssignment> assignments)
    {
        var errors = new List<string>();

        if (assignments.Count == 0)
        {
            errors.Add("At least one layout must be assigned to the plate.");
            return errors;
        }

        // Check for overlapping slots
        var allSlots = assignments.SelectMany(a => a.Slots).ToList();
        if (allSlots.Distinct().Count() != allSlots.Count)
            errors.Add("Plate has overlapping slot assignments.");

        // Check total slots <= 4
        if (allSlots.Count > 4)
            errors.Add("Plate cannot use more than 4 slots.");

        // Check all slots are valid (0-3)
        if (allSlots.Any(s => s < 0 || s > 3))
            errors.Add("Invalid slot index. Slots must be 0-3.");

        // Load all referenced layouts
        var layoutIds = assignments.Select(a => a.CertifiedLayoutId).Distinct().ToList();
        var layouts = await _db.CertifiedLayouts
            .Include(l => l.Part)
            .Include(l => l.Material)
            .Where(l => layoutIds.Contains(l.Id))
            .ToListAsync();

        var layoutMap = layouts.ToDictionary(l => l.Id);

        foreach (var assignment in assignments)
        {
            if (!layoutMap.TryGetValue(assignment.CertifiedLayoutId, out var layout))
            {
                errors.Add($"Layout ID {assignment.CertifiedLayoutId} not found.");
                continue;
            }

            // Check certification status
            if (layout.Status != CertifiedLayoutStatus.Certified)
                errors.Add($"Layout '{layout.Name}' is not certified (status: {layout.Status}).");
            else if (layout.NeedsRecertification)
                errors.Add($"Layout '{layout.Name}' needs recertification.");

            // Check half layout slot adjacency
            if (layout.Size == LayoutSize.Half)
            {
                if (assignment.Slots.Length != 2)
                {
                    errors.Add($"Half layout '{layout.Name}' must occupy exactly 2 slots.");
                }
                else
                {
                    var sorted = assignment.Slots.OrderBy(s => s).ToArray();
                    if (!ValidHalfPairs.Any(p => p[0] == sorted[0] && p[1] == sorted[1]))
                        errors.Add($"Half layout '{layout.Name}' must occupy 2 adjacent slots (top/bottom row or left/right column).");
                }
            }
            else // Quadrant
            {
                if (assignment.Slots.Length != 1)
                    errors.Add($"Quadrant layout '{layout.Name}' must occupy exactly 1 slot.");
            }
        }

        // Check material compatibility (all layouts should share the same material, or have no constraint)
        var materialsUsed = layouts
            .Where(l => l.MaterialId.HasValue)
            .Select(l => l.MaterialId!.Value)
            .Distinct()
            .ToList();

        if (materialsUsed.Count > 1)
            errors.Add("All layouts on a plate must share the same material.");

        return errors;
    }

    // ── Revisions ────────────────────────────────────────────

    public async Task<List<CertifiedLayoutRevision>> GetRevisionsAsync(int layoutId)
    {
        return await _db.CertifiedLayoutRevisions
            .Where(r => r.CertifiedLayoutId == layoutId)
            .OrderByDescending(r => r.RevisionNumber)
            .ToListAsync();
    }

    // ── Hash ─────────────────────────────────────────────────

    private static string ComputePartVersionHash(Part part)
    {
        var input = $"{part.Id}:{part.LastModifiedDate.Ticks}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..32];
    }
}
