using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;
using Opcentrix_V3.Tests.Helpers;
using Xunit;

namespace Opcentrix_V3.Tests.Services;

public class BuildTemplateServiceTests : IDisposable
{
    private readonly Data.TenantDbContext _db;
    private readonly BuildTemplateService _sut;

    public BuildTemplateServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new BuildTemplateService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────

    private Part CreateTestPart(string partNumber = "PN-001", string name = "Test Part") =>
        new()
        {
            PartNumber = partNumber,
            Name = name,
            Material = "Ti-6Al-4V",
            CreatedBy = "test-user",
            LastModifiedBy = "test-user"
        };

    private async Task<Part> SavePartAsync(string partNumber = "PN-001", string name = "Test Part")
    {
        var part = CreateTestPart(partNumber, name);
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();
        return part;
    }

    private BuildTemplate CreateTestTemplate(string name = "Test Template", double duration = 8.0) =>
        new()
        {
            Name = name,
            EstimatedDurationHours = duration,
            CreatedBy = "test-user",
            LastModifiedBy = "test-user"
        };

    private async Task<Machine> SeedMachineAsync(string machineId = "M4-1", string name = "Machine 4-1")
    {
        var machine = new Machine
        {
            MachineId = machineId,
            Name = name,
            MachineType = "SLS",
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task<BuildTemplate> CreateCertifiedTemplateAsync(string name = "Certified Template")
    {
        var part = await SavePartAsync();
        var template = CreateTestTemplate(name);
        template = await _sut.CreateAsync(template);
        await _sut.AddPartAsync(template.Id, part.Id, 10);
        return await _sut.CertifyAsync(template.Id, "certifier");
    }

    // ── CreateAsync ───────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsTemplate()
    {
        var template = CreateTestTemplate();

        var result = await _sut.CreateAsync(template);

        Assert.True(result.Id > 0);
        Assert.Equal("Test Template", result.Name);
        Assert.Equal(BuildTemplateStatus.Draft, result.Status);
        Assert.Equal(1, await _db.BuildTemplates.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_SetsAuditDates()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await _sut.CreateAsync(CreateTestTemplate());

        Assert.True(result.CreatedDate >= before);
        Assert.True(result.LastModifiedDate >= before);
    }

    // ── GetAllAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllTemplates()
    {
        await _sut.CreateAsync(CreateTestTemplate("Template A"));
        await _sut.CreateAsync(CreateTestTemplate("Template B"));

        var result = await _sut.GetAllAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilterByStatus_ReturnsOnlyMatchingTemplates()
    {
        await _sut.CreateAsync(CreateTestTemplate("Draft"));
        var certified = await CreateCertifiedTemplateAsync("Certified");

        var result = await _sut.GetAllAsync(BuildTemplateStatus.Certified);

        Assert.Single(result);
        Assert.Equal(certified.Id, result[0].Id);
    }

    // ── GetByIdAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsTemplateWithParts()
    {
        var part = await SavePartAsync();
        var template = await _sut.CreateAsync(CreateTestTemplate());
        await _sut.AddPartAsync(template.Id, part.Id, 5);

        var result = await _sut.GetByIdAsync(template.Id);

        Assert.NotNull(result);
        Assert.Single(result.Parts);
        Assert.Equal(5, result.Parts.First().Quantity);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(999);

        Assert.Null(result);
    }

    // ── UpdateAsync ───────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        var template = await _sut.CreateAsync(CreateTestTemplate());
        template.Name = "Updated Name";
        template.EstimatedDurationHours = 12.0;

        var result = await _sut.UpdateAsync(template);

        Assert.Equal("Updated Name", result.Name);
        Assert.Equal(12.0, result.EstimatedDurationHours);
    }

    // ── ArchiveAsync ──────────────────────────────────────────

    [Fact]
    public async Task ArchiveAsync_SetsStatusToArchived()
    {
        var template = await _sut.CreateAsync(CreateTestTemplate());

        await _sut.ArchiveAsync(template.Id);

        var result = await _sut.GetByIdAsync(template.Id);
        Assert.Equal(BuildTemplateStatus.Archived, result!.Status);
    }

    [Fact]
    public async Task ArchiveAsync_WhenNotFound_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ArchiveAsync(999));
    }

    // ── AddPartAsync ──────────────────────────────────────────

    [Fact]
    public async Task AddPartAsync_AddsPartToTemplate()
    {
        var part = await SavePartAsync();
        var template = await _sut.CreateAsync(CreateTestTemplate());

        var result = await _sut.AddPartAsync(template.Id, part.Id, 10, 2, "Top position");

        Assert.True(result.Id > 0);
        Assert.Equal(10, result.Quantity);
        Assert.Equal(2, result.StackLevel);
        Assert.Equal("Top position", result.PositionNotes);
    }

    [Fact]
    public async Task AddPartAsync_WhenTemplateArchived_Throws()
    {
        var part = await SavePartAsync();
        var template = await _sut.CreateAsync(CreateTestTemplate());
        await _sut.ArchiveAsync(template.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddPartAsync(template.Id, part.Id, 5));
    }

    [Fact]
    public async Task AddPartAsync_WhenTemplateCertified_SetsNeedsRecertification()
    {
        var certified = await CreateCertifiedTemplateAsync();
        var extraPart = await SavePartAsync("PN-002", "Extra Part");

        await _sut.AddPartAsync(certified.Id, extraPart.Id, 5);

        var result = await _sut.GetByIdAsync(certified.Id);
        Assert.True(result!.NeedsRecertification);
    }

    // ── UpdatePartAsync ───────────────────────────────────────

    [Fact]
    public async Task UpdatePartAsync_UpdatesQuantityAndStackLevel()
    {
        var part = await SavePartAsync();
        var template = await _sut.CreateAsync(CreateTestTemplate());
        var tp = await _sut.AddPartAsync(template.Id, part.Id, 5);

        var result = await _sut.UpdatePartAsync(tp.Id, 20, 3, "Changed notes");

        Assert.Equal(20, result.Quantity);
        Assert.Equal(3, result.StackLevel);
        Assert.Equal("Changed notes", result.PositionNotes);
    }

    // ── RemovePartAsync ───────────────────────────────────────

    [Fact]
    public async Task RemovePartAsync_RemovesPartFromTemplate()
    {
        var part = await SavePartAsync();
        var template = await _sut.CreateAsync(CreateTestTemplate());
        var tp = await _sut.AddPartAsync(template.Id, part.Id, 5);

        await _sut.RemovePartAsync(tp.Id);

        Assert.Equal(0, await _db.BuildTemplateParts.CountAsync());
    }

    // ── CertifyAsync ──────────────────────────────────────────

    [Fact]
    public async Task CertifyAsync_SetsCertifiedStatusAndHash()
    {
        var part = await SavePartAsync();
        var template = await _sut.CreateAsync(CreateTestTemplate());
        await _sut.AddPartAsync(template.Id, part.Id, 10);

        var result = await _sut.CertifyAsync(template.Id, "certifier");

        Assert.Equal(BuildTemplateStatus.Certified, result.Status);
        Assert.Equal("certifier", result.CertifiedBy);
        Assert.NotNull(result.CertifiedDate);
        Assert.NotNull(result.PartVersionHash);
        Assert.False(result.NeedsRecertification);
        Assert.True(result.IsCertified);
    }

    [Fact]
    public async Task CertifyAsync_WhenNoParts_Throws()
    {
        var template = await _sut.CreateAsync(CreateTestTemplate());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CertifyAsync(template.Id, "certifier"));
    }

    [Fact]
    public async Task CertifyAsync_WhenZeroDuration_Throws()
    {
        var part = await SavePartAsync();
        var template = await _sut.CreateAsync(CreateTestTemplate("Zero Duration", 0));
        await _sut.AddPartAsync(template.Id, part.Id, 10);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CertifyAsync(template.Id, "certifier"));
    }

    // ── RecertifyAsync ────────────────────────────────────────

    [Fact]
    public async Task RecertifyAsync_ClearsRecertificationFlagAndUpdatesHash()
    {
        var certified = await CreateCertifiedTemplateAsync();
        var oldHash = certified.PartVersionHash;

        // Simulate part modification by updating LastModifiedDate
        var part = certified.Parts.First().Part;
        part.LastModifiedDate = DateTime.UtcNow.AddMinutes(1);
        _db.Parts.Update(part);
        await _db.SaveChangesAsync();

        certified.NeedsRecertification = true;
        await _db.SaveChangesAsync();

        var result = await _sut.RecertifyAsync(certified.Id, "recertifier");

        Assert.False(result.NeedsRecertification);
        Assert.Equal(BuildTemplateStatus.Certified, result.Status);
        Assert.Equal("recertifier", result.CertifiedBy);
        Assert.NotEqual(oldHash, result.PartVersionHash);
    }

    // ── GetTemplatesForPartAsync ──────────────────────────────

    [Fact]
    public async Task GetTemplatesForPartAsync_ReturnsCertifiedTemplatesContainingPart()
    {
        var certified = await CreateCertifiedTemplateAsync();
        var partId = certified.Parts.First().PartId;

        var result = await _sut.GetTemplatesForPartAsync(partId, certifiedOnly: true);

        Assert.Single(result);
        Assert.Equal(certified.Id, result[0].Id);
    }

    [Fact]
    public async Task GetTemplatesForPartAsync_WhenCertifiedOnly_ExcludesDrafts()
    {
        var part = await SavePartAsync();
        var draft = await _sut.CreateAsync(CreateTestTemplate("Draft"));
        await _sut.AddPartAsync(draft.Id, part.Id, 5);

        var result = await _sut.GetTemplatesForPartAsync(part.Id, certifiedOnly: true);

        Assert.Empty(result);
    }

    // ── GetTemplatesNeedingRecertificationAsync ───────────────

    [Fact]
    public async Task GetTemplatesNeedingRecertificationAsync_ReturnsInvalidatedTemplates()
    {
        var certified = await CreateCertifiedTemplateAsync();
        var partId = certified.Parts.First().PartId;

        await _sut.InvalidateTemplatesForPartAsync(partId);

        var result = await _sut.GetTemplatesNeedingRecertificationAsync();

        Assert.Single(result);
        Assert.Equal(certified.Id, result[0].Id);
    }

    // ── InvalidateTemplatesForPartAsync ───────────────────────

    [Fact]
    public async Task InvalidateTemplatesForPartAsync_FlagsCertifiedTemplatesContainingPart()
    {
        var certified = await CreateCertifiedTemplateAsync();
        var partId = certified.Parts.First().PartId;

        await _sut.InvalidateTemplatesForPartAsync(partId);

        var template = await _sut.GetByIdAsync(certified.Id);
        Assert.True(template!.NeedsRecertification);
        Assert.False(template.IsCertified);
    }

    [Fact]
    public async Task InvalidateTemplatesForPartAsync_DoesNotAffectDraftTemplates()
    {
        var part = await SavePartAsync();
        var draft = await _sut.CreateAsync(CreateTestTemplate("Draft"));
        await _sut.AddPartAsync(draft.Id, part.Id, 5);

        await _sut.InvalidateTemplatesForPartAsync(part.Id);

        var template = await _sut.GetByIdAsync(draft.Id);
        Assert.False(template!.NeedsRecertification);
    }

    // ── ComputePartVersionHash ────────────────────────────────

    [Fact]
    public void ComputePartVersionHash_ReturnsDeterministicHash()
    {
        var parts = new[]
        {
            new Part { Id = 1, LastModifiedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Part { Id = 2, LastModifiedDate = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc) }
        };

        var hash1 = _sut.ComputePartVersionHash(parts);
        var hash2 = _sut.ComputePartVersionHash(parts);

        Assert.Equal(hash1, hash2);
        Assert.Equal(32, hash1.Length);
    }

    [Fact]
    public void ComputePartVersionHash_ChangesWhenPartModified()
    {
        var parts = new[]
        {
            new Part { Id = 1, LastModifiedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        };

        var hash1 = _sut.ComputePartVersionHash(parts);

        parts[0].LastModifiedDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var hash2 = _sut.ComputePartVersionHash(parts);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputePartVersionHash_OrderIndependent()
    {
        var partsAsc = new[]
        {
            new Part { Id = 1, LastModifiedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Part { Id = 2, LastModifiedDate = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc) }
        };
        var partsDesc = new[]
        {
            new Part { Id = 2, LastModifiedDate = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
            new Part { Id = 1, LastModifiedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        };

        var hash1 = _sut.ComputePartVersionHash(partsAsc);
        var hash2 = _sut.ComputePartVersionHash(partsDesc);

        Assert.Equal(hash1, hash2);
    }
}
