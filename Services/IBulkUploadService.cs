using InventorySystem.ViewModels;
using InventorySystem.Models;
// ✅ ADD: Explicitly resolve the ambiguity
using BulkUploadResult = InventorySystem.Models.BulkUploadResult;

namespace InventorySystem.Services
{
  public interface IBulkUploadService
  {
    // ✅ ENHANCED: Updated method signatures to support vendor creation choices
    Task<List<ItemValidationResult>> ValidateCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<BulkUploadResult> ImportValidItemsAsync(List<BulkItemPreview> validItems, Dictionary<string, bool>? vendorCreationChoices = null);
    Task<List<BulkItemPreview>> ParseCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<VendorAssignmentResult> CompleteVendorAssignmentsAsync(ImportVendorAssignmentViewModel model);
    
    // Existing vendor import methods
    Task<List<BulkVendorPreview>> ParseVendorCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<List<VendorValidationResult>> ValidateVendorCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<BulkVendorUploadResult> ImportValidVendorsAsync(List<BulkVendorPreview> validVendors);
  }
}
