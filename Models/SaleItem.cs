using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class SaleItem
  {
    public int Id { get; set; }
    
    [Required]
    public int SaleId { get; set; }
    public virtual Sale Sale { get; set; } = null!;

    // Item relationship (for physical products)
    public int? ItemId { get; set; }
    public virtual Item? Item { get; set; }

    // ServiceType relationship (for services)
    public int? ServiceTypeId { get; set; }
    public virtual ServiceType? ServiceType { get; set; }

    // FinishedGood relationship for manufactured products
    public int? FinishedGoodId { get; set; }
    public virtual FinishedGood? FinishedGood { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; } = 1;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
    public decimal UnitPrice { get; set; }

    public string? Notes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    public int QuantitySold { get; set; }
    public int QuantityBackordered { get; set; }
    public string? SerialNumber { get; set; }
    public string? ModelNumber { get; set; }

    // Computed properties using the shared Quantity field
    [NotMapped]
    public decimal ExtendedPrice => Quantity * UnitPrice;

    [NotMapped]
    public decimal TotalPrice => QuantitySold * UnitPrice;

    [NotMapped]
    public decimal TotalCost => QuantitySold * UnitCost;

    [NotMapped]
    public decimal Profit => TotalPrice - TotalCost;

    [NotMapped]
    public string DisplayName => Item?.DisplayPartNumber ?? ServiceType?.DisplayName ?? FinishedGood?.PartNumber ?? "Unknown";

    [NotMapped]
    public string EntityType => Item != null ? "Item" : ServiceType != null ? "ServiceType" : FinishedGood != null ? "FinishedGood" : "Unknown";

    [NotMapped]
    public bool IsService => ServiceTypeId.HasValue;

    [NotMapped]
    public bool IsItem => ItemId.HasValue;

    [NotMapped]
    public bool IsFinishedGood => FinishedGoodId.HasValue;

    [NotMapped]
    public bool HasSerialModelInfo => !string.IsNullOrEmpty(SerialNumber) || !string.IsNullOrEmpty(ModelNumber);

    [NotMapped]
    public string ProductName => Item?.Description ?? ServiceType?.ServiceName ?? FinishedGood?.Description ?? "Unknown";

    [NotMapped]
    public string ProductPartNumber => Item?.PartNumber ?? ServiceType?.ServiceCode ?? FinishedGood?.PartNumber ?? "Unknown";

    // Validation - ensure only one product type is selected
    public bool IsValid => (ItemId.HasValue && !ServiceTypeId.HasValue && !FinishedGoodId.HasValue) || 
                          (!ItemId.HasValue && ServiceTypeId.HasValue && !FinishedGoodId.HasValue) ||
                          (!ItemId.HasValue && !ServiceTypeId.HasValue && FinishedGoodId.HasValue);
                          

    // Add these properties to the SaleItem class

    [NotMapped]
    [Display(Name = "Available for Shipment")]
    public bool IsAvailableForShipment
    {
        get
        {
            if (QuantityBackordered <= 0) return false;
            
            // For Items - check current stock
            if (ItemId.HasValue && Item != null)
            {
                return Item.TrackInventory ? Item.CurrentStock >= QuantityBackordered : true;
            }
            
            // For Finished Goods - check current stock
            if (FinishedGoodId.HasValue && FinishedGood != null)
            {
                return FinishedGood.CurrentStock >= QuantityBackordered;
            }
            
            // Services are always "available"
            if (ServiceTypeId.HasValue)
            {
                return true;
            }
            
            return false;
        }
    }

    [NotMapped]
    [Display(Name = "Available Stock")]
    public int AvailableStock
    {
        get
        {
            if (ItemId.HasValue && Item != null)
            {
                return Item.TrackInventory ? Item.CurrentStock : int.MaxValue;
            }
            
            if (FinishedGoodId.HasValue && FinishedGood != null)
            {
                return FinishedGood.CurrentStock;
            }
            
            return int.MaxValue; // Services
        }
    }

    [NotMapped]
    [Display(Name = "Can Fulfill Quantity")]
    public int CanFulfillQuantity
    {
        get
        {
            return Math.Min(QuantityBackordered, AvailableStock);
        }
    }
  }
}