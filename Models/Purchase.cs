using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class Purchase
    {
        public int Id { get; set; }
        
        public int ItemId { get; set; }
        public virtual Item Item { get; set; } = null!;
        
        [Required]
        public string Vendor { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Purchase Date")]
        public DateTime PurchaseDate { get; set; }
        
        [Required]
        [Display(Name = "Quantity Purchased")]
        public int QuantityPurchased { get; set; }
        
        [Required]
        [Display(Name = "Cost Per Unit")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPerUnit { get; set; }
        
        [Display(Name = "Total Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost => QuantityPurchased * CostPerUnit;
        
        [Display(Name = "Remaining Quantity")]
        public int RemainingQuantity { get; set; }
        
        [Display(Name = "Purchase Order Number")]
        public string? PurchaseOrderNumber { get; set; }
        
        public string? Notes { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}