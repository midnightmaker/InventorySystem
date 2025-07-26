// Models/ProductionConsumption.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class ProductionConsumption
  {
    public int Id { get; set; }

    public int ProductionId { get; set; }
    public virtual Production Production { get; set; } = null!;

    public int ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;

    [Required]
    [Display(Name = "Quantity Consumed")]
    public int QuantityConsumed { get; set; }

    [Display(Name = "Unit Cost at Consumption")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCostAtConsumption { get; set; }

    [Display(Name = "Total Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost => QuantityConsumed * UnitCostAtConsumption;

    public DateTime ConsumedDate { get; set; } = DateTime.Now;
  }
}