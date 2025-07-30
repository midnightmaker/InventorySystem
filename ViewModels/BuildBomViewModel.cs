// ViewModels/BuildBomViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
  public class BuildBomViewModel
  {
    [Required]
    [Display(Name = "BOM")]
    public int BomId { get; set; }

    [Required]
    [Display(Name = "Quantity to Build")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; }

    [Required]
    [Display(Name = "Production Date")]
    public DateTime ProductionDate { get; set; } = DateTime.Now;

    [Display(Name = "Labor Cost")]
    [Range(0, double.MaxValue, ErrorMessage = "Labor cost cannot be negative")]
    public decimal LaborCost { get; set; }

    [Display(Name = "Overhead Cost")]
    [Range(0, double.MaxValue, ErrorMessage = "Overhead cost cannot be negative")]
    public decimal OverheadCost { get; set; }

    public string? Notes { get; set; }

    // Read-only display properties
    public string BomName { get; set; } = string.Empty;
    public string BomDescription { get; set; } = string.Empty;
    public bool CanBuild { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal TotalCost => MaterialCost + LaborCost + OverheadCost;
    public decimal UnitCost => Quantity > 0 ? TotalCost / Quantity : 0;
    public bool CreateWithWorkflow { get; set; } = true;
  }
}