using System.ComponentModel.DataAnnotations.Schema;

namespace Vectrik.Models;

public class ShipmentLine
{
    public int Id { get; set; }

    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;

    public int WorkOrderLineId { get; set; }
    public WorkOrderLine WorkOrderLine { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityShipped { get; set; }
}
