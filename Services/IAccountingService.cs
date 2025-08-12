// Services/IAccountingService.cs
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
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
	}

	public class AccountValidationResult
	{
		public bool IsValid { get; set; }
		public List<string> Errors { get; set; } = new();

		public static AccountValidationResult Success() => new() { IsValid = true };
		public static AccountValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
	}
}