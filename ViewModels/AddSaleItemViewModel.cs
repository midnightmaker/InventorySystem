// ViewModels/AddSaleItemViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
  public class AddSaleItemViewModel
  {
    public int SaleId { get; set; }

    [Required]
    [Display(Name = "Product Type")]
    public string ProductType { get; set; } = "Item"; // "Item" or "FinishedGood"

    [Display(Name = "Item")]
    public int? ItemId { get; set; }

    [Display(Name = "Finished Good")]
    public int? FinishedGoodId { get; set; }

    [Required]
    [Display(Name = "Quantity")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; }

    [Required]
    [Display(Name = "Unit Price")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
    public decimal UnitPrice { get; set; }

    public string? Notes { get; set; }
  }
}