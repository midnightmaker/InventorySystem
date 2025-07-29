using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class ChangeOrder
  {
    public int Id { get; set; }

    // Remove [Required] attribute since this is auto-generated
    [Display(Name = "Change Order Number")]
    public string ChangeOrderNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Entity Type")]
    public string EntityType { get; set; } = string.Empty; // "Item" or "BOM"

    [Required]
    [Display(Name = "Base Entity ID")]
    public int BaseEntityId { get; set; }

    [Display(Name = "Previous Version")]
    public string? PreviousVersion { get; set; }

    [Required]
    [Display(Name = "New Version")]
    public string NewVersion { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Reason for Change")]
    public string? Reason { get; set; }

    [Display(Name = "Impact Analysis")]
    public string? ImpactAnalysis { get; set; }

    [Required]
    [Display(Name = "Status")]
    public string Status { get; set; } = "Pending"; // Pending, Implemented, Cancelled

    [Display(Name = "Created By")]
    public string CreatedBy { get; set; } = string.Empty;

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Display(Name = "Implemented Date")]
    public DateTime? ImplementedDate { get; set; }

    [Display(Name = "Implemented By")]
    public string? ImplementedBy { get; set; }

    [Display(Name = "Cancelled Date")]
    public DateTime? CancelledDate { get; set; }

    [Display(Name = "Cancelled By")]
    public string? CancelledBy { get; set; }

    [Display(Name = "Cancellation Reason")]
    public string? CancellationReason { get; set; }

    // Foreign keys for navigation properties
    public int? BaseItemId { get; set; }
    public int? BaseBomId { get; set; }
    public int? NewItemId { get; set; }
    public int? NewBomId { get; set; }

    // Navigation properties
    public virtual Item? BaseItem { get; set; }
    public virtual Bom? BaseBom { get; set; }
    public virtual Item? NewItem { get; set; }
    public virtual Bom? NewBom { get; set; }

    // Navigation property for change order documents
    public virtual ICollection<ChangeOrderDocument> ChangeOrderDocuments { get; set; } = new List<ChangeOrderDocument>();

    // Helper properties
    [NotMapped]
    public string EntityDisplayName => EntityType == "Item" ? "Item" : "BOM";

    [NotMapped]
    public string StatusBadgeColor => Status switch
    {
      "Pending" => "warning",
      "Implemented" => "success",
      "Cancelled" => "danger",
      _ => "secondary"
    };

    [NotMapped]
    public bool HasDocuments => ChangeOrderDocuments?.Any() == true;

    [NotMapped]
    public int DocumentCount => ChangeOrderDocuments?.Count ?? 0;

    [NotMapped]
    public bool CanBeImplemented => Status == "Pending";

    [NotMapped]
    public bool CanBeCancelled => Status == "Pending";

    [NotMapped]
    public string FormattedChangeOrderNumber =>
        !string.IsNullOrEmpty(ChangeOrderNumber) ? ChangeOrderNumber : "Pending";

    // Helper methods to get the related entity info
    public string GetEntityDisplayName()
    {
      if (EntityType == "Item" && BaseItem != null)
      {
        return BaseItem.PartNumber;
      }
      else if (EntityType == "BOM" && BaseBom != null)
      {
        return BaseBom.BomNumber;
      }
      return $"{EntityType} ID: {BaseEntityId}";
    }

    public string GetEntityDescription()
    {
      if (EntityType == "Item" && BaseItem != null)
      {
        return BaseItem.Description ?? "";
      }
      else if (EntityType == "BOM" && BaseBom != null)
      {
        return BaseBom.Description ?? "";
      }
      return "";
    }
  }
}