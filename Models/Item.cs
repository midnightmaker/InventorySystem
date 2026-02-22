using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;
using InventorySystem.Models.Interfaces;

namespace InventorySystem.Models
{
  public class Item : ISellableEntity
  {
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string PartNumber { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Comments { get; set; }

    public int MinimumStock { get; set; }
    public int CurrentStock { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Display(Name = "Unit of Measure")]
    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

    [StringLength(100)]
    [Display(Name = "Vendor Part Number")]
    public string? VendorPartNumber { get; set; }

    // Preferred Vendor relationship via VendorItem
    [Display(Name = "Sellable")]
    public bool IsSellable { get; set; } = true;

    [Display(Name = "Item Type")]
    public ItemType ItemType { get; set; } = ItemType.Inventoried;

    [Required]
    [StringLength(10)]
    [Display(Name = "Version")]
    public string Version { get; set; } = "A";

    [Display(Name = "Requires Serial Number")]
    public bool RequiresSerialNumber { get; set; } = false;

    [Display(Name = "Requires Model Number")]
    public bool RequiresModelNumber { get; set; } = false;

    // UPDATED: Simplified computed properties
    [NotMapped]
    public bool TrackInventory => ItemType == ItemType.Inventoried || 
                                  ItemType == ItemType.Consumable || 
                                  ItemType == ItemType.RnDMaterials;

    [NotMapped]
    public string DisplayPartNumber => $"{PartNumber}-{Version}";

    [NotMapped]
    public string ItemTypeDisplayName => ItemType switch
    {
      ItemType.Inventoried => "Inventoried",
      ItemType.Consumable => "Consumable", 
      ItemType.RnDMaterials => "R&D Materials",
      ItemType.Service => "Service", // ADD THIS LINE
      _ => "Unknown"
    };

    // UPDATED: Simplified business purpose - only operational items
    [NotMapped]
    public string BusinessPurpose => IsSellable ? "Sellable" : "Internal Use";

    [NotMapped]
    public string FullDisplayName => $"{ItemTypeDisplayName} ({BusinessPurpose})";

    [NotMapped]
    [Display(Name = "Serial/Model Requirements")]
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
		
		[NotMapped]
		[Display(Name = "Primary Vendor")]
		public string? PrimaryVendor
		{
			get
			{
				var primaryVendorItem = VendorItems?.FirstOrDefault(vi => vi.IsPrimary && vi.IsActive);
				return primaryVendorItem?.Vendor?.CompanyName ?? "TBA";
			}
		}

		[NotMapped]
		[Display(Name = "Has Primary Vendor")]
		public bool HasPrimaryVendor
		{
			get
			{
				return VendorItems?.Any(vi => vi.IsPrimary && vi.IsActive) == true;
			}
		}
		[NotMapped]
    [Display(Name = "Has Requirements")]
    public bool HasRequirements => RequiresSerialNumber || RequiresModelNumber;

    // UPDATED: Simplified requirements by type (only operational types)
    [NotMapped]
    [Display(Name = "Typical Requirements by Type")]
    public string TypicalRequirementsForType => ItemType switch
    {
      ItemType.Inventoried => "Often requires serial numbers for tracking",
      ItemType.Consumable => "Usually no tracking needed",
      ItemType.RnDMaterials => "May require batch/serial tracking",
      _ => "Depends on specific use case"
    };

    // Image properties
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public string? ImageFileName { get; set; }

    [NotMapped]
    public bool HasImage => ImageData != null && ImageData.Length > 0;

    // Version Control Properties
    public bool IsCurrentVersion { get; set; } = true;
    public int? BaseItemId { get; set; }
    public virtual Item? BaseItem { get; set; }
    public virtual ICollection<Item> Versions { get; set; } = new List<Item>();
    public string? VersionHistory { get; set; }
    public int? CreatedFromChangeOrderId { get; set; }
    public virtual ChangeOrder? CreatedFromChangeOrder { get; set; }

    [NotMapped]
    public string VersionedPartNumber => $"{PartNumber} Rev {Version}";
    [NotMapped]
    public int VersionCount => Versions?.Count ?? 0;

    [Display(Name = "Material Type")]
    public MaterialType MaterialType { get; set; } = MaterialType.Standard;

    [Display(Name = "Parent Raw Material")]
    public int? ParentRawMaterialId { get; set; }
    public virtual Item? ParentRawMaterial { get; set; }

    [Display(Name = "Yield Factor")]
    [Column(TypeName = "decimal(10,4)")]
    public decimal? YieldFactor { get; set; }

    [Display(Name = "Waste Percentage")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal? WastePercentage { get; set; }

    // Sale Price Properties
    [Display(Name = "Sale Price")]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0, double.MaxValue, ErrorMessage = "Sale price must be 0 or greater")]
    public decimal SalePrice { get; set; }

		// NEW: Allow items to override default revenue account
		[Display(Name = "Revenue Account Code")]
		[StringLength(10)]
		public string? PreferredRevenueAccountCode { get; set; }

		[Display(Name = "Has Sale Price")]
    [NotMapped]
    public bool HasSalePrice => SalePrice > 0;

    // Simplified suggested pricing for operational items only
    [Display(Name = "Suggested Sale Price")]
    [NotMapped]
    public decimal SuggestedSalePrice
    {
        get
        {
            if (HasSalePrice) return SalePrice;

            var markupFactor = ItemType switch
            {
                ItemType.Inventoried => 1.5m,       // 50% markup for physical items
                ItemType.Consumable => 1.4m,        // 40% markup for consumables
                ItemType.RnDMaterials => 1.6m,      // 60% markup for R&D materials
                _ => 1.5m                            // Default 50% markup
            };

            if (TrackInventory)
            {
                return 10.00m * markupFactor; // This would use actual cost in real implementation
            }
            else
            {
                return 25.00m; // Default for non-tracked items
            }
        }
    }

    // Navigation properties
    public virtual ICollection<Item> TransformedItems { get; set; } = new List<Item>();
    public virtual ICollection<ItemDocument> DesignDocuments { get; set; } = new List<ItemDocument>();
    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public virtual ICollection<VendorItem> VendorItems { get; set; } = new List<VendorItem>();

    // Computed properties
    [NotMapped]
    public bool IsRawMaterial => MaterialType == MaterialType.RawMaterial;

    [NotMapped]
    public bool IsTransformed => MaterialType == MaterialType.Transformed;

    [NotMapped]
    public decimal EffectiveYield => YieldFactor ?? 1.0m;

    // Validation method for sale item requirements
    public bool ValidateSaleItemRequirements(string? serialNumber, string? modelNumber)
    {
      if (RequiresSerialNumber && string.IsNullOrWhiteSpace(serialNumber))
        return false;
      
      if (RequiresModelNumber && string.IsNullOrWhiteSpace(modelNumber))
        return false;
      
      return true;
    }

    // UPDATED: Simplified requirements suggestions for operational items only
    public (bool suggestSerial, bool suggestModel, string reason) GetSuggestedRequirements()
    {
      return ItemType switch
      {
        ItemType.Inventoried => (true, true, "Physical items often need tracking"),
        ItemType.Consumable => (false, false, "Consumables are typically not tracked individually"),
        ItemType.RnDMaterials => (true, false, "R&D materials often need batch/serial tracking"),
        _ => (false, false, "Requirements depend on specific use case")
      };
    }

    // Unit of Measure display properties
    [NotMapped]
    public string UnitOfMeasureDisplayName => UnitOfMeasure.ToString();

    [NotMapped]
    public string UnitOfMeasureCategory => UnitOfMeasure switch
    {
      UnitOfMeasure.Each or UnitOfMeasure.Box or UnitOfMeasure.Case or UnitOfMeasure.Dozen or UnitOfMeasure.Pair or UnitOfMeasure.Set => "Count",
      UnitOfMeasure.Gram or UnitOfMeasure.Kilogram or UnitOfMeasure.Ounce or UnitOfMeasure.Pound => "Weight",
      UnitOfMeasure.Millimeter or UnitOfMeasure.Centimeter or UnitOfMeasure.Meter or UnitOfMeasure.Inch or UnitOfMeasure.Foot or UnitOfMeasure.Yard => "Length",
      UnitOfMeasure.Milliliter or UnitOfMeasure.Liter or UnitOfMeasure.FluidOunce or UnitOfMeasure.Pint or UnitOfMeasure.Quart or UnitOfMeasure.Gallon => "Volume",
      UnitOfMeasure.SquareCentimeter or UnitOfMeasure.SquareMeter or UnitOfMeasure.SquareInch or UnitOfMeasure.SquareFoot => "Area",
      UnitOfMeasure.Roll or UnitOfMeasure.Sheet => "Material",
      _ => "Other"
    };

    // ISellableEntity implementation - FIXED: Added missing DisplayName property
    [NotMapped]
    public string DisplayName => DisplayPartNumber;

    [NotMapped]
    public string? Code => PartNumber;
    
    [NotMapped]
    public string EntityType => "Item";

		[NotMapped]
		string? ISellableEntity.PreferredRevenueAccountCode => PreferredRevenueAccountCode;


		public string GetDefaultRevenueAccountCode()
		{
			// Use preferred account if specified, otherwise use ItemType logic
			if (!string.IsNullOrEmpty(PreferredRevenueAccountCode))
				return PreferredRevenueAccountCode;

			return ItemType switch
			{
				ItemType.Inventoried => "4000", // Product Sales
				ItemType.Consumable => "4010",  // Supply Sales
				ItemType.RnDMaterials => "4020", // Research Material Sales
				ItemType.Service => "4100",     // Service Revenue
				_ => "4000" // Default
			};
		}
	}
}