
// Models/FinishedGood.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
  public class FinishedGood
  {
    public int Id { get; set; }

    [Required]
    [Display(Name = "Part Number")]
    public string PartNumber { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Current Stock")]
    public int CurrentStock { get; set; }

    [Display(Name = "Minimum Stock Level")]
    public int MinimumStock { get; set; }

    [Display(Name = "Unit Cost")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [Display(Name = "Selling Price")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal SellingPrice { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // Foreign key to BOM
    public int? BomId { get; set; }
    public virtual Bom? Bom { get; set; }

    // Navigation properties
    public virtual ICollection<Production> Productions { get; set; } = new List<Production>();
    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

    // Helper properties
    [NotMapped]
    public decimal TotalValue => CurrentStock * UnitCost;

    [NotMapped]
    public bool IsLowStock => CurrentStock <= MinimumStock;
  }
}