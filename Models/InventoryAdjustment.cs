using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

namespace InventorySystem.Models
{
    /// <summary>
    /// Tracks inventory adjustments made to items
    /// </summary>
    public class InventoryAdjustment
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Item")]
        public int ItemId { get; set; }
        public virtual Item Item { get; set; } = null!;

        [Required]
        [Display(Name = "Adjustment Type")]
        public AdjustmentType AdjustmentType { get; set; }

        [Required]
        [Display(Name = "Quantity Adjusted")]
        [Range(-99999, 99999, ErrorMessage = "Quantity adjusted must be between -99,999 and 99,999")]
        public int QuantityAdjusted { get; set; }

        [Required]
        [Display(Name = "Adjustment Date")]
        [DataType(DataType.Date)]
        public DateTime AdjustmentDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Reason")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string Reason { get; set; } = string.Empty;

        [Display(Name = "Reference Number")]
        [StringLength(100, ErrorMessage = "Reference number cannot exceed 100 characters")]
        public string? ReferenceNumber { get; set; }

        [Display(Name = "Adjusted By")]
        [StringLength(100, ErrorMessage = "Adjusted by cannot exceed 100 characters")]
        public string AdjustedBy { get; set; } = string.Empty;

        [Display(Name = "Cost Impact")]
        [Column(TypeName = "decimal(18,6)")]
        public decimal CostImpact { get; set; }

        [Display(Name = "Journal Entry Number")]
        [StringLength(50)]
        public string? JournalEntryNumber { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastModifiedDate { get; set; }

        // Computed Properties
        [NotMapped]
        [Display(Name = "Adjustment Type Display")]
        public string AdjustmentTypeDisplay => AdjustmentType switch
        {
            AdjustmentType.Increase => "Increase",
            AdjustmentType.Decrease => "Decrease", 
            AdjustmentType.CycleCount => "Cycle Count",
            AdjustmentType.PhysicalCount => "Physical Count",
            AdjustmentType.Shrinkage => "Shrinkage/Loss",
            AdjustmentType.Damaged => "Damaged Goods",
            AdjustmentType.ReturnToVendor => "Return to Vendor",
            AdjustmentType.Other => "Other",
            _ => "Unknown"
        };

        [NotMapped]
        public bool IsIncrease => QuantityAdjusted > 0;

        [NotMapped]
        public bool IsDecrease => QuantityAdjusted < 0;

        // Additional computed properties for UI
        [NotMapped]
        public string AdjustmentIcon => AdjustmentType switch
        {
            AdjustmentType.Increase => "fas fa-plus-circle text-success",
            AdjustmentType.Decrease => "fas fa-minus-circle text-danger",
            AdjustmentType.CycleCount => "fas fa-list-check text-info",
            AdjustmentType.PhysicalCount => "fas fa-clipboard-list text-primary",
            AdjustmentType.Shrinkage => "fas fa-chart-line-down text-warning",
            AdjustmentType.Damaged => "fas fa-exclamation-triangle text-danger",
            AdjustmentType.ReturnToVendor => "fas fa-undo text-info",
            AdjustmentType.Other => "fas fa-edit text-secondary",
            _ => "fas fa-question-circle text-muted"
        };

        // Stock tracking properties (these need to be set by the controller)
        [NotMapped]
        public int StockBefore { get; set; }

        [NotMapped]
        public int StockAfter { get; set; }

        // Static helper for adjustment types
        public static List<string> AdjustmentTypes => new()
        {
            "Cycle Count",
            "Physical Count", 
            "Shrinkage/Loss",
            "Damaged Goods",
            "Return to Vendor",
            "Receiving Adjustment",
            "Quality Control",
            "Other"
        };

        public string GetFormattedCostImpact()
        {
            return CostImpact.ToString("C");
        }
    }
}