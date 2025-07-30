// Models/ProductionConsumption.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  // Supporting model for production consumption tracking
  public class ProductionConsumption
  {
    public int Id { get; set; }

    [Required]
    public int ProductionId { get; set; }
    public virtual Production? Production { get; set; }

    [Required]
    public int ItemId { get; set; }
    public virtual Item? Item { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Quantity consumed must be greater than 0")]
    public decimal QuantityConsumed { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Unit cost cannot be negative")]
    public decimal UnitCostAtConsumption { get; set; }

    [Required]
    public DateTime ConsumedDate { get; set; }

    // Calculated property for total cost of this consumption
    public decimal TotalCost => QuantityConsumed * UnitCostAtConsumption;
  }
}