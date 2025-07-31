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

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Comments { get; set; } = string.Empty;

    public int MinimumStock { get; set; }
    public int CurrentStock { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // UNIT OF MEASURE - NEW PROPERTY
    [Display(Name = "Unit of Measure")]
    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

    // NEW PHASE 1 PROPERTIES

    [StringLength(100)]
    [Display(Name = "Vendor Part Number")]
    public string? VendorPartNumber { get; set; }

    [StringLength(200)]
    [Display(Name = "Preferred Vendor")]
    public string? PreferredVendor { get; set; }

    [Display(Name = "Sellable")]
    public bool IsSellable { get; set; } = true;

    [Display(Name = "Item Type")]
    public ItemType ItemType { get; set; } = ItemType.Inventoried;

    [Required]
    [StringLength(10)]
    [Display(Name = "Version")]
    public string Version { get; set; } = "A";

    // Computed properties
    [NotMapped]
    public bool TrackInventory => ItemType == ItemType.Inventoried;

    [NotMapped]
    public string DisplayPartNumber => $"{PartNumber}-{Version}";

    [NotMapped]
    public string ItemTypeDisplayName => ItemType switch
    {
      ItemType.Inventoried => "Inventoried",
      ItemType.NonInventoried => "Non-Inventoried",
      ItemType.Service => "Service",
      ItemType.Virtual => "Virtual",
      _ => "Unknown"
    };

    // UNIT OF MEASURE DISPLAY PROPERTIES
    [NotMapped]
    [Display(Name = "Unit of Measure")]
    public string UnitOfMeasureDisplayName => UnitOfMeasure switch
    {
      UnitOfMeasure.Each => "EA",
      UnitOfMeasure.Gram => "g",
      UnitOfMeasure.Kilogram => "kg",
      UnitOfMeasure.Ounce => "oz",
      UnitOfMeasure.Pound => "lb",
      UnitOfMeasure.Millimeter => "mm",
      UnitOfMeasure.Centimeter => "cm",
      UnitOfMeasure.Meter => "m",
      UnitOfMeasure.Inch => "in",
      UnitOfMeasure.Foot => "ft",
      UnitOfMeasure.Yard => "yd",
      UnitOfMeasure.Milliliter => "ml",
      UnitOfMeasure.Liter => "L",
      UnitOfMeasure.FluidOunce => "fl oz",
      UnitOfMeasure.Pint => "pt",
      UnitOfMeasure.Quart => "qt",
      UnitOfMeasure.Gallon => "gal",
      UnitOfMeasure.SquareCentimeter => "cm²",
      UnitOfMeasure.SquareMeter => "m²",
      UnitOfMeasure.SquareInch => "in²",
      UnitOfMeasure.SquareFoot => "ft²",
      UnitOfMeasure.Box => "BOX",
      UnitOfMeasure.Case => "CASE",
      UnitOfMeasure.Dozen => "DOZ",
      UnitOfMeasure.Pair => "PR",
      UnitOfMeasure.Set => "SET",
      UnitOfMeasure.Roll => "ROLL",
      UnitOfMeasure.Sheet => "SHT",
      UnitOfMeasure.Hour => "hr",
      UnitOfMeasure.Day => "day",
      UnitOfMeasure.Month => "mo",
      _ => "EA"
    };

    [NotMapped]
    [Display(Name = "UOM Category")]
    public string UnitOfMeasureCategory => UnitOfMeasure switch
    {
      UnitOfMeasure.Each => "Count",
      UnitOfMeasure.Gram or UnitOfMeasure.Kilogram or UnitOfMeasure.Ounce or UnitOfMeasure.Pound => "Weight",
      UnitOfMeasure.Millimeter or UnitOfMeasure.Centimeter or UnitOfMeasure.Meter or UnitOfMeasure.Inch or UnitOfMeasure.Foot or UnitOfMeasure.Yard => "Length",
      UnitOfMeasure.Milliliter or UnitOfMeasure.Liter or UnitOfMeasure.FluidOunce or UnitOfMeasure.Pint or UnitOfMeasure.Quart or UnitOfMeasure.Gallon => "Volume",
      UnitOfMeasure.SquareCentimeter or UnitOfMeasure.SquareMeter or UnitOfMeasure.SquareInch or UnitOfMeasure.SquareFoot => "Area",
      UnitOfMeasure.Box or UnitOfMeasure.Case or UnitOfMeasure.Dozen or UnitOfMeasure.Pair or UnitOfMeasure.Set or UnitOfMeasure.Roll or UnitOfMeasure.Sheet => "Packaging",
      UnitOfMeasure.Hour or UnitOfMeasure.Day or UnitOfMeasure.Month => "Time",
      _ => "Other"
    };

    // Existing navigation properties
    public List<Purchase> Purchases { get; set; } = new List<Purchase>();
    public List<BomItem> BomItems { get; set; } = new List<BomItem>();
    public List<ItemDocument> DesignDocuments { get; set; } = new List<ItemDocument>();

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
  }
}