using InventorySystem.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
  public class CreateFinishedGoodViewModel : ISellableEntity
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

    [Display(Name = "Requires Serial Number")]
    public bool RequiresSerialNumber { get; set; } = true; // Default TRUE for Finished Goods

    [Display(Name = "Requires Model Number")]
    public bool RequiresModelNumber { get; set; } = true; // Default TRUE for Finished Goods

		[Display(Name = "Revenue Account Code")]
		[StringLength(10)]
		public string? PreferredRevenueAccountCode { get; set; }

		// Calculated properties
		public decimal ProfitMargin => SellingPrice > 0 && UnitCost > 0 ? ((SellingPrice - UnitCost) / SellingPrice) * 100 : 0;
    public decimal ProfitPerUnit => SellingPrice - UnitCost;
    public bool IsEditing => Id > 0;

    // Helper properties for requirements display
    public string RequirementsDisplay
    {
      get
      {
        var requirements = new List<string>();
        if (RequiresSerialNumber) requirements.Add("Serial Number");
        if (RequiresModelNumber) requirements.Add("Model Number");
        return requirements.Any() ? string.Join(", ", requirements) : "None";
      }
    }

    public bool HasRequirements => RequiresSerialNumber || RequiresModelNumber;

    // Image upload support
    [Display(Name = "Product Image")]
    public IFormFile? ImageFile { get; set; }

    [Display(Name = "Has Image")]
    public bool HasImage { get; set; }

    [Display(Name = "Image File Name")]
    public string? ImageFileName { get; set; }

		// ISellableEntity implementation
		public string DisplayName => !string.IsNullOrEmpty(PartNumber) ? $"{PartNumber} - {Description}" : Description;

		string ISellableEntity.Description => Description;

		public decimal SalePrice => SellingPrice;

		public bool IsSellable => SellingPrice > 0;

		public string EntityType => "FinishedGood";

		public string? Code => PartNumber;

		public string GetDefaultRevenueAccountCode()
		{
			if (!string.IsNullOrEmpty(PreferredRevenueAccountCode))
				return PreferredRevenueAccountCode;

			return "4000"; // Product Sales
		}
	}
}