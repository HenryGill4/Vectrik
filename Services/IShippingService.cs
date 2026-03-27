using Vectrik.Models;

namespace Vectrik.Services;

public interface IShippingService
{
    Task<Shipment> CreateShipmentAsync(int workOrderId, string carrier, string? trackingNumber,
        int packageCount, string? packingListJson, string? notes, string shippedBy,
        List<(int WorkOrderLineId, decimal Quantity)> lines);

    Task<List<Shipment>> GetShipmentsByWorkOrderAsync(int workOrderId);

    Task<List<Shipment>> GetRecentShipmentsAsync(int count = 20);

    Task<Shipment?> GetByIdAsync(int shipmentId);
}
