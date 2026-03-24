using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

/// <summary>
/// Manages the lifecycle of production batches: creation, assignment, rebatching,
/// consolidation, and ITAR traceability history.
/// </summary>
public interface IBatchService
{
    /// <summary>
    /// Create batches from a list of part instances using ceil(N / capacity) splitting.
    /// </summary>
    Task<List<ProductionBatch>> CreateBatchesFromPartsAsync(List<int> partInstanceIds, int batchCapacity, string createdBy);

    /// <summary>
    /// Assign a part instance to a batch, recording immutable history.
    /// </summary>
    Task AssignPartToBatchAsync(int partInstanceId, int batchId, string reason, string performedBy, int? atProcessStageId = null);

    /// <summary>
    /// Remove a part instance from a batch, recording immutable history.
    /// </summary>
    Task RemovePartFromBatchAsync(int partInstanceId, int batchId, string reason, string performedBy, int? atProcessStageId = null);

    /// <summary>
    /// Re-group part instances into new batches with a different capacity.
    /// Dissolves old batches and creates new ones.
    /// </summary>
    Task<List<ProductionBatch>> RebatchPartsAsync(List<int> partInstanceIds, int newCapacity, string reason, string performedBy);

    /// <summary>
    /// Attempt machine-driven consolidation: merge batches if total parts fit in machine capacity.
    /// </summary>
    Task<ConsolidationResult> TryConsolidateBatchesAsync(List<int> batchIds, int targetMachineCapacity, string performedBy);

    /// <summary>
    /// Get the full assignment history for a part instance (ITAR traceability).
    /// </summary>
    Task<List<BatchPartAssignment>> GetAssignmentHistoryForPartAsync(int partInstanceId);

    /// <summary>
    /// Get a batch by ID with assignments.
    /// </summary>
    Task<ProductionBatch?> GetByIdAsync(int id);

    /// <summary>
    /// Get all active (non-dissolved) batches.
    /// </summary>
    Task<List<ProductionBatch>> GetActiveBatchesAsync();

    /// <summary>
    /// Seal a batch (no more parts can be added).
    /// </summary>
    Task SealBatchAsync(int batchId);

    /// <summary>
    /// Mark a batch as completed.
    /// </summary>
    Task CompleteBatchAsync(int batchId);

    /// <summary>
    /// Dissolve a batch (parts must be removed or reassigned first).
    /// </summary>
    Task DissolveBatchAsync(int batchId, string reason, string performedBy);
}

/// <summary>
/// Result of a batch consolidation attempt.
/// </summary>
public record ConsolidationResult(
    bool Success,
    string Message,
    ProductionBatch? MergedBatch,
    List<int> DissolvedBatchIds);
