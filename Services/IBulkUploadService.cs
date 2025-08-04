using InventorySystem.ViewModels;
using InventorySystem.Models;


// Services/IBulkUploadService.cs
namespace InventorySystem.Services
{
  public interface IBulkUploadService
  {
    Task<List<ItemValidationResult>> ValidateCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<BulkUploadResult> ImportValidItemsAsync(List<BulkItemPreview> validItems);
    Task<List<BulkItemPreview>> ParseCsvFileAsync(IFormFile file, bool skipHeaderRow = true);
    Task<VendorAssignmentResult> CompleteVendorAssignmentsAsync(ImportVendorAssignmentViewModel model);
  }

}
