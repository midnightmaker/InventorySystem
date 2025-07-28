// Make sure your Purchase.cs model has these validation attributes:

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class Purchase
  {
    public int Id { get; set; }

    [Required(ErrorMessage = "Please select an item")]
    public int ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;

    [Required(ErrorMessage = "Vendor name is required")]
    [StringLength(200, ErrorMessage = "Vendor name cannot exceed 200 characters")]
    public string Vendor { get; set; } = string.Empty;

    [Required(ErrorMessage = "Purchase date is required")]
    [Display(Name = "Purchase Date")]
    public DateTime PurchaseDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Quantity is required")]
    [Display(Name = "Quantity Purchased")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int QuantityPurchased { get; set; }

    [Required(ErrorMessage = "Cost per unit is required")]
    [Display(Name = "Cost Per Unit")]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Cost per unit must be greater than 0")]
    public decimal CostPerUnit { get; set; }

    [Display(Name = "Total Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost => QuantityPurchased * CostPerUnit;

    [Display(Name = "Remaining Quantity")]
    public int RemainingQuantity { get; set; }

    [Display(Name = "Purchase Order Number")]
    [StringLength(100, ErrorMessage = "PO number cannot exceed 100 characters")]
    public string? PurchaseOrderNumber { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }

    [Display(Name = "Shipping Cost")]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0, double.MaxValue, ErrorMessage = "Shipping cost cannot be negative")]
    public decimal ShippingCost { get; set; } = 0;

    [Display(Name = "Tax Amount")]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0, double.MaxValue, ErrorMessage = "Tax amount cannot be negative")]
    public decimal TaxAmount { get; set; } = 0;

    [Display(Name = "Total Paid")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPaid => TotalCost + ShippingCost + TaxAmount;

    [Display(Name = "Item Version")]
    public string? ItemVersion { get; set; }

    public int? ItemVersionId { get; set; } // Specific version reference
    public virtual Item? ItemVersionReference { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // Navigation property for purchase documents
    public virtual ICollection<PurchaseDocument> PurchaseDocuments { get; set; } = new List<PurchaseDocument>();

    // Helper properties
    [NotMapped]
    public bool HasDocuments => PurchaseDocuments?.Any() == true;

    [NotMapped]
    public int DocumentCount => PurchaseDocuments?.Count ?? 0;
  }
}