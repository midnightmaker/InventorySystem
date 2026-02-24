using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
    public class SaleDetailsViewModel
    {
        public Sale Sale { get; set; } = null!;
        public List<ServiceOrder> ServiceOrders { get; set; } = new();

        // Shipments data for the comprehensive shipments card
        public List<Shipment> Shipments { get; set; } = new();

        /// <summary>
        /// Service type names whose required documents are still missing.
        /// Populated by the controller when SaleStatus == PartiallyShipped.
        /// </summary>
        public List<string> MissingDocumentServiceNames { get; set; } = new();

        public bool HasMissingDocumentWarning => MissingDocumentServiceNames.Any();

        // Computed properties for shipment summary
        public int TotalShipments => Shipments?.Count ?? 0;
        public int TotalItemsShipped => Shipments?.SelectMany(s => s.ShipmentItems).Sum(si => si.QuantityShipped) ?? 0;
        public decimal TotalShipmentValue => Shipments?.SelectMany(s => s.ShipmentItems).Sum(si => si.QuantityShipped * si.SaleItem.UnitPrice) ?? 0;
        public bool HasMultipleShipments => TotalShipments > 1;
        public DateTime? FirstShipmentDate => Shipments?.OrderBy(s => s.ShipmentDate).FirstOrDefault()?.ShipmentDate;
        public DateTime? LastShipmentDate => Shipments?.OrderByDescending(s => s.ShipmentDate).FirstOrDefault()?.ShipmentDate;

        /// <summary>
        /// A sale can be manually marked as Delivered when it is Shipped or PartiallyShipped
        /// (covers the case where PartiallyShipped was set solely because of missing service docs,
        /// not because of remaining backordered units).
        /// </summary>
        public bool CanMarkAsDelivered =>
            Sale?.SaleStatus == SaleStatus.Shipped ||
            Sale?.SaleStatus == SaleStatus.PartiallyShipped;

        // Computed properties for consolidated status display
        public string ConsolidatedStatus
        {
            get
            {
                if (Sale?.SaleStatus == null) return "Unknown";

                return Sale.SaleStatus switch
                {
                    SaleStatus.Quotation => "Quotation",
                    SaleStatus.Processing => "Processing",
                    SaleStatus.Backordered when TotalShipments > 0 => $"Partially Shipped ({TotalShipments} shipment{(TotalShipments > 1 ? "s" : "")})",
                    SaleStatus.Backordered => "Backordered",
                    SaleStatus.PartiallyShipped when HasMissingDocumentWarning => $"Partially Shipped — Pending Service Docs",
                    SaleStatus.PartiallyShipped when TotalShipments > 1 => $"Partially Shipped ({TotalShipments} shipments)",
                    SaleStatus.PartiallyShipped => "Partially Shipped",
                    SaleStatus.Shipped when TotalShipments > 1 => $"Fully Shipped ({TotalShipments} shipments)",
                    SaleStatus.Shipped => "Shipped",
                    SaleStatus.Delivered when TotalShipments > 1 => $"Delivered ({TotalShipments} shipments)",
                    SaleStatus.Delivered => "Delivered",
                    SaleStatus.Cancelled => "Cancelled",
                    _ => Sale.SaleStatus.ToString()
                };
            }
        }

        public string ConsolidatedStatusBadgeColor
        {
            get
            {
                return Sale?.SaleStatus switch
                {
                    SaleStatus.Quotation      => "secondary",
                    SaleStatus.Processing     => "primary",
                    SaleStatus.Backordered    => "warning",
                    SaleStatus.PartiallyShipped => HasMissingDocumentWarning ? "danger" : "warning",
                    SaleStatus.Shipped        => "success",
                    SaleStatus.Delivered      => "info",
                    SaleStatus.Cancelled      => "danger",
                    _ => "secondary"
                };
            }
        }
    }
}