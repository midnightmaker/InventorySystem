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
    Virtual = 3,

    [Display(Name = "Consumable", Description = "Items used up in operations (office supplies, consumables)")]
    Consumable = 4,

    [Display(Name = "Expense", Description = "Direct expense items (utilities, rent, one-time expenses)")]
    Expense = 5,

    [Display(Name = "Subscription", Description = "Recurring subscription services (software licenses, cloud services)")]
    Subscription = 6,

    [Display(Name = "Utility", Description = "Utility expenses (electricity, water, internet)")]
    Utility = 7,

    [Display(Name = "R&D Materials", Description = "Research and development specific materials")]
    RnDMaterials = 8
  }
}