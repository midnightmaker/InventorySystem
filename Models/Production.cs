// Models/Production.cs
using InventorySystem.Models;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models
{
  public class Production
  {
    public int Id { get; set; }

    [Required]
    public int FinishedGoodId { get; set; }
    public virtual FinishedGood? FinishedGood { get; set; }

    [Required]
    public int BomId { get; set; }
    public virtual Bom? Bom { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity produced must be greater than 0")]
    public int QuantityProduced { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime ProductionDate { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Material cost cannot be negative")]
    public decimal MaterialCost { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Labor cost cannot be negative")]
    public decimal LaborCost { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Overhead cost cannot be negative")]
    public decimal OverheadCost { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    // Navigation property for material consumptions
    public virtual ICollection<ProductionConsumption> MaterialConsumptions { get; set; } = new List<ProductionConsumption>();

    // FIXED: Safe calculated properties with divide by zero protection
    public decimal TotalCost => MaterialCost + LaborCost + OverheadCost;

    /// <summary>
    /// Unit cost with safe division by zero protection
    /// </summary>
    public decimal UnitCost => QuantityProduced > 0 ? TotalCost / QuantityProduced : 0;

    /// <summary>
    /// Total value of this production run
    /// </summary>
    public decimal TotalValue => TotalCost;

    /// <summary>
    /// Material cost percentage of total (safe calculation)
    /// </summary>
    public decimal MaterialCostPercentage => TotalCost > 0 ? (MaterialCost / TotalCost * 100) : 0;

    /// <summary>
    /// Labor cost percentage of total (safe calculation)
    /// </summary>
    public decimal LaborCostPercentage => TotalCost > 0 ? (LaborCost / TotalCost * 100) : 0;

    /// <summary>
    /// Overhead cost percentage of total (safe calculation)
    /// </summary>
    public decimal OverheadCostPercentage => TotalCost > 0 ? (OverheadCost / TotalCost * 100) : 0;

    /// <summary>
    /// Efficiency indicator - lower unit cost is better
    /// </summary>
    public string EfficiencyRating
    {
      get
      {
        if (UnitCost == 0) return "Unknown";
        if (UnitCost < 10) return "Excellent";
        if (UnitCost < 50) return "Good";
        if (UnitCost < 100) return "Fair";
        return "Poor";
      }
    }

    /// <summary>
    /// Check if this production has any cost data
    /// </summary>
    public bool HasCostData => MaterialCost > 0 || LaborCost > 0 || OverheadCost > 0;

    /// <summary>
    /// Check if this production is cost-efficient (has valid cost data)
    /// </summary>
    public bool IsCostEfficient => HasCostData && QuantityProduced > 0;
  }
}

