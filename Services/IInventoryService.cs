using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IInventoryService
{
    // Item CRUD
    Task<List<InventoryItem>> GetAllItemsAsync(InventoryItemType? type = null, bool activeOnly = true);
    Task<InventoryItem?> GetItemByIdAsync(int id);
    Task<InventoryItem> CreateItemAsync(InventoryItem item);
    Task<InventoryItem> UpdateItemAsync(InventoryItem item);
    Task DeleteItemAsync(int id);
    Task<decimal> GetAvailableQtyAsync(int itemId);
    Task<List<InventoryItem>> GetLowStockItemsAsync();

    // Stock Locations
    Task<List<StockLocation>> GetAllLocationsAsync(bool activeOnly = true);
    Task<StockLocation> CreateLocationAsync(StockLocation location);
    Task<StockLocation> UpdateLocationAsync(StockLocation location);

    // Transactions
    Task ReceiveStockAsync(int itemId, decimal qty, string? lotNumber, string? certNumber,
                           int? locationId, string userId, string? reference);
    Task ConsumeForJobAsync(int itemId, decimal qty, int jobId, int? lotId, string userId);
    Task TransferAsync(int itemId, decimal qty, int fromLocationId, int toLocationId, string userId);
    Task AdjustAsync(int itemId, decimal newQty, string reason, string userId);
    Task<List<InventoryTransaction>> GetTransactionHistoryAsync(int itemId);
    Task<List<InventoryTransaction>> GetRecentTransactionsAsync(int count = 20);

    // Reservations
    Task ReserveForJobAsync(int itemId, decimal qty, int jobId);
    Task ReleaseReservationAsync(int itemId, decimal qty);

    // Material Requests
    Task<MaterialRequest> CreateRequestAsync(MaterialRequest request);
    Task FulfillRequestAsync(int requestId, decimal qty, int? lotId, string userId);
    Task<List<MaterialRequest>> GetPendingRequestsAsync();

    // Lots
    Task<List<InventoryLot>> GetLotsForItemAsync(int itemId);
}
