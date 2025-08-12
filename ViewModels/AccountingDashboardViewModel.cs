using InventorySystem.Models.Accounting;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Accounting
{
	// ============= DASHBOARD =============

	public class AccountingDashboardViewModel
	{
		public decimal CashBalance { get; set; }
		public decimal AccountsReceivableBalance { get; set; }
		public decimal AccountsPayableBalance { get; set; }
		public decimal TotalAssets { get; set; }
		public decimal TotalLiabilities { get; set; }
		public decimal OwnerEquity { get; set; }
		public decimal MonthlyRevenue { get; set; }
		public decimal MonthlyExpenses { get; set; }
		public decimal NetIncome { get; set; }
		public decimal InventoryValue { get; set; }

		// Recent activity
		public List<GeneralLedgerEntry> RecentTransactions { get; set; } = new();
		public List<AccountsPayable> UpcomingPayments { get; set; } = new();

		// Quick stats
		public int TotalAccounts { get; set; }
		public int ActiveAccounts { get; set; }
		public int UnpaidInvoicesCount { get; set; }
		public int OverdueInvoicesCount { get; set; }

		// Charts data
		public List<MonthlyFinancialData> MonthlyData { get; set; } = new();
		public List<ExpenseCategoryData> ExpenseBreakdown { get; set; } = new();
	}

	public class MonthlyFinancialData
	{
		public DateTime Month { get; set; }
		public decimal Revenue { get; set; }
		public decimal Expenses { get; set; }
		public decimal NetIncome { get; set; }
		public string MonthName => Month.ToString("MMM yyyy");
	}

	public class ExpenseCategoryData
	{
		public string Category { get; set; } = string.Empty;
		public string CategoryDisplayName { get; set; } = string.Empty;
		public decimal TotalAmount { get; set; }
		public decimal Percentage { get; set; }
	}

	// ============= CHART OF ACCOUNTS =============

	public class ChartOfAccountsViewModel
	{
		public List<Account> Accounts { get; set; } = new();
		public string SearchTerm { get; set; } = string.Empty;
		public AccountType? FilterAccountType { get; set; }
		public bool ShowInactiveAccounts { get; set; } = false;

		// Grouped accounts for display
		public IEnumerable<IGrouping<AccountType, Account>> AccountsByType =>
				Accounts.GroupBy(a => a.AccountType);
	}

	public class CreateAccountViewModel
	{
		[Required, StringLength(10)]
		[Display(Name = "Account Code")]
		public string AccountCode { get; set; } = string.Empty;

		[Required, StringLength(100)]
		[Display(Name = "Account Name")]
		public string AccountName { get; set; } = string.Empty;

		[StringLength(200)]
		public string? Description { get; set; }

		[Required]
		[Display(Name = "Account Type")]
		public AccountType AccountType { get; set; }

		[Required]
		[Display(Name = "Account Sub Type")]
		public AccountSubType AccountSubType { get; set; }

		[Display(Name = "Parent Account")]
		public int? ParentAccountId { get; set; }

		// Available parent accounts for dropdown
		public List<Account> AvailableParentAccounts { get; set; } = new();
	}

	public class EditAccountViewModel : CreateAccountViewModel
	{
		public int Id { get; set; }

		[Display(Name = "Is Active")]
		public bool IsActive { get; set; } = true;

		public bool IsSystemAccount { get; set; }
		public decimal CurrentBalance { get; set; }
		public DateTime LastTransactionDate { get; set; }
	}

	public class AccountDetailsViewModel
	{
		public Account Account { get; set; } = null!;
		public List<GeneralLedgerEntry> LedgerEntries { get; set; } = new();
		public decimal CurrentBalance { get; set; }
		public DateTime? StartDate { get; set; }
		public DateTime? EndDate { get; set; }

		public decimal TotalDebits => LedgerEntries.Sum(e => e.DebitAmount);
		public decimal TotalCredits => LedgerEntries.Sum(e => e.CreditAmount);
		public int TransactionCount => LedgerEntries.Count;
	}

	// ============= GENERAL LEDGER =============

	public class GeneralLedgerViewModel
	{
		public List<GeneralLedgerEntry> Entries { get; set; } = new();
		public List<Account> Accounts { get; set; } = new();

		public string? SelectedAccountCode { get; set; }
		public DateTime StartDate { get; set; } = DateTime.Today.AddMonths(-1);
		public DateTime EndDate { get; set; } = DateTime.Today;

		public decimal TotalDebits => Entries.Sum(e => e.DebitAmount);
		public decimal TotalCredits => Entries.Sum(e => e.CreditAmount);
		public bool IsBalanced => Math.Abs(TotalDebits - TotalCredits) < 0.01m;
	}

	// ============= FINANCIAL REPORTS =============

	public class TrialBalanceViewModel
	{
		public DateTime AsOfDate { get; set; } = DateTime.Today;
		public List<TrialBalanceEntry> Entries { get; set; } = new();

		public decimal TotalDebits => Entries.Sum(e => e.DebitBalance);
		public decimal TotalCredits => Entries.Sum(e => e.CreditBalance);
		public bool IsBalanced => Math.Abs(TotalDebits - TotalCredits) < 0.01m;
	}

	public class TrialBalanceEntry
	{
		public string AccountCode { get; set; } = string.Empty;
		public string AccountName { get; set; } = string.Empty;
		public AccountType AccountType { get; set; }
		public decimal DebitBalance { get; set; }
		public decimal CreditBalance { get; set; }
		public decimal Balance => DebitBalance - CreditBalance;
	}

	public class BalanceSheetViewModel
	{
		public DateTime AsOfDate { get; set; } = DateTime.Today;

		// Assets
		public List<BalanceSheetAccount> CurrentAssets { get; set; } = new();
		public List<BalanceSheetAccount> FixedAssets { get; set; } = new();
		public List<BalanceSheetAccount> OtherAssets { get; set; } = new();

		// Liabilities
		public List<BalanceSheetAccount> CurrentLiabilities { get; set; } = new();
		public List<BalanceSheetAccount> LongTermLiabilities { get; set; } = new();

		// Equity
		public List<BalanceSheetAccount> EquityAccounts { get; set; } = new();

		// Totals
		public decimal TotalCurrentAssets => CurrentAssets.Sum(a => a.Balance);
		public decimal TotalFixedAssets => FixedAssets.Sum(a => a.Balance);
		public decimal TotalAssets => TotalCurrentAssets + TotalFixedAssets + OtherAssets.Sum(a => a.Balance);

		public decimal TotalCurrentLiabilities => CurrentLiabilities.Sum(l => l.Balance);
		public decimal TotalLongTermLiabilities => LongTermLiabilities.Sum(l => l.Balance);
		public decimal TotalLiabilities => TotalCurrentLiabilities + TotalLongTermLiabilities;

		public decimal TotalEquity => EquityAccounts.Sum(e => e.Balance);

		public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;

		public bool IsBalanced => Math.Abs(TotalAssets - TotalLiabilitiesAndEquity) < 0.01m;
	}

	public class BalanceSheetAccount
	{
		public string AccountCode { get; set; } = string.Empty;
		public string AccountName { get; set; } = string.Empty;
		public decimal Balance { get; set; }
		public string FormattedBalance => Balance.ToString("C");
	}

	public class IncomeStatementViewModel
	{
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }

		// Revenue
		public List<IncomeStatementAccount> RevenueAccounts { get; set; } = new();
		public decimal TotalRevenue => RevenueAccounts.Sum(r => r.Amount);

		// Cost of Goods Sold
		public List<IncomeStatementAccount> COGSAccounts { get; set; } = new();
		public decimal TotalCOGS => COGSAccounts.Sum(c => c.Amount);

		// Operating Expenses
		public List<IncomeStatementAccount> OperatingExpenses { get; set; } = new();
		public decimal TotalOperatingExpenses => OperatingExpenses.Sum(e => e.Amount);

		// Other Expenses
		public List<IncomeStatementAccount> OtherExpenses { get; set; } = new();
		public decimal TotalOtherExpenses => OtherExpenses.Sum(e => e.Amount);

		// Calculated totals
		public decimal GrossProfit => TotalRevenue - TotalCOGS;
		public decimal NetIncome => GrossProfit - TotalOperatingExpenses - TotalOtherExpenses;

		// Percentages
		public decimal GrossProfitMargin => TotalRevenue > 0 ? (GrossProfit / TotalRevenue) * 100 : 0;
		public decimal NetProfitMargin => TotalRevenue > 0 ? (NetIncome / TotalRevenue) * 100 : 0;

		public string PeriodDisplay => $"{StartDate:MMM dd, yyyy} to {EndDate:MMM dd, yyyy}";
	}

	public class IncomeStatementAccount
	{
		public string AccountCode { get; set; } = string.Empty;
		public string AccountName { get; set; } = string.Empty;
		public decimal Amount { get; set; }
		public string FormattedAmount => Amount.ToString("C");
	}

	public class CashFlowStatementViewModel
	{
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }

		// Operating Activities
		public decimal NetIncome { get; set; }
		public List<CashFlowItem> OperatingAdjustments { get; set; } = new();
		public decimal NetCashFromOperations => NetIncome + OperatingAdjustments.Sum(a => a.Amount);

		// Investing Activities
		public List<CashFlowItem> InvestingActivities { get; set; } = new();
		public decimal NetCashFromInvesting => InvestingActivities.Sum(i => i.Amount);

		// Financing Activities
		public List<CashFlowItem> FinancingActivities { get; set; } = new();
		public decimal NetCashFromFinancing => FinancingActivities.Sum(f => f.Amount);

		// Net change in cash
		public decimal NetChangeInCash => NetCashFromOperations + NetCashFromInvesting + NetCashFromFinancing;

		public decimal BeginningCashBalance { get; set; }
		public decimal EndingCashBalance => BeginningCashBalance + NetChangeInCash;

		public string PeriodDisplay => $"{StartDate:MMM dd, yyyy} to {EndDate:MMM dd, yyyy}";
	}

	public class CashFlowItem
	{
		public string Description { get; set; } = string.Empty;
		public decimal Amount { get; set; }
		public string FormattedAmount => Amount.ToString("C");
	}

	// ============= ACCOUNTS PAYABLE =============

	public class AccountsPayableViewModel
	{
		public List<AccountsPayable> UnpaidAccountsPayable { get; set; } = new();
		public List<AccountsPayable> OverdueAccountsPayable { get; set; } = new();

		public decimal TotalAccountsPayable { get; set; }
		public decimal TotalOverdueAmount { get; set; }

		public int TotalUnpaidCount => UnpaidAccountsPayable.Count;
		public int TotalOverdueCount => OverdueAccountsPayable.Count;

		public string FormattedTotalAP => TotalAccountsPayable.ToString("C");
		public string FormattedTotalOverdue => TotalOverdueAmount.ToString("C");
	}

	// ============= SETUP =============

	public class SetupViewModel
	{
		public bool IsSystemInitialized { get; set; }

		[Display(Name = "Create Default Chart of Accounts")]
		public bool SeedDefaultAccounts { get; set; } = true;

		[Display(Name = "Generate Journal Entries for Existing Transactions")]
		public bool GenerateHistoricalEntries { get; set; } = false;

		public string StatusMessage { get; set; } = string.Empty;
	}
}