using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
  //public class ImportVendorAssignmentViewModel
  //{
  //  public List<PendingVendorAssignment> PendingAssignments { get; set; } = new();
  //  public List<VendorCreationRequest> NewVendorRequests { get; set; } = new();
  //  public BulkUploadResult ImportResult { get; set; } = new();
  //}

  //public class PendingVendorAssignment
  //{
  //  public int ItemId { get; set; }
  //  public string PartNumber { get; set; } = string.Empty;
  //  public string Description { get; set; } = string.Empty;
  //  public string? VendorPartNumber { get; set; }
  //  public string VendorName { get; set; } = string.Empty;
  //  public bool VendorExists { get; set; }
  //  public int? FoundVendorId { get; set; }
  //  public string? FoundVendorName { get; set; }
  //  public bool IsAssigned { get; set; }
  //}

  //public class VendorCreationRequest
  //{
  //  public string VendorName { get; set; } = string.Empty;
  //  public List<PendingVendorAssignment> RelatedItems { get; set; } = new();
  //  public bool ShouldCreate { get; set; } = true;
  //}

  //public class VendorAssignmentResult
  //{
  //  public bool Success { get; set; }
  //  public int VendorsCreated { get; set; }
  //  public int AssignmentsCompleted { get; set; }
  //  public List<string> Errors { get; set; } = new();
  //  public string Summary => Success 
  //    ? $"Successfully created {VendorsCreated} vendors and completed {AssignmentsCompleted} assignments"
  //    : $"Failed with {Errors.Count} errors";
  //}
}