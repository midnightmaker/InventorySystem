using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;
using InventorySystem.Models;

namespace InventorySystem.ViewModels.Accounting
{
    /// <summary>
    /// Enhanced manual journal entry view model with customer adjustment support
    /// </summary>
    public class EnhancedManualJournalEntryViewModel : ManualJournalEntryViewModel
    {
        // Customer Adjustment Properties
        [Display(Name = "Is Customer Adjustment")]
        public bool IsCustomerAdjustment { get; set; }

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Related Sale")]
        public int? SaleId { get; set; }

        [Display(Name = "Adjustment Type")]
        [StringLength(50)]
        public string? AdjustmentType { get; set; }

        [Display(Name = "Adjustment Reason")]
        [StringLength(500)]
        public string? AdjustmentReason { get; set; }

        // Navigation Properties
        public List<Customer> AvailableCustomers { get; set; } = new();
        public List<Sale> AvailableSales { get; set; } = new();

        // Helper methods
        public string GetCustomerName()
        {
            return AvailableCustomers.FirstOrDefault(c => c.Id == CustomerId)?.CustomerName ?? "";
        }

        public string GetSaleNumber()
        {
            return AvailableSales.FirstOrDefault(s => s.Id == SaleId)?.SaleNumber ?? "";
        }

        // Additional properties for UI
        public List<string> AdjustmentTypes => new()
        {
            "Sales Allowance",
            "Sales Discount", 
            "Bad Debt Write-off",
            "Credit Memo",
            "Other Adjustment"
        };
    }

    /// <summary>
    /// Customer adjustment template for predefined adjustments
    /// </summary>
    public class CustomerAdjustmentTemplate
    {
        public string AdjustmentType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DebitAccount { get; set; } = string.Empty;
        public string CreditAccount { get; set; } = string.Empty;
        public string DebitDescription { get; set; } = string.Empty;
        public string CreditDescription { get; set; } = string.Empty;

        public static List<CustomerAdjustmentTemplate> GetTemplates()
        {
            return new List<CustomerAdjustmentTemplate>
            {
                new CustomerAdjustmentTemplate
                {
                    AdjustmentType = "Sales Allowance",
                    Description = "Customer concession for quality issues, damaged goods, or service problems",
                    DebitAccount = "4900", // Sales Allowances
                    CreditAccount = "1100", // Accounts Receivable
                    DebitDescription = "Sales allowance granted",
                    CreditDescription = "Reduce accounts receivable"
                },
                new CustomerAdjustmentTemplate
                {
                    AdjustmentType = "Sales Discount",
                    Description = "Early payment discount or promotional discount given",
                    DebitAccount = "4910", // Sales Discounts
                    CreditAccount = "1100", // Accounts Receivable
                    DebitDescription = "Sales discount given",
                    CreditDescription = "Reduce accounts receivable"
                },
                new CustomerAdjustmentTemplate
                {
                    AdjustmentType = "Bad Debt Write-off",
                    Description = "Write off uncollectible customer debt",
                    DebitAccount = "6200", // Bad Debt Expense
                    CreditAccount = "1100", // Accounts Receivable
                    DebitDescription = "Bad debt expense",
                    CreditDescription = "Write off uncollectible account"
                }
            };
        }
    }
}