using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels
{
  public class CreateItemViewModel
  {
    [Required]
    [StringLength(100, ErrorMessage = "Part Number cannot exceed 100 characters.")]
    [Display(Name = "Part Number")]
    public string PartNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Comments cannot exceed 1000 characters.")]
    [Display(Name = "Comments")]
    public string? Comments { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Minimum Stock must be 0 or greater.")]
    [Display(Name = "Minimum Stock")]
    public int MinimumStock { get; set; }

    [Display(Name = "Unit of Measure")]
    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

    [StringLength(100, ErrorMessage = "Vendor Part Number cannot exceed 100 characters.")]
    [Display(Name = "Vendor Part Number")]
    public string? VendorPartNumber { get; set; }

    [StringLength(200, ErrorMessage = "Preferred Vendor cannot exceed 200 characters.")]
    [Display(Name = "Preferred Vendor")]
    public string? PreferredVendor { get; set; }

    [Display(Name = "Preferred Vendor")]
    public int? PreferredVendorId { get; set; }

    [Display(Name = "Item can be sold")]
    public bool IsSellable { get; set; } = true;

    [Display(Name = "Expense Item")]
    public bool IsExpense { get; set; } = false;

    [Display(Name = "Item Type")]
    public ItemType ItemType { get; set; } = ItemType.Inventoried;

    [Required]
    [StringLength(10, ErrorMessage = "Version cannot exceed 10 characters.")]
    [Display(Name = "Version")]
    public string Version { get; set; } = "A";

    [Display(Name = "Item Image")]
    public IFormFile? ImageFile { get; set; }

    // Initial Purchase Section
    [Display(Name = "Add Initial Purchase")]
    public bool HasInitialPurchase { get; set; }

    [Display(Name = "Initial Quantity")]
    public int InitialQuantity { get; set; }

    [Display(Name = "Initial Cost Per Unit")]
    [DataType(DataType.Currency)]
    public decimal InitialCostPerUnit { get; set; }

    [Display(Name = "Initial Vendor")]
    public string? InitialVendor { get; set; }

    [Display(Name = "Initial Purchase Date")]
    [DataType(DataType.Date)]
    public DateTime? InitialPurchaseDate { get; set; }

    [Display(Name = "Initial Purchase Order Number")]
    public string? InitialPurchaseOrderNumber { get; set; }

    [StringLength(200, ErrorMessage = "Manufacturer cannot exceed 200 characters.")]
    [Display(Name = "Manufacturer")]
    public string? Manufacturer { get; set; }

    [StringLength(100, ErrorMessage = "Manufacturer Part Number cannot exceed 100 characters.")]
    [Display(Name = "Manufacturer Part Number")]
    public string? ManufacturerPartNumber { get; set; }

    // ENHANCED MATERIAL TYPE FIELDS WITH VALIDATION

    [Display(Name = "Material Type")]
    public MaterialType MaterialType { get; set; } = MaterialType.Standard;

    [Display(Name = "Parent Raw Material")]
    public int? ParentRawMaterialId { get; set; }

    [Display(Name = "Yield Factor")]
    [Range(0.01, 1.0, ErrorMessage = "Yield factor must be between 0.01 and 1.0 (1% to 100%)")]
    public decimal? YieldFactor { get; set; }

    [Display(Name = "Waste Percentage")]
    [Range(0, 50, ErrorMessage = "Waste percentage must be between 0 and 50%")]
    public decimal? WastePercentage { get; set; }

    // Sale Price Field
    [Display(Name = "Sale Price")]
    [Range(0, double.MaxValue, ErrorMessage = "Sale price must be 0 or greater")]
    [DataType(DataType.Currency)]
    public decimal? SalePrice { get; set; }

    // Helper properties
    public bool ShowStockFields => ItemType == ItemType.Inventoried || ItemType == ItemType.Consumable || ItemType == ItemType.RnDMaterials;
    public bool IsMaterialItem => ItemType == ItemType.Inventoried || ItemType == ItemType.Consumable || ItemType == ItemType.RnDMaterials;
    public bool IsTransformedMaterial => MaterialType == MaterialType.Transformed;
    public bool ShowYieldFields => IsTransformedMaterial && IsMaterialItem;
    public bool RequiresManufacturingBom => IsTransformedMaterial && ItemType == ItemType.Inventoried;
  }
}