// Models/ChangeOrder.cs
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models
{
  public class ChangeOrder
  {
    public int Id { get; set; }

    [Display(Name = "Change Order Number")]
    public string ChangeOrderNumber { get; set; } = string.Empty;

    [Required]
    public string EntityType { get; set; } = string.Empty; // "Item" or "BOM"

    [Required]
    public int BaseEntityId { get; set; } // Item.Id or Bom.Id

    [Required]
    [Display(Name = "New Version")]
    public string NewVersion { get; set; } = string.Empty;

    [Display(Name = "Previous Version")]
    public string? PreviousVersion { get; set; }

    public string? Description { get; set; }
    public string? Reason { get; set; }

    [Required]
    [Display(Name = "Created By")]
    public string CreatedBy { get; set; } = string.Empty;

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public string Status { get; set; } = "Pending";

    [Display(Name = "Implemented By")]
    public string? ImplementedBy { get; set; }

    [Display(Name = "Implementation Date")]
    public DateTime? ImplementedDate { get; set; }

    [Display(Name = "Change Order Document")]
    public string? DocumentPath { get; set; }

    // Navigation properties for polymorphic relationship
    public virtual Item? RelatedItem { get; set; }
    public virtual Bom? RelatedBom { get; set; }

    // Helper method to get the related entity info
    public string GetEntityDisplayName()
    {
      if (EntityType == "Item" && RelatedItem != null)
      {
        return RelatedItem.PartNumber;
      }
      else if (EntityType == "BOM" && RelatedBom != null)
      {
        return RelatedBom.Name;
      }
      return $"{EntityType} ID: {BaseEntityId}";
    }

    public string GetEntityDescription()
    {
      if (EntityType == "Item" && RelatedItem != null)
      {
        return RelatedItem.Description ?? "";
      }
      else if (EntityType == "BOM" && RelatedBom != null)
      {
        return RelatedBom.Description ?? "";
      }
      return "";
    }
  }
}