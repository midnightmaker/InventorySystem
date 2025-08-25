using InventorySystem.Models.Enums;
using InventorySystem.Models.Accounting;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Expense Code")]
        public string ExpenseCode { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Comments")]
        public string? Comments { get; set; }

        [Required]
        [Display(Name = "Expense Category")]
        public ExpenseCategory Category { get; set; } = ExpenseCategory.GeneralBusiness;

        [Required]
        [Display(Name = "Tax Category")]
        public TaxCategory TaxCategory { get; set; } = TaxCategory.BusinessExpense;

        [Display(Name = "Unit of Measure")]
        public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Each;

        [Display(Name = "Default Vendor")]
        public int? DefaultVendorId { get; set; }
        public virtual Vendor? DefaultVendor { get; set; }

        // ✅ NEW: User-selectable ledger account
        [Required]
        [Display(Name = "Ledger Account")]
        public int LedgerAccountId { get; set; }
        public virtual Account LedgerAccount { get; set; } = null!;

        [StringLength(100)]
        [Display(Name = "Vendor Expense Code")]
        public string? VendorExpenseCode { get; set; }

        [Display(Name = "Is Recurring")]
        public bool IsRecurring { get; set; } = false;

        [Display(Name = "Recurring Frequency")]
        public RecurringFrequency? RecurringFrequency { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Requires Approval")]
        public bool RequiresApproval { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Default Amount")]
        public decimal? DefaultAmount { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastModified { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? ModifiedBy { get; set; }

        // Image properties for receipts/documentation
        public byte[]? ImageData { get; set; }
        public string? ImageContentType { get; set; }
        public string? ImageFileName { get; set; }

        [NotMapped]
        public bool HasImage => ImageData != null && ImageData.Length > 0;

        // Navigation properties - PRESERVE ALL DOCUMENT FUNCTIONALITY
        public virtual ICollection<ExpensePayment> Payments { get; set; } = new List<ExpensePayment>();
        public virtual ICollection<PurchaseDocument> Documents { get; set; } = new List<PurchaseDocument>();

        // Computed properties for backward compatibility
        [NotMapped]
        public string PartNumber => ExpenseCode; // For compatibility with existing views

        [NotMapped]
        public string CategoryDisplayName => Category switch
        {
            ExpenseCategory.OfficeSupplies => "Office Supplies",
            ExpenseCategory.Utilities => "Utilities",
            ExpenseCategory.ProfessionalServices => "Professional Services",
            ExpenseCategory.SoftwareLicenses => "Software & Licenses",
            ExpenseCategory.Travel => "Travel & Transportation",
            ExpenseCategory.Equipment => "Equipment & Maintenance",
            ExpenseCategory.Marketing => "Marketing & Advertising",
            ExpenseCategory.Research => "Research & Development",
            ExpenseCategory.Insurance => "Insurance",
            ExpenseCategory.GeneralBusiness => "General Business",
            _ => Category.ToString()
        };

        [NotMapped]
        public string TaxCategoryDisplayName => TaxCategory switch
        {
            TaxCategory.BusinessExpense => "Deductible Business Expense",
            TaxCategory.CapitalExpense => "Capital Expenditure",
            TaxCategory.NonDeductible => "Non-Deductible",
            TaxCategory.PersonalUse => "Personal Use",
            _ => TaxCategory.ToString()
        };

        [NotMapped]
        public string RecurrenceDescription => IsRecurring && RecurringFrequency.HasValue 
            ? RecurringFrequency.Value switch
            {
                Models.Enums.RecurringFrequency.Weekly => "Weekly",
                Models.Enums.RecurringFrequency.Monthly => "Monthly", 
                Models.Enums.RecurringFrequency.Quarterly => "Quarterly",
                Models.Enums.RecurringFrequency.Annually => "Annually",
                _ => "Custom"
            }
            : "One-time";

        // For compatibility with existing PayExpenseViewModel
        [NotMapped]
        public string ItemTypeDisplayName => Category.ToString().Replace("_", " ");

        [NotMapped]
        public string DisplayText => $"{ExpenseCode} - {Description}";

        // ✅ NEW: Helper method to get suggested account for category
        public static string GetSuggestedAccountCodeForCategory(ExpenseCategory category)
        {
            return category switch
            {
                ExpenseCategory.OfficeSupplies => "6100",        // Office Supplies Expense
                ExpenseCategory.Utilities => "6200",             // Utilities Expense
                ExpenseCategory.ProfessionalServices => "6300",  // Professional Services Expense
                ExpenseCategory.SoftwareLicenses => "6400",      // Software & Technology Expense
                ExpenseCategory.Travel => "6500",                // Travel & Transportation Expense
                ExpenseCategory.Equipment => "6600",             // Equipment & Maintenance Expense
                ExpenseCategory.Marketing => "6700",             // Marketing & Advertising Expense
                ExpenseCategory.Research => "6800",              // Research & Development Expense
                ExpenseCategory.Insurance => "6900",             // Insurance Expense
                ExpenseCategory.GeneralBusiness => "6000",       // General Business Expense
                _ => "6000"                                       // Default to General Business Expense
            };
        }
    }
}