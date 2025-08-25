using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class ShipmentIndexViewModel
    {
        public int ShipmentId { get; set; }
        public string PackingSlipNumber { get; set; } = string.Empty;
        public DateTime ShipmentDate { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public int SaleId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        // NEW: Add Company Name for B2B priority display
        public string? CompanyName { get; set; }
        public string CourierService { get; set; } = string.Empty;
        public string TrackingNumber { get; set; } = string.Empty;
        public DateTime? ExpectedDeliveryDate { get; set; }
        public decimal? PackageWeight { get; set; }
        public string? PackageDimensions { get; set; }
        public string ShippedBy { get; set; } = string.Empty;
        public int TotalItemsShipped { get; set; }
        public decimal ShipmentValue { get; set; }
        public bool IsDelivered { get; set; }
        public DateTime? DeliveredDate { get; set; }
        
        // NEW: Computed property for B2B display priority
        public string DisplayName => !string.IsNullOrEmpty(CompanyName) ? 
                                   $"{CompanyName} ({CustomerName})" : 
                                   CustomerName;
        
        // Computed properties
        public bool IsOverdue => ExpectedDeliveryDate.HasValue && 
                                DateTime.Now > ExpectedDeliveryDate.Value && 
                                !IsDelivered;
        
        public int DaysInTransit => IsDelivered && DeliveredDate.HasValue ? 
                                   (DeliveredDate.Value - ShipmentDate).Days :
                                   (DateTime.Now - ShipmentDate).Days;
    }
}