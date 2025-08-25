// ViewModels/BulkPurchaseRequest.cs - Complete updated version
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

    // Primary vendor selection (highest priority)
    public int? VendorId { get; set; } // Selected vendor ID for dropdown
    public string? PreferredVendor { get; set; } // Keep for backward compatibility and display

    // Vendor priority information for UI display and selection
    public int? PrimaryVendorId { get; set; } // Primary vendor from VendorItem relationship
    public string? PrimaryVendorName { get; set; } // Primary vendor name

    public int? LastVendorId { get; set; } // Last vendor used for this item
    public string? LastVendorName { get; set; } // Last vendor name for display

    // Item preferred vendor name (for display in UI) - ADDED
    public string? ItemPreferredVendorName { get; set; }

    // Selection context for debugging and UI feedback
    public string? SelectionReason { get; set; } // Why this vendor was recommended

    public string? Notes { get; set; }

    // Helper properties for UI display
    public bool HasPrimaryVendor => PrimaryVendorId.HasValue;
    public bool HasLastVendor => LastVendorId.HasValue;
    public bool HasItemPreferredVendor => !string.IsNullOrEmpty(ItemPreferredVendorName); // ADDED

    public string VendorPriorityDisplay
    {
      get
      {
        if (HasPrimaryVendor) return "Primary Vendor";
        if (HasItemPreferredVendor) return "Item Preferred"; // UPDATED
        if (HasLastVendor) return "Last Used";
        return "No Preference";
      }
    }
  }
}