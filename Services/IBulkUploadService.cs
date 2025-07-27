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
  }

  public class BulkUploadResult
  {
    public int SuccessfulImports { get; set; }
    public int FailedImports { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<int> CreatedItemIds { get; set; } = new List<int>();
  }

}
