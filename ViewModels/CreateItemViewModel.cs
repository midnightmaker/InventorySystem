using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class CreateItemViewModel
    {
        [Required]
        [Display(Name = "Internal Part Number")]
        public string PartNumber { get; set; } = string.Empty;
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        public string Comments { get; set; } = string.Empty;
        
        [Display(Name = "Minimum Stock Level")]
        public int MinimumStock { get; set; }
        
        // Image upload
        [Display(Name = "Item Image")]
        public IFormFile? ImageFile { get; set; }
        
        // Initial purchase fields
        [Display(Name = "Add Initial Purchase")]
        public bool HasInitialPurchase { get; set; }
        
        [Display(Name = "Initial Quantity")]
        public int InitialQuantity { get; set; }
        
        [Display(Name = "Initial Cost Per Unit")]
        [DataType(DataType.Currency)]
        public decimal InitialCostPerUnit { get; set; }
        
        [Display(Name = "Initial Vendor")]
        public string InitialVendor { get; set; } = string.Empty;
        
        [Display(Name = "Initial Purchase Date")]
        [DataType(DataType.Date)]
        public DateTime? InitialPurchaseDate { get; set; } = DateTime.Today;
        
        [Display(Name = "Initial Purchase Order Number")]
        public string? InitialPurchaseOrderNumber { get; set; }
        
        // Validation for initial purchase
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (HasInitialPurchase)
            {
                if (InitialQuantity <= 0)
                {
                    yield return new ValidationResult(
                        "Initial quantity must be greater than 0 when adding initial purchase.",
                        new[] { nameof(InitialQuantity) });
                }
                
                if (InitialCostPerUnit <= 0)
                {
                    yield return new ValidationResult(
                        "Initial cost per unit must be greater than 0 when adding initial purchase.",
                        new[] { nameof(InitialCostPerUnit) });
                }
                
                if (string.IsNullOrWhiteSpace(InitialVendor))
                {
                    yield return new ValidationResult(
                        "Initial vendor is required when adding initial purchase.",
                        new[] { nameof(InitialVendor) });
                }
            }
        }
    }
}