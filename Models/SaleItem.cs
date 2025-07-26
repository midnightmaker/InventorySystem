// Models/SaleItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class SaleItem
  {
    public int Id { get; set; }

    public int SaleId { get; set; }
    public virtual Sale Sale { get; set; } = null!;

    // Can sell either raw items or finished goods
    public int? ItemId { get; set; }
    public virtual Item? Item { get; set; }

    public int? FinishedGoodId { get; set; }
    public virtual FinishedGood? FinishedGood { get; set; }

    [Required]
    [Display(Name = "Quantity Sold")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int QuantitySold { get; set; }

    [Required]
    [Display(Name = "Unit Price")]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Unit Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [Display(Name = "Total Price")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice => QuantitySold * UnitPrice;

    [Display(Name = "Total Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost => QuantitySold * UnitCost;

    [Display(Name = "Profit")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Profit => TotalPrice - TotalCost;

    [Display(Name = "Profit Margin")]
    [Column(TypeName = "decimal(18,4)")]
    public decimal ProfitMargin => TotalPrice > 0 ? (Profit / TotalPrice) * 100 : 0;

    public string? Notes { get; set; }

    // Helper properties
    [NotMapped]
    public string ProductName => Item?.Description ?? FinishedGood?.Description ?? "Unknown";

    [NotMapped]
    public string ProductPartNumber => Item?.PartNumber ?? FinishedGood?.PartNumber ?? "Unknown";
  }
}
