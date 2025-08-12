using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

namespace InventorySystem.Models
{
  public class Item
  {
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string PartNumber { get; set; } = string.Empty;

    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Comments { get; set; } = string.Empty;

    public int MinimumStock { get; set; }
    public int CurrentStock { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Display(Name = "Unit of Measure")]
    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

    [StringLength(100)]
    [Display(Name = "Vendor Part Number")]
    public string? VendorPartNumber { get; set; }

    // Preferred Vendor relationship via VendorItem
    [Display(Name = "Preferred Vendor")]
    public int? PreferredVendorItemId { get; set; }
    public virtual VendorItem? PreferredVendorItem { get; set; }

    [Display(Name = "Sellable")]
    public bool IsSellable { get; set; } = true;

    [Display(Name = "Expense Item")]
    public bool IsExpense { get; set; } = false;

    [Display(Name = "Item Type")]
    public ItemType ItemType { get; set; } = ItemType.Inventoried;

    [Required]
    [StringLength(10)]
    [Display(Name = "Version")]
    public string Version { get; set; } = "A";

    // Computed properties
    [NotMapped]
    public bool TrackInventory => !IsExpense && (ItemType == ItemType.Inventoried || ItemType == ItemType.Consumable || ItemType == ItemType.RnDMaterials);

    [NotMapped]
    public string DisplayPartNumber => $"{PartNumber}-{Version}";

    [NotMapped]
    public string ItemTypeDisplayName => ItemType switch
    {
      ItemType.Inventoried => "Inventoried",
      ItemType.NonInventoried => "Non-Inventoried",
      ItemType.Service => "Service",
      ItemType.Virtual => "Virtual",
      ItemType.Consumable => "Consumable",
      ItemType.Expense => "Expense",
      ItemType.Subscription => "Subscription",
      ItemType.Utility => "Utility",
      ItemType.RnDMaterials => "R&D Materials",
      _ => "Unknown"
    };

    [NotMapped]
    public string BusinessPurpose => IsExpense ? "Expense" : (IsSellable ? "Sellable" : "Internal Use");

    [NotMapped]
    public string FullDisplayName => $"{ItemTypeDisplayName} ({BusinessPurpose})";

    // NEW: Computed property for backward compatibility and display
    [NotMapped]
    [Display(Name = "Preferred Vendor")]
    public string? PreferredVendor => PreferredVendorItem?.Vendor?.CompanyName ?? "TBA";

    [NotMapped]
    [Display(Name = "Has Preferred Vendor")]
    public bool HasPreferredVendor => PreferredVendorItem != null;

    // Image properties
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public string? ImageFileName { get; set; }

    [NotMapped]
    public bool HasImage => ImageData != null && ImageData.Length > 0;

    // Version Control Properties
    public bool IsCurrentVersion { get; set; } = true;
    public int? BaseItemId { get; set; } // References the original item
    public virtual Item? BaseItem { get; set; }
    public virtual ICollection<Item> Versions { get; set; } = new List<Item>();
    public string? VersionHistory { get; set; }
    public int? CreatedFromChangeOrderId { get; set; }
    public virtual ChangeOrder? CreatedFromChangeOrder { get; set; }

    // Helper properties
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
    public decimal? SalePrice { get; set; }

    [Display(Name = "Has Sale Price")]
    [NotMapped]
    public bool HasSalePrice => SalePrice.HasValue && SalePrice.Value > 0;

    [Display(Name = "Suggested Sale Price")]
    [NotMapped]
    public decimal SuggestedSalePrice
    {
        get
        {
            // If sale price is already set, use it
            if (HasSalePrice) return SalePrice.Value;

            // Try to calculate from latest cost with appropriate markup
            try
            {
                // Use different markup strategies based on item type
                var markupFactor = ItemType switch
                {
                    ItemType.Service => 3.0m,           // 200% markup for services
                    ItemType.Virtual => 4.0m,           // 300% markup for virtual items
                    ItemType.Subscription => 2.5m,      // 150% markup for subscriptions
                    ItemType.Utility => 1.2m,           // 20% markup for utilities
                    ItemType.Inventoried => 1.5m,       // 50% markup for physical items
                    ItemType.Consumable => 1.4m,        // 40% markup for consumables
                    ItemType.RnDMaterials => 1.6m,      // 60% markup for R&D materials
                    _ => 1.5m                            // Default 50% markup
                };

                // For inventory items, try to use cost-based pricing
                if (TrackInventory)
                {
                    // This would need to be calculated via service call in real implementation
                    // For now, provide a reasonable default
                    return 10.00m * markupFactor;
                }
                else
                {
                    // For non-inventory items, use type-based defaults
                    return ItemType switch
                    {
                        ItemType.Service => 75.00m,
                        ItemType.Virtual => 50.00m,
                        ItemType.Subscription => 25.00m,
                        ItemType.Utility => 150.00m,
                        ItemType.NonInventoried => 30.00m,
                        _ => 25.00m
                    };
                }
            }
            catch
            {
                // Fallback pricing
                return ItemType switch
                {
                    ItemType.Service => 75.00m,
                    ItemType.Virtual => 50.00m,
                    ItemType.Subscription => 25.00m,
                    ItemType.Utility => 150.00m,
                    _ => 25.00m
                };
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
  }
}