using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class PackingSlipViewModel
    {
        public Sale Sale { get; set; } = null!;
        public List<PackingSlipItem> Items { get; set; } = new();
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = string.Empty;
        public string PackingSlipNumber { get; set; } = string.Empty;

        // Company info
        public CompanyInfo CompanyInfo { get; set; } = new();
        
        // NEW: Shipment reference for multi-shipment support
        public Shipment? Shipment { get; set; }

        // Computed properties
        public int TotalItemCount => Items.Sum(i => i.Quantity);
        public int TotalLineItems => Items.Count;
        public decimal TotalWeight => Items.Sum(i => i.Weight ?? 0);
        public bool IsPartialShipment => Items.Any(i => i.IsBackordered);
        public int TotalShippedItems => Items.Sum(i => i.QuantityShipped);
        public int TotalBackorderedItems => Items.Sum(i => i.QuantityBackordered);
    }

    public class PackingSlipItem
    {
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } // Total quantity ordered
        public string UnitOfMeasure { get; set; } = "Each";
        public decimal? Weight { get; set; }
        public string? Notes { get; set; }
        public bool IsBackordered { get; set; }
        public int QuantityBackordered { get; set; }
        public int QuantityShipped { get; set; } // Actual quantity in this shipment
    }
}