using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class BatchService : IBatchService
{
    private readonly TenantDbContext _db;

    public BatchService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProductionBatch>> CreateBatchesFromBuildAsync(int buildPackageId, int batchCapacity, string createdBy)
    {
        if (batchCapacity <= 0) throw new ArgumentException("Batch capacity must be positive.", nameof(batchCapacity));
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("CreatedBy is required.", nameof(createdBy));

        var partInstances = await _db.PartInstances
            .Where(pi => pi.BuildPackageId == buildPackageId && pi.CurrentBatchId == null)
            .OrderBy(pi => pi.Id)
            .ToListAsync();

        if (partInstances.Count == 0)
            return [];

        var batchCount = (int)Math.Ceiling((double)partInstances.Count / batchCapacity);
        var batches = new List<ProductionBatch>();

        // Use Max(Id) to avoid collision when batches are dissolved/deleted
        var maxId = await _db.ProductionBatches.MaxAsync(b => (int?)b.Id) ?? 0;

        for (int i = 0; i < batchCount; i++)
        {
            var batchNumber = $"BATCH-{buildPackageId:D4}-{maxId + i + 1:D3}";
            var partsForBatch = partInstances
                .Skip(i * batchCapacity)
                .Take(batchCapacity)
                .ToList();

            var batch = new ProductionBatch
            {
                BatchNumber = batchNumber,
                OriginBuildPackageId = buildPackageId,
                Capacity = batchCapacity,
                CurrentPartCount = partsForBatch.Count,
                Status = BatchStatus.Open,
                CreatedBy = createdBy
            };

            _db.ProductionBatches.Add(batch);
            // Save to get batch.Id for part assignments
            await _db.SaveChangesAsync();

            // Assign parts and record history
            foreach (var pi in partsForBatch)
            {
                pi.CurrentBatchId = batch.Id;

                _db.BatchPartAssignments.Add(new BatchPartAssignment
                {
                    ProductionBatchId = batch.Id,
                    PartInstanceId = pi.Id,
                    Action = BatchAssignmentAction.Assigned,
                    Reason = $"Initial batch from build #{buildPackageId}",
                    PerformedBy = createdBy
                });
            }

            batches.Add(batch);
        }

        // Single save for all part assignments
        await _db.SaveChangesAsync();
        return batches;
    }

    public async Task AssignPartToBatchAsync(int partInstanceId, int batchId, string reason, string performedBy, int? atProcessStageId = null)
    {
        if (string.IsNullOrWhiteSpace(performedBy)) throw new ArgumentException("PerformedBy is required.", nameof(performedBy));

        var batch = await _db.ProductionBatches.FindAsync(batchId)
            ?? throw new InvalidOperationException("Batch not found.");
        var partInstance = await _db.PartInstances.FindAsync(partInstanceId)
            ?? throw new InvalidOperationException("Part instance not found.");

        if (batch.Status == BatchStatus.Sealed || batch.Status == BatchStatus.Completed || batch.Status == BatchStatus.Dissolved)
            throw new InvalidOperationException($"Cannot assign parts to a batch with status '{batch.Status}'.");

        if (batch.CurrentPartCount >= batch.Capacity)
            throw new InvalidOperationException("Batch is at capacity.");

        partInstance.CurrentBatchId = batchId;
        batch.CurrentPartCount++;
        batch.LastModifiedDate = DateTime.UtcNow;

        _db.BatchPartAssignments.Add(new BatchPartAssignment
        {
            ProductionBatchId = batchId,
            PartInstanceId = partInstanceId,
            Action = BatchAssignmentAction.Assigned,
            Reason = reason,
            AtProcessStageId = atProcessStageId,
            PerformedBy = performedBy
        });

        await _db.SaveChangesAsync();
    }

    public async Task RemovePartFromBatchAsync(int partInstanceId, int batchId, string reason, string performedBy, int? atProcessStageId = null)
    {
        if (string.IsNullOrWhiteSpace(performedBy)) throw new ArgumentException("PerformedBy is required.", nameof(performedBy));

        var batch = await _db.ProductionBatches.FindAsync(batchId)
            ?? throw new InvalidOperationException("Batch not found.");
        var partInstance = await _db.PartInstances.FindAsync(partInstanceId)
            ?? throw new InvalidOperationException("Part instance not found.");

        if (partInstance.CurrentBatchId != batchId)
            throw new InvalidOperationException("Part instance is not in the specified batch.");

        partInstance.CurrentBatchId = null;
        batch.CurrentPartCount = Math.Max(0, batch.CurrentPartCount - 1);
        batch.LastModifiedDate = DateTime.UtcNow;

        _db.BatchPartAssignments.Add(new BatchPartAssignment
        {
            ProductionBatchId = batchId,
            PartInstanceId = partInstanceId,
            Action = BatchAssignmentAction.Removed,
            Reason = reason,
            AtProcessStageId = atProcessStageId,
            PerformedBy = performedBy
        });

        await _db.SaveChangesAsync();
    }

    public async Task<List<ProductionBatch>> RebatchPartsAsync(List<int> partInstanceIds, int newCapacity, string reason, string performedBy)
    {
        ArgumentNullException.ThrowIfNull(partInstanceIds);
        if (newCapacity <= 0) throw new ArgumentException("Batch capacity must be positive.", nameof(newCapacity));
        if (string.IsNullOrWhiteSpace(performedBy)) throw new ArgumentException("PerformedBy is required.", nameof(performedBy));

        var partInstances = await _db.PartInstances
            .Where(pi => partInstanceIds.Contains(pi.Id))
            .ToListAsync();

        // Capture original batch assignments before nulling
        var originalBatchMap = partInstances
            .Where(pi => pi.CurrentBatchId.HasValue)
            .GroupBy(pi => pi.CurrentBatchId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var pi in partInstances.Where(pi => pi.CurrentBatchId.HasValue))
        {
            _db.BatchPartAssignments.Add(new BatchPartAssignment
            {
                ProductionBatchId = pi.CurrentBatchId!.Value,
                PartInstanceId = pi.Id,
                Action = BatchAssignmentAction.Removed,
                Reason = reason,
                PerformedBy = performedBy
            });
            pi.CurrentBatchId = null;
        }

        // Update old batch counts using pre-captured map
        var oldBatches = await _db.ProductionBatches
            .Where(b => originalBatchMap.Keys.Contains(b.Id))
            .ToListAsync();
        foreach (var batch in oldBatches)
        {
            var removedCount = originalBatchMap.GetValueOrDefault(batch.Id, 0);
            batch.CurrentPartCount = Math.Max(0, batch.CurrentPartCount - removedCount);
            batch.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Determine origin build for numbering (use first part's build)
        var originBuildId = partInstances.FirstOrDefault()?.BuildPackageId;

        // Create new batches
        var batchCount = (int)Math.Ceiling((double)partInstances.Count / newCapacity);
        var maxId = await _db.ProductionBatches.MaxAsync(b => (int?)b.Id) ?? 0;
        var newBatches = new List<ProductionBatch>();

        for (int i = 0; i < batchCount; i++)
        {
            var batchNumber = $"BATCH-R-{maxId + i + 1:D4}";
            var partsForBatch = partInstances
                .Skip(i * newCapacity)
                .Take(newCapacity)
                .ToList();

            var newBatch = new ProductionBatch
            {
                BatchNumber = batchNumber,
                OriginBuildPackageId = originBuildId,
                Capacity = newCapacity,
                CurrentPartCount = partsForBatch.Count,
                Status = BatchStatus.Open,
                CreatedBy = performedBy
            };

            _db.ProductionBatches.Add(newBatch);
            await _db.SaveChangesAsync();

            foreach (var pi in partsForBatch)
            {
                pi.CurrentBatchId = newBatch.Id;
                _db.BatchPartAssignments.Add(new BatchPartAssignment
                {
                    ProductionBatchId = newBatch.Id,
                    PartInstanceId = pi.Id,
                    Action = BatchAssignmentAction.Assigned,
                    Reason = reason,
                    PerformedBy = performedBy
                });
            }

            await _db.SaveChangesAsync();
            newBatches.Add(newBatch);
        }

        return newBatches;
    }

    public async Task<ConsolidationResult> TryConsolidateBatchesAsync(List<int> batchIds, int targetMachineCapacity, string performedBy)
    {
        ArgumentNullException.ThrowIfNull(batchIds);
        if (string.IsNullOrWhiteSpace(performedBy)) throw new ArgumentException("PerformedBy is required.", nameof(performedBy));

        var batches = await _db.ProductionBatches
            .Where(b => batchIds.Contains(b.Id))
            .ToListAsync();

        var totalParts = batches.Sum(b => b.CurrentPartCount);

        if (totalParts > targetMachineCapacity)
        {
            return new ConsolidationResult(
                false,
                $"Cannot consolidate: {totalParts} total parts exceeds machine capacity of {targetMachineCapacity}.",
                null,
                []);
        }

        // Create merged batch
        var maxId = await _db.ProductionBatches.MaxAsync(b => (int?)b.Id) ?? 0;
        var mergedBatch = new ProductionBatch
        {
            BatchNumber = $"BATCH-M-{maxId + 1:D4}",
            OriginBuildPackageId = batches.FirstOrDefault()?.OriginBuildPackageId,
            Capacity = targetMachineCapacity,
            CurrentPartCount = 0,
            Status = BatchStatus.Open,
            CreatedBy = performedBy
        };

        _db.ProductionBatches.Add(mergedBatch);
        await _db.SaveChangesAsync();

        // Move all parts from source batches to merged batch
        var dissolvedIds = new List<int>();
        foreach (var sourceBatch in batches)
        {
            var partsInBatch = await _db.PartInstances
                .Where(pi => pi.CurrentBatchId == sourceBatch.Id)
                .ToListAsync();

            foreach (var pi in partsInBatch)
            {
                _db.BatchPartAssignments.Add(new BatchPartAssignment
                {
                    ProductionBatchId = sourceBatch.Id,
                    PartInstanceId = pi.Id,
                    Action = BatchAssignmentAction.Removed,
                    Reason = "Consolidated into merged batch",
                    PerformedBy = performedBy
                });

                pi.CurrentBatchId = mergedBatch.Id;
                mergedBatch.CurrentPartCount++;

                _db.BatchPartAssignments.Add(new BatchPartAssignment
                {
                    ProductionBatchId = mergedBatch.Id,
                    PartInstanceId = pi.Id,
                    Action = BatchAssignmentAction.Assigned,
                    Reason = $"Consolidated from batch {sourceBatch.BatchNumber}",
                    PerformedBy = performedBy
                });
            }

            sourceBatch.Status = BatchStatus.Dissolved;
            sourceBatch.CurrentPartCount = 0;
            sourceBatch.LastModifiedDate = DateTime.UtcNow;
            dissolvedIds.Add(sourceBatch.Id);
        }

        await _db.SaveChangesAsync();

        return new ConsolidationResult(
            true,
            $"Consolidated {totalParts} parts from {batches.Count} batches into {mergedBatch.BatchNumber}.",
            mergedBatch,
            dissolvedIds);
    }

    public async Task<List<BatchPartAssignment>> GetAssignmentHistoryForPartAsync(int partInstanceId)
    {
        return await _db.BatchPartAssignments
            .Include(a => a.ProductionBatch)
            .Include(a => a.AtProcessStage)
            .Where(a => a.PartInstanceId == partInstanceId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<ProductionBatch?> GetByIdAsync(int id)
    {
        return await _db.ProductionBatches
            .Include(b => b.PartAssignments.OrderByDescending(a => a.Timestamp))
                .ThenInclude(a => a.PartInstance)
            .Include(b => b.CurrentProcessStage)
            .Include(b => b.OriginBuildPackage)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<List<ProductionBatch>> GetBatchesForBuildAsync(int buildPackageId)
    {
        return await _db.ProductionBatches
            .Include(b => b.CurrentProcessStage)
            .Where(b => b.OriginBuildPackageId == buildPackageId && b.Status != BatchStatus.Dissolved)
            .OrderBy(b => b.BatchNumber)
            .ToListAsync();
    }

    public async Task SealBatchAsync(int batchId)
    {
        var batch = await _db.ProductionBatches.FindAsync(batchId)
            ?? throw new InvalidOperationException("Batch not found.");

        if (batch.Status != BatchStatus.Open)
            throw new InvalidOperationException($"Cannot seal batch with status '{batch.Status}'. Only Open batches can be sealed.");

        batch.Status = BatchStatus.Sealed;
        batch.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task CompleteBatchAsync(int batchId)
    {
        var batch = await _db.ProductionBatches.FindAsync(batchId)
            ?? throw new InvalidOperationException("Batch not found.");

        if (batch.Status == BatchStatus.Dissolved)
            throw new InvalidOperationException("Cannot complete a dissolved batch.");

        batch.Status = BatchStatus.Completed;
        batch.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DissolveBatchAsync(int batchId, string reason, string performedBy)
    {
        if (string.IsNullOrWhiteSpace(performedBy)) throw new ArgumentException("PerformedBy is required.", nameof(performedBy));

        var batch = await _db.ProductionBatches.FindAsync(batchId)
            ?? throw new InvalidOperationException("Batch not found.");

        // Remove all parts from the batch
        var partsInBatch = await _db.PartInstances
            .Where(pi => pi.CurrentBatchId == batchId)
            .ToListAsync();

        foreach (var pi in partsInBatch)
        {
            _db.BatchPartAssignments.Add(new BatchPartAssignment
            {
                ProductionBatchId = batchId,
                PartInstanceId = pi.Id,
                Action = BatchAssignmentAction.Removed,
                Reason = reason,
                PerformedBy = performedBy
            });
            pi.CurrentBatchId = null;
        }

        batch.Status = BatchStatus.Dissolved;
        batch.CurrentPartCount = 0;
        batch.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
