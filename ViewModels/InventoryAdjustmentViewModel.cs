using System.ComponentModel.DataAnnotations;
using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class InventoryAdjustmentViewModel
    {
        public int ItemId { get; set; }
        
        [Display(Name = "Item Part Number")]
        public string ItemPartNumber { get; set; } = string.Empty;
        
        [Display(Name = "Item Description")]
        public string ItemDescription { get; set; } = string.Empty;
        
        [Display(Name = "Current Stock")]
        public int CurrentStock { get; set; }
        
        [Required]
        [Display(Name = "Adjustment Type")]
        public string AdjustmentType { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Quantity Adjusted")]
        [Range(-99999, 99999, ErrorMessage = "Quantity adjusted must be between -99,999 and 99,999")]
        public int QuantityAdjusted { get; set; }
        
        [Required]
        [Display(Name = "Adjustment Date")]
        [DataType(DataType.Date)]
        public DateTime AdjustmentDate { get; set; } = DateTime.Today;
        
        [Required]
        [Display(Name = "Reason")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string Reason { get; set; } = string.Empty;
        
        [Display(Name = "Reference Number")]
        [StringLength(100, ErrorMessage = "Reference number cannot exceed 100 characters")]
        public string? ReferenceNumber { get; set; }
        
        [Display(Name = "Adjusted By")]
        [StringLength(100, ErrorMessage = "Adjusted by cannot exceed 100 characters")]
        public string? AdjustedBy { get; set; }
        
        // Helper properties
        public int NewStock => CurrentStock + QuantityAdjusted;
        
        public bool IsDecrease => QuantityAdjusted < 0;
        
        public bool IsIncrease => QuantityAdjusted > 0;
        
        // Available adjustment types
        public List<string> AvailableAdjustmentTypes => InventoryAdjustment.AdjustmentTypes;
        
        // Validation
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (QuantityAdjusted == 0)
            {
                yield return new ValidationResult(
                    "Quantity adjusted cannot be zero.",
                    new[] { nameof(QuantityAdjusted) });
            }
            
            if (CurrentStock + QuantityAdjusted < 0)
            {
                yield return new ValidationResult(
                    $"Adjustment would result in negative stock. Current stock: {CurrentStock}, Maximum decrease: {CurrentStock}",
                    new[] { nameof(QuantityAdjusted) });
            }
            
            if (string.IsNullOrWhiteSpace(AdjustmentType))
            {
                yield return new ValidationResult(
                    "Adjustment type is required.",
                    new[] { nameof(AdjustmentType) });
            }
            
            if (!AvailableAdjustmentTypes.Contains(AdjustmentType))
            {
                yield return new ValidationResult(
                    "Invalid adjustment type selected.",
                    new[] { nameof(AdjustmentType) });
            }
        }
    }
}