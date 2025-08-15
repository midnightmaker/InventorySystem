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
        public string AdjustmentType { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Quantity Adjusted")]
        public int QuantityAdjusted { get; set; }
        
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
        public string? ReferenceNumber { get; set; }
        
        [Display(Name = "Adjusted By")]
        [StringLength(100)]
        public string? AdjustedBy { get; set; }
        
        [Display(Name = "Cost Impact")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostImpact { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        // ? NEW: Journal entry tracking
        [Display(Name = "Journal Entry Number")]
        [StringLength(50)]
        public string? JournalEntryNumber { get; set; }
        
        // Helper properties
        [NotMapped]
        public bool IsDecrease => QuantityAdjusted < 0;
        
        [NotMapped]
        public bool IsIncrease => QuantityAdjusted > 0;
        
        [NotMapped]
        public bool HasJournalEntry => !string.IsNullOrEmpty(JournalEntryNumber);
        
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
            "Return" => "fas fa-undo text-info",
            "Scrap" => "fas fa-times-circle text-danger",
            _ => "fas fa-question-circle text-muted"
        };

        // ? NEW: Available adjustment types as static property
        public static List<string> AdjustmentTypes => new()
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