using Microsoft.EntityFrameworkCore;
using Vectrik.Models;
using Vectrik.Services;
using Vectrik.Tests.Helpers;
using Xunit;

namespace Vectrik.Tests.Services;

public class PartServiceTests : IDisposable
{
    private readonly Data.TenantDbContext _db;
    private readonly PartService _sut;

    public PartServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new PartService(_db, new BuildTemplateService(_db), new CertifiedLayoutService(_db));
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────

    private Part CreateTestPart(string partNumber = "PN-001", string name = "Test Part") =>
        new()
        {
            PartNumber = partNumber,
            Name = name,
            Material = "Ti-6Al-4V Grade 5",
            CreatedBy = "test-user",
            LastModifiedBy = "test-user"
        };

    private ProductionStage CreateTestStage(string name = "SLS Printing", string slug = "sls-printing") =>
        new()
        {
            Name = name,
            StageSlug = slug,
            CreatedBy = "test-user",
            LastModifiedBy = "test-user"
        };

    // ── CreatePartAsync ───────────────────────────────────────

    [Fact]
    public async Task CreatePartAsync_PersistsPartToDatabase()
    {
        var part = CreateTestPart();

        var result = await _sut.CreatePartAsync(part);

        Assert.True(result.Id > 0);
        Assert.Equal("PN-001", result.PartNumber);
        Assert.Equal(1, await _db.Parts.CountAsync());
    }

    [Fact]
    public async Task CreatePartAsync_SetsAuditDates()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var part = CreateTestPart();

        var result = await _sut.CreatePartAsync(part);

        Assert.True(result.CreatedDate >= before);
        Assert.True(result.LastModifiedDate >= before);
    }

    // ── GetAllPartsAsync ──────────────────────────────────────

    [Fact]
    public async Task GetAllPartsAsync_WhenActiveOnly_ExcludesInactiveParts()
    {
        await _sut.CreatePartAsync(CreateTestPart("PN-001", "Active Part"));
        var inactive = CreateTestPart("PN-002", "Inactive Part");
        inactive.IsActive = false;
        await _sut.CreatePartAsync(inactive);

        var result = await _sut.GetAllPartsAsync(activeOnly: true);

        Assert.Single(result);
        Assert.Equal("PN-001", result[0].PartNumber);
    }

    [Fact]
    public async Task GetAllPartsAsync_WhenNotActiveOnly_ReturnsAll()
    {
        await _sut.CreatePartAsync(CreateTestPart("PN-001", "Active Part"));
        var inactive = CreateTestPart("PN-002", "Inactive Part");
        inactive.IsActive = false;
        await _sut.CreatePartAsync(inactive);

        var result = await _sut.GetAllPartsAsync(activeOnly: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllPartsAsync_OrdersByPartNumber()
    {
        await _sut.CreatePartAsync(CreateTestPart("PN-C", "Part C"));
        await _sut.CreatePartAsync(CreateTestPart("PN-A", "Part A"));
        await _sut.CreatePartAsync(CreateTestPart("PN-B", "Part B"));

        var result = await _sut.GetAllPartsAsync();

        Assert.Equal("PN-A", result[0].PartNumber);
        Assert.Equal("PN-B", result[1].PartNumber);
        Assert.Equal("PN-C", result[2].PartNumber);
    }

    // ── GetPartByIdAsync ──────────────────────────────────────

    [Fact]
    public async Task GetPartByIdAsync_WhenExists_ReturnsPart()
    {
        var created = await _sut.CreatePartAsync(CreateTestPart());

        var result = await _sut.GetPartByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("PN-001", result.PartNumber);
    }

    [Fact]
    public async Task GetPartByIdAsync_WhenNotExists_ReturnsNull()
    {
        var result = await _sut.GetPartByIdAsync(999);

        Assert.Null(result);
    }

    // ── GetPartByNumberAsync ──────────────────────────────────

    [Fact]
    public async Task GetPartByNumberAsync_WhenExists_ReturnsPart()
    {
        await _sut.CreatePartAsync(CreateTestPart("FIND-ME"));

        var result = await _sut.GetPartByNumberAsync("FIND-ME");

        Assert.NotNull(result);
        Assert.Equal("FIND-ME", result.PartNumber);
    }

    [Fact]
    public async Task GetPartByNumberAsync_WhenNotExists_ReturnsNull()
    {
        var result = await _sut.GetPartByNumberAsync("NONEXISTENT");

        Assert.Null(result);
    }

    // ── UpdatePartAsync ───────────────────────────────────────

    [Fact]
    public async Task UpdatePartAsync_UpdatesFieldsAndTimestamp()
    {
        var created = await _sut.CreatePartAsync(CreateTestPart());
        created.Name = "Updated Name";
        created.Material = "Inconel 718";

        var result = await _sut.UpdatePartAsync(created);

        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Inconel 718", result.Material);
    }

    // ── DeletePartAsync ───────────────────────────────────────

    [Fact]
    public async Task DeletePartAsync_SoftDeletesPartBySettingInactive()
    {
        var created = await _sut.CreatePartAsync(CreateTestPart());

        await _sut.DeletePartAsync(created.Id);

        var part = await _db.Parts.FindAsync(created.Id);
        Assert.NotNull(part);
        Assert.False(part.IsActive);
    }

    [Fact]
    public async Task DeletePartAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeletePartAsync(999));
    }

    // ── ValidatePartAsync ─────────────────────────────────────

    [Fact]
    public async Task ValidatePartAsync_WhenValid_ReturnsNoErrors()
    {
        var part = CreateTestPart();

        var errors = await _sut.ValidatePartAsync(part);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidatePartAsync_WhenPartNumberEmpty_ReturnsError()
    {
        var part = CreateTestPart();
        part.PartNumber = "";

        var errors = await _sut.ValidatePartAsync(part);

        Assert.Contains(errors, e => e.Contains("Part number"));
    }

    [Fact]
    public async Task ValidatePartAsync_WhenNameEmpty_ReturnsError()
    {
        var part = CreateTestPart();
        part.Name = "";

        var errors = await _sut.ValidatePartAsync(part);

        Assert.Contains(errors, e => e.Contains("Part name"));
    }

    [Fact]
    public async Task ValidatePartAsync_WhenMaterialEmpty_ReturnsError()
    {
        var part = CreateTestPart();
        part.Material = "";

        var errors = await _sut.ValidatePartAsync(part);

        Assert.Contains(errors, e => e.Contains("Material"));
    }

    [Fact]
    public async Task ValidatePartAsync_WhenDuplicatePartNumber_ReturnsError()
    {
        await _sut.CreatePartAsync(CreateTestPart("DUPE-001"));

        var newPart = CreateTestPart("DUPE-001", "Different Part");
        var errors = await _sut.ValidatePartAsync(newPart);

        Assert.Contains(errors, e => e.Contains("already exists"));
    }

    [Fact]
    public async Task ValidatePartAsync_WhenSamePartUpdated_DoesNotFlagDuplicate()
    {
        var existing = await _sut.CreatePartAsync(CreateTestPart("PN-001"));

        var errors = await _sut.ValidatePartAsync(existing);

        Assert.DoesNotContain(errors, e => e.Contains("already exists"));
    }

    [Fact]
    public async Task ValidatePartAsync_IncludesStackingValidationErrors()
    {
        var part = CreateTestPart();
        part.AdditiveBuildConfig = new PartAdditiveBuildConfig
        {
            AllowStacking = true
            // Missing SingleStackDurationHours
        };

        var errors = await _sut.ValidatePartAsync(part);

        Assert.Contains(errors, e => e.Contains("Single stack duration"));
    }

    // ── Stage Requirements ────────────────────────────────────

    [Fact]
    public async Task AddStageRequirementAsync_PersistsRequirement()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var stage = CreateTestStage();
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();

        var req = new PartStageRequirement
        {
            PartId = part.Id,
            ProductionStageId = stage.Id,
            ExecutionOrder = 1,
            EstimatedMinutes = 180, // 3.0 hours
            CreatedBy = "test-user",
            LastModifiedBy = "test-user"
        };

        var result = await _sut.AddStageRequirementAsync(req);

        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task GetStageRequirementsAsync_ReturnsActiveOnlyOrderedByExecutionOrder()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var stage1 = CreateTestStage("Stage A", "stage-a");
        var stage2 = CreateTestStage("Stage B", "stage-b");
        _db.ProductionStages.AddRange(stage1, stage2);
        await _db.SaveChangesAsync();

        await _sut.AddStageRequirementAsync(new PartStageRequirement
        {
            PartId = part.Id, ProductionStageId = stage2.Id, ExecutionOrder = 2,
            CreatedBy = "test-user", LastModifiedBy = "test-user"
        });
        await _sut.AddStageRequirementAsync(new PartStageRequirement
        {
            PartId = part.Id, ProductionStageId = stage1.Id, ExecutionOrder = 1,
            CreatedBy = "test-user", LastModifiedBy = "test-user"
        });

        var requirements = await _sut.GetStageRequirementsAsync(part.Id);

        Assert.Equal(2, requirements.Count);
        Assert.Equal(1, requirements[0].ExecutionOrder);
        Assert.Equal(2, requirements[1].ExecutionOrder);
    }

    [Fact]
    public async Task RemoveStageRequirementAsync_SoftDeletesRequirement()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var stage = CreateTestStage();
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();

        var req = await _sut.AddStageRequirementAsync(new PartStageRequirement
        {
            PartId = part.Id, ProductionStageId = stage.Id, ExecutionOrder = 1,
            CreatedBy = "test-user", LastModifiedBy = "test-user"
        });

        await _sut.RemoveStageRequirementAsync(req.Id);

        var dbReq = await _db.PartStageRequirements.FindAsync(req.Id);
        Assert.NotNull(dbReq);
        Assert.False(dbReq.IsActive);
    }

    [Fact]
    public async Task RemoveStageRequirementAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RemoveStageRequirementAsync(999));
    }

    // ── SearchPartsAsync ──────────────────────────────────────

    [Fact]
    public async Task SearchPartsAsync_FindsByPartNumber()
    {
        await _sut.CreatePartAsync(CreateTestPart("TI-BRACKET-001", "Bracket"));
        await _sut.CreatePartAsync(CreateTestPart("AL-PLATE-001", "Plate"));

        var results = await _sut.SearchPartsAsync("bracket");

        Assert.Single(results);
        Assert.Equal("TI-BRACKET-001", results[0].PartNumber);
    }

    [Fact]
    public async Task SearchPartsAsync_FindsByName()
    {
        await _sut.CreatePartAsync(CreateTestPart("PN-001", "Turbine Blade"));
        await _sut.CreatePartAsync(CreateTestPart("PN-002", "Mounting Bracket"));

        var results = await _sut.SearchPartsAsync("turbine");

        Assert.Single(results);
        Assert.Equal("Turbine Blade", results[0].Name);
    }

    [Fact]
    public async Task SearchPartsAsync_ExcludesInactiveByDefault()
    {
        var part = CreateTestPart("PN-001", "Inactive Part");
        part.IsActive = false;
        await _sut.CreatePartAsync(part);

        var results = await _sut.SearchPartsAsync("inactive");

        Assert.Empty(results);
    }

    // ── BumpRevisionAsync ─────────────────────────────────────

    [Fact]
    public async Task BumpRevisionAsync_CreatesHistoryAndUpdatesPartRevision()
    {
        var part = CreateTestPart();
        part.Revision = "A";
        var created = await _sut.CreatePartAsync(part);

        var history = await _sut.BumpRevisionAsync(created.Id, "B", "Improved tolerances", "engineer");

        Assert.Equal("B", history.Revision);
        Assert.Equal("A", history.PreviousRevision);
        Assert.Equal("Improved tolerances", history.ChangeDescription);

        var updated = await _db.Parts.FindAsync(created.Id);
        Assert.Equal("B", updated!.Revision);
    }

    [Fact]
    public async Task BumpRevisionAsync_WhenPartNotFound_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.BumpRevisionAsync(999, "B", "Change", "user"));
    }

    // ── ClonePartAsync ────────────────────────────────────────

    [Fact]
    public async Task ClonePartAsync_CreatesNewPartWithNewNumber()
    {
        var source = await _sut.CreatePartAsync(CreateTestPart("SRC-001", "Source Part"));

        var clone = await _sut.ClonePartAsync(source.Id, "CLN-001", "cloner");

        Assert.NotEqual(source.Id, clone.Id);
        Assert.Equal("CLN-001", clone.PartNumber);
        Assert.Contains("(Copy)", clone.Name);
        Assert.Equal("A", clone.Revision);
        Assert.True(clone.IsActive);
    }

    [Fact]
    public async Task ClonePartAsync_DeepCopiesStageRequirements()
    {
        var source = await _sut.CreatePartAsync(CreateTestPart("SRC-001", "Source Part"));
        var stage = CreateTestStage();
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();

        await _sut.AddStageRequirementAsync(new PartStageRequirement
        {
            PartId = source.Id,
            ProductionStageId = stage.Id,
            ExecutionOrder = 1,
            EstimatedMinutes = 300, // 5.0 hours
            CreatedBy = "test-user",
            LastModifiedBy = "test-user"
        });

        var clone = await _sut.ClonePartAsync(source.Id, "CLN-001", "cloner");

        var cloneReqs = await _sut.GetStageRequirementsAsync(clone.Id);
        Assert.Single(cloneReqs);
        Assert.Equal(300, cloneReqs[0].EstimatedMinutes);
        Assert.Equal(stage.Id, cloneReqs[0].ProductionStageId);
    }

    [Fact]
    public async Task ClonePartAsync_WhenDuplicateNewNumber_ThrowsInvalidOperationException()
    {
        var source = await _sut.CreatePartAsync(CreateTestPart("SRC-001", "Source Part"));
        await _sut.CreatePartAsync(CreateTestPart("TAKEN-001", "Existing Part"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ClonePartAsync(source.Id, "TAKEN-001", "cloner"));
    }

    [Fact]
    public async Task ClonePartAsync_WhenSourceNotFound_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ClonePartAsync(999, "CLN-001", "cloner"));
    }

    // ── Notes ─────────────────────────────────────────────────

    [Fact]
    public async Task AddNoteAsync_PersistsNote()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var note = new PartNote
        {
            PartId = part.Id,
            Title = "Engineering Note",
            Content = "Check tolerances on feature X.",
            CreatedBy = "engineer"
        };

        var result = await _sut.AddNoteAsync(note);

        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task GetNotesAsync_ReturnsPinnedFirst()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());

        await _sut.AddNoteAsync(new PartNote
        {
            PartId = part.Id, Title = "Regular", Content = "...",
            CreatedBy = "user", IsPinned = false
        });
        await _sut.AddNoteAsync(new PartNote
        {
            PartId = part.Id, Title = "Pinned", Content = "...",
            CreatedBy = "user", IsPinned = true
        });

        var notes = await _sut.GetNotesAsync(part.Id);

        Assert.Equal(2, notes.Count);
        Assert.True(notes[0].IsPinned);
    }

    [Fact]
    public async Task DeleteNoteAsync_RemovesNoteFromDatabase()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var note = await _sut.AddNoteAsync(new PartNote
        {
            PartId = part.Id, Title = "Temp", Content = "Delete me",
            CreatedBy = "user"
        });

        await _sut.DeleteNoteAsync(note.Id);

        Assert.Null(await _db.PartNotes.FindAsync(note.Id));
    }

    [Fact]
    public async Task DeleteNoteAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteNoteAsync(999));
    }

    // ── GetPartDetailAsync ────────────────────────────────────

    [Fact]
    public async Task GetPartDetailAsync_WhenExists_ReturnsPartWithAllIncludes()
    {
        var stage = CreateTestStage();
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();

        var part = await _sut.CreatePartAsync(CreateTestPart());
        await _sut.AddStageRequirementAsync(new PartStageRequirement
        {
            PartId = part.Id,
            ProductionStageId = stage.Id,
            ExecutionOrder = 1,
            CreatedBy = "test",
            LastModifiedBy = "test"
        });
        await _sut.AddNoteAsync(new PartNote
        {
            PartId = part.Id,
            Title = "Note",
            Content = "Content",
            CreatedBy = "test"
        });

        var result = await _sut.GetPartDetailAsync(part.Id);

        Assert.NotNull(result);
        Assert.NotNull(result.StageRequirements);
        Assert.Single(result.StageRequirements);
        Assert.NotNull(result.Notes);
        Assert.Single(result.Notes);
    }

    [Fact]
    public async Task GetPartDetailAsync_WhenNotExists_ReturnsNull()
    {
        Assert.Null(await _sut.GetPartDetailAsync(999));
    }

    // ── UpdateStageRequirementAsync ───────────────────────────

    [Fact]
    public async Task UpdateStageRequirementAsync_UpdatesFieldsAndTimestamp()
    {
        var stage = CreateTestStage();
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var req = await _sut.AddStageRequirementAsync(new PartStageRequirement
        {
            PartId = part.Id,
            ProductionStageId = stage.Id,
            ExecutionOrder = 1,
            EstimatedMinutes = 120, // 2.0 hours
            CreatedBy = "test",
            LastModifiedBy = "test"
        });

        req.EstimatedMinutes = 300; // 5.0 hours
        req.ExecutionOrder = 3;
        var result = await _sut.UpdateStageRequirementAsync(req);

        Assert.Equal(300, result.EstimatedMinutes);
        Assert.Equal(3, result.ExecutionOrder);
    }

    // ── UpdateNoteAsync ──────────────────────────────────────

    [Fact]
    public async Task UpdateNoteAsync_UpdatesContentAndTimestamp()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var note = await _sut.AddNoteAsync(new PartNote
        {
            PartId = part.Id,
            Title = "Original",
            Content = "Old content",
            CreatedBy = "user"
        });

        note.Title = "Updated";
        note.Content = "New content";
        var result = await _sut.UpdateNoteAsync(note);

        Assert.Equal("Updated", result.Title);
        Assert.Equal("New content", result.Content);
        Assert.NotNull(result.LastModifiedDate);
    }

    // ── GetPartUsageSummaryAsync ──────────────────────────────

    [Fact]
    public async Task GetPartUsageSummaryAsync_ReturnsEmptySummaryForNewPart()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());

        var summary = await _sut.GetPartUsageSummaryAsync(part.Id);

        Assert.NotNull(summary);
        Assert.Empty(summary.ActiveWorkOrderLines);
        Assert.Empty(summary.ActiveJobs);
        Assert.Empty(summary.RecentQuoteLines);
        Assert.Equal(0, summary.NcrCount);
        Assert.Equal(0, summary.InspectionCount);
        Assert.Equal(0, summary.SpcDataPointCount);
    }

    [Fact]
    public async Task GetPartUsageSummaryAsync_ReturnsJobsLinkedToPart()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var job = new Job
        {
            PartId = part.Id,
            Quantity = 10,
            Status = Vectrik.Models.Enums.JobStatus.Scheduled,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddHours(8),
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        var summary = await _sut.GetPartUsageSummaryAsync(part.Id);

        Assert.Single(summary.ActiveJobs);
    }

    // ── BOM CRUD ──────────────────────────────────────────────

    [Fact]
    public async Task AddBomItemAsync_PersistsItem()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var item = new PartBomItem
        {
            PartId = part.Id,
            QuantityRequired = 2.5m,
            UnitOfMeasure = "kg",
            Notes = "Raw material",
            SortOrder = 1,
            CreatedBy = "test"
        };

        var result = await _sut.AddBomItemAsync(item);

        Assert.True(result.Id > 0);
        Assert.Equal(2.5m, result.QuantityRequired);
        Assert.Equal("kg", result.UnitOfMeasure);
    }

    [Fact]
    public async Task AddBomItemAsync_SetsTimestamps()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var item = new PartBomItem { PartId = part.Id, QuantityRequired = 1, CreatedBy = "test" };

        var result = await _sut.AddBomItemAsync(item);

        Assert.True(result.CreatedDate >= before);
        Assert.True(result.LastModifiedDate >= before);
    }

    [Fact]
    public async Task GetBomItemsAsync_ReturnsActiveOnlyOrderedBySortOrder()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        await _sut.AddBomItemAsync(new PartBomItem
        {
            PartId = part.Id, QuantityRequired = 1, SortOrder = 2, CreatedBy = "test"
        });
        await _sut.AddBomItemAsync(new PartBomItem
        {
            PartId = part.Id, QuantityRequired = 2, SortOrder = 1, CreatedBy = "test"
        });
        var inactive = new PartBomItem
        {
            PartId = part.Id, QuantityRequired = 3, SortOrder = 0, IsActive = false, CreatedBy = "test"
        };
        _db.PartBomItems.Add(inactive);
        await _db.SaveChangesAsync();

        var result = await _sut.GetBomItemsAsync(part.Id);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].SortOrder);
        Assert.Equal(2, result[1].SortOrder);
    }

    [Fact]
    public async Task UpdateBomItemAsync_UpdatesFieldsAndTimestamp()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var item = await _sut.AddBomItemAsync(new PartBomItem
        {
            PartId = part.Id, QuantityRequired = 1, CreatedBy = "test"
        });

        item.QuantityRequired = 5.0m;
        item.Notes = "Updated notes";
        var result = await _sut.UpdateBomItemAsync(item);

        Assert.Equal(5.0m, result.QuantityRequired);
        Assert.Equal("Updated notes", result.Notes);
    }

    [Fact]
    public async Task RemoveBomItemAsync_SoftDeletesItem()
    {
        var part = await _sut.CreatePartAsync(CreateTestPart());
        var item = await _sut.AddBomItemAsync(new PartBomItem
        {
            PartId = part.Id, QuantityRequired = 1, CreatedBy = "test"
        });

        await _sut.RemoveBomItemAsync(item.Id);

        var deleted = await _db.PartBomItems.FindAsync(item.Id);
        Assert.NotNull(deleted);
        Assert.False(deleted.IsActive);
    }

    [Fact]
    public async Task RemoveBomItemAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RemoveBomItemAsync(999));
    }
}
