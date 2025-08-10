using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace InventorySystem.ViewModels
{
    public class CreateExpenseItemViewModel
    {
        [Required]
        [StringLength(100, ErrorMessage = "Part Number cannot exceed 100 characters.")]
        [Display(Name = "Part Number")]
        public string PartNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Comments cannot exceed 1000 characters.")]
        [Display(Name = "Comments")]
        public string? Comments { get; set; }

        [Display(Name = "Expense Type")]
        public ItemType ItemType { get; set; } = ItemType.Service;

        [Display(Name = "Unit of Measure")]
        public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

        [Display(Name = "Preferred Vendor")]
        public int? PreferredVendorId { get; set; }

        [StringLength(100, ErrorMessage = "Vendor Part Number cannot exceed 100 characters.")]
        [Display(Name = "Vendor Part Number")]
        public string? VendorPartNumber { get; set; }

        [StringLength(200, ErrorMessage = "Account Code cannot exceed 200 characters.")]
        [Display(Name = "Account Code")]
        public string? AccountCode { get; set; }

        [StringLength(100, ErrorMessage = "Tax Category cannot exceed 100 characters.")]
        [Display(Name = "Tax Category")]
        public string? TaxCategory { get; set; }

        [Display(Name = "Recurring Expense")]
        public bool IsRecurring { get; set; }

        [Display(Name = "Frequency")]
        public string? RecurringFrequency { get; set; }

        [Display(Name = "Item Image")]
        public IFormFile? ImageFile { get; set; }

        [Required]
        [StringLength(10, ErrorMessage = "Version cannot exceed 10 characters.")]
        [Display(Name = "Version")]
        public string Version { get; set; } = "A";

        // Properties for expense-specific validation
        public List<string> AvailableExpenseTypes => new()
        {
            "Expense",
            "Utility", 
            "Subscription",
            "Service",
            "Virtual"
        };

        public List<string> AvailableFrequencies => new()
        {
            "Monthly",
            "Quarterly",
            "Annually",
            "Weekly",
            "One-time"
        };

        public List<string> AvailableTaxCategories => new()
        {
            "Office Expenses",
            "Utilities",
            "Software & Technology",
            "Professional Services",
            "Travel & Entertainment",
            "Rent & Facilities",
            "Insurance",
            "Marketing & Advertising",
            "Research & Development",
            "Equipment & Supplies"
        };
    }
}