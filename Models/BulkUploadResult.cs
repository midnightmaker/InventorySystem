using InventorySystem.ViewModels;

namespace InventorySystem.Models
{
  public class BulkUploadResult
  {
    public int SuccessfulImports { get; set; }
    public int FailedImports { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<int> CreatedItemIds { get; set; } = new List<int>();

    // NEW: Add detailed error tracking for individual items
    public List<ItemImportError> DetailedErrors { get; set; } = new List<ItemImportError>();

    // NEW: Vendor assignment information
    public ImportVendorAssignmentViewModel? VendorAssignments { get; set; }

    public bool IsSuccess => FailedImports == 0 && !Errors.Any();
    public string GetSummary() => IsSuccess
        ? $"Successfully imported {SuccessfulImports} items"
        : $"Imported {SuccessfulImports} items with {FailedImports} failures";
  }

  // NEW: Add detailed error information for individual items
  public class ItemImportError
  {
    public int RowNumber { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime ErrorTime { get; set; } = DateTime.Now;
  }
}