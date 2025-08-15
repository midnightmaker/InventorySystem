// ViewModels/CreatePurchaseViewModel.cs - Enhanced with higher precision decimal fields
using System;
using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
  public class CreatePurchaseViewModel
  {
    [Required(ErrorMessage = "Please select an item")]
    [Display(Name = "Item")]
    public int ItemId { get; set; }

    [Required(ErrorMessage = "Please select a vendor")]
    [Display(Name = "Vendor")]
    public int VendorId { get; set; }

    [Required(ErrorMessage = "Purchase date is required")]
    [Display(Name = "Purchase Date")]
    [DataType(DataType.Date)]
    public DateTime PurchaseDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Quantity is required")]
    [Display(Name = "Quantity Purchased")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int QuantityPurchased { get; set; }

    [Required(ErrorMessage = "Cost per unit is required")]
    [Display(Name = "Cost Per Unit")]
    [Range(0.0001, (double)decimal.MaxValue, ErrorMessage = "Cost per unit must be greater than 0")]
    public decimal CostPerUnit { get; set; }

    [Display(Name = "Shipping Cost")]
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Shipping cost cannot be negative")]
    public decimal ShippingCost { get; set; } = 0;

    [Display(Name = "Tax Amount")]
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Tax amount cannot be negative")]
    public decimal TaxAmount { get; set; } = 0;

    [Display(Name = "Purchase Order Number")]
    [StringLength(100, ErrorMessage = "PO number cannot exceed 100 characters")]
    public string? PurchaseOrderNumber { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }

    [Display(Name = "Purchase Order Status")]
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;

    [Display(Name = "Expected Delivery Date")]
    [DataType(DataType.Date)]
    public DateTime? ExpectedDeliveryDate { get; set; }

    [Display(Name = "Actual Delivery Date")]
    [DataType(DataType.Date)]
    public DateTime? ActualDeliveryDate { get; set; }

    // Helper properties for display
    public decimal TotalCost => QuantityPurchased * CostPerUnit;
    public decimal TotalPaid => TotalCost + ShippingCost + TaxAmount;
  }
}