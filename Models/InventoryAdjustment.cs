using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class InventoryAdjustment
    {
        public int Id { get; set; }
        
        public int ItemId { get; set; }
        public virtual Item Item { get; set; } = null!;
        
        [Required]
        [Display(Name = "Adjustment Type")]
        public string AdjustmentType { get; set; } = string.Empty; // "Damage", "Loss", "Found", "Correction", "Theft", "Obsolete"
        
        [Required]
        [Display(Name = "Quantity Adjusted")]
        public int QuantityAdjusted { get; set; } // Positive for increases, negative for decreases
        
        [Display(Name = "Stock Before")]
        public int StockBefore { get; set; }
        
        [Display(Name = "Stock After")]
        public int StockAfter { get; set; }
        
        [Required]
        [Display(Name = "Adjustment Date")]
        public DateTime AdjustmentDate { get; set; } = DateTime.Now;
        
        [Required]
        [Display(Name = "Reason")]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
        
        [Display(Name = "Reference Number")]
        [StringLength(100)]
        public string? ReferenceNumber { get; set; } // Work order, incident report, etc.
        
        [Display(Name = "Adjusted By")]
        [StringLength(100)]
        public string? AdjustedBy { get; set; }
        
        [Display(Name = "Cost Impact")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostImpact { get; set; } // Calculated based on FIFO or average cost
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        // Helper properties
        [NotMapped]
        public bool IsDecrease => QuantityAdjusted < 0;
        
        [NotMapped]
        public bool IsIncrease => QuantityAdjusted > 0;
        
        [NotMapped]
        public string AdjustmentTypeDisplay => AdjustmentType switch
        {
            "Damage" => "Damaged",
            "Loss" => "Lost/Missing",
            "Found" => "Found",
            "Correction" => "Count Correction",
            "Theft" => "Theft",
            "Obsolete" => "Obsolete/Disposed",
            "Return" => "Customer Return",
            "Scrap" => "Scrapped",
            _ => AdjustmentType
        };
        
        [NotMapped]
        public string AdjustmentIcon => AdjustmentType switch
        {
            "Damage" => "fas fa-exclamation-triangle text-warning",
            "Loss" => "fas fa-minus-circle text-danger",
            "Found" => "fas fa-plus-circle text-success",
            "Correction" => "fas fa-edit text-info",
            "Theft" => "fas fa-user-minus text-danger",
            "Obsolete" => "fas fa-trash text-secondary",
            "Return" => "fas fa-undo text-primary",
            "Scrap" => "fas fa-times-circle text-danger",
            _ => "fas fa-balance-scale text-muted"
        };
        
        // Predefined adjustment types
        public static readonly List<string> AdjustmentTypes = new List<string>
        {
            "Damage",
            "Loss", 
            "Found",
            "Correction",
            "Theft",
            "Obsolete",
            "Return",
            "Scrap"
        };
    }
}