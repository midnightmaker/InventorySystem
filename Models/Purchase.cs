using InventorySystem.Models.Enums;
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

    [NotMapped]
    [Display(Name = "Total Cost")]
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

    [NotMapped]
    [Display(Name = "Total Paid")]
    public decimal TotalPaid => TotalCost + ShippingCost + TaxAmount;

    [Display(Name = "Item Version")]
    public string? ItemVersion { get; set; }

    public int? ItemVersionId { get; set; }
    public virtual Item? ItemVersionReference { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Display(Name = "Purchase Status")]
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;

    [Display(Name = "Expected Delivery Date")]
    [DataType(DataType.Date)]
    public DateTime? ExpectedDeliveryDate { get; set; }

    [Display(Name = "Actual Delivery Date")]
    [DataType(DataType.Date)]
    public DateTime? ActualDeliveryDate { get; set; }

    // Navigation properties
    public virtual ICollection<PurchaseDocument> PurchaseDocuments { get; set; } = new List<PurchaseDocument>();

    // Computed properties
    [NotMapped]
    public bool HasDocuments => PurchaseDocuments?.Any() == true;

    [NotMapped]
    public int DocumentCount => PurchaseDocuments?.Count ?? 0;

    [NotMapped]
    public bool IsOverdue => ExpectedDeliveryDate.HasValue &&
                            ExpectedDeliveryDate.Value < DateTime.Today &&
                            Status != PurchaseStatus.Received &&
                            Status != PurchaseStatus.Cancelled;

    [NotMapped]
    public bool IsDelivered => Status == PurchaseStatus.Received ||
                              Status == PurchaseStatus.PartiallyReceived;

    [NotMapped]
    public int DaysUntilExpected => ExpectedDeliveryDate.HasValue ?
                                   (ExpectedDeliveryDate.Value - DateTime.Today).Days : 0;
  }
}