using InventorySystem.Models;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Accounting
{
	public class EnhancedManualJournalEntryViewModel : ManualJournalEntryViewModel
	{
		// Customer-specific fields for A/R adjustments
		[Display(Name = "Is Customer Adjustment")]
		public bool IsCustomerAdjustment { get; set; }

		[Display(Name = "Customer")]
		public int? CustomerId { get; set; }

		[Display(Name = "Invoice/Sale")]
		public int? SaleId { get; set; }

		[Display(Name = "Adjustment Type")]
		public string? AdjustmentType { get; set; } // "Sales Allowance", "Bad Debt", "Discount"

		[Display(Name = "Adjustment Reason")]
		[StringLength(500)]
		public string? AdjustmentReason { get; set; }

		// Available data for dropdowns
		public List<Customer> AvailableCustomers { get; set; } = new List<Customer>();
		public List<Sale> AvailableSales { get; set; } = new List<Sale>();
		public List<string> AdjustmentTypes { get; set; } = new List<string>
				{
						"Sales Allowance",
						"Sales Discount",
						"Bad Debt Write-off",
						"Credit Memo",
						"Other Adjustment"
				};
	}

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