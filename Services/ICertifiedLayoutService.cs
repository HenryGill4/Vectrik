using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface ICertifiedLayoutService
{
    // CRUD
    Task<List<CertifiedLayout>> GetAllAsync(CertifiedLayoutStatus? status = null, LayoutSize? size = null);
    Task<CertifiedLayout?> GetByIdAsync(int id);
    Task<CertifiedLayout> CreateAsync(CertifiedLayout layout);
    Task<CertifiedLayout> UpdateAsync(CertifiedLayout layout);
    Task ArchiveAsync(int layoutId);

    // Certification
    Task<CertifiedLayout> CertifyAsync(int layoutId, string certifiedBy);
    Task<CertifiedLayout> RecertifyAsync(int layoutId, string certifiedBy, string? changeNotes = null);

    // Queries
    Task<List<CertifiedLayout>> GetCertifiedAsync(LayoutSize? size = null, int? materialId = null);
    Task<List<CertifiedLayout>> GetCertifiedForPartAsync(int partId);
    Task<List<CertifiedLayout>> GetLayoutsNeedingRecertificationAsync();

    // Invalidation (called when Part is modified)
    Task InvalidateLayoutsForPartAsync(int partId);

    // Plate composition validation
    Task<List<string>> ValidatePlateCompositionAsync(List<PlateSlotAssignment> assignments);

    // Revision history
    Task<List<CertifiedLayoutRevision>> GetRevisionsAsync(int layoutId);
}

/// <summary>
/// Represents a certified layout placed on specific plate slots.
/// Quadrant layouts occupy 1 slot, Half layouts occupy 2 adjacent slots.
/// </summary>
public record PlateSlotAssignment(int CertifiedLayoutId, int[] Slots);
