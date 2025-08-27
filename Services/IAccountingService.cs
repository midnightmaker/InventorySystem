// Services/IAccountingService.cs
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels.Accounting;

namespace InventorySystem.Services
{
	public interface IAccountingService
	{
		// ============= Chart of Accounts Management =============
		Task<IEnumerable<Account>> GetAllAccountsAsync();
		Task<IEnumerable<Account>> GetActiveAccountsAsync();
		Task<Account?> GetAccountByIdAsync(int accountId);
		Task<Account?> GetAccountByCodeAsync(string accountCode);
		Task<Account> CreateAccountAsync(Account account);
		Task<Account> UpdateAccountAsync(Account account);
		Task<bool> CanDeleteAccountAsync(int accountId);
		Task DeleteAccountAsync(int accountId);

		// Account search and filtering
		Task<IEnumerable<Account>> GetAccountsByTypeAsync(AccountType accountType);
		Task<IEnumerable<Account>> SearchAccountsAsync(string searchTerm);

		// ✅ NEW: Expense-specific account methods
		Task<IEnumerable<Account>> GetExpenseAccountsAsync();
		Task<Account?> GetSuggestedAccountForExpenseCategoryAsync(ExpenseCategory category);

		// ============= General Ledger Management =============
		Task<GeneralLedgerEntry> CreateJournalEntryAsync(GeneralLedgerEntry entry);
		Task<IEnumerable<GeneralLedgerEntry>> CreateJournalEntriesAsync(IEnumerable<GeneralLedgerEntry> entries);
		Task<IEnumerable<GeneralLedgerEntry>> GetAccountLedgerEntriesAsync(string accountCode, DateTime? startDate = null, DateTime? endDate = null);
		Task<IEnumerable<GeneralLedgerEntry>> GetAllLedgerEntriesAsync(DateTime? startDate = null, DateTime? endDate = null);
		Task<string> GenerateNextJournalNumberAsync(string prefix = "JE");

		// ============= Automatic Journal Entry Generation =============
		Task<bool> GenerateJournalEntriesForPurchaseAsync(Purchase purchase);
		Task<bool> GenerateJournalEntriesForSaleAsync(Sale sale);
		Task<bool> GenerateJournalEntriesForProductionAsync(Production production);
		Task<bool> GenerateJournalEntriesForVendorPaymentAsync(VendorPayment payment);
		Task<bool> GenerateJournalEntriesForCustomerPaymentAsync(CustomerPayment payment);
		Task<bool> GenerateJournalEntriesForExpensePaymentAsync(ExpensePayment expensePayment);

		// ============= Account Balances =============
		Task<decimal> GetAccountBalanceAsync(string accountCode, DateTime? asOfDate = null);
		Task<decimal> GetAccountBalanceAsync(int accountId, DateTime? asOfDate = null);
		Task UpdateAccountBalanceAsync(string accountCode, decimal amount, bool isDebit = true);
		Task RecalculateAccountBalanceAsync(string accountCode);
		Task RecalculateAllAccountBalancesAsync();

		// ============= Financial Reports =============
		Task<TrialBalanceViewModel> GetTrialBalanceAsync(DateTime asOfDate);
		Task<BalanceSheetViewModel> GetBalanceSheetAsync(DateTime asOfDate);
		Task<IncomeStatementViewModel> GetIncomeStatementAsync(DateTime startDate, DateTime endDate);
		Task<CashFlowStatementViewModel> GetCashFlowStatementAsync(DateTime startDate, DateTime endDate);

		// ============= Accounts Payable =============
		Task<IEnumerable<AccountsPayable>> GetAllAccountsPayableAsync();
		Task<IEnumerable<AccountsPayable>> GetUnpaidAccountsPayableAsync();
		Task<IEnumerable<AccountsPayable>> GetOverdueAccountsPayableAsync();
		Task<AccountsPayable?> GetAccountsPayableByIdAsync(int id);
		Task<AccountsPayable> CreateAccountsPayableAsync(AccountsPayable ap);
		Task<AccountsPayable> UpdateAccountsPayableAsync(AccountsPayable ap);
		Task<decimal> GetTotalAccountsPayableAsync();
		Task<decimal> GetTotalOverdueAccountsPayableAsync();

		// ============= Vendor Payments =============
		Task<VendorPayment> CreateVendorPaymentAsync(VendorPayment payment);
		Task<IEnumerable<VendorPayment>> GetVendorPaymentsAsync(int vendorId);
		Task<IEnumerable<VendorPayment>> GetAccountsPayablePaymentsAsync(int accountsPayableId);

		// ============= Validation & Utilities =============
		Task<bool> IsValidJournalEntryAsync(IEnumerable<GeneralLedgerEntry> entries);
		Task<bool> DoesAccountExistAsync(string accountCode);
		Task<bool> IsAccountCodeUniqueAsync(string accountCode, int? excludeId = null);
		Task<AccountValidationResult> ValidateAccountAsync(Account account);
		Task<bool> HasAccountActivityAsync(int accountId);

		// ============= Setup & Maintenance =============
		Task SeedDefaultAccountsAsync();
		Task<bool> IsSystemInitializedAsync();
		Task<AccountingDashboardViewModel> GetAccountingDashboardAsync();

		// ============= MANUAL JOURNAL ENTRIES =============

		/// <summary>
		/// Creates a manual journal entry from the provided view model
		/// </summary>
		/// <param name="model">The manual journal entry view model</param>
		/// <returns>True if successful, false otherwise</returns>
		Task<bool> CreateManualJournalEntryAsync(ManualJournalEntryViewModel model);

		/// <summary>
		/// Retrieves all manual journal entries within the specified date range
		/// </summary>
		/// <param name="startDate">Start date for filtering (optional)</param>
		/// <param name="endDate">End date for filtering (optional)</param>
		/// <returns>List of manual journal entries</returns>
		Task<List<GeneralLedgerEntry>> GetManualJournalEntriesAsync(DateTime? startDate = null, DateTime? endDate = null);

		/// <summary>
		/// Reverses a manual journal entry by creating offsetting entries
		/// </summary>
		/// <param name="transactionNumber">The transaction number to reverse</param>
		/// <param name="reason">Reason for the reversal</param>
		/// <returns>True if successful, false otherwise</returns>
		Task<bool> ReverseManualJournalEntryAsync(string transactionNumber, string reason);

		/// <summary>
		/// Calculates the total amount of manual journal entries for a date range
		/// </summary>
		/// <param name="startDate">Start date</param>
		/// <param name="endDate">End date</param>
		/// <returns>Total debit amount of manual journal entries</returns>
		Task<decimal> GetManualJournalEntriesTotalAsync(DateTime startDate, DateTime endDate);

		/// <summary>
		/// Gets all ledger entries with enhanced reference information (actual sale numbers, etc.)
		/// </summary>
		/// <param name="startDate">Start date for filtering (optional)</param>
		/// <param name="endDate">End date for filtering (optional)</param>
		/// <returns>Ledger entries with enhanced reference display information</returns>
		Task<IEnumerable<GeneralLedgerEntry>> GetAllLedgerEntriesWithEnhancedReferencesAsync(DateTime? startDate = null, DateTime? endDate = null);

		/// <summary>
		/// Gets enhanced reference information for a journal entry
		/// </summary>
		/// <param name="referenceType">Type of reference (Sale, Purchase, etc.)</param>
		/// <param name="referenceId">ID of the referenced entity</param>
		/// <returns>Enhanced display text, URL, and icon information</returns>
		Task<(string displayText, string? url, string icon)> GetEnhancedReferenceInfoAsync(string? referenceType, int? referenceId);

		/// <summary>
		/// Gets revenue accounts suitable for ISellableEntity objects
		/// </summary>
		/// <returns>List of active revenue accounts ordered by account code</returns>
		Task<IEnumerable<Account>> GetRevenueAccountsForSellableEntitiesAsync();

		/// <summary>
		/// Gets the recommended revenue account for a sale based on its items
		/// </summary>
		/// <param name="sale">The sale to analyze</param>
		/// <returns>Recommended revenue account code</returns>
		Task<string> GetRecommendedRevenueAccountForSaleAsync(Sale sale);

		/// <summary>
		/// Validates that a revenue account code is valid and active
		/// </summary>
		/// <param name="accountCode">Account code to validate</param>
		/// <returns>True if valid and active, false otherwise</returns>
		Task<bool> IsValidRevenueAccountAsync(string? accountCode);

		// ============= YEAR-END CLOSING METHODS =============

		/// <summary>
		/// Performs year-end closing by transferring revenue and expense balances to Current Year Earnings,
		/// then transferring Current Year Earnings to Retained Earnings
		/// </summary>
		/// <param name="financialPeriod">The financial period to close</param>
		/// <param name="closingNotes">Notes about the closing process</param>
		/// <param name="createdBy">User performing the closing</param>
		/// <returns>True if successful, false otherwise</returns>
		Task<bool> PerformYearEndClosingAsync(FinancialPeriod financialPeriod, string closingNotes, string? createdBy = null);

		/// <summary>
		/// Creates year-end closing entries to transfer revenue accounts to Current Year Earnings
		/// </summary>
		/// <param name="financialPeriod">The financial period being closed</param>
		/// <param name="transactionNumber">The transaction number for the journal entry</param>
		/// <param name="createdBy">User performing the closing</param>
		/// <returns>List of created journal entries</returns>
		Task<List<GeneralLedgerEntry>> CreateRevenueClosingEntriesAsync(FinancialPeriod financialPeriod, string transactionNumber, string? createdBy = null);

		/// <summary>
		/// Creates year-end closing entries to transfer expense accounts to Current Year Earnings
		/// </summary>
		/// <param name="financialPeriod">The financial period being closed</param>
		/// <param name="transactionNumber">The transaction number for the journal entry</param>
		/// <param name="createdBy">User performing the closing</param>
		/// <returns>List of created journal entries</returns>
		Task<List<GeneralLedgerEntry>> CreateExpenseClosingEntriesAsync(FinancialPeriod financialPeriod, string transactionNumber, string? createdBy = null);

		/// <summary>
		/// Creates final year-end closing entry to transfer Current Year Earnings to Retained Earnings
		/// </summary>
		/// <param name="financialPeriod">The financial period being closed</param>
		/// <param name="transactionNumber">The transaction number for the journal entry</param>
		/// <param name="createdBy">User performing the closing</param>
		/// <returns>List of created journal entries</returns>
		Task<List<GeneralLedgerEntry>> CreateRetainedEarningsTransferAsync(FinancialPeriod financialPeriod, string transactionNumber, string? createdBy = null);

		/// <summary>
		/// Calculates the net income for a financial period (Revenue - Expenses)
		/// </summary>
		/// <param name="startDate">Period start date</param>
		/// <param name="endDate">Period end date</param>
		/// <returns>Net income amount</returns>
		Task<decimal> CalculateNetIncomeAsync(DateTime startDate, DateTime endDate);

		/// <summary>
		/// Validates that all accounts are ready for year-end closing
		/// </summary>
		/// <param name="financialPeriod">The financial period to validate</param>
		/// <returns>Validation result with any errors</returns>
		Task<YearEndValidationResult> ValidateYearEndClosingAsync(FinancialPeriod financialPeriod);

		/// <summary>
		/// Gets a summary of account balances that will be affected by year-end closing
		/// </summary>
		/// <param name="financialPeriod">The financial period being closed</param>
		/// <returns>Summary of closing balances</returns>
		Task<YearEndClosingSummary> GetYearEndClosingSummaryAsync(FinancialPeriod financialPeriod);

		// ============= ENHANCED CASH FLOW ANALYSIS =============

		/// <summary>
		/// Gets detailed cash flow analysis with trends and projections
		/// </summary>
		/// <param name="startDate">Period start date</param>
		/// <param name="endDate">Period end date</param>
		/// <param name="includePriorPeriod">Include prior period for comparison</param>
		/// <returns>Enhanced cash flow analysis</returns>
		Task<EnhancedCashFlowAnalysisViewModel> GetEnhancedCashFlowAnalysisAsync(DateTime startDate, DateTime endDate, bool includePriorPeriod = true);

		/// <summary>
		/// Gets cash flow projections based on historical patterns
		/// </summary>
		/// <param name="projectionMonths">Number of months to project forward</param>
		/// <returns>Cash flow projections</returns>
		Task<CashFlowProjectionViewModel> GetCashFlowProjectionsAsync(int projectionMonths = 12);

		/// <summary>
		/// Gets working capital analysis and changes
		/// </summary>
		/// <param name="startDate">Period start date</param>
		/// <param name="endDate">Period end date</param>
		/// <returns>Working capital analysis</returns>
		Task<WorkingCapitalAnalysisViewModel> GetWorkingCapitalAnalysisAsync(DateTime startDate, DateTime endDate);

		/// <summary>
		/// Gets cash flow by customer analysis for collections insight
		/// </summary>
		/// <param name="startDate">Period start date</param>
		/// <param name="endDate">Period end date</param>
		/// <returns>Customer cash flow analysis</returns>
		Task<CustomerCashFlowAnalysisViewModel> GetCustomerCashFlowAnalysisAsync(DateTime startDate, DateTime endDate);

		/// <summary>
		/// Gets free cash flow calculation and analysis
		/// </summary>
		/// <param name="startDate">Period start date</param>
		/// <param name="endDate">Period end date</param>
		/// <returns>Free cash flow analysis</returns>
		Task<FreeCashFlowAnalysisViewModel> GetFreeCashFlowAnalysisAsync(DateTime startDate, DateTime endDate);

		/// <summary>
		/// Gets monthly cash flow trend data for charting
		/// </summary>
		/// <param name="months">Number of months of historical data</param>
		/// <returns>Monthly cash flow trends</returns>
		Task<List<MonthlyCashFlowTrend>> GetMonthlyCashFlowTrendsAsync(int months = 12);
	}

	public class YearEndValidationResult
	{
		public bool IsValid { get; set; }
		public List<string> Errors { get; set; } = new();
		public List<string> Warnings { get; set; } = new();
		public bool TrialBalanceIsBalanced { get; set; }
		public decimal TotalDebits { get; set; }
		public decimal TotalCredits { get; set; }
		public decimal NetIncome { get; set; }
	}

	public class YearEndClosingSummary
	{
		public decimal TotalRevenue { get; set; }
		public decimal TotalExpenses { get; set; }
		public decimal NetIncome { get; set; }
		public decimal CurrentYearEarningsBalance { get; set; }
		public decimal RetainedEarningsBalanceBefore { get; set; }
		public decimal RetainedEarningsBalanceAfter { get; set; }
		public List<AccountClosingSummary> RevenueAccounts { get; set; } = new();
		public List<AccountClosingSummary> ExpenseAccounts { get; set; } = new();
	}

	public class AccountClosingSummary
	{
		public string AccountCode { get; set; } = string.Empty;
		public string AccountName { get; set; } = string.Empty;
		public decimal Balance { get; set; }
		public string FormattedBalance => Balance.ToString("C");
	}
	public class AccountValidationResult
	{
		public bool IsValid { get; set; }
		public List<string> Errors { get; set; } = new();

		public static AccountValidationResult Success() => new() { IsValid = true };
		public static AccountValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
	}
}