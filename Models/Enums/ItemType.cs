using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models.Enums
{
  public enum ItemType
  {
    [Display(Name = "Inventoried", Description = "Raw materials and components with precise quantity tracking")]
    Inventoried = 0,

    [Display(Name = "Consumable", Description = "Office supplies, hand tools, PPE, small parts that wear out or get used up")]
    Consumable = 4,

    [Display(Name = "R&D Materials", Description = "Research and development specific materials and components")]
    RnDMaterials = 8,

    // ✅ ADD: Sellable services like calibration, maintenance, consulting
    [Display(Name = "Service", Description = "Sellable services like calibration, maintenance, consulting, and training that can be sold to customers")]
    Service = 12
  }
}