using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models.Enums
{
  public enum ItemType
  {
    [Display(Name = "Inventoried", Description = "Physical items with stock tracking")]
    Inventoried = 0,

    [Display(Name = "Non-Inventoried", Description = "Items without stock tracking (firmware, software, etc.)")]
    NonInventoried = 1,

    [Display(Name = "Service", Description = "Labor, consulting, or service items")]
    Service = 2,

    [Display(Name = "Virtual", Description = "Licenses, digital assets, or virtual items")]
    Virtual = 3
  }
}