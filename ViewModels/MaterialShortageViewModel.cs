// ViewModels/MaterialShortageViewModel.cs
using InventorySystem.Models;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
  public class MaterialShortageViewModel
  {
    public int BomId { get; set; }
    public string BomName { get; set; } = string.Empty;
    public string BomDescription { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public bool CanBuild { get; set; }
    public decimal TotalCost { get; set; }
    public decimal ShortageValue { get; set; }

    public IEnumerable<MaterialShortageItem> MaterialShortages { get; set; } = new List<MaterialShortageItem>();
    public IEnumerable<MaterialRequirement> MaterialRequirements { get; set; } = new List<MaterialRequirement>();

    // Summary statistics
    public int TotalShortageItems => MaterialShortages.Count();
    public int TotalRequiredItems => MaterialRequirements.Count();
    public decimal TotalShortageQuantity => MaterialShortages.Sum(ms => ms.ShortageQuantity);
    public bool HasShortages => MaterialShortages.Any();
  }

  public class MaterialShortageItem
  {
    public int ItemId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int ShortageQuantity { get; set; }
    public decimal EstimatedUnitCost { get; set; }
    public decimal ShortageValue { get; set; }
    public string? PreferredVendor { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public decimal? LastPurchasePrice { get; set; }
    public int MinimumStock { get; set; }
    public bool IsCriticalShortage => AvailableQuantity == 0;
    public int SuggestedPurchaseQuantity { get; set; }

    // BOM context
    public string BomContext { get; set; } = string.Empty; // "Direct" or "Sub-Assembly: {Name}"
    public int QuantityPerAssembly { get; set; }
  }

  public class MaterialRequirement
  {
    public int ItemId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public decimal EstimatedUnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public bool HasSufficientStock { get; set; }
    public string BomContext { get; set; } = string.Empty;
    public int QuantityPerAssembly { get; set; }
  }

  
}