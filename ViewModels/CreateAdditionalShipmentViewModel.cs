using System.ComponentModel.DataAnnotations;
using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class CreateAdditionalShipmentViewModel
    {
        public int SaleId { get; set; }
        public Sale Sale { get; set; } = null!;
        
        [Required]
        [Display(Name = "Courier Service")]
        public string CourierService { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Tracking Number")]
        public string TrackingNumber { get; set; } = string.Empty;
        
        [Display(Name = "Expected Delivery Date")]
        public DateTime? ExpectedDeliveryDate { get; set; }
        
        [Display(Name = "Package Weight")]
        public decimal? PackageWeight { get; set; }
        
        [Display(Name = "Package Dimensions")]
        public string? PackageDimensions { get; set; }
        
        [Display(Name = "Shipping Instructions")]
        public string? ShippingInstructions { get; set; }
        
        public List<ShippableItemViewModel> AvailableItems { get; set; } = new();
    }

    public class ShippableItemViewModel
    {
        public int SaleItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductPartNumber { get; set; } = string.Empty;
        public int QuantityBackordered { get; set; }
        public int CanFulfillQuantity { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Quantity to ship cannot be negative")]
        [Display(Name = "Quantity to Ship")]
        public int QuantityToShip { get; set; }
        
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => QuantityToShip * UnitPrice;
    }
}