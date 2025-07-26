// Models / Production.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class Production
  {
    public int Id { get; set; }

    [Required]
    public int FinishedGoodId { get; set; }
    public virtual FinishedGood FinishedGood { get; set; } = null!;

    [Required]
    public int BomId { get; set; }
    public virtual Bom Bom { get; set; } = null!;

    [Required]
    [Display(Name = "Quantity Produced")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int QuantityProduced { get; set; }

    [Required]
    [Display(Name = "Production Date")]
    public DateTime ProductionDate { get; set; } = DateTime.Now;

    [Display(Name = "Material Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MaterialCost { get; set; }

    [Display(Name = "Labor Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal LaborCost { get; set; } = 0;

    [Display(Name = "Overhead Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal OverheadCost { get; set; } = 0;

    [Display(Name = "Total Production Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost => MaterialCost + LaborCost + OverheadCost;

    [Display(Name = "Unit Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost => QuantityProduced > 0 ? TotalCost / QuantityProduced : 0;

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual ICollection<ProductionConsumption> MaterialConsumptions { get; set; } = new List<ProductionConsumption>();
  }
}
