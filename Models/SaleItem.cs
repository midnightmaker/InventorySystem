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

    // NEW - Track backorder quantities
    [Display(Name = "Quantity Backordered")]
    public int QuantityBackordered { get; set; } = 0;

    [Required]
    [Display(Name = "Unit Price")]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Unit Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    // ✅ NEW: Serial Number and Model Number fields
    [StringLength(100, ErrorMessage = "Serial number cannot exceed 100 characters")]
    [Display(Name = "Serial Number")]
    public string? SerialNumber { get; set; }

    [StringLength(100, ErrorMessage = "Model number cannot exceed 100 characters")]
    [Display(Name = "Model Number")]
    public string? ModelNumber { get; set; }

    [NotMapped]
    [Display(Name = "Total Price")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice => QuantitySold * UnitPrice;

    [Display(Name = "Total Cost")]
    [NotMapped]
    public decimal TotalCost => QuantitySold * UnitCost;

    [Display(Name = "Profit")]
    [NotMapped]
    public decimal Profit => TotalPrice - TotalCost;

    [Display(Name = "Profit Margin")]
    [NotMapped]
    public decimal ProfitMargin => TotalPrice > 0 ? (Profit / TotalPrice) * 100 : 0;

    // NEW - Calculated properties for backorder management
    [NotMapped]
    [Display(Name = "Quantity Available")]
    public int QuantityAvailable => QuantitySold - QuantityBackordered;

    [NotMapped]
    [Display(Name = "Is Backordered")]
    public bool IsBackordered => QuantityBackordered > 0;

    [NotMapped]
    [Display(Name = "Backorder Status")]
    public string BackorderStatus => QuantityBackordered > 0 ? $"{QuantityBackordered} backordered" : "In stock";

    public string? Notes { get; set; }

    // Helper properties
    [NotMapped]
    public string ProductName => Item?.Description ?? FinishedGood?.Description ?? "Unknown";

    [NotMapped]
    public string ProductPartNumber => Item?.PartNumber ?? FinishedGood?.PartNumber ?? "Unknown";

    // ✅ NEW: Helper properties for serial/model requirements
    [NotMapped]
    [Display(Name = "Requires Serial Number")]
    public bool RequiresSerialNumber => Item?.RequiresSerialNumber == true || FinishedGood?.RequiresSerialNumber == true;

    [NotMapped]
    [Display(Name = "Requires Model Number")]
    public bool RequiresModelNumber => Item?.RequiresModelNumber == true || FinishedGood?.RequiresModelNumber == true;

    [NotMapped]
    [Display(Name = "Has Serial/Model Info")]
    public bool HasSerialModelInfo => !string.IsNullOrWhiteSpace(SerialNumber) || !string.IsNullOrWhiteSpace(ModelNumber);

    [NotMapped]
    [Display(Name = "Serial/Model Display")]
    public string SerialModelDisplay
    {
      get
      {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(SerialNumber)) parts.Add($"S/N: {SerialNumber}");
        if (!string.IsNullOrWhiteSpace(ModelNumber)) parts.Add($"Model: {ModelNumber}");
        return parts.Any() ? string.Join(" | ", parts) : "Not Specified";
      }
    }

    // ✅ NEW: Validation method for requirements
    public bool ValidateRequirements(out List<string> errors)
    {
      errors = new List<string>();

      if (RequiresSerialNumber && string.IsNullOrWhiteSpace(SerialNumber))
      {
        errors.Add($"Serial number is required for {ProductPartNumber}");
      }

      if (RequiresModelNumber && string.IsNullOrWhiteSpace(ModelNumber))
      {
        errors.Add($"Model number is required for {ProductPartNumber}");
      }

      return !errors.Any();
    }
  }
}