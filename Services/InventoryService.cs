using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class InventoryService : IInventoryService
{
    private readonly TenantDbContext _db;

    public InventoryService(TenantDbContext db)
    {
        _db = db;
    }

    // ── Item CRUD ──

    public async Task<List<InventoryItem>> GetAllItemsAsync(InventoryItemType? type = null, bool activeOnly = true)
    {
        var query = _db.InventoryItems.AsQueryable();
        if (activeOnly) query = query.Where(i => i.IsActive);
        if (type.HasValue) query = query.Where(i => i.ItemType == type.Value);
        return await query.OrderBy(i => i.ItemNumber).ToListAsync();
    }

    public async Task<InventoryItem?> GetItemByIdAsync(int id)
    {
        return await _db.InventoryItems
            .Include(i => i.Material)
            .Include(i => i.Lots.Where(l => l.Status != LotStatus.Depleted))
                .ThenInclude(l => l.Location)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<InventoryItem> CreateItemAsync(InventoryItem item)
    {
        item.CreatedDate = DateTime.UtcNow;
        item.LastModifiedDate = DateTime.UtcNow;
        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<InventoryItem> UpdateItemAsync(InventoryItem item)
    {
        item.LastModifiedDate = DateTime.UtcNow;
        _db.InventoryItems.Update(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task DeleteItemAsync(int id)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item == null) throw new InvalidOperationException("Item not found.");
        item.IsActive = false;
        await _db.SaveChangesAsync();
    }

    public async Task<decimal> GetAvailableQtyAsync(int itemId)
    {
        var item = await _db.InventoryItems.FindAsync(itemId);
        return item?.AvailableQty ?? 0;
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync()
    {
        return await _db.InventoryItems
            .Where(i => i.IsActive && i.ReorderPoint > 0 && i.CurrentStockQty <= i.ReorderPoint)
            .OrderBy(i => i.CurrentStockQty - i.ReorderPoint)
            .ToListAsync();
    }

    // ── Stock Locations ──

    public async Task<List<StockLocation>> GetAllLocationsAsync(bool activeOnly = true)
    {
        var query = _db.StockLocations.AsQueryable();
        if (activeOnly) query = query.Where(l => l.IsActive);
        return await query.OrderBy(l => l.Code).ToListAsync();
    }

    public async Task<StockLocation> CreateLocationAsync(StockLocation location)
    {
        _db.StockLocations.Add(location);
        await _db.SaveChangesAsync();
        return location;
    }

    public async Task<StockLocation> UpdateLocationAsync(StockLocation location)
    {
        _db.StockLocations.Update(location);
        await _db.SaveChangesAsync();
        return location;
    }

    // ── Transactions ──

    public async Task ReceiveStockAsync(int itemId, decimal qty, string? lotNumber, string? certNumber,
                                        int? locationId, string userId, string? reference)
    {
        var item = await _db.InventoryItems.FindAsync(itemId)
            ?? throw new InvalidOperationException("Item not found.");

        var qtyBefore = item.CurrentStockQty;
        item.CurrentStockQty += qty;
        item.LastModifiedDate = DateTime.UtcNow;

        int? lotId = null;
        if (!string.IsNullOrWhiteSpace(lotNumber) && item.TrackLots)
        {
            var lot = new InventoryLot
            {
                InventoryItemId = itemId,
                LotNumber = lotNumber,
                CertificateNumber = certNumber,
                ReceivedQty = qty,
                CurrentQty = qty,
                StockLocationId = locationId,
                ReceivedAt = DateTime.UtcNow,
                Status = LotStatus.Available
            };
            _db.InventoryLots.Add(lot);
            await _db.SaveChangesAsync();
            lotId = lot.Id;
        }

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryItemId = itemId,
            TransactionType = TransactionType.Receipt,
            Quantity = qty,
            QuantityBefore = qtyBefore,
            QuantityAfter = item.CurrentStockQty,
            ToLocationId = locationId,
            LotId = lotId,
            PerformedByUserId = userId,
            Reference = reference,
            TransactedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task ConsumeForJobAsync(int itemId, decimal qty, int jobId, int? lotId, string userId)
    {
        var item = await _db.InventoryItems.FindAsync(itemId)
            ?? throw new InvalidOperationException("Item not found.");

        if (item.AvailableQty < qty)
            throw new InvalidOperationException($"Insufficient stock. Available: {item.AvailableQty}, Requested: {qty}");

        var qtyBefore = item.CurrentStockQty;
        item.CurrentStockQty -= qty;
        item.LastModifiedDate = DateTime.UtcNow;

        if (lotId.HasValue)
        {
            var lot = await _db.InventoryLots.FindAsync(lotId.Value);
            if (lot != null)
            {
                lot.CurrentQty -= qty;
                if (lot.CurrentQty <= 0) lot.Status = LotStatus.Depleted;
            }
        }

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryItemId = itemId,
            TransactionType = TransactionType.JobConsumption,
            Quantity = -qty,
            QuantityBefore = qtyBefore,
            QuantityAfter = item.CurrentStockQty,
            LotId = lotId,
            JobId = jobId,
            PerformedByUserId = userId,
            Reference = $"Job #{jobId}",
            TransactedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task TransferAsync(int itemId, decimal qty, int fromLocationId, int toLocationId, string userId)
    {
        var item = await _db.InventoryItems.FindAsync(itemId)
            ?? throw new InvalidOperationException("Item not found.");

        if (qty <= 0)
            throw new InvalidOperationException("Transfer quantity must be greater than zero.");

        // Find lots at the source location, oldest first (FIFO)
        var sourceLots = await _db.InventoryLots
            .Where(l => l.InventoryItemId == itemId
                     && l.StockLocationId == fromLocationId
                     && l.Status != LotStatus.Depleted
                     && l.CurrentQty > 0)
            .OrderBy(l => l.ReceivedAt)
            .ToListAsync();

        var totalAvailable = sourceLots.Sum(l => l.CurrentQty);
        if (totalAvailable < qty)
            throw new InvalidOperationException(
                $"Insufficient stock at source location. Available: {totalAvailable}, Requested: {qty}");

        var qtyBefore = item.CurrentStockQty;
        var remaining = qty;

        // Deduct from source lots FIFO and create/add to destination lots
        foreach (var sourceLot in sourceLots)
        {
            if (remaining <= 0) break;

            var deduct = Math.Min(sourceLot.CurrentQty, remaining);
            sourceLot.CurrentQty -= deduct;
            if (sourceLot.CurrentQty <= 0)
                sourceLot.Status = LotStatus.Depleted;

            // Find or create a matching lot at the destination location
            var destLot = await _db.InventoryLots
                .FirstOrDefaultAsync(l => l.InventoryItemId == itemId
                                       && l.StockLocationId == toLocationId
                                       && l.LotNumber == sourceLot.LotNumber
                                       && l.Status != LotStatus.Depleted);

            if (destLot != null)
            {
                destLot.CurrentQty += deduct;
            }
            else
            {
                _db.InventoryLots.Add(new InventoryLot
                {
                    InventoryItemId = itemId,
                    LotNumber = sourceLot.LotNumber,
                    CertificateNumber = sourceLot.CertificateNumber,
                    ReceivedQty = deduct,
                    CurrentQty = deduct,
                    StockLocationId = toLocationId,
                    ReceivedAt = DateTime.UtcNow,
                    Status = LotStatus.Available
                });
            }

            remaining -= deduct;
        }

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryItemId = itemId,
            TransactionType = TransactionType.Transfer,
            Quantity = qty,
            QuantityBefore = qtyBefore,
            QuantityAfter = qtyBefore, // Transfer doesn't change total stock, just location
            FromLocationId = fromLocationId,
            ToLocationId = toLocationId,
            PerformedByUserId = userId,
            Reference = $"Transfer {fromLocationId} → {toLocationId}",
            TransactedAt = DateTime.UtcNow
        });

        item.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task AdjustAsync(int itemId, decimal newQty, string reason, string userId)
    {
        var item = await _db.InventoryItems.FindAsync(itemId)
            ?? throw new InvalidOperationException("Item not found.");

        var qtyBefore = item.CurrentStockQty;
        var diff = newQty - qtyBefore;
        item.CurrentStockQty = newQty;
        item.LastModifiedDate = DateTime.UtcNow;

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryItemId = itemId,
            TransactionType = TransactionType.CycleCount,
            Quantity = diff,
            QuantityBefore = qtyBefore,
            QuantityAfter = newQty,
            PerformedByUserId = userId,
            Reference = reason,
            Notes = $"Cycle count adjustment: {qtyBefore} → {newQty}",
            TransactedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task<List<InventoryTransaction>> GetTransactionHistoryAsync(int itemId)
    {
        return await _db.InventoryTransactions
            .Where(t => t.InventoryItemId == itemId)
            .Include(t => t.Lot)
            .OrderByDescending(t => t.TransactedAt)
            .ToListAsync();
    }

    public async Task<List<InventoryTransaction>> GetRecentTransactionsAsync(int count = 20)
    {
        return await _db.InventoryTransactions
            .Include(t => t.InventoryItem)
            .Include(t => t.Lot)
            .OrderByDescending(t => t.TransactedAt)
            .Take(count)
            .ToListAsync();
    }

    // ── Reservations ──

    public async Task ReserveForJobAsync(int itemId, decimal qty, int jobId)
    {
        var item = await _db.InventoryItems.FindAsync(itemId)
            ?? throw new InvalidOperationException("Item not found.");

        if (item.AvailableQty < qty)
            throw new InvalidOperationException($"Insufficient available stock to reserve. Available: {item.AvailableQty}");

        item.ReservedQty += qty;
        await _db.SaveChangesAsync();
    }

    public async Task ReleaseReservationAsync(int itemId, decimal qty)
    {
        var item = await _db.InventoryItems.FindAsync(itemId)
            ?? throw new InvalidOperationException("Item not found.");

        item.ReservedQty = Math.Max(0, item.ReservedQty - qty);
        await _db.SaveChangesAsync();
    }

    // ── Material Requests ──

    public async Task<MaterialRequest> CreateRequestAsync(MaterialRequest request)
    {
        request.RequestedAt = DateTime.UtcNow;
        request.Status = MaterialRequestStatus.Pending;
        _db.MaterialRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task FulfillRequestAsync(int requestId, decimal qty, int? lotId, string userId)
    {
        var request = await _db.MaterialRequests
            .Include(r => r.InventoryItem)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new InvalidOperationException("Request not found.");

        await ConsumeForJobAsync(request.InventoryItemId, qty, request.JobId, lotId, userId);

        request.QuantityIssued = (request.QuantityIssued ?? 0) + qty;
        request.LotId = lotId;
        request.FulfilledAt = DateTime.UtcNow;
        request.Status = request.QuantityIssued >= request.QuantityRequested
            ? MaterialRequestStatus.Fulfilled
            : MaterialRequestStatus.PartiallyFulfilled;

        await _db.SaveChangesAsync();
    }

    public async Task<List<MaterialRequest>> GetPendingRequestsAsync()
    {
        return await _db.MaterialRequests
            .Where(r => r.Status == MaterialRequestStatus.Pending || r.Status == MaterialRequestStatus.PartiallyFulfilled)
            .Include(r => r.Job)
            .Include(r => r.InventoryItem)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync();
    }

    // ── Lots ──

    public async Task<List<InventoryLot>> GetLotsForItemAsync(int itemId)
    {
        return await _db.InventoryLots
            .Where(l => l.InventoryItemId == itemId)
            .Include(l => l.Location)
            .OrderByDescending(l => l.ReceivedAt)
            .ToListAsync();
    }
}
