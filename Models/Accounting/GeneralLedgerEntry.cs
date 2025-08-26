// Models/Accounting/GeneralLedgerEntry.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Accounting
{
	public class GeneralLedgerEntry
	{
		public int Id { get; set; }

		[Required]
		public DateTime TransactionDate { get; set; }

		[Required, StringLength(50)]
		public string TransactionNumber { get; set; } = string.Empty;

		[Required]
		public int AccountId { get; set; }
		public Account Account { get; set; } = null!;

		[StringLength(200)]
		public string Description { get; set; } = string.Empty;

		[Column(TypeName = "decimal(18,2)")]
		public decimal DebitAmount { get; set; } = 0;

		[Column(TypeName = "decimal(18,2)")]
		public decimal CreditAmount { get; set; } = 0;

		// Reference back to source transaction
		[StringLength(50)]
		public string? ReferenceType { get; set; }  // "Purchase", "Sale", "Production", etc.
		public int? ReferenceId { get; set; }       // ID of source transaction

		[StringLength(100)]
		public string? CreatedBy { get; set; }
		public DateTime CreatedDate { get; set; } = DateTime.Now;

		// Computed properties
		[NotMapped]
		public decimal Amount => DebitAmount > 0 ? DebitAmount : CreditAmount;

		[NotMapped]
		public bool IsDebit => DebitAmount > 0;

		[NotMapped]
		public bool IsCredit => CreditAmount > 0;

		[NotMapped]
		public bool HasReference => !string.IsNullOrEmpty(ReferenceType) && ReferenceId.HasValue;

		
		public string? GetReferenceUrl()
		{
			if (!HasReference) return null;

			return ReferenceType?.ToLower() switch
			{
				"sale" => $"/Sales/Details/{ReferenceId}",
				"purchase" => $"/Purchases/Details/{ReferenceId}",
				"customerpayment" => !string.IsNullOrEmpty(EnhancedReferenceUrl) ? EnhancedReferenceUrl : $"/Sales/Details/{ReferenceId}",
				"vendorpayment" => $"/Vendors/PaymentDetails/{ReferenceId}",
				"expensepayment" => $"/Expenses/Details/{ReferenceId}",
				"inventoryadjustment" => $"/Inventory/AdjustmentDetails/{ReferenceId}", // ✅ Fixed path
				"production" => $"/Production/Details/{ReferenceId}",
				"manualjournalentry" => null, // Manual entries don't have source documents
				_ => null
			};
		}

		
		public string GetReferenceDisplayText()
		{
			if (!HasReference) return "";

			return ReferenceType?.ToLower() switch
			{
				"sale" => $"Sale #{ReferenceId}",
				"purchase" => $"Purchase #{ReferenceId}",
				"customerpayment" => $"Payment #{ReferenceId}",
				"vendorpayment" => $"Vendor Payment #{ReferenceId}",
				"expensepayment" => $"Expense Payment #{ReferenceId}",
				"inventoryadjustment" => $"Adjustment #{ReferenceId}",
				"production" => $"Production #{ReferenceId}",
				_ => $"{ReferenceType} #{ReferenceId}"
			};
		}

		
		public string GetReferenceIcon()
		{
			return ReferenceType?.ToLower() switch
			{
				"sale" => "fas fa-shopping-cart text-success",
				"purchase" => "fas fa-shopping-bag text-primary",
				"customerpayment" => "fas fa-credit-card text-success",
				"vendorpayment" => "fas fa-money-check text-danger",
				"expensepayment" => "fas fa-receipt text-warning",
				"inventoryadjustment" => "fas fa-boxes text-info",
				"production" => "fas fa-cogs text-secondary",
				_ => "fas fa-link text-muted"
			};
		}

		public string GetReferenceBadgeClass()
		{
			return ReferenceType?.ToLower() switch
			{
				"sale" => "badge bg-success",
				"purchase" => "badge bg-primary",
				"customerpayment" => "badge bg-success",
				"vendorpayment" => "badge bg-danger",
				"expensepayment" => "badge bg-warning",
				"inventoryadjustment" => "badge bg-info",
				"production" => "badge bg-secondary",
				"manualjournalentry" => "badge bg-dark",
				_ => "badge bg-secondary"
			};
		}

		public string GetFinalReferenceText()
		{
			return !string.IsNullOrEmpty(EnhancedReferenceText)
					? EnhancedReferenceText
					: GetReferenceDisplayText();
		}

		public string? GetFinalReferenceUrl()
		{
			return !string.IsNullOrEmpty(EnhancedReferenceUrl)
					? EnhancedReferenceUrl
					: GetReferenceUrl();
		}
		// Helper method to get sale ID from customer payment (you'll need to implement this)
		private int? GetSaleIdFromPayment()
		{
			// This would need to be populated when loading the journal entries
			// For now, return ReferenceId which should work for most cases
			return ReferenceId;
		}
		public string GetFormattedAmount()
		{
			return Amount.ToString("C");
		}

		public string GetDebitCreditDisplay()
		{
			if (IsDebit)
				return $"Dr. {DebitAmount:C}";
			else if (IsCredit)
				return $"Cr. {CreditAmount:C}";
			else
				return "$0.00";
		}

		// Add these properties for enhanced reference display
		[NotMapped]
		public string? EnhancedReferenceText { get; set; }

		[NotMapped]
		public string? EnhancedReferenceUrl { get; set; }


	}
}