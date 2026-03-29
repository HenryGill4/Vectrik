using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services;
using Vectrik.Tests.Helpers;
using Xunit;

namespace Vectrik.Tests.Services;

public class CertifiedLayoutServiceTests : IDisposable
{
    private readonly Data.TenantDbContext _db;
    private readonly CertifiedLayoutService _sut;

    public CertifiedLayoutServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new CertifiedLayoutService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ─────────────────────────────────────────────

    private async Task<Part> CreateTestPartAsync(string partNumber = "PN-001")
    {
        var part = new Part
        {
            PartNumber = partNumber,
            Name = $"Test Part {partNumber}",
            Material = "PA12",
            CreatedBy = "test-user",
            LastModifiedDate = DateTime.UtcNow
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();
        return part;
    }

    private async Task<CertifiedLayout> CreateTestLayoutAsync(
        int partId, LayoutSize size = LayoutSize.Quadrant, int positions = 10, int stackLevel = 1)
    {
        var layout = new CertifiedLayout
        {
            Name = $"Test Layout {partId}-{size}",
            Size = size,
            PartId = partId,
            Positions = positions,
            StackLevel = stackLevel,
            CreatedBy = "test-user"
        };
        return await _sut.CreateAsync(layout);
    }

    // ── CRUD ────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SetsAuditFields()
    {
        var part = await CreateTestPartAsync();
        var layout = await CreateTestLayoutAsync(part.Id);

        Assert.True(layout.Id > 0);
        Assert.Equal(CertifiedLayoutStatus.Draft, layout.Status);
        Assert.True(layout.CreatedDate <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetAllAsync_FiltersbyStatus()
    {
        var part = await CreateTestPartAsync();
        await CreateTestLayoutAsync(part.Id);
        var certified = await CreateTestLayoutAsync(part.Id, positions: 5);
        await _sut.CertifyAsync(certified.Id, "engineer");

        var drafts = await _sut.GetAllAsync(status: CertifiedLayoutStatus.Draft);
        Assert.Single(drafts);

        var certifiedList = await _sut.GetAllAsync(status: CertifiedLayoutStatus.Certified);
        Assert.Single(certifiedList);
    }

    [Fact]
    public async Task GetAllAsync_FiltersBySize()
    {
        var part = await CreateTestPartAsync();
        await CreateTestLayoutAsync(part.Id, LayoutSize.Quadrant);
        await CreateTestLayoutAsync(part.Id, LayoutSize.Half);

        var quadrants = await _sut.GetAllAsync(size: LayoutSize.Quadrant);
        Assert.Single(quadrants);

        var halves = await _sut.GetAllAsync(size: LayoutSize.Half);
        Assert.Single(halves);
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesCertification()
    {
        var part = await CreateTestPartAsync();
        var layout = await CreateTestLayoutAsync(part.Id);
        await _sut.CertifyAsync(layout.Id, "engineer");

        layout.Positions = 20;
        await _sut.UpdateAsync(layout);

        var updated = await _sut.GetByIdAsync(layout.Id);
        Assert.True(updated!.NeedsRecertification);
    }

    [Fact]
    public async Task ArchiveAsync_SetsStatusToArchived()
    {
        var part = await CreateTestPartAsync();
        var layout = await CreateTestLayoutAsync(part.Id);

        await _sut.ArchiveAsync(layout.Id);

        var archived = await _sut.GetByIdAsync(layout.Id);
        Assert.Equal(CertifiedLayoutStatus.Archived, archived!.Status);
    }

    // ── Certification ───────────────────────────────────────

    [Fact]
    public async Task CertifyAsync_SetsCertifiedStatus()
    {
        var part = await CreateTestPartAsync();
        var layout = await CreateTestLayoutAsync(part.Id);

        var certified = await _sut.CertifyAsync(layout.Id, "engineer-a");

        Assert.Equal(CertifiedLayoutStatus.Certified, certified.Status);
        Assert.Equal("engineer-a", certified.CertifiedBy);
        Assert.NotNull(certified.CertifiedDate);
        Assert.NotNull(certified.PartVersionHash);
        Assert.False(certified.NeedsRecertification);
        Assert.True(certified.IsCertified);
    }

    [Fact]
    public async Task CertifyAsync_FailsWithZeroPositions()
    {
        var part = await CreateTestPartAsync();
        var layout = new CertifiedLayout
        {
            Name = "Zero pos",
            PartId = part.Id,
            Positions = 0,
            CreatedBy = "test"
        };
        _db.CertifiedLayouts.Add(layout);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CertifyAsync(layout.Id, "engineer"));
    }

    [Fact]
    public async Task RecertifyAsync_CreatesRevisionSnapshot()
    {
        var part = await CreateTestPartAsync();
        var layout = await CreateTestLayoutAsync(part.Id, positions: 10);
        await _sut.CertifyAsync(layout.Id, "engineer");

        // Modify and recertify
        layout.Positions = 15;
        layout.NeedsRecertification = true;
        _db.CertifiedLayouts.Update(layout);
        await _db.SaveChangesAsync();

        var recertified = await _sut.RecertifyAsync(layout.Id, "engineer-b", "Increased positions");

        Assert.False(recertified.NeedsRecertification);
        Assert.Equal("engineer-b", recertified.CertifiedBy);

        var revisions = await _sut.GetRevisionsAsync(layout.Id);
        Assert.Single(revisions);
        Assert.Equal(1, revisions[0].RevisionNumber);
        Assert.Equal("Increased positions", revisions[0].ChangeNotes);
        Assert.Equal(15, revisions[0].PreviousPositions); // Snapshot of state at recertification time
    }

    // ── Invalidation ────────────────────────────────────────

    [Fact]
    public async Task InvalidateLayoutsForPartAsync_MarksAsNeedsRecertification()
    {
        var part = await CreateTestPartAsync();
        var layout = await CreateTestLayoutAsync(part.Id);
        await _sut.CertifyAsync(layout.Id, "engineer");

        await _sut.InvalidateLayoutsForPartAsync(part.Id);

        var invalidated = await _sut.GetByIdAsync(layout.Id);
        Assert.True(invalidated!.NeedsRecertification);
        Assert.False(invalidated.IsCertified);
    }

    [Fact]
    public async Task InvalidateLayoutsForPartAsync_IgnoresDraftLayouts()
    {
        var part = await CreateTestPartAsync();
        var layout = await CreateTestLayoutAsync(part.Id);

        await _sut.InvalidateLayoutsForPartAsync(part.Id);

        var unchanged = await _sut.GetByIdAsync(layout.Id);
        Assert.False(unchanged!.NeedsRecertification); // Draft, not certified
    }

    // ── Plate Composition Validation ────────────────────────

    [Fact]
    public async Task ValidatePlateCompositionAsync_AcceptsValidQuadrants()
    {
        var part = await CreateTestPartAsync();
        var l1 = await CreateTestLayoutAsync(part.Id, LayoutSize.Quadrant);
        var l2 = await CreateTestLayoutAsync(part.Id, LayoutSize.Quadrant, positions: 5);
        await _sut.CertifyAsync(l1.Id, "eng");
        await _sut.CertifyAsync(l2.Id, "eng");

        var errors = await _sut.ValidatePlateCompositionAsync(
        [
            new PlateSlotAssignment(l1.Id, [0]),
            new PlateSlotAssignment(l2.Id, [1]),
        ]);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidatePlateCompositionAsync_AcceptsHalfPlusQuadrants()
    {
        var part = await CreateTestPartAsync();
        var half = await CreateTestLayoutAsync(part.Id, LayoutSize.Half, positions: 20);
        var quad = await CreateTestLayoutAsync(part.Id, LayoutSize.Quadrant, positions: 5);
        await _sut.CertifyAsync(half.Id, "eng");
        await _sut.CertifyAsync(quad.Id, "eng");

        var errors = await _sut.ValidatePlateCompositionAsync(
        [
            new PlateSlotAssignment(half.Id, [0, 2]),  // Left half
            new PlateSlotAssignment(quad.Id, [1]),
        ]);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidatePlateCompositionAsync_RejectsOverlappingSlots()
    {
        var part = await CreateTestPartAsync();
        var l1 = await CreateTestLayoutAsync(part.Id);
        var l2 = await CreateTestLayoutAsync(part.Id, positions: 5);
        await _sut.CertifyAsync(l1.Id, "eng");
        await _sut.CertifyAsync(l2.Id, "eng");

        var errors = await _sut.ValidatePlateCompositionAsync(
        [
            new PlateSlotAssignment(l1.Id, [0]),
            new PlateSlotAssignment(l2.Id, [0]),  // Same slot!
        ]);

        Assert.Contains(errors, e => e.Contains("overlapping"));
    }

    [Fact]
    public async Task ValidatePlateCompositionAsync_RejectsNonAdjacentHalf()
    {
        var part = await CreateTestPartAsync();
        var half = await CreateTestLayoutAsync(part.Id, LayoutSize.Half);
        await _sut.CertifyAsync(half.Id, "eng");

        var errors = await _sut.ValidatePlateCompositionAsync(
        [
            new PlateSlotAssignment(half.Id, [0, 3]),  // Diagonal — not adjacent!
        ]);

        Assert.Contains(errors, e => e.Contains("adjacent"));
    }

    [Fact]
    public async Task ValidatePlateCompositionAsync_RejectsUncertifiedLayout()
    {
        var part = await CreateTestPartAsync();
        var layout = await CreateTestLayoutAsync(part.Id);  // Draft, not certified

        var errors = await _sut.ValidatePlateCompositionAsync(
        [
            new PlateSlotAssignment(layout.Id, [0]),
        ]);

        Assert.Contains(errors, e => e.Contains("not certified"));
    }

    [Fact]
    public async Task ValidatePlateCompositionAsync_RejectsStaleLayout()
    {
        var part = await CreateTestPartAsync();
        var layout = await CreateTestLayoutAsync(part.Id);
        await _sut.CertifyAsync(layout.Id, "eng");
        await _sut.InvalidateLayoutsForPartAsync(part.Id);

        var errors = await _sut.ValidatePlateCompositionAsync(
        [
            new PlateSlotAssignment(layout.Id, [0]),
        ]);

        Assert.Contains(errors, e => e.Contains("recertification"));
    }

    [Fact]
    public async Task ValidatePlateCompositionAsync_RejectsEmptyComposition()
    {
        var errors = await _sut.ValidatePlateCompositionAsync([]);
        Assert.Contains(errors, e => e.Contains("At least one"));
    }

    // ── Queries ─────────────────────────────────────────────

    [Fact]
    public async Task GetCertifiedAsync_ReturnsOnlyCertifiedNonStale()
    {
        var part = await CreateTestPartAsync();
        var l1 = await CreateTestLayoutAsync(part.Id);
        var l2 = await CreateTestLayoutAsync(part.Id, positions: 5);
        var l3 = await CreateTestLayoutAsync(part.Id, positions: 8);
        await _sut.CertifyAsync(l1.Id, "eng");
        await _sut.CertifyAsync(l2.Id, "eng");
        // l3 stays as Draft

        // Invalidate l1
        await _sut.InvalidateLayoutsForPartAsync(part.Id);

        var certified = await _sut.GetCertifiedAsync();
        Assert.Empty(certified); // Both certified ones are now NeedsRecertification
    }

    [Fact]
    public async Task GetCertifiedForPartAsync_FiltersCorrectly()
    {
        var part1 = await CreateTestPartAsync("PN-001");
        var part2 = await CreateTestPartAsync("PN-002");
        var l1 = await CreateTestLayoutAsync(part1.Id);
        var l2 = await CreateTestLayoutAsync(part2.Id);
        await _sut.CertifyAsync(l1.Id, "eng");
        await _sut.CertifyAsync(l2.Id, "eng");

        var results = await _sut.GetCertifiedForPartAsync(part1.Id);
        Assert.Single(results);
        Assert.Equal(part1.Id, results[0].PartId);
    }

    // ── Computed Properties ─────────────────────────────────

    [Fact]
    public void TotalParts_CalculatesCorrectly()
    {
        var layout = new CertifiedLayout { Positions = 10, StackLevel = 2 };
        Assert.Equal(20, layout.TotalParts);
    }

    [Fact]
    public void SlotCount_ReturnsCorrectValue()
    {
        var quad = new CertifiedLayout { Size = LayoutSize.Quadrant };
        var half = new CertifiedLayout { Size = LayoutSize.Half };
        Assert.Equal(1, quad.SlotCount);
        Assert.Equal(2, half.SlotCount);
    }
}
