using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;
using Opcentrix_V3.Tests.Helpers;
using Xunit;

namespace Opcentrix_V3.Tests.Services;

public class BuildSuggestionServiceTests : IDisposable
{
    private readonly Data.TenantDbContext _db;
    private readonly BuildTemplateService _templateService;
    private readonly BuildSuggestionService _sut;

    public BuildSuggestionServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _templateService = new BuildTemplateService(_db);
        _sut = new BuildSuggestionService(_db, _templateService);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────

    private ManufacturingApproach CreateAdditiveApproach() =>
        new()
        {
            Name = "SLS",
            Slug = "sls",
            IsAdditive = true,
            RequiresBuildPlate = true
        };

    private ManufacturingApproach CreateCncApproach() =>
        new()
        {
            Name = "CNC",
            Slug = "cnc",
            IsAdditive = false,
            RequiresBuildPlate = false
        };

    private Material CreateMaterial(string name = "Nylon 12") =>
        new()
        {
            Name = name,
            Category = "Polymer Powder",
            CostPerKg = 50m
        };

    private Part CreatePart(string partNumber, ManufacturingApproach approach, Material? material = null, int plannedPerBuild = 76) =>
        new()
        {
            PartNumber = partNumber,
            Name = $"Part {partNumber}",
            Material = material?.Name ?? "Ti-6Al-4V",
            MaterialId = material?.Id,
            MaterialEntity = material,
            ManufacturingApproachId = approach.Id,
            ManufacturingApproach = approach,
            CreatedBy = "test",
            LastModifiedBy = "test",
            AdditiveBuildConfig = approach.RequiresBuildPlate
                ? new PartAdditiveBuildConfig { PlannedPartsPerBuildSingle = plannedPerBuild }
                : null
        };

    private async Task<(ManufacturingApproach approach, Material material)> SeedAdditiveInfraAsync(string materialName = "Nylon 12")
    {
        var approach = CreateAdditiveApproach();
        _db.ManufacturingApproaches.Add(approach);
        var material = CreateMaterial(materialName);
        _db.Materials.Add(material);
        await _db.SaveChangesAsync();
        return (approach, material);
    }

    private async Task<WorkOrder> CreateWorkOrderWithLineAsync(
        Part part,
        int quantity,
        WorkOrderStatus status = WorkOrderStatus.Released,
        DateTime? dueDate = null,
        JobPriority priority = JobPriority.Normal)
    {
        var wo = new WorkOrder
        {
            OrderNumber = $"WO-{Guid.NewGuid().ToString()[..6]}",
            CustomerName = "Test Customer",
            DueDate = dueDate ?? DateTime.UtcNow.AddDays(14),
            Status = status,
            Priority = priority,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        wo.Lines.Add(new WorkOrderLine
        {
            PartId = part.Id,
            Part = part,
            Quantity = quantity,
            Status = status
        });
        _db.WorkOrders.Add(wo);
        await _db.SaveChangesAsync();
        return wo;
    }

    private async Task<BuildTemplate> CreateCertifiedTemplateForPartAsync(Part part, int templateQty = 76, double durationHours = 18.5)
    {
        var template = new BuildTemplate
        {
            Name = $"Template-{part.PartNumber}",
            EstimatedDurationHours = durationHours,
            MaterialId = part.MaterialId,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        template = await _templateService.CreateAsync(template);
        await _templateService.AddPartAsync(template.Id, part.Id, templateQty);
        return await _templateService.CertifyAsync(template.Id, "test");
    }

    // ── No Demand → No Suggestions ───────────────────────────

    [Fact]
    public async Task GetSuggestionsAsync_NoDemand_ReturnsEmpty()
    {
        var result = await _sut.GetSuggestionsAsync();

        Assert.Empty(result.TemplateSuggestions);
        Assert.Empty(result.MixedBuildSuggestions);
    }

    [Fact]
    public async Task GetSuggestionsAsync_OnlyCncDemand_ReturnsEmpty()
    {
        var cncApproach = CreateCncApproach();
        _db.ManufacturingApproaches.Add(cncApproach);
        await _db.SaveChangesAsync();

        var part = CreatePart("CNC-001", cncApproach);
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        await CreateWorkOrderWithLineAsync(part, 50);

        var result = await _sut.GetSuggestionsAsync();

        Assert.Empty(result.TemplateSuggestions);
        Assert.Empty(result.MixedBuildSuggestions);
    }

    // ── Single-Part Template Suggestions ─────────────────────

    [Fact]
    public async Task GetSuggestionsAsync_DemandWithMatchingTemplate_ReturnsTemplateSuggestion()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part = CreatePart("SUP-001", approach, material);
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        await CreateWorkOrderWithLineAsync(part, 76);
        await CreateCertifiedTemplateForPartAsync(part, templateQty: 76, durationHours: 18.5);

        var result = await _sut.GetSuggestionsAsync();

        Assert.Single(result.TemplateSuggestions);
        var suggestion = result.TemplateSuggestions[0];
        Assert.Equal("SUP-001", suggestion.PartNumber);
        Assert.Equal(76, suggestion.SuggestedQuantity);
        Assert.Equal(18.5, suggestion.EstimatedDurationHours);
        Assert.Single(suggestion.FulfillsWorkOrders);
    }

    [Fact]
    public async Task GetSuggestionsAsync_DemandExceedsTemplateCapacity_SuggestsMultipleRuns()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part = CreatePart("SUP-002", approach, material);
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        await CreateWorkOrderWithLineAsync(part, 150);
        await CreateCertifiedTemplateForPartAsync(part, templateQty: 76, durationHours: 18.5);

        var result = await _sut.GetSuggestionsAsync();

        Assert.Single(result.TemplateSuggestions);
        var suggestion = result.TemplateSuggestions[0];
        Assert.Equal(152, suggestion.SuggestedQuantity); // 2 runs × 76 = 152
        Assert.Equal(37.0, suggestion.EstimatedDurationHours); // 2 × 18.5
    }

    [Fact]
    public async Task GetSuggestionsAsync_DemandWithNoTemplate_ReturnsNoTemplateSuggestion()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part = CreatePart("SUP-003", approach, material);
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        await CreateWorkOrderWithLineAsync(part, 50);
        // No template created

        var result = await _sut.GetSuggestionsAsync();

        Assert.Empty(result.TemplateSuggestions);
    }

    [Fact]
    public async Task GetSuggestionsAsync_FullyFulfilledDemand_ReturnsEmpty()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part = CreatePart("SUP-004", approach, material);
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var wo = await CreateWorkOrderWithLineAsync(part, 76);
        var line = wo.Lines.First();
        line.ProducedQuantity = 76; // fully produced
        await _db.SaveChangesAsync();

        await CreateCertifiedTemplateForPartAsync(part);

        var result = await _sut.GetSuggestionsAsync();

        Assert.Empty(result.TemplateSuggestions);
    }

    [Fact]
    public async Task GetSuggestionsAsync_MultipleWosSamePart_AggregatesDemand()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part = CreatePart("SUP-005", approach, material);
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        await CreateWorkOrderWithLineAsync(part, 40, dueDate: DateTime.UtcNow.AddDays(7));
        await CreateWorkOrderWithLineAsync(part, 40, dueDate: DateTime.UtcNow.AddDays(10));
        await CreateCertifiedTemplateForPartAsync(part, templateQty: 76, durationHours: 18.5);

        var result = await _sut.GetSuggestionsAsync();

        Assert.Single(result.TemplateSuggestions);
        var suggestion = result.TemplateSuggestions[0];
        // 40+40=80 demand, ceil(80/76)=2, 2×76=152
        Assert.Equal(152, suggestion.SuggestedQuantity);
        Assert.Equal(2, suggestion.FulfillsWorkOrders.Count);
    }

    // ── Mixed-Build Suggestions ──────────────────────────────

    [Fact]
    public async Task GetSuggestionsAsync_TwoPartialDemandsSameMaterial_ReturnsMixedSuggestion()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part1 = CreatePart("MIX-001", approach, material, plannedPerBuild: 76);
        var part2 = CreatePart("MIX-002", approach, material, plannedPerBuild: 76);
        _db.Parts.AddRange(part1, part2);
        await _db.SaveChangesAsync();

        // Both parts need less than their planned per build (partial plate)
        var dueDate = DateTime.UtcNow.AddDays(7);
        await CreateWorkOrderWithLineAsync(part1, 30, dueDate: dueDate);
        await CreateWorkOrderWithLineAsync(part2, 20, dueDate: dueDate);
        // No templates — so they won't be covered by template suggestions

        var result = await _sut.GetSuggestionsAsync();

        Assert.Single(result.MixedBuildSuggestions);
        var mixed = result.MixedBuildSuggestions[0];
        Assert.Equal(2, mixed.Parts.Count);
        Assert.Contains(mixed.Parts, p => p.PartNumber == "MIX-001" && p.SuggestedQuantity == 30);
        Assert.Contains(mixed.Parts, p => p.PartNumber == "MIX-002" && p.SuggestedQuantity == 20);
    }

    [Fact]
    public async Task GetSuggestionsAsync_TwoPartsDifferentMaterials_NoMixedSuggestion()
    {
        var approach = CreateAdditiveApproach();
        _db.ManufacturingApproaches.Add(approach);
        var material1 = CreateMaterial("Nylon 12");
        var material2 = CreateMaterial("Ti-6Al-4V");
        _db.Materials.AddRange(material1, material2);
        await _db.SaveChangesAsync();

        var part1 = CreatePart("MIX-A1", approach, material1, plannedPerBuild: 76);
        var part2 = CreatePart("MIX-A2", approach, material2, plannedPerBuild: 76);
        _db.Parts.AddRange(part1, part2);
        await _db.SaveChangesAsync();

        var dueDate = DateTime.UtcNow.AddDays(7);
        await CreateWorkOrderWithLineAsync(part1, 30, dueDate: dueDate);
        await CreateWorkOrderWithLineAsync(part2, 20, dueDate: dueDate);

        var result = await _sut.GetSuggestionsAsync();

        Assert.Empty(result.MixedBuildSuggestions);
    }

    [Fact]
    public async Task GetSuggestionsAsync_TwoPartsDueDatesTooFarApart_NoMixedSuggestion()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part1 = CreatePart("MIX-B1", approach, material, plannedPerBuild: 76);
        var part2 = CreatePart("MIX-B2", approach, material, plannedPerBuild: 76);
        _db.Parts.AddRange(part1, part2);
        await _db.SaveChangesAsync();

        await CreateWorkOrderWithLineAsync(part1, 30, dueDate: DateTime.UtcNow.AddDays(3));
        await CreateWorkOrderWithLineAsync(part2, 20, dueDate: DateTime.UtcNow.AddDays(30));

        var result = await _sut.GetSuggestionsAsync();

        Assert.Empty(result.MixedBuildSuggestions);
    }

    [Fact]
    public async Task GetSuggestionsAsync_PartCoveredByTemplate_ExcludedFromMixed()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part1 = CreatePart("MIX-C1", approach, material, plannedPerBuild: 76);
        var part2 = CreatePart("MIX-C2", approach, material, plannedPerBuild: 76);
        _db.Parts.AddRange(part1, part2);
        await _db.SaveChangesAsync();

        var dueDate = DateTime.UtcNow.AddDays(7);
        await CreateWorkOrderWithLineAsync(part1, 30, dueDate: dueDate);
        await CreateWorkOrderWithLineAsync(part2, 20, dueDate: dueDate);

        // Create template for part1 — its demand is covered by template suggestion
        await CreateCertifiedTemplateForPartAsync(part1, templateQty: 76, durationHours: 18.5);

        var result = await _sut.GetSuggestionsAsync();

        // part1 covered by template, only part2 remains for mixed — but need 2+ parts for mixed
        Assert.Empty(result.MixedBuildSuggestions);
        Assert.Single(result.TemplateSuggestions);
    }

    // ── Draft WO Excluded ────────────────────────────────────

    [Fact]
    public async Task GetSuggestionsAsync_DraftWorkOrder_Excluded()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part = CreatePart("DRAFT-001", approach, material);
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        await CreateWorkOrderWithLineAsync(part, 76, status: WorkOrderStatus.Draft);
        await CreateCertifiedTemplateForPartAsync(part);

        var result = await _sut.GetSuggestionsAsync();

        Assert.Empty(result.TemplateSuggestions);
    }

    // ── WO References ────────────────────────────────────────

    [Fact]
    public async Task GetSuggestionsAsync_TemplateSuggestion_ContainsCorrectWoReferences()
    {
        var (approach, material) = await SeedAdditiveInfraAsync();
        var part = CreatePart("REF-001", approach, material);
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var dueDate = DateTime.UtcNow.AddDays(10);
        var wo = await CreateWorkOrderWithLineAsync(part, 50, dueDate: dueDate);
        await CreateCertifiedTemplateForPartAsync(part, templateQty: 76, durationHours: 18.5);

        var result = await _sut.GetSuggestionsAsync();

        Assert.Single(result.TemplateSuggestions);
        var woRef = result.TemplateSuggestions[0].FulfillsWorkOrders[0];
        Assert.Equal(wo.Id, woRef.WorkOrderId);
        Assert.Equal(wo.OrderNumber, woRef.OrderNumber);
        Assert.Equal(50, woRef.QuantityFulfilled);
    }
}
