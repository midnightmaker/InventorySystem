// Services/AccountingService.ChartOfAccounts.cs
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels.Accounting;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public partial class AccountingService
	{
		// ============= Chart of Accounts Management =============

		public async Task<IEnumerable<Account>> GetAllAccountsAsync()
		{
			return await _context.Accounts
				.Include(a => a.ParentAccount)
				.OrderBy(a => a.AccountCode)
				.ToListAsync();
		}

		public async Task<IEnumerable<Account>> GetActiveAccountsAsync()
		{
			return await _context.Accounts
				.Where(a => a.IsActive)
				.Include(a => a.ParentAccount)
				.OrderBy(a => a.AccountCode)
				.ToListAsync();
		}

		public async Task<Account?> GetAccountByIdAsync(int accountId)
		{
			return await _context.Accounts
				.Include(a => a.ParentAccount)
				.Include(a => a.LedgerEntries)
				.FirstOrDefaultAsync(a => a.Id == accountId);
		}

		public async Task<Account?> GetAccountByCodeAsync(string accountCode)
		{
			return await _context.Accounts
				.Include(a => a.ParentAccount)
				.FirstOrDefaultAsync(a => a.AccountCode == accountCode);
		}

		public async Task<Account> CreateAccountAsync(Account account)
		{
			var validation = await ValidateAccountAsync(account);
			if (!validation.IsValid)
				throw new InvalidOperationException($"Account validation failed: {string.Join(", ", validation.Errors)}");

			account.CreatedDate = DateTime.Now;
			_context.Accounts.Add(account);
			await _context.SaveChangesAsync();

			_logger.LogInformation("Created account {AccountCode} - {AccountName}", account.AccountCode, account.AccountName);
			return account;
		}

		public async Task<Account> UpdateAccountAsync(Account account)
		{
			var existingAccount = await GetAccountByIdAsync(account.Id);
			if (existingAccount == null)
				throw new InvalidOperationException($"Account with ID {account.Id} not found");

			if (existingAccount.IsSystemAccount &&
				(account.AccountCode != existingAccount.AccountCode || account.AccountType != existingAccount.AccountType))
			{
				throw new InvalidOperationException("Cannot modify account code or type for system accounts");
			}

			var validation = await ValidateAccountAsync(account);
			if (!validation.IsValid)
				throw new InvalidOperationException($"Account validation failed: {string.Join(", ", validation.Errors)}");

			_context.Entry(existingAccount).CurrentValues.SetValues(account);
			await _context.SaveChangesAsync();

			_logger.LogInformation("Updated account {AccountCode} - {AccountName}", account.AccountCode, account.AccountName);
			return existingAccount;
		}

		public async Task<bool> CanDeleteAccountAsync(int accountId)
		{
			var account = await GetAccountByIdAsync(accountId);
			if (account == null) return false;
			if (account.IsSystemAccount) return false;
			return !await HasAccountActivityAsync(accountId);
		}

		public async Task DeleteAccountAsync(int accountId)
		{
			if (!await CanDeleteAccountAsync(accountId))
				throw new InvalidOperationException("Cannot delete this account - it's a system account or has activity");

			var account = await GetAccountByIdAsync(accountId);
			if (account != null)
			{
				_context.Accounts.Remove(account);
				await _context.SaveChangesAsync();
				_logger.LogInformation("Deleted account {AccountCode} - {AccountName}", account.AccountCode, account.AccountName);
			}
		}

		public async Task<IEnumerable<Account>> GetAccountsByTypeAsync(AccountType accountType)
		{
			return await _context.Accounts
				.Where(a => a.AccountType == accountType && a.IsActive)
				.OrderBy(a => a.AccountCode)
				.ToListAsync();
		}

		public async Task<IEnumerable<Account>> SearchAccountsAsync(string searchTerm)
		{
			if (string.IsNullOrWhiteSpace(searchTerm))
				return await GetActiveAccountsAsync();

			var lowerSearchTerm = searchTerm.ToLower();
			return await _context.Accounts
				.Where(a => a.IsActive &&
					(a.AccountCode.ToLower().Contains(lowerSearchTerm) ||
					 a.AccountName.ToLower().Contains(lowerSearchTerm) ||
					 (a.Description != null && a.Description.ToLower().Contains(lowerSearchTerm))))
				.OrderBy(a => a.AccountCode)
				.ToListAsync();
		}

		public async Task<IEnumerable<Account>> GetExpenseAccountsAsync()
		{
			return await _context.Accounts
				.Where(a => a.AccountType == AccountType.Expense && a.IsActive)
				.OrderBy(a => a.AccountCode)
				.ToListAsync();
		}

		public async Task<Account?> GetSuggestedAccountForExpenseCategoryAsync(ExpenseCategory category)
		{
			var suggestedCode = Expense.GetSuggestedAccountCodeForCategory(category);
			return await GetAccountByCodeAsync(suggestedCode);
		}

		public async Task<IEnumerable<Account>> GetRevenueAccountsForSellableEntitiesAsync()
		{
			return await _context.Accounts
				.Where(a => a.AccountType == AccountType.Revenue &&
							a.IsActive &&
							(a.AccountCode.StartsWith("40") || a.AccountCode.StartsWith("41")))
				.OrderBy(a => a.AccountCode)
				.ToListAsync();
		}

		public async Task<bool> IsValidRevenueAccountAsync(string? accountCode)
		{
			if (string.IsNullOrEmpty(accountCode)) return false;

			var account = await GetAccountByCodeAsync(accountCode);
			return account != null && account.IsActive && account.AccountType == AccountType.Revenue;
		}

		// ============= Validation =============

		public async Task<bool> DoesAccountExistAsync(string accountCode)
		{
			return await _context.Accounts.AnyAsync(a => a.AccountCode == accountCode);
		}

		public async Task<bool> IsAccountCodeUniqueAsync(string accountCode, int? excludeId = null)
		{
			var query = _context.Accounts.Where(a => a.AccountCode == accountCode);
			if (excludeId.HasValue)
				query = query.Where(a => a.Id != excludeId.Value);
			return !await query.AnyAsync();
		}

		public async Task<AccountValidationResult> ValidateAccountAsync(Account account)
		{
			var errors = new List<string>();

			if (string.IsNullOrWhiteSpace(account.AccountCode))
				errors.Add("Account code is required");
			else if (!await IsAccountCodeUniqueAsync(account.AccountCode, account.Id))
				errors.Add("Account code must be unique");

			if (string.IsNullOrWhiteSpace(account.AccountName))
				errors.Add("Account name is required");

			return errors.Any()
				? AccountValidationResult.Failure(errors.ToArray())
				: AccountValidationResult.Success();
		}

		public async Task<bool> HasAccountActivityAsync(int accountId)
		{
			return await _context.GeneralLedgerEntries.AnyAsync(e => e.AccountId == accountId);
		}

		// ============= Setup & Maintenance =============

		public async Task SeedDefaultAccountsAsync()
		{
			if (await _context.Accounts.AnyAsync()) return;

			var defaultAccounts = DefaultChartOfAccounts.GetDefaultAccounts();
			_context.Accounts.AddRange(defaultAccounts);
			await _context.SaveChangesAsync();

			_logger.LogInformation("Seeded {Count} default accounts", defaultAccounts.Count);
		}

		public async Task<bool> IsSystemInitializedAsync()
		{
			return await _context.Accounts.AnyAsync();
		}
	}
}
