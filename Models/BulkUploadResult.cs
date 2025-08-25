using InventorySystem.ViewModels;

namespace InventorySystem.Models
{
  // ✅ ENHANCED: Comprehensive BulkUploadResult with vendor assignment support
  public class BulkUploadResult
  {
    public bool IsSuccess => !Errors.Any();
    public int SuccessfulImports { get; set; }
    public int FailedImports { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<int> CreatedItemIds { get; set; } = new List<int>();
    public List<ItemImportError> DetailedErrors { get; set; } = new List<ItemImportError>();
    public ImportVendorAssignmentViewModel VendorAssignments { get; set; } = new ImportVendorAssignmentViewModel();

    public string GetSummary()
    {
        var summary = $"Import completed: {SuccessfulImports} successful, {FailedImports} failed";
        if (VendorAssignments.VendorsCreated > 0)
        {
            summary += $", {VendorAssignments.VendorsCreated} vendors created";
        }
        if (VendorAssignments.VendorLinksCreated > 0)
        {
            summary += $", {VendorAssignments.VendorLinksCreated} vendor links created";
        }
        return summary;
    }
  }

  // Detailed error information for individual items
  public class ItemImportError
  {
    public int RowNumber { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime ErrorTime { get; set; } = DateTime.Now;
  }

  // Vendor upload result classes
  public class BulkVendorUploadResult
  {
    public int SuccessfulImports { get; set; }
    public int FailedImports { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<int> CreatedVendorIds { get; set; } = new List<int>();
    public List<VendorImportError> DetailedErrors { get; set; } = new List<VendorImportError>();

    public bool IsSuccess => FailedImports == 0 && !Errors.Any();
    public string GetSummary() => IsSuccess
        ? $"Successfully imported {SuccessfulImports} vendors"
        : $"Imported {SuccessfulImports} vendors with {FailedImports} failures";
  }

  public class VendorImportError
  {
    public int RowNumber { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string VendorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime ErrorTime { get; set; } = DateTime.Now;
  }
}