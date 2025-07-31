using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
  public class CreateFinishedGoodViewModel
  {
    public int Id { get; set; } // For editing

    [Required]
    [Display(Name = "Part Number")]
    [StringLength(50, ErrorMessage = "Part number cannot exceed 50 characters")]
    public string PartNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Description")]
    [StringLength(200, ErrorMessage = "Description cannot exceed 200 characters")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Linked BOM")]
    public int? BomId { get; set; }

    [Required]
    [Display(Name = "Unit Cost")]
    [Range(0, double.MaxValue, ErrorMessage = "Unit cost must be 0 or greater")]
    public decimal UnitCost { get; set; }

    [Display(Name = "Selling Price")]
    [Range(0, double.MaxValue, ErrorMessage = "Selling price must be 0 or greater")]
    public decimal SellingPrice { get; set; }

    [Required]
    [Display(Name = "Current Stock")]
    [Range(0, int.MaxValue, ErrorMessage = "Current stock must be 0 or greater")]
    public int CurrentStock { get; set; }

    [Required]
    [Display(Name = "Minimum Stock")]
    [Range(0, int.MaxValue, ErrorMessage = "Minimum stock must be 0 or greater")]
    public int MinimumStock { get; set; }

    // Calculated properties
    public decimal ProfitMargin => SellingPrice > 0 && UnitCost > 0 ? ((SellingPrice - UnitCost) / SellingPrice) * 100 : 0;
    public decimal ProfitPerUnit => SellingPrice - UnitCost;
    public bool IsEditing => Id > 0;
  }
}