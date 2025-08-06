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
    public List<ItemValidationResult> ValidationResults { get; set; } = new List<ItemValidationResult>();

    [Display(Name = "Preview Items")]  
    public List<BulkItemPreview> PreviewItems { get; set; } = new List<BulkItemPreview>();

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    // Helper properties
    public int ValidItemsCount => ValidationResults.Count(vr => vr.IsValid);
    public int InvalidItemsCount => ValidationResults.Count(vr => !vr.IsValid);
    public bool HasValidationResults => ValidationResults.Any();
    public bool CanProceedWithImport => ValidItemsCount > 0;
  }

  public class BulkItemPreview
  {
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public int MinimumStock { get; set; }
    public int RowNumber { get; set; }

    // Vendor information
    public string? VendorPartNumber { get; set; }
    public string? PreferredVendor { get; set; }

    // NEW: Manufacturer information
    public string? Manufacturer { get; set; }
    public string? ManufacturerPartNumber { get; set; }

    public bool IsSellable { get; set; } = true;
    public ItemType ItemType { get; set; } = ItemType.Inventoried;
    public string Version { get; set; } = "A";
    
    // ADD: Unit of Measure support
    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

    // Initial purchase data (optional)
    public decimal? InitialQuantity { get; set; }
    public decimal? InitialCostPerUnit { get; set; }
    public string? InitialVendor { get; set; }
    public DateTime? InitialPurchaseDate { get; set; }
    public string? InitialPurchaseOrderNumber { get; set; }

    // Helper properties
    public bool TrackInventory => ItemType == ItemType.Inventoried;
    public string ItemTypeDisplayName => ItemType.ToString();

    // NEW: Helper property for display
    public string ManufacturerInfo => !string.IsNullOrEmpty(Manufacturer) || !string.IsNullOrEmpty(ManufacturerPartNumber)
        ? $"{Manufacturer ?? "Unknown"} - {ManufacturerPartNumber ?? "N/A"}"
        : "Not specified";
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

  // NEW: Vendor upload classes
  public class BulkVendorPreview
  {
    public int RowNumber { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? VendorCode { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
    
    // Address Information
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = "United States";
    
    // Business Information
    public string? TaxId { get; set; }
    public string? PaymentTerms { get; set; } = "Net 30";
    public decimal DiscountPercentage { get; set; } = 0;
    public decimal CreditLimit { get; set; } = 0;
    
    // Status and Preferences
    public bool IsActive { get; set; } = true;
    public bool IsPreferred { get; set; } = false;
    public int QualityRating { get; set; } = 3;
    public int DeliveryRating { get; set; } = 3;
    public int ServiceRating { get; set; } = 3;
    public string? Notes { get; set; }
  }

  public class BulkVendorUploadViewModel
  {
    public IFormFile? CsvFile { get; set; }
    public bool SkipHeaderRow { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    
    public List<VendorValidationResult>? ValidationResults { get; set; }
    public List<BulkVendorPreview>? PreviewVendors { get; set; }
    
    public bool HasValidationResults => ValidationResults?.Any() == true;
    public int ValidVendorsCount => ValidationResults?.Count(vr => vr.IsValid) ?? 0;
    public int InvalidVendorsCount => ValidationResults?.Count(vr => !vr.IsValid) ?? 0;
    public bool CanProceedWithImport => ValidVendorsCount > 0;
  }

  public class VendorValidationResult
  {
    public int RowNumber { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string VendorCode { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public BulkVendorPreview? VendorData { get; set; }
  }
}
