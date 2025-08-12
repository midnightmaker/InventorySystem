// Services/AccountingService.cs
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels.Accounting;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public class AccountingService : IAccountingService
	{
		private readonly InventoryContext _context;
		private readonly ILogger<AccountingService> _logger;

		public AccountingService(InventoryContext context, ILogger<AccountingService> logger)
		{
			_context = context;
			_logger = logger;
		}

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
			{
				throw new InvalidOperationException($"Account validation failed: {string.Join(", ", validation.Errors)}");
			}

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
			{
				throw new InvalidOperationException($"Account with ID {account.Id} not found");
			}

			if (existingAccount.IsSystemAccount && (account.AccountCode != existingAccount.AccountCode || account.AccountType != existingAccount.AccountType))
			{
				throw new InvalidOperationException("Cannot modify account code or type for system accounts");
			}

			var validation = await ValidateAccountAsync(account);
			if (!validation.IsValid)
			{
				throw new InvalidOperationException($"Account validation failed: {string.Join(", ", validation.Errors)}");
			}

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
			{
				throw new InvalidOperationException("Cannot delete this account - it's a system account or has activity");
			}

			var account = await GetAccountByIdAsync(accountId);
			if (account != null)
			{
				_context.Accounts.Remove(account);
				await _context.SaveChangesAsync();
				_logger.LogInformation("Deleted account {AccountCode} - {AccountName}", account.AccountCode, account.AccountName);
			}
		}

		// ============= General Ledger Management =============

		public async Task<GeneralLedgerEntry> CreateJournalEntryAsync(GeneralLedgerEntry entry)
		{
			entry.CreatedDate = DateTime.Now;
			_context.GeneralLedgerEntries.Add(entry);
			await _context.SaveChangesAsync();

			// Update account balance
			await UpdateAccountBalanceFromEntryAsync(entry);

			return entry;
		}

		public async Task<IEnumerable<GeneralLedgerEntry>> CreateJournalEntriesAsync(IEnumerable<GeneralLedgerEntry> entries)
		{
			var entryList = entries.ToList();

			// Validate that debits equal credits
			if (!await IsValidJournalEntryAsync(entryList))
			{
				throw new InvalidOperationException("Journal entry is not balanced - debits must equal credits");
			}

			foreach (var entry in entryList)
			{
				entry.CreatedDate = DateTime.Now;
				_context.GeneralLedgerEntries.Add(entry);
			}

			await _context.SaveChangesAsync();

			// Update account balances
			foreach (var entry in entryList)
			{
				await UpdateAccountBalanceFromEntryAsync(entry);
			}

			_logger.LogInformation("Created journal entry {TransactionNumber} with {EntryCount} entries",
					entryList.First().TransactionNumber, entryList.Count);

			return entryList;
		}

		public async Task<string> GenerateNextJournalNumberAsync(string prefix = "JE")
		{
			var today = DateTime.Now;
			var datePrefix = $"{prefix}-{today:yyyyMMdd}";

			var lastEntry = await _context.GeneralLedgerEntries
					.Where(e => e.TransactionNumber.StartsWith(datePrefix))
					.OrderByDescending(e => e.TransactionNumber)
					.FirstOrDefaultAsync();

			if (lastEntry == null)
			{
				return $"{datePrefix}-001";
			}

			var lastNumber = lastEntry.TransactionNumber.Split('-').LastOrDefault();
			if (int.TryParse(lastNumber, out var number))
			{
				return $"{datePrefix}-{(number + 1):D3}";
			}

			return $"{datePrefix}-001";
		}

		// ============= Automatic Journal Entry Generation =============

		public async Task<bool> GenerateJournalEntriesForPurchaseAsync(Purchase purchase)
		{
			try
			{
				if (purchase.IsJournalEntryGenerated) return true;

				var journalNumber = await GenerateNextJournalNumberAsync("JE-PUR");
				var entries = new List<GeneralLedgerEntry>();

				// Determine account based on item type and material type
				var accountCode = purchase.Item?.ItemType.GetDefaultPurchaseAccountCode(purchase.Item?.MaterialType) ?? "6000";
				var account = await GetAccountByCodeAsync(accountCode);
				if (account == null)
				{
					_logger.LogError("Account {AccountCode} not found for purchase {PurchaseId}", accountCode, purchase.Id);
					return false;
				}

				// Debit: Inventory/Expense Account
				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = purchase.PurchaseDate,
					TransactionNumber = journalNumber,
					AccountId = account.Id,
					Description = $"Purchase: {purchase.Item?.Description ?? "Unknown Item"}",
					DebitAmount = purchase.TotalCost,
					CreditAmount = 0,
					ReferenceType = "Purchase",
					ReferenceId = purchase.Id
				});

				// Credit: Accounts Payable
				var apAccount = await GetAccountByCodeAsync("2000");
				if (apAccount == null)
				{
					_logger.LogError("Accounts Payable account (2000) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = purchase.PurchaseDate,
					TransactionNumber = journalNumber,
					AccountId = apAccount.Id,
					Description = $"Purchase: {purchase.Vendor?.CompanyName ?? "Unknown Vendor"}",
					DebitAmount = 0,
					CreditAmount = purchase.TotalCost,
					ReferenceType = "Purchase",
					ReferenceId = purchase.Id
				});

				await CreateJournalEntriesAsync(entries);

				// Update purchase to mark journal entry as generated
				purchase.JournalEntryNumber = journalNumber;
				purchase.IsJournalEntryGenerated = true;
				purchase.AccountCode = accountCode;

				await _context.SaveChangesAsync();
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for purchase {PurchaseId}", purchase.Id);
				return false;
			}
		}

		public async Task<bool> GenerateJournalEntriesForSaleAsync(Sale sale)
		{
			try
			{
				if (sale.IsJournalEntryGenerated) return true;

				var journalNumber = await GenerateNextJournalNumberAsync("JE-SAL");
				var entries = new List<GeneralLedgerEntry>();

				// Debit: Accounts Receivable
				var arAccount = await GetAccountByCodeAsync("1100");
				if (arAccount == null)
				{
					_logger.LogError("Accounts Receivable account (1100) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = sale.SaleDate,
					TransactionNumber = journalNumber,
					AccountId = arAccount.Id,
					Description = $"Sale: {sale.Customer?.CustomerName ?? "Unknown Customer"}",
					DebitAmount = sale.TotalAmount,
					CreditAmount = 0,
					ReferenceType = "Sale",
					ReferenceId = sale.Id
				});

				// Credit: Sales Revenue
				var revenueAccount = await GetAccountByCodeAsync(sale.RevenueAccountCode ?? "4000");
				if (revenueAccount == null)
				{
					_logger.LogError("Revenue account {AccountCode} not found", sale.RevenueAccountCode ?? "4000");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = sale.SaleDate,
					TransactionNumber = journalNumber,
					AccountId = revenueAccount.Id,
					Description = $"Sale: {sale.SaleNumber}",
					DebitAmount = 0,
					CreditAmount = sale.TotalAmount,
					ReferenceType = "Sale",
					ReferenceId = sale.Id
				});

				// Record COGS if there are sale items with cost
				if (sale.SaleItems?.Any() == true)
				{
					var totalCogs = sale.SaleItems.Sum(si => si.UnitCost * si.QuantitySold);

					if (totalCogs > 0)
					{
						var cogsAccount = await GetAccountByCodeAsync("5000");
						var inventoryAccount = await GetAccountByCodeAsync("1220");

						if (cogsAccount != null && inventoryAccount != null)
						{
							// Debit: Cost of Goods Sold
							entries.Add(new GeneralLedgerEntry
							{
								TransactionDate = sale.SaleDate,
								TransactionNumber = journalNumber,
								AccountId = cogsAccount.Id,
								Description = $"COGS for Sale: {sale.SaleNumber}",
								DebitAmount = totalCogs,
								CreditAmount = 0,
								ReferenceType = "Sale",
								ReferenceId = sale.Id
							});

							// Credit: Inventory
							entries.Add(new GeneralLedgerEntry
							{
								TransactionDate = sale.SaleDate,
								TransactionNumber = journalNumber,
								AccountId = inventoryAccount.Id,
								Description = $"Inventory reduction for Sale: {sale.SaleNumber}",
								DebitAmount = 0,
								CreditAmount = totalCogs,
								ReferenceType = "Sale",
								ReferenceId = sale.Id
							});
						}
					}
				}

				await CreateJournalEntriesAsync(entries);

				sale.JournalEntryNumber = journalNumber;
				sale.IsJournalEntryGenerated = true;

				await _context.SaveChangesAsync();
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for sale {SaleId}", sale.Id);
				return false;
			}
		}

		public async Task<bool> GenerateJournalEntriesForProductionAsync(Production production)
		{
			// Implementation for production journal entries
			// This would involve moving costs from raw materials to WIP to finished goods
			await Task.CompletedTask;
			return true;
		}

		public async Task<bool> GenerateJournalEntriesForVendorPaymentAsync(VendorPayment payment)
		{
			try
			{
				var journalNumber = await GenerateNextJournalNumberAsync("JE-PAY");
				var entries = new List<GeneralLedgerEntry>();

				// Debit: Accounts Payable
				var apAccount = await GetAccountByCodeAsync("2000");
				if (apAccount == null)
				{
					_logger.LogError("Accounts Payable account (2000) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = payment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId = apAccount.Id,
					Description = $"Payment to {payment.AccountsPayable.Vendor.CompanyName}",
					DebitAmount = payment.PaymentAmount,
					CreditAmount = 0,
					ReferenceType = "VendorPayment",
					ReferenceId = payment.Id
				});

				// Credit: Cash
				var cashAccount = await GetAccountByCodeAsync("1000");
				if (cashAccount == null)
				{
					_logger.LogError("Cash account (1000) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = payment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId = cashAccount.Id,
					Description = $"Payment to {payment.AccountsPayable.Vendor.CompanyName} - {payment.GetPaymentReference()}",
					DebitAmount = 0,
					CreditAmount = payment.PaymentAmount,
					ReferenceType = "VendorPayment",
					ReferenceId = payment.Id
				});

				await CreateJournalEntriesAsync(entries);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for vendor payment {PaymentId}", payment.Id);
				return false;
			}
		}

		// ============= Account Balances =============

		public async Task<decimal> GetAccountBalanceAsync(string accountCode, DateTime? asOfDate = null)
		{
			var account = await GetAccountByCodeAsync(accountCode);
			if (account == null) return 0;

			return await GetAccountBalanceAsync(account.Id, asOfDate);
		}

		public async Task<decimal> GetAccountBalanceAsync(int accountId, DateTime? asOfDate = null)
		{
			var query = _context.GeneralLedgerEntries
					.Where(e => e.AccountId == accountId);

			if (asOfDate.HasValue)
			{
				query = query.Where(e => e.TransactionDate <= asOfDate.Value);
			}

			var entries = await query.ToListAsync();
			var totalDebits = entries.Sum(e => e.DebitAmount);
			var totalCredits = entries.Sum(e => e.CreditAmount);

			var account = await GetAccountByIdAsync(accountId);
			if (account != null && account.IsDebitAccount)
			{
				return totalDebits - totalCredits;
			}
			else
			{
				return totalCredits - totalDebits;
			}
		}

		public async Task UpdateAccountBalanceAsync(string accountCode, decimal amount, bool isDebit = true)
		{
			var account = await GetAccountByCodeAsync(accountCode);
			if (account == null) return;

			if (account.IsDebitAccount)
			{
				account.CurrentBalance += isDebit ? amount : -amount;
			}
			else
			{
				account.CurrentBalance += isDebit ? -amount : amount;
			}

			account.LastTransactionDate = DateTime.Now;
			await _context.SaveChangesAsync();
		}

		private async Task UpdateAccountBalanceFromEntryAsync(GeneralLedgerEntry entry)
		{
			var account = await GetAccountByIdAsync(entry.AccountId);
			if (account == null) return;

			if (account.IsDebitAccount)
			{
				account.CurrentBalance += entry.DebitAmount - entry.CreditAmount;
			}
			else
			{
				account.CurrentBalance += entry.CreditAmount - entry.DebitAmount;
			}

			account.LastTransactionDate = entry.TransactionDate;
			await _context.SaveChangesAsync();
		}

		// ============= Validation & Utilities =============

		public async Task<bool> IsValidJournalEntryAsync(IEnumerable<GeneralLedgerEntry> entries)
		{
			var entryList = entries.ToList();
			if (!entryList.Any()) return false;

			var totalDebits = entryList.Sum(e => e.DebitAmount);
			var totalCredits = entryList.Sum(e => e.CreditAmount);

			return Math.Abs(totalDebits - totalCredits) < 0.01m; // Allow for rounding differences
		}

		public async Task<bool> DoesAccountExistAsync(string accountCode)
		{
			return await _context.Accounts.AnyAsync(a => a.AccountCode == accountCode);
		}

		public async Task<bool> IsAccountCodeUniqueAsync(string accountCode, int? excludeId = null)
		{
			var query = _context.Accounts.Where(a => a.AccountCode == accountCode);
			if (excludeId.HasValue)
			{
				query = query.Where(a => a.Id != excludeId.Value);
			}
			return !await query.AnyAsync();
		}

		public async Task<AccountValidationResult> ValidateAccountAsync(Account account)
		{
			var errors = new List<string>();

			if (string.IsNullOrWhiteSpace(account.AccountCode))
			{
				errors.Add("Account code is required");
			}
			else if (!await IsAccountCodeUniqueAsync(account.AccountCode, account.Id))
			{
				errors.Add("Account code must be unique");
			}

			if (string.IsNullOrWhiteSpace(account.AccountName))
			{
				errors.Add("Account name is required");
			}

			return errors.Any() ? AccountValidationResult.Failure(errors.ToArray()) : AccountValidationResult.Success();
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

		// Additional helper methods would be implemented here...
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

		public async Task<IEnumerable<GeneralLedgerEntry>> GetAccountLedgerEntriesAsync(string accountCode, DateTime? startDate = null, DateTime? endDate = null)
		{
			var account = await GetAccountByCodeAsync(accountCode);
			if (account == null)
				return new List<GeneralLedgerEntry>();

			var query = _context.GeneralLedgerEntries
					.Include(e => e.Account)
					.Where(e => e.AccountId == account.Id);

			if (startDate.HasValue)
				query = query.Where(e => e.TransactionDate >= startDate.Value);

			if (endDate.HasValue)
				query = query.Where(e => e.TransactionDate <= endDate.Value);

			return await query
					.OrderByDescending(e => e.TransactionDate)
					.ThenBy(e => e.TransactionNumber)
					.ToListAsync();
		}

		public async Task<IEnumerable<GeneralLedgerEntry>> GetAllLedgerEntriesAsync(DateTime? startDate = null, DateTime? endDate = null)
		{
			IQueryable<GeneralLedgerEntry> query = _context.GeneralLedgerEntries
					.Include(e => e.Account);

			if (startDate.HasValue)
				query = query.Where(e => e.TransactionDate >= startDate.Value);

			if (endDate.HasValue)
				query = query.Where(e => e.TransactionDate <= endDate.Value);

			return await query
					.OrderByDescending(e => e.TransactionDate)
					.ThenBy(e => e.TransactionNumber)
					.ToListAsync();
		}

		public async Task RecalculateAccountBalanceAsync(string accountCode)
		{
			var account = await GetAccountByCodeAsync(accountCode);
			if (account == null) return;

			var entries = await _context.GeneralLedgerEntries
					.Where(e => e.AccountId == account.Id)
					.ToListAsync();

			var totalDebits = entries.Sum(e => e.DebitAmount);
			var totalCredits = entries.Sum(e => e.CreditAmount);

			if (account.IsDebitAccount)
			{
				account.CurrentBalance = totalDebits - totalCredits;
			}
			else
			{
				account.CurrentBalance = totalCredits - totalDebits;
			}

			account.LastTransactionDate = entries.Any() ? entries.Max(e => e.TransactionDate) : DateTime.Now;
			await _context.SaveChangesAsync();
		}

		public async Task RecalculateAllAccountBalancesAsync()
		{
			var accounts = await GetAllAccountsAsync();
			foreach (var account in accounts)
			{
				await RecalculateAccountBalanceAsync(account.AccountCode);
			}
		}

		public async Task<TrialBalanceViewModel> GetTrialBalanceAsync(DateTime asOfDate)
		{
			var accounts = await GetActiveAccountsAsync();
			var entries = new List<TrialBalanceEntry>();

			foreach (var account in accounts)
			{
				var balance = await GetAccountBalanceAsync(account.Id, asOfDate);
				if (balance != 0) // Only include accounts with balances
				{
					var entry = new TrialBalanceEntry
					{
						AccountCode = account.AccountCode,
						AccountName = account.AccountName,
						AccountType = account.AccountType
					};

					if (account.IsDebitAccount)
					{
						entry.DebitBalance = Math.Max(0, balance);
						entry.CreditBalance = Math.Max(0, -balance);
					}
					else
					{
						entry.CreditBalance = Math.Max(0, balance);
						entry.DebitBalance = Math.Max(0, -balance);
					}

					entries.Add(entry);
				}
			}

			return new TrialBalanceViewModel
			{
				AsOfDate = asOfDate,
				Entries = entries
			};
		}

		public async Task<BalanceSheetViewModel> GetBalanceSheetAsync(DateTime asOfDate)
		{
			var accounts = await GetActiveAccountsAsync();
			var balanceSheet = new BalanceSheetViewModel
			{
				AsOfDate = asOfDate
			};

			foreach (var account in accounts)
			{
				var balance = await GetAccountBalanceAsync(account.Id, asOfDate);
				if (balance == 0) continue; // Skip accounts with zero balance

				var balanceSheetAccount = new BalanceSheetAccount
				{
					AccountCode = account.AccountCode,
					AccountName = account.AccountName,
					Balance = balance
				};

				switch (account.AccountType)
				{
					case AccountType.Asset:
						if (account.AccountSubType == AccountSubType.CurrentAsset ||
								account.AccountSubType == AccountSubType.InventoryAsset)
						{
							balanceSheet.CurrentAssets.Add(balanceSheetAccount);
						}
						else if (account.AccountSubType == AccountSubType.FixedAsset)
						{
							balanceSheet.FixedAssets.Add(balanceSheetAccount);
						}
						else
						{
							balanceSheet.OtherAssets.Add(balanceSheetAccount);
						}
						break;
					case AccountType.Liability:
						if (account.AccountSubType == AccountSubType.CurrentLiability)
						{
							balanceSheet.CurrentLiabilities.Add(balanceSheetAccount);
						}
						else
						{
							balanceSheet.LongTermLiabilities.Add(balanceSheetAccount);
						}
						break;
					case AccountType.Equity:
						balanceSheet.EquityAccounts.Add(balanceSheetAccount);
						break;
				}
			}

			return balanceSheet;
		}

		public async Task<IncomeStatementViewModel> GetIncomeStatementAsync(DateTime startDate, DateTime endDate)
		{
			var accounts = await GetActiveAccountsAsync();
			var incomeStatement = new IncomeStatementViewModel
			{
				StartDate = startDate,
				EndDate = endDate
			};

			foreach (var account in accounts)
			{
				// Get account activity for the period
				var entries = await _context.GeneralLedgerEntries
						.Where(e => e.AccountId == account.Id &&
											 e.TransactionDate >= startDate &&
											 e.TransactionDate <= endDate)
						.ToListAsync();

				if (!entries.Any()) continue;

				var totalDebits = entries.Sum(e => e.DebitAmount);
				var totalCredits = entries.Sum(e => e.CreditAmount);
				var netAmount = account.AccountType == AccountType.Revenue ? totalCredits - totalDebits : totalDebits - totalCredits;

				if (netAmount == 0) continue;

				var incomeStatementAccount = new IncomeStatementAccount
				{
					AccountCode = account.AccountCode,
					AccountName = account.AccountName,
					Amount = Math.Abs(netAmount)
				};

				switch (account.AccountType)
				{
					case AccountType.Revenue:
						incomeStatement.RevenueAccounts.Add(incomeStatementAccount);
						break;
					case AccountType.Expense:
						if (account.AccountSubType == AccountSubType.CostOfGoodsSold)
						{
							incomeStatement.COGSAccounts.Add(incomeStatementAccount);
						}
						else if (account.AccountSubType == AccountSubType.OperatingExpense ||
										account.AccountSubType == AccountSubType.UtilityExpense ||
										account.AccountSubType == AccountSubType.SubscriptionExpense)
						{
							incomeStatement.OperatingExpenses.Add(incomeStatementAccount);
						}
						else
						{
							incomeStatement.OtherExpenses.Add(incomeStatementAccount);
						}
						break;
				}
			}

			return incomeStatement;
		}

		public async Task<CashFlowStatementViewModel> GetCashFlowStatementAsync(DateTime startDate, DateTime endDate)
		{
			// This is a complex implementation that would analyze cash movements
			// For now, return a basic structure
			return new CashFlowStatementViewModel
			{
				StartDate = startDate,
				EndDate = endDate,
				NetIncome = 0,
				BeginningCashBalance = await GetAccountBalanceAsync("1000", startDate.AddDays(-1))
			};
		}

		public async Task<IEnumerable<AccountsPayable>> GetAllAccountsPayableAsync()
		{
			return await _context.AccountsPayable
					.Include(ap => ap.Vendor)
					.Include(ap => ap.Purchase)
					.Include(ap => ap.Payments)
					.OrderBy(ap => ap.DueDate)
					.ToListAsync();
		}

		public async Task<IEnumerable<AccountsPayable>> GetUnpaidAccountsPayableAsync()
		{
			return await _context.AccountsPayable
					.Include(ap => ap.Vendor)
					.Include(ap => ap.Purchase)
					.Include(ap => ap.Payments)
					.Where(ap => ap.PaymentStatus != PaymentStatus.Paid)
					.OrderBy(ap => ap.DueDate)
					.ToListAsync();
		}

		public async Task<IEnumerable<AccountsPayable>> GetOverdueAccountsPayableAsync()
		{
			var today = DateTime.Today;
			return await _context.AccountsPayable
					.Include(ap => ap.Vendor)
					.Include(ap => ap.Purchase)
					.Include(ap => ap.Payments)
					.Where(ap => ap.PaymentStatus != PaymentStatus.Paid && ap.DueDate < today)
					.OrderBy(ap => ap.DueDate)
					.ToListAsync();
		}

		public async Task<AccountsPayable?> GetAccountsPayableByIdAsync(int id)
		{
			return await _context.AccountsPayable
					.Include(ap => ap.Vendor)
					.Include(ap => ap.Purchase)
					.Include(ap => ap.Payments)
					.FirstOrDefaultAsync(ap => ap.Id == id);
		}

		public async Task<AccountsPayable> CreateAccountsPayableAsync(AccountsPayable ap)
		{
			ap.CreatedDate = DateTime.Now;
			_context.AccountsPayable.Add(ap);
			await _context.SaveChangesAsync();
			return ap;
		}

		public async Task<AccountsPayable> UpdateAccountsPayableAsync(AccountsPayable ap)
		{
			ap.LastModifiedDate = DateTime.Now;
			_context.AccountsPayable.Update(ap);
			await _context.SaveChangesAsync();
			return ap;
		}

		public async Task<decimal> GetTotalAccountsPayableAsync()
		{
			return await _context.AccountsPayable
					.Where(ap => ap.PaymentStatus != PaymentStatus.Paid)
					.SumAsync(ap => ap.BalanceRemaining);
		}

		public async Task<decimal> GetTotalOverdueAccountsPayableAsync()
		{
			var today = DateTime.Today;
			return await _context.AccountsPayable
					.Where(ap => ap.PaymentStatus != PaymentStatus.Paid && ap.DueDate < today)
					.SumAsync(ap => ap.BalanceRemaining);
		}

		public async Task<VendorPayment> CreateVendorPaymentAsync(VendorPayment payment)
		{
			payment.CreatedDate = DateTime.Now;
			_context.VendorPayments.Add(payment);

			// Update the associated accounts payable
			var accountsPayable = await GetAccountsPayableByIdAsync(payment.AccountsPayableId);
			if (accountsPayable != null)
			{
				accountsPayable.AmountPaid += payment.PaymentAmount;
				accountsPayable.UpdatePaymentStatus();
				await UpdateAccountsPayableAsync(accountsPayable);
			}

			await _context.SaveChangesAsync();

			// Generate journal entry for the payment
			await GenerateJournalEntriesForVendorPaymentAsync(payment);

			return payment;
		}

		public async Task<IEnumerable<VendorPayment>> GetVendorPaymentsAsync(int vendorId)
		{
			return await _context.VendorPayments
					.Include(vp => vp.AccountsPayable)
					.Where(vp => vp.AccountsPayable.VendorId == vendorId)
					.OrderByDescending(vp => vp.PaymentDate)
					.ToListAsync();
		}

		public async Task<IEnumerable<VendorPayment>> GetAccountsPayablePaymentsAsync(int accountsPayableId)
		{
			return await _context.VendorPayments
					.Where(vp => vp.AccountsPayableId == accountsPayableId)
					.OrderByDescending(vp => vp.PaymentDate)
					.ToListAsync();
		}

		public async Task<AccountingDashboardViewModel> GetAccountingDashboardAsync()
		{
			var dashboard = new AccountingDashboardViewModel
			{
				CashBalance = await GetAccountBalanceAsync("1000"),
				AccountsReceivableBalance = await GetAccountBalanceAsync("1100"),
				AccountsPayableBalance = await GetAccountBalanceAsync("2000"),
				TotalAccounts = await _context.Accounts.CountAsync(),
				ActiveAccounts = await _context.Accounts.CountAsync(a => a.IsActive)
			};

			// Get recent transactions
			dashboard.RecentTransactions = (await GetAllLedgerEntriesAsync(DateTime.Today.AddDays(-30)))
					.Take(10)
					.ToList();

			// Get upcoming payments
			dashboard.UpcomingPayments = (await GetUnpaidAccountsPayableAsync())
					.Where(ap => ap.DueDate <= DateTime.Today.AddDays(30))
					.Take(10)
					.ToList();

			// Calculate totals
			var accounts = await GetActiveAccountsAsync();
			foreach (var account in accounts)
			{
				var balance = await GetAccountBalanceAsync(account.Id);
				switch (account.AccountType)
				{
					case AccountType.Asset:
						dashboard.TotalAssets += balance;
						if (account.AccountCode.StartsWith("12")) // Inventory accounts
							dashboard.InventoryValue += balance;
						break;
					case AccountType.Liability:
						dashboard.TotalLiabilities += balance;
						break;
					case AccountType.Equity:
						dashboard.OwnerEquity += balance;
						break;
				}
			}

			// Get monthly revenue and expenses
			var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
			var incomeStatement = await GetIncomeStatementAsync(startOfMonth, DateTime.Today);
			dashboard.MonthlyRevenue = incomeStatement.TotalRevenue;
			dashboard.MonthlyExpenses = incomeStatement.TotalOperatingExpenses + incomeStatement.TotalOtherExpenses;
			dashboard.NetIncome = dashboard.MonthlyRevenue - dashboard.MonthlyExpenses;

			return dashboard;
		}

		public async Task<bool> GenerateJournalEntriesForCustomerPaymentAsync(CustomerPayment payment)
		{
			try
			{
				if (payment.IsJournalEntryGenerated) return true;

				var journalNumber = await GenerateNextJournalNumberAsync("JE-PMT");
				var entries = new List<GeneralLedgerEntry>();

				// Get the sale for context
				var sale = await _context.Sales
						.Include(s => s.Customer)
						.FirstOrDefaultAsync(s => s.Id == payment.SaleId);

				if (sale == null)
				{
					_logger.LogError("Sale {SaleId} not found for payment {PaymentId}", payment.SaleId, payment.Id);
					return false;
				}

				// Determine cash account based on payment method
				var cashAccountCode = GetCashAccountCodeByPaymentMethod(payment.PaymentMethod);
				var cashAccount = await GetAccountByCodeAsync(cashAccountCode);

				if (cashAccount == null)
				{
					_logger.LogError("Cash account {AccountCode} not found for payment method {PaymentMethod}",
							cashAccountCode, payment.PaymentMethod);
					return false;
				}

				// Debit: Cash/Bank Account
				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = payment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId = cashAccount.Id,
					Description = $"Customer payment: {sale.Customer?.CustomerName ?? "Unknown Customer"} - {sale.SaleNumber}",
					DebitAmount = payment.Amount,
					CreditAmount = 0,
					ReferenceType = "CustomerPayment",
					ReferenceId = payment.Id
				});

				// Credit: Accounts Receivable
				var arAccount = await GetAccountByCodeAsync("1100");
				if (arAccount == null)
				{
					_logger.LogError("Accounts Receivable account (1100) not found");
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = payment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId = arAccount.Id,
					Description = $"Payment received: {sale.Customer?.CustomerName ?? "Unknown Customer"} - {sale.SaleNumber}",
					DebitAmount = 0,
					CreditAmount = payment.Amount,
					ReferenceType = "CustomerPayment",
					ReferenceId = payment.Id
				});

				await CreateJournalEntriesAsync(entries);

				// Update payment to mark journal entry as generated
				payment.JournalEntryNumber = journalNumber;
				payment.IsJournalEntryGenerated = true;

				await _context.SaveChangesAsync();

				_logger.LogInformation("Generated journal entry {JournalNumber} for customer payment {PaymentId}",
						journalNumber, payment.Id);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for customer payment {PaymentId}", payment.Id);
				return false;
			}
		}

		// Helper method to determine cash account based on payment method
		private string GetCashAccountCodeByPaymentMethod(string paymentMethod)
		{
			return paymentMethod?.ToLower() switch
			{
				"cash" => "1000",           // Cash
				"check" => "1010",          // Checking Account
				"credit card" => "1020",    // Credit Card Clearing
				"debit card" => "1010",     // Checking Account
				"bank transfer" => "1010",  // Checking Account
				"ach" => "1010",           // Checking Account
				"wire transfer" => "1010",  // Checking Account
				"paypal" => "1030",        // PayPal Account
				"stripe" => "1031",        // Stripe Account
				"square" => "1032",        // Square Account
				_ => "1000"                // Default to Cash
			};
		}



	}
}