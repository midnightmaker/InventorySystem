// Models/Accounting/Account.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models.Accounting
{
	public enum AccountType
	{
		Asset = 1,
		Liability = 2,
		Equity = 3,
		Revenue = 4,
		Expense = 5
	}

	public enum AccountSubType
	{
		// Assets
		CurrentAsset = 101,
		FixedAsset = 102,
		InventoryAsset = 103,

		// Liabilities  
		CurrentLiability = 201,
		LongTermLiability = 202,

		// Equity
		OwnerEquity = 301,
		RetainedEarnings = 302,

		// Revenue
		SalesRevenue = 401,
		ServiceRevenue = 402,

		// Expenses
		CostOfGoodsSold = 501,
		OperatingExpense = 502,
		UtilityExpense = 503,
		SubscriptionExpense = 504
	}

	public class Account
	{
		public int Id { get; set; }

		[Required, StringLength(10)]
		public string AccountCode { get; set; } = string.Empty;  // e.g., "1200"

		[Required, StringLength(100)]
		public string AccountName { get; set; } = string.Empty;  // e.g., "Raw Materials Inventory"

		[StringLength(200)]
		public string? Description { get; set; }

		public AccountType AccountType { get; set; }
		public AccountSubType AccountSubType { get; set; }

		public bool IsActive { get; set; } = true;
		public bool IsSystemAccount { get; set; } = false;  // Can't be deleted

		// Balance tracking
		[Column(TypeName = "decimal(18,2)")]
		public decimal CurrentBalance { get; set; } = 0;

		public DateTime LastTransactionDate { get; set; }

		// Hierarchy support (optional)
		public int? ParentAccountId { get; set; }
		public Account? ParentAccount { get; set; }
		public List<Account> SubAccounts { get; set; } = new();

		// Relationships
		public List<GeneralLedgerEntry> LedgerEntries { get; set; } = new();

		public DateTime CreatedDate { get; set; } = DateTime.Now;
		public string? CreatedBy { get; set; }

		// Computed properties
		public bool IsDebitAccount => AccountType == AccountType.Asset || AccountType == AccountType.Expense;
		public bool IsCreditAccount => AccountType == AccountType.Liability || AccountType == AccountType.Equity || AccountType == AccountType.Revenue;

		// Helper methods
		public string GetAccountTypeDisplay()
		{
			return AccountType switch
			{
				AccountType.Asset => "Asset",
				AccountType.Liability => "Liability",
				AccountType.Equity => "Equity",
				AccountType.Revenue => "Revenue",
				AccountType.Expense => "Expense",
				_ => "Unknown"
			};
		}

		public string GetFormattedBalance()
		{
			return CurrentBalance.ToString("C");
		}
	}
}