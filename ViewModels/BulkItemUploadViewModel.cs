// ViewModels/BulkItemUploadViewModel.cs
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
  public class BulkItemUploadViewModel
  {
    [Required(ErrorMessage = "Please select a CSV file to upload.")]
    [Display(Name = "CSV File")]
    public IFormFile? CsvFile { get; set; }

    [Display(Name = "Skip Header Row")]
    public bool SkipHeaderRow { get; set; } = true;

    [Display(Name = "Validation Results")]
    public List<ItemValidationResult>? ValidationResults { get; set; }

    [Display(Name = "Preview Items")]
    public List<BulkItemPreview>? PreviewItems { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    // Helper properties
    public int ValidItemsCount => ValidationResults?.Count(vr => vr.IsValid) ?? 0;
    public int InvalidItemsCount => ValidationResults?.Count(vr => !vr.IsValid) ?? 0;
    public bool HasValidationResults => ValidationResults?.Any() == true;
    public bool CanProceedWithImport => ValidItemsCount > 0;
  }

  public class BulkItemPreview
  {
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public int MinimumStock { get; set; }
    public int RowNumber { get; set; }


    public string? VendorPartNumber { get; set; }
    public string? PreferredVendor { get; set; }
    public bool IsSellable { get; set; } = true;
    public ItemType ItemType { get; set; } = ItemType.Inventoried;
    public string Version { get; set; } = "A";

    // Initial purchase data (optional)
    public decimal? InitialQuantity { get; set; }
    public decimal? InitialCostPerUnit { get; set; }
    public string? InitialVendor { get; set; }
    public DateTime? InitialPurchaseDate { get; set; }
    public string? InitialPurchaseOrderNumber { get; set; }

    // Helper properties
    public bool TrackInventory => ItemType == ItemType.Inventoried;
    public string ItemTypeDisplayName => ItemType.ToString();
  }


  public class ItemValidationResult
  {
    public int RowNumber { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public BulkItemPreview? ItemData { get; set; }
  }

 
}
