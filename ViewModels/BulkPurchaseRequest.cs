// ViewModels/BulkPurchaseRequest.cs
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
  public class BulkPurchaseRequest
  {
    [Required]
    public int BomId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; }

    [Display(Name = "Include Safety Stock")]
    public bool IncludeSafetyStock { get; set; } = true;

    [Display(Name = "Safety Stock Multiplier")]
    [Range(1.0, 5.0, ErrorMessage = "Safety stock multiplier must be between 1.0 and 5.0")]
    public decimal SafetyStockMultiplier { get; set; } = 1.2m;

    [Display(Name = "Purchase Order Number")]
    public string? PurchaseOrderNumber { get; set; }

    [Display(Name = "Expected Delivery Date")]
    public DateTime? ExpectedDeliveryDate { get; set; }

    public string? Notes { get; set; }

    // Selected items for purchase
    public List<ShortageItemPurchase> ItemsToPurchase { get; set; } = new List<ShortageItemPurchase>();
  }

  public class ShortageItemPurchase
  {
    public int ItemId { get; set; }
    public bool Selected { get; set; } = true;
    public int QuantityToPurchase { get; set; }
    public decimal EstimatedUnitCost { get; set; }
    public string? PreferredVendor { get; set; }
    public string? Notes { get; set; }
  }
}