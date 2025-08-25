// ViewModels/BulkItemUploadViewModel.cs
using InventorySystem.Models;
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

    // Session-based upload support
    public string? UploadSessionId { get; set; }

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

    // Manufacturer information
    public string? Manufacturer { get; set; }
    public string? ManufacturerPartNumber { get; set; }

    public bool IsSellable { get; set; } = true;
    // ✅ REMOVED: IsExpense property (no longer used in operational-only items)
    public ItemType ItemType { get; set; } = ItemType.Inventoried;
    public string Version { get; set; } = "A";
    
    // Unit of Measure support
    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

    // Initial purchase data (optional)
    public decimal? InitialQuantity { get; set; }
    public decimal? InitialCostPerUnit { get; set; }
    public string? InitialVendor { get; set; }
    public DateTime? InitialPurchaseDate { get; set; }
    public string? InitialPurchaseOrderNumber { get; set; }

    // Helper properties - ✅ UPDATED for operational items only
    public bool TrackInventory => ItemType == ItemType.Inventoried || 
                                  ItemType == ItemType.Consumable || 
                                  ItemType == ItemType.RnDMaterials;
    
    public string ItemTypeDisplayName => ItemType.ToString();
    public string BusinessPurpose => IsSellable ? "Sellable" : "Internal Use";
    public string FullDisplayName => $"{ItemTypeDisplayName} ({BusinessPurpose})";

    public string ManufacturerInfo => !string.IsNullOrEmpty(Manufacturer) || !string.IsNullOrEmpty(ManufacturerPartNumber)
        ? $"{Manufacturer ?? "Unknown"} - {ManufacturerPartNumber ?? "N/A"}"
        : "Not specified";
  }

  // ✅ ENHANCED: Updated with vendor creation prompts
  public class ItemValidationResult
  {
    public int RowNumber { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> InfoMessages { get; set; } = new List<string>(); // ✅ NEW
    public List<VendorCreationPrompt> VendorCreationPrompts { get; set; } = new List<VendorCreationPrompt>(); // ✅ NEW
    public BulkItemPreview? ItemData { get; set; }
  }

  // Vendor upload classes
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

  // ✅ NEW: Vendor creation prompt classes
  public class VendorCreationPrompt
  {
    public string VendorName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string? VendorPartNumber { get; set; }
    public bool IsInitialVendor { get; set; } = false;
    public string PromptMessage { get; set; } = string.Empty;
    public bool ShouldCreate { get; set; } = true; // Default to true for convenience
  }

  public class VendorCreationRequest
  {
    public string VendorName { get; set; } = string.Empty;
    public bool ShouldCreate { get; set; } = true;
    public List<PendingVendorAssignment> RelatedItems { get; set; } = new List<PendingVendorAssignment>();
  }

  public class PendingVendorAssignment
  {
    public int ItemId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? VendorPartNumber { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public bool VendorExists { get; set; }
    public int? FoundVendorId { get; set; }
    public string? FoundVendorName { get; set; }
    public bool IsAssigned { get; set; }
  }

  public class ImportVendorAssignmentViewModel
  {
    public List<VendorCreationRequest> NewVendorRequests { get; set; } = new List<VendorCreationRequest>();
    public List<PendingVendorAssignment> PendingAssignments { get; set; } = new List<PendingVendorAssignment>();
    public int VendorsCreated { get; set; }
    public int VendorLinksCreated { get; set; }
  }

  public class VendorAssignmentResult
  {
    public bool Success { get; set; }
    public int VendorsCreated { get; set; }
    public int AssignmentsCompleted { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
  }
}
