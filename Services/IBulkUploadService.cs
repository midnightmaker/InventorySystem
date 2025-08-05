using InventorySystem.ViewModels;
using InventorySystem.Models;


// Services/IBulkUploadService.cs
namespace InventorySystem.Services
{
  public interface IBulkUploadService
  {
    // Existing item methods
    Task<List<ItemValidationResult>> ValidateCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<BulkUploadResult> ImportValidItemsAsync(List<BulkItemPreview> validItems);
    Task<List<BulkItemPreview>> ParseCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<VendorAssignmentResult> CompleteVendorAssignmentsAsync(ImportVendorAssignmentViewModel model);
    
    // NEW: Vendor import methods
    Task<List<BulkVendorPreview>> ParseVendorCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<List<VendorValidationResult>> ValidateVendorCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<BulkVendorUploadResult> ImportValidVendorsAsync(List<BulkVendorPreview> validVendors);
  }

}
