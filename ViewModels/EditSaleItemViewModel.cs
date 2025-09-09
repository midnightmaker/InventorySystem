using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class EditSaleItemViewModel
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        
        // Product identification (read-only in edit)
        public string ProductType { get; set; } = string.Empty; // "Item", "FinishedGood", or "ServiceType"
        public int? ItemId { get; set; }
        public int? FinishedGoodId { get; set; }
        public int? ServiceTypeId { get; set; } // ADDED: Missing ServiceType support
        
        // Display information (read-only)
        public string ProductPartNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        
        // Editable fields
        [Required]
        [Display(Name = "Quantity")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Required]
        [Display(Name = "Unit Price")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Serial Number and Model Number fields
        [StringLength(100, ErrorMessage = "Serial number cannot exceed 100 characters")]
        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        [StringLength(100, ErrorMessage = "Model number cannot exceed 100 characters")]
        [Display(Name = "Model Number")]
        public string? ModelNumber { get; set; }

        // Requirements (for validation and display)
        public bool RequiresSerialNumber { get; set; }
        public bool RequiresModelNumber { get; set; }

        // Calculated properties
        public decimal TotalPrice => Quantity * UnitPrice;
        
        public bool HasRequiredInfo
        {
            get
            {
                if (RequiresSerialNumber && string.IsNullOrWhiteSpace(SerialNumber))
                    return false;
                if (RequiresModelNumber && string.IsNullOrWhiteSpace(ModelNumber))
                    return false;
                return true;
            }
        }

        public List<string> GetMissingRequirements()
        {
            var missing = new List<string>();
            if (RequiresSerialNumber && string.IsNullOrWhiteSpace(SerialNumber))
                missing.Add("Serial Number");
            if (RequiresModelNumber && string.IsNullOrWhiteSpace(ModelNumber))
                missing.Add("Model Number");
            return missing;
        }
    }
}