using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class ShippingService : IShippingService
{
    private readonly TenantDbContext _db;
    private readonly IWorkOrderService _workOrderService;
    private readonly INumberSequenceService _numberSequence;

    public ShippingService(TenantDbContext db, IWorkOrderService workOrderService, INumberSequenceService numberSequence)
    {
        _db = db;
        _workOrderService = workOrderService;
        _numberSequence = numberSequence;
    }

    public async Task<Shipment> CreateShipmentAsync(int workOrderId, string carrier, string? trackingNumber,
        int packageCount, string? packingListJson, string? notes, string shippedBy,
        List<(int WorkOrderLineId, decimal Quantity)> lines)
    {
        var shipment = new Shipment
        {
            ShipmentNumber = await _numberSequence.NextAsync("shipment"),
            WorkOrderId = workOrderId,
            Status = ShipmentStatus.Shipped,
            CarrierName = carrier,
            TrackingNumber = trackingNumber,
            PackageCount = packageCount,
            PackingListJson = packingListJson,
            ShipperNotes = notes,
            ShippedBy = shippedBy,
            ShippedAt = DateTime.UtcNow
        };

        foreach (var (woLineId, qty) in lines)
        {
            shipment.Lines.Add(new ShipmentLine
            {
                WorkOrderLineId = woLineId,
                QuantityShipped = qty
            });

            // Update WO line shipped quantity
            await _workOrderService.UpdateFulfillmentAsync(woLineId, producedDelta: 0, shippedDelta: (int)qty);
        }

        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();
        return shipment;
    }

    public async Task<List<Shipment>> GetShipmentsByWorkOrderAsync(int workOrderId)
    {
        return await _db.Shipments
            .Include(s => s.Lines).ThenInclude(l => l.WorkOrderLine)
            .Where(s => s.WorkOrderId == workOrderId)
            .OrderByDescending(s => s.ShippedAt)
            .ToListAsync();
    }

    public async Task<List<Shipment>> GetRecentShipmentsAsync(int count = 20)
    {
        return await _db.Shipments
            .Include(s => s.WorkOrder)
            .Include(s => s.Lines)
            .OrderByDescending(s => s.CreatedDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<Shipment?> GetByIdAsync(int shipmentId)
    {
        return await _db.Shipments
            .Include(s => s.WorkOrder)
            .Include(s => s.Lines).ThenInclude(l => l.WorkOrderLine)
            .FirstOrDefaultAsync(s => s.Id == shipmentId);
    }
}
