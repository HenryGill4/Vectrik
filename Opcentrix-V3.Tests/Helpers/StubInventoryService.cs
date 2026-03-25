using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;

namespace Opcentrix_V3.Tests.Helpers;

/// <summary>
/// No-op stub for IInventoryService used by StageService tests.
/// </summary>
internal sealed class StubInventoryService : IInventoryService
{
    public Task<List<InventoryItem>> GetAllItemsAsync(InventoryItemType? type = null, bool activeOnly = true) => Task.FromResult(new List<InventoryItem>());
    public Task<InventoryItem?> GetItemByIdAsync(int id) => Task.FromResult<InventoryItem?>(null);
    public Task<InventoryItem> CreateItemAsync(InventoryItem item) => Task.FromResult(item);
    public Task<InventoryItem> UpdateItemAsync(InventoryItem item) => Task.FromResult(item);
    public Task DeleteItemAsync(int id) => Task.CompletedTask;
    public Task<decimal> GetAvailableQtyAsync(int itemId) => Task.FromResult(0m);
    public Task<List<InventoryItem>> GetLowStockItemsAsync() => Task.FromResult(new List<InventoryItem>());
    public Task<List<StockLocation>> GetAllLocationsAsync(bool activeOnly = true) => Task.FromResult(new List<StockLocation>());
    public Task<StockLocation> CreateLocationAsync(StockLocation location) => Task.FromResult(location);
    public Task<StockLocation> UpdateLocationAsync(StockLocation location) => Task.FromResult(location);
    public Task ReceiveStockAsync(int itemId, decimal qty, string? lotNumber, string? certNumber, int? locationId, string userId, string? reference) => Task.CompletedTask;
    public Task ConsumeForJobAsync(int itemId, decimal qty, int jobId, int? lotId, string userId) => Task.CompletedTask;
    public Task TransferAsync(int itemId, decimal qty, int fromLocationId, int toLocationId, string userId) => Task.CompletedTask;
    public Task AdjustAsync(int itemId, decimal newQty, string reason, string userId) => Task.CompletedTask;
    public Task<List<InventoryTransaction>> GetTransactionHistoryAsync(int itemId) => Task.FromResult(new List<InventoryTransaction>());
    public Task<List<InventoryTransaction>> GetRecentTransactionsAsync(int count = 20) => Task.FromResult(new List<InventoryTransaction>());
    public Task ReserveForJobAsync(int itemId, decimal qty, int jobId) => Task.CompletedTask;
    public Task ReleaseReservationAsync(int itemId, decimal qty) => Task.CompletedTask;
    public Task<MaterialRequest> CreateRequestAsync(MaterialRequest request) => Task.FromResult(request);
    public Task FulfillRequestAsync(int requestId, decimal qty, int? lotId, string userId) => Task.CompletedTask;
    public Task<List<MaterialRequest>> GetPendingRequestsAsync() => Task.FromResult(new List<MaterialRequest>());
    public Task<List<InventoryLot>> GetLotsForItemAsync(int itemId) => Task.FromResult(new List<InventoryLot>());
}
