// ViewModels/AddSaleItemViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
  public class AddSaleItemViewModel
  {
    public int SaleId { get; set; }

    [Required]
    [Display(Name = "Product Type")]
    public string ProductType { get; set; } = "Item"; // "Item", "FinishedGood", or "ServiceType"

    [Display(Name = "Item")]
    public int? ItemId { get; set; }

    [Display(Name = "Finished Good")]
    public int? FinishedGoodId { get; set; }

    // ✅ NEW: Add ServiceType support
    [Display(Name = "Service")]
    public int? ServiceTypeId { get; set; }

    [Required]
    [Display(Name = "Quantity")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; }

    [Required]
    [Display(Name = "Unit Price")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
    public decimal UnitPrice { get; set; }

    public string? Notes { get; set; }

		// Computed property
		public decimal TotalPrice => Quantity * UnitPrice;

		// ✅ EXISTING: Serial Number and Model Number fields
		[StringLength(100, ErrorMessage = "Serial number cannot exceed 100 characters")]
    [Display(Name = "Serial Number")]
    public string? SerialNumber { get; set; }

    [StringLength(100, ErrorMessage = "Model number cannot exceed 100 characters")]
    [Display(Name = "Model Number")]
    public string? ModelNumber { get; set; }

    // ✅ EXISTING: Helper properties for validation (populated via AJAX)
    public bool RequiresSerialNumber { get; set; }
    public bool RequiresModelNumber { get; set; }

    // ✅ NEW: Backorder support properties
    public bool AllowBackorder { get; set; } = true; // Allow backorder by default
    public int AvailableStock { get; set; } // Populated from AJAX
    public bool TracksInventory { get; set; } = true; // Populated from AJAX
    
    // Computed properties for backorder logic
    public bool WillBeBackordered => TracksInventory && Quantity > AvailableStock;
    public int BackorderedQuantity => WillBeBackordered ? Quantity - AvailableStock : 0;
    public int AvailableQuantity => WillBeBackordered ? AvailableStock : Quantity;
  }
}