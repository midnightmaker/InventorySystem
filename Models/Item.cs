using System.ComponentModel.DataAnnotations;
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
    public bool TrackInventory => ItemType == ItemType.Inventoried;

    public string DisplayPartNumber => $"{PartNumber}-{Version}";

    public string ItemTypeDisplayName => ItemType switch
    {
      ItemType.Inventoried => "Inventoried",
      ItemType.NonInventoried => "Non-Inventoried",
      ItemType.Service => "Service",
      ItemType.Virtual => "Virtual",
      _ => "Unknown"
    };

    // Existing navigation properties
    public List<Purchase> Purchases { get; set; } = new List<Purchase>();
    public List<BomItem> BomItems { get; set; } = new List<BomItem>();
    public List<ItemDocument> DesignDocuments { get; set; } = new List<ItemDocument>();

    // Image properties
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public string? ImageFileName { get; set; }
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
    public string VersionedPartNumber => $"{PartNumber} Rev {Version}";
    public int VersionCount => Versions?.Count ?? 0;
  }
}