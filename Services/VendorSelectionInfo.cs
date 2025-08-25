// Services/VendorSelectionInfo.cs - New class for vendor selection information
using InventorySystem.Models;

namespace InventorySystem.Services
{
  /// <summary>
  /// Contains comprehensive vendor selection information for an item
  /// Used to determine the best vendor based on priority rules
  /// UPDATED: Simplified to use only VendorItem.IsPrimary system
  /// </summary>
  public class VendorSelectionInfo
  {
    public int ItemId { get; set; }

    // Primary vendor from VendorItem relationship (highest priority)
    public Vendor? PrimaryVendor { get; set; }
    public decimal? PrimaryVendorCost { get; set; }

    // Item preferred vendor name (for compatibility with existing UI)
    public string? ItemPreferredVendorName { get; set; }

    // Last purchase vendor (fallback priority)
    public Vendor? LastPurchaseVendor { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public decimal? LastPurchaseCost { get; set; }

    // Recommended selection based on priority rules
    public Vendor? RecommendedVendor { get; set; }
    public decimal? RecommendedCost { get; set; }
    public string SelectionReason { get; set; } = string.Empty;

    // Helper properties
    public bool HasPrimaryVendor => PrimaryVendor != null;
    public bool HasLastPurchaseVendor => LastPurchaseVendor != null;
    public bool HasRecommendation => RecommendedVendor != null;
    public bool HasItemPreferredVendor => !string.IsNullOrEmpty(ItemPreferredVendorName);
  }
}