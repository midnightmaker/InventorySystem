using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class BomItem
    {
        public int Id { get; set; }
        
        public int BomId { get; set; }
        public virtual Bom Bom { get; set; } = null!;
        
        public int ItemId { get; set; }
        public virtual Item Item { get; set; } = null!;
        
        [Required]
        [Display(Name = "Quantity Required")]
        public int Quantity { get; set; }
        
        [Display(Name = "Reference Designator")]
        public string? ReferenceDesignator { get; set; }
        
        public string? Notes { get; set; }
        
        [Display(Name = "Unit Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }
        
        [Display(Name = "Extended Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ExtendedCost => Quantity * UnitCost;
    }
}