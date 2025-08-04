using InventorySystem.ViewModels;

namespace InventorySystem.Models
{
  public class BulkUploadResult
  {
    public int SuccessfulImports { get; set; }
    public int FailedImports { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<int> CreatedItemIds { get; set; } = new List<int>();

    // NEW: Vendor assignment information
    public ImportVendorAssignmentViewModel? VendorAssignments { get; set; }

    public bool IsSuccess => FailedImports == 0 && !Errors.Any();
    public string GetSummary() => IsSuccess
        ? $"Successfully imported {SuccessfulImports} items"
        : $"Imported {SuccessfulImports} items with {FailedImports} failures";
  }
}