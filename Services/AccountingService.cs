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

				// Calculate totals
				var subtotal = sale.SaleItems?.Sum(si => si.TotalPrice) ?? 0;
				var discountAmount = sale.DiscountCalculated;
				var netSaleAmount = sale.TotalAmount; // This already includes discount calculation

				// UPDATED: Get proper customer identification for B2B vs B2C
				var (primaryName, secondaryName) = GetCustomerIdentificationForJournal(sale.Customer);

				// Debit: Accounts Receivable (net amount after discount)
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
					Description = $"Sale: {primaryName} - {sale.SaleNumber}",
					DebitAmount = netSaleAmount,
					CreditAmount = 0,
					ReferenceType = "Sale",
					ReferenceId = sale.Id
				});

				// NEW: Debit: Sales Discounts (if discount applied)
				if (sale.HasDiscount && discountAmount > 0)
				{
					var discountAccount = await GetAccountByCodeAsync("4910");
					if (discountAccount == null)
					{
						_logger.LogError("Sales Discounts account (4910) not found");
						return false;
					}

					entries.Add(new GeneralLedgerEntry
					{
						TransactionDate = sale.SaleDate,
						TransactionNumber = journalNumber,
						AccountId = discountAccount.Id,
						Description = $"Sales Discount: {sale.DiscountReason ?? $"{sale.DiscountType} discount"} - {primaryName}",
						DebitAmount = discountAmount,
						CreditAmount = 0,
						ReferenceType = "Sale",
						ReferenceId = sale.Id
					});
				}

				// Credit: Sales Revenue (gross amount before discount)
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
					Description = $"Sale: {sale.SaleNumber} - {primaryName}",
					DebitAmount = 0,
					CreditAmount = subtotal, // Gross revenue before discount
					ReferenceType = "Sale",
					ReferenceId = sale.Id
				});

				// Handle shipping and tax if present
				if (sale.ShippingCost > 0)
				{
					var shippingAccount = await GetAccountByCodeAsync("4100"); // Service Revenue
					if (shippingAccount != null)
					{
						entries.Add(new GeneralLedgerEntry
						{
							TransactionDate = sale.SaleDate,
							TransactionNumber = journalNumber,
							AccountId = shippingAccount.Id,
							Description = $"Shipping Revenue: {sale.SaleNumber} - {primaryName}",
							DebitAmount = 0,
							CreditAmount = sale.ShippingCost,
							ReferenceType = "Sale",
							ReferenceId = sale.Id
						});
					}
				}

				if (sale.TaxAmount > 0)
				{
					var taxPayableAccount = await GetAccountByCodeAsync("2300"); // Sales Tax Payable
					if (taxPayableAccount != null)
					{
						entries.Add(new GeneralLedgerEntry
						{
							TransactionDate = sale.SaleDate,
							TransactionNumber = journalNumber,
							AccountId = taxPayableAccount.Id,
							Description = $"Sales Tax: {sale.SaleNumber} - {primaryName}",
							DebitAmount = 0,
							CreditAmount = sale.TaxAmount,
							ReferenceType = "Sale",
							ReferenceId = sale.Id
						});
					}
				}

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
								Description = $"COGS for Sale: {sale.SaleNumber} - {primaryName}",
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
								Description = $"Inventory reduction for Sale: {sale.SaleNumber} - {primaryName}",
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

				_logger.LogInformation("Generated journal entry {JournalNumber} for sale {SaleId} to {PrimaryName}",
								journalNumber, sale.Id, primaryName);

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

		public async Task<bool> GenerateJournalEntriesForExpensePaymentAsync(ExpensePayment expensePayment)
		{
			try
			{
				if (expensePayment.IsJournalEntryGenerated) return true;

				var journalNumber = await GenerateNextJournalNumberAsync("JE-EXP");
				var entries = new List<GeneralLedgerEntry>();

				// Get the expense with its ledger account
				var expense = await _context.Expenses
						.Include(e => e.LedgerAccount)
						.Include(e => e.DefaultVendor)
						.FirstOrDefaultAsync(e => e.Id == expensePayment.ExpenseId);

				var vendor = await _context.Vendors
						.FirstOrDefaultAsync(v => v.Id == expensePayment.VendorId);

				if (expense == null)
				{
					_logger.LogError("Expense {ExpenseId} not found for payment {PaymentId}",
							expensePayment.ExpenseId, expensePayment.Id);
					return false;
				}

				// Use the expense's configured ledger account
				if (expense.LedgerAccount == null)
				{
					_logger.LogError("Ledger account not found for expense {ExpenseId}", expense.Id);
					return false;
				}

				// Debit: Expense's Configured Ledger Account
				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = expensePayment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId = expense.LedgerAccountId,
					Description = $"Expense Payment: {expense.Description} - {vendor?.CompanyName ?? "Unknown Vendor"}",
					DebitAmount = expensePayment.Amount,
					CreditAmount = 0,
					ReferenceType = "ExpensePayment",
					ReferenceId = expensePayment.Id
				});

				// Credit: Cash Account (based on payment method)
				var cashAccountCode = GetCashAccountCodeByPaymentMethod(expensePayment.PaymentMethod);
				var cashAccount = await GetAccountByCodeAsync(cashAccountCode);

				if (cashAccount == null)
				{
					_logger.LogError("Cash account {AccountCode} not found for payment method {PaymentMethod}",
							cashAccountCode, expensePayment.PaymentMethod);
					return false;
				}

				entries.Add(new GeneralLedgerEntry
				{
					TransactionDate = expensePayment.PaymentDate,
					TransactionNumber = journalNumber,
					AccountId = cashAccount.Id,
					Description = $"Expense Payment: {vendor?.CompanyName ?? "Unknown Vendor"} - {expensePayment.GetPaymentReference()}",
					DebitAmount = 0,
					CreditAmount = expensePayment.Amount,
					ReferenceType = "ExpensePayment",
					ReferenceId = expensePayment.Id
				});

				await CreateJournalEntriesAsync(entries);

				// Update expense payment to mark journal entry as generated
				expensePayment.JournalEntryNumber = journalNumber;
				expensePayment.IsJournalEntryGenerated = true;

				await _context.SaveChangesAsync();

				_logger.LogInformation("Generated journal entry {JournalNumber} for expense payment {PaymentId} using account {AccountCode}",
						journalNumber, expensePayment.Id, expense.LedgerAccount.AccountCode);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for expense payment {PaymentId}", expensePayment.Id);
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

		// Add this method to AccountingService for enhanced reference information
		public async Task<IEnumerable<GeneralLedgerEntry>> GetAllLedgerEntriesWithEnhancedReferencesAsync(DateTime? startDate = null, DateTime? endDate = null)
		{
			IQueryable<GeneralLedgerEntry> query = _context.GeneralLedgerEntries
					.Include(e => e.Account);

			if (startDate.HasValue)
				query = query.Where(e => e.TransactionDate >= startDate.Value);

			if (endDate.HasValue)
				query = query.Where(e => e.TransactionDate <= endDate.Value);

			var entries = await query
					.OrderByDescending(e => e.TransactionDate)
					.ThenBy(e => e.TransactionNumber)
					.ToListAsync();

			// Enhance entries with actual sale/purchase numbers
			foreach (var entry in entries.Where(e => e.HasReference))
			{
				await EnhanceEntryWithReferenceInfoAsync(entry);
			}

			return entries;
		}

		private async Task EnhanceEntryWithReferenceInfoAsync(GeneralLedgerEntry entry)
		{
			if (!entry.HasReference) return;

			try
			{
				switch (entry.ReferenceType?.ToLower())
				{
					case "sale":
						var sale = await _context.Sales.FindAsync(entry.ReferenceId!.Value);
						if (sale != null)
						{
							// Store enhanced info in Description if needed, or use a custom property
							entry.EnhancedReferenceText = $"Sale {sale.SaleNumber}";
						}
						break;

					case "purchase":
						var purchase = await _context.Purchases.FindAsync(entry.ReferenceId!.Value);
						if (purchase != null)
						{
							entry.EnhancedReferenceText = $"Purchase {purchase.PurchaseOrderNumber ?? $"#{purchase.Id}"}";
						}
						break;

					case "customerpayment":
						var payment = await _context.CustomerPayments
							.Include(p => p.Sale)
							.FirstOrDefaultAsync(p => p.Id == entry.ReferenceId!.Value);
						if (payment?.Sale != null)
						{
							entry.EnhancedReferenceText = $"Payment for {payment.Sale.SaleNumber}";
							// Update the URL to point to the correct sale
							entry.EnhancedReferenceUrl = $"/Sales/Details/{payment.SaleId}";
						}
						break;
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to enhance reference info for entry {EntryId}", entry.Id);
			}
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
		// ============= MANUAL JOURNAL ENTRIES =============

		public async Task<bool> CreateManualJournalEntryAsync(ManualJournalEntryViewModel model)
		{
			try
			{
				var journalNumber = await GenerateNextJournalNumberAsync("JE-MAN");
				var entries = new List<GeneralLedgerEntry>();

				// Create journal entries from the model
				foreach (var line in model.JournalEntries.Where(e => e.AccountId > 0))
				{
					var account = await GetAccountByIdAsync(line.AccountId);
					if (account == null)
					{
						_logger.LogError("Account {AccountId} not found for manual journal entry", line.AccountId);
						throw new InvalidOperationException($"Account with ID {line.AccountId} not found");
					}

					var entry = new GeneralLedgerEntry
					{
						TransactionDate = model.TransactionDate,
						TransactionNumber = journalNumber,
						AccountId = line.AccountId,
						Description = !string.IsNullOrWhiteSpace(line.LineDescription)
									? line.LineDescription
									: model.Description ?? "Manual journal entry",
						DebitAmount = line.DebitAmount ?? 0,
						CreditAmount = line.CreditAmount ?? 0,
						ReferenceType = "ManualJournalEntry",
						ReferenceId = null, // No specific reference ID for manual entries
						CreatedBy = "Manual Entry",
						CreatedDate = DateTime.Now
					};

					entries.Add(entry);
				}

				// Validate that debits equal credits
				var totalDebits = entries.Sum(e => e.DebitAmount);
				var totalCredits = entries.Sum(e => e.CreditAmount);

				if (Math.Abs(totalDebits - totalCredits) > 0.01m)
				{
					throw new InvalidOperationException($"Journal entry is not balanced. Debits: {totalDebits:C}, Credits: {totalCredits:C}");
				}

				// Create the journal entries
				await CreateJournalEntriesAsync(entries);

				_logger.LogInformation("Created manual journal entry {JournalNumber} with {EntryCount} lines. Reference: {Reference}",
						journalNumber, entries.Count, model.ReferenceNumber);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating manual journal entry");
				throw;
			}
		}

		public async Task<List<GeneralLedgerEntry>> GetManualJournalEntriesAsync(DateTime? startDate = null, DateTime? endDate = null)
		{
			try
			{
				var query = _context.GeneralLedgerEntries
						.Include(e => e.Account)
						.Where(e => e.ReferenceType == "ManualJournalEntry");

				if (startDate.HasValue)
				{
					query = query.Where(e => e.TransactionDate >= startDate.Value);
				}

				if (endDate.HasValue)
				{
					query = query.Where(e => e.TransactionDate <= endDate.Value);
				}

				return await query
						.OrderByDescending(e => e.TransactionDate)
						.ThenBy(e => e.TransactionNumber)
						.ToListAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving manual journal entries");
				throw;
			}
		}

		public async Task<bool> ReverseManualJournalEntryAsync(string transactionNumber, string reason)
		{
			try
			{
				var originalEntries = await _context.GeneralLedgerEntries
						.Include(e => e.Account)
						.Where(e => e.TransactionNumber == transactionNumber && e.ReferenceType == "ManualJournalEntry")
						.ToListAsync();

				if (!originalEntries.Any())
				{
					throw new InvalidOperationException($"Manual journal entry {transactionNumber} not found");
				}

				var reversalNumber = await GenerateNextJournalNumberAsync("JE-REV");
				var reversalEntries = new List<GeneralLedgerEntry>();

				// Create reversal entries (swap debits and credits)
				foreach (var originalEntry in originalEntries)
				{
					var reversalEntry = new GeneralLedgerEntry
					{
						TransactionDate = DateTime.Today,
						TransactionNumber = reversalNumber,
						AccountId = originalEntry.AccountId,
						Description = $"REVERSAL: {reason} (Original: {originalEntry.TransactionNumber})",
						DebitAmount = originalEntry.CreditAmount, // Swap credit to debit
						CreditAmount = originalEntry.DebitAmount, // Swap debit to credit
						ReferenceType = "ManualJournalEntryReversal",
						ReferenceId = null,
						CreatedBy = "System Reversal",
						CreatedDate = DateTime.Now
					};

					reversalEntries.Add(reversalEntry);
				}

				await CreateJournalEntriesAsync(reversalEntries);

				_logger.LogInformation("Created reversal journal entry {ReversalNumber} for original entry {OriginalNumber}. Reason: {Reason}",
						reversalNumber, transactionNumber, reason);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reversing manual journal entry {TransactionNumber}", transactionNumber);
				throw;
			}
		}

		public async Task<decimal> GetManualJournalEntriesTotalAsync(DateTime startDate, DateTime endDate)
		{
			try
			{
				var total = await _context.GeneralLedgerEntries
						.Where(e => e.ReferenceType == "ManualJournalEntry" &&
											 e.TransactionDate >= startDate &&
											 e.TransactionDate <= endDate)
						.SumAsync(e => e.DebitAmount);

				return total;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating manual journal entries total");
				return 0;
			}
		}

		// Add these methods to the AccountingService class:

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

				// Get proper customer identification for B2B vs B2C
				var (primaryName, secondaryName) = GetCustomerIdentificationForJournal(sale.Customer);

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
					Description = $"Customer payment: {primaryName} - {sale.SaleNumber}",
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
					Description = $"Payment received: {primaryName} - {sale.SaleNumber}",
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

				_logger.LogInformation("Generated journal entry {JournalNumber} for customer payment {PaymentId} from {PrimaryName}",
								journalNumber, payment.Id, primaryName);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entries for customer payment {PaymentId}", payment.Id);
				return false;
			}
		}

		// Helper method to get proper customer identification for journal entries
		private (string primaryName, string secondaryName) GetCustomerIdentificationForJournal(Customer? customer)
		{
			if (customer == null)
			{
				return ("Unknown Customer", "");
			}

			// For B2B customers (where company name exists), prioritize company name
			if (!string.IsNullOrWhiteSpace(customer.CompanyName))
			{
				// Primary: Company Name, Secondary: Customer Contact Name
				return (customer.CompanyName, customer.CustomerName);
			}

			// For B2C customers (no company name), use customer name
			return (customer.CustomerName, "");
		}

		// ✅ NEW: Helper method to get expense accounts
		public async Task<IEnumerable<Account>> GetExpenseAccountsAsync()
		{
			return await _context.Accounts
				.Where(a => a.AccountType == AccountType.Expense && a.IsActive)
				.OrderBy(a => a.AccountCode)
				.ToListAsync();
		}

		// ✅ NEW: Helper method to get suggested account for expense category
		public async Task<Account?> GetSuggestedAccountForExpenseCategoryAsync(ExpenseCategory category)
		{
			var suggestedCode = Expense.GetSuggestedAccountCodeForCategory(category);
			return await GetAccountByCodeAsync(suggestedCode);
		}

		// Add this method to AccountingService to get enhanced reference info:

		public async Task<(string displayText, string? url, string icon)> GetEnhancedReferenceInfoAsync(string? referenceType, int? referenceId)
		{
			if (string.IsNullOrEmpty(referenceType) || !referenceId.HasValue)
				return ("", null, "fas fa-link text-muted");

			try
			{
				switch (referenceType.ToLower())
				{
					case "sale":
						var sale = await _context.Sales.FindAsync(referenceId.Value);
						return (
							$"Sale {sale?.SaleNumber ?? $"#{referenceId}"}",
							$"/Sales/Details/{referenceId}",
							"fas fa-shopping-cart text-success"
						);

					case "purchase": 
						var purchase = await _context.Purchases.FindAsync(referenceId.Value);
						return (
							$"Purchase {purchase?.PurchaseOrderNumber ?? $"#{referenceId}"}",
							$"/Purchases/Details/{referenceId}",
							"fas fa-shopping-bag text-primary"
						);

					case "customerpayment":
						var payment = await _context.CustomerPayments
							.Include(p => p.Sale)
							.FirstOrDefaultAsync(p => p.Id == referenceId.Value);
						return (
							$"Payment for {payment?.Sale?.SaleNumber ?? $"#{referenceId}"}",
							$"/Sales/Details/{payment?.SaleId}",
							"fas fa-credit-card text-success"
						);

					default:
						return ($"{referenceType} #{referenceId}", null, "fas fa-link text-muted");
				}
			}
			catch
			{
				return ($"{referenceType} #{referenceId}", null, "fas fa-link text-muted");
			}
		}

		/// <summary>
		/// Gets revenue accounts suitable for ISellableEntity objects
		/// </summary>
		/// <returns>List of active revenue accounts ordered by account code</returns>
		public async Task<IEnumerable<Account>> GetRevenueAccountsForSellableEntitiesAsync()
		{
			return await _context.Accounts
				.Where(a => a.AccountType == AccountType.Revenue && 
									 a.IsActive &&
									 (a.AccountCode.StartsWith("40") || a.AccountCode.StartsWith("41"))) // Revenue accounts 4000-4199
				.OrderBy(a => a.AccountCode)
				.ToListAsync();
		}

		/// <summary>
		/// Gets the recommended revenue account for a sale based on its items
		/// Uses the GetDefaultRevenueAccountCode logic for ISellableEntity objects
		/// </summary>
		/// <param name="sale">The sale to analyze</param>
		/// <returns>Recommended revenue account code</returns>
		public async Task<string> GetRecommendedRevenueAccountForSaleAsync(Sale sale)
		{
			// If sale has a specific revenue account set, use it
			if (!string.IsNullOrEmpty(sale.RevenueAccountCode))
			{
				return sale.RevenueAccountCode;
			}

			// Analyze sale items to determine best revenue account
			if (sale.SaleItems?.Any() == true)
			{
				// Get all unique revenue account codes from sale items
				var itemAccountCodes = new List<string>();
				
				foreach (var saleItem in sale.SaleItems)
				{
					if (saleItem.ItemId.HasValue && saleItem.Item != null)
					{
						itemAccountCodes.Add(saleItem.Item.GetDefaultRevenueAccountCode());
					}
					else if (saleItem.ServiceTypeId.HasValue && saleItem.ServiceType != null)
					{
						itemAccountCodes.Add(saleItem.ServiceType.GetDefaultRevenueAccountCode());
					}
					// Add logic for other ISellableEntity types as needed
				}

				// Use the most common account code, or prioritize based on business rules
				if (itemAccountCodes.Any())
				{
					// If all items use the same account, use that
					var distinctCodes = itemAccountCodes.Distinct().ToList();
					if (distinctCodes.Count == 1)
					{
						return distinctCodes.First();
					}

					// If mixed, prioritize based on business logic
					if (itemAccountCodes.Contains("4000")) return "4000"; // Product Sales takes priority
					if (itemAccountCodes.Contains("4100")) return "4100"; // Service Revenue
					if (itemAccountCodes.Contains("4010")) return "4010"; // Supply Sales
					if (itemAccountCodes.Contains("4020")) return "4020"; // Research Material Sales
					
					return itemAccountCodes.First(); // Fallback to first found
				}
			}

			return "4000"; // Default to Product Sales
		}

		/// <summary>
		/// Validates that a revenue account code is valid and active
		/// </summary>
		/// <param name="accountCode">Account code to validate</param>
		/// <returns>True if valid and active, false otherwise</returns>
		public async Task<bool> IsValidRevenueAccountAsync(string? accountCode)
		{
			if (string.IsNullOrEmpty(accountCode))
				return false;

			var account = await GetAccountByCodeAsync(accountCode);
			return account != null && 
				   account.IsActive && 
				   account.AccountType == AccountType.Revenue;
		}

		// Add these methods to your existing AccountingService class

		public async Task<bool> PerformYearEndClosingAsync(FinancialPeriod financialPeriod, string closingNotes, string? createdBy = null)
		{
			using var transaction = await _context.Database.BeginTransactionAsync();

			try
			{
				_logger.LogInformation("Starting year-end closing for period {PeriodName} ({PeriodId})",
						financialPeriod.PeriodName, financialPeriod.Id);

				// 1. Validate the closing can be performed
				var validation = await ValidateYearEndClosingAsync(financialPeriod);
				if (!validation.IsValid)
				{
					_logger.LogError("Year-end closing validation failed: {Errors}", string.Join(", ", validation.Errors));
					return false;
				}

				// 2. Generate closing transaction numbers
				var revenueClosingTxn = await GenerateNextJournalNumberAsync("CLOSE-REV");
				var expenseClosingTxn = await GenerateNextJournalNumberAsync("CLOSE-EXP");
				var retainedEarningsTransferTxn = await GenerateNextJournalNumberAsync("CLOSE-RE");

				// 3. Close revenue accounts to Current Year Earnings
				var revenueEntries = await CreateRevenueClosingEntriesAsync(financialPeriod, revenueClosingTxn, createdBy);

				// 4. Close expense accounts to Current Year Earnings  
				var expenseEntries = await CreateExpenseClosingEntriesAsync(financialPeriod, expenseClosingTxn, createdBy);

				// 5. Transfer Current Year Earnings balance to Retained Earnings
				var retainedEarningsEntries = await CreateRetainedEarningsTransferAsync(financialPeriod, retainedEarningsTransferTxn, createdBy);

				// 6. Save all entries
				var allEntries = revenueEntries.Concat(expenseEntries).Concat(retainedEarningsEntries);
				_context.GeneralLedgerEntries.AddRange(allEntries);
				await _context.SaveChangesAsync();

				// 7. Commit the transaction
				await transaction.CommitAsync();

				_logger.LogInformation("Year-end closing completed successfully for period {PeriodName}. " +
						"Created {RevenueEntries} revenue entries, {ExpenseEntries} expense entries, {TransferEntries} transfer entries",
						financialPeriod.PeriodName, revenueEntries.Count, expenseEntries.Count, retainedEarningsEntries.Count);

				return true;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error performing year-end closing for period {PeriodId}", financialPeriod.Id);
				return false;
			}
		}

		public async Task<List<GeneralLedgerEntry>> CreateRevenueClosingEntriesAsync(FinancialPeriod financialPeriod, string transactionNumber, string? createdBy = null)
		{
			var entries = new List<GeneralLedgerEntry>();

			// Get all revenue accounts with balances in the period
			var revenueAccounts = await _context.Accounts
					.Where(a => a.AccountType == AccountType.Revenue && a.IsActive)
					.ToListAsync();

			var currentYearEarningsAccount = await GetAccountByCodeAsync("3200");
			if (currentYearEarningsAccount == null)
			{
				throw new InvalidOperationException("Current Year Earnings account (3200) not found");
			}

			decimal totalRevenueToClose = 0;

			foreach (var account in revenueAccounts)
			{
				var balance = await GetAccountBalanceAsync(account.AccountCode, financialPeriod.EndDate);

				if (balance > 0.01m) // Only close accounts with significant balances
				{
					// Debit the Revenue account to zero it out
					entries.Add(new GeneralLedgerEntry
					{
						TransactionNumber = transactionNumber,
						TransactionDate = financialPeriod.EndDate,
						AccountId = account.Id,
						Description = $"Year-end closing: Close {account.AccountName} to Current Year Earnings",
						DebitAmount = balance,
						CreditAmount = 0,
						ReferenceType = "YearEndClosing",
						ReferenceId = financialPeriod.Id,
						CreatedBy = createdBy ?? "System",
						CreatedDate = DateTime.Now
					});

					totalRevenueToClose += balance;
				}
			}

			// Credit Current Year Earnings with total revenue
			if (totalRevenueToClose > 0)
			{
				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate = financialPeriod.EndDate,
					AccountId = currentYearEarningsAccount.Id,
					Description = $"Year-end closing: Transfer revenue to Current Year Earnings ({financialPeriod.PeriodName})",
					DebitAmount = 0,
					CreditAmount = totalRevenueToClose,
					ReferenceType = "YearEndClosing",
					ReferenceId = financialPeriod.Id,
					CreatedBy = createdBy ?? "System",
					CreatedDate = DateTime.Now
				});
			}

			return entries;
		}

		public async Task<List<GeneralLedgerEntry>> CreateExpenseClosingEntriesAsync(FinancialPeriod financialPeriod, string transactionNumber, string? createdBy = null)
		{
			var entries = new List<GeneralLedgerEntry>();

			// Get all expense accounts with balances in the period
			var expenseAccounts = await _context.Accounts
					.Where(a => a.AccountType == AccountType.Expense && a.IsActive)
					.ToListAsync();

			var currentYearEarningsAccount = await GetAccountByCodeAsync("3200");
			if (currentYearEarningsAccount == null)
			{
				throw new InvalidOperationException("Current Year Earnings account (3200) not found");
			}

			decimal totalExpensesToClose = 0;

			foreach (var account in expenseAccounts)
			{
				var balance = await GetAccountBalanceAsync(account.AccountCode, financialPeriod.EndDate);

				if (balance > 0.01m) // Only close accounts with significant balances
				{
					// Credit the Expense account to zero it out
					entries.Add(new GeneralLedgerEntry
					{
						TransactionNumber = transactionNumber,
						TransactionDate = financialPeriod.EndDate,
						AccountId = account.Id,
						Description = $"Year-end closing: Close {account.AccountName} to Current Year Earnings",
						DebitAmount = 0,
						CreditAmount = balance,
						ReferenceType = "YearEndClosing",
						ReferenceId = financialPeriod.Id,
						CreatedBy = createdBy ?? "System",
						CreatedDate = DateTime.Now
					});

					totalExpensesToClose += balance;
				}
			}

			// Debit Current Year Earnings with total expenses
			if (totalExpensesToClose > 0)
			{
				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate = financialPeriod.EndDate,
					AccountId = currentYearEarningsAccount.Id,
					Description = $"Year-end closing: Transfer expenses to Current Year Earnings ({financialPeriod.PeriodName})",
					DebitAmount = totalExpensesToClose,
					CreditAmount = 0,
					ReferenceType = "YearEndClosing",
					ReferenceId = financialPeriod.Id,
					CreatedBy = createdBy ?? "System",
					CreatedDate = DateTime.Now
				});
			}

			return entries;
		}

		public async Task<List<GeneralLedgerEntry>> CreateRetainedEarningsTransferAsync(FinancialPeriod financialPeriod, string transactionNumber, string? createdBy = null)
		{
			var entries = new List<GeneralLedgerEntry>();

			var currentYearEarningsAccount = await GetAccountByCodeAsync("3200");
			var retainedEarningsAccount = await GetAccountByCodeAsync("3100");

			if (currentYearEarningsAccount == null)
			{
				throw new InvalidOperationException("Current Year Earnings account (3200) not found");
			}

			if (retainedEarningsAccount == null)
			{
				throw new InvalidOperationException("Retained Earnings account (3100) not found");
			}

			// Get the Current Year Earnings balance after revenue/expense closing
			var currentYearEarningsBalance = await GetAccountBalanceAsync("3200", financialPeriod.EndDate);

			if (Math.Abs(currentYearEarningsBalance) > 0.01m) // Only transfer if there's a significant balance
			{
				if (currentYearEarningsBalance > 0) // Profit - transfer credit balance
				{
					// Debit Current Year Earnings to zero it out
					entries.Add(new GeneralLedgerEntry
					{
						TransactionNumber = transactionNumber,
						TransactionDate = financialPeriod.EndDate,
						AccountId = currentYearEarningsAccount.Id,
						Description = $"Year-end closing: Transfer profit to Retained Earnings ({financialPeriod.PeriodName})",
						DebitAmount = currentYearEarningsBalance,
						CreditAmount = 0,
						ReferenceType = "YearEndClosing",
						ReferenceId = financialPeriod.Id,
						CreatedBy = createdBy ?? "System",
						CreatedDate = DateTime.Now
					});

					// Credit Retained Earnings
					entries.Add(new GeneralLedgerEntry
					{
						TransactionNumber = transactionNumber,
						TransactionDate = financialPeriod.EndDate,
						AccountId = retainedEarningsAccount.Id,
						Description = $"Year-end closing: Net income transfer from {financialPeriod.PeriodName}",
						DebitAmount = 0,
						CreditAmount = currentYearEarningsBalance,
						ReferenceType = "YearEndClosing",
						ReferenceId = financialPeriod.Id,
						CreatedBy = createdBy ?? "System",
						CreatedDate = DateTime.Now
					});
				}
				else // Loss - transfer debit balance
				{
					var lossAmount = Math.Abs(currentYearEarningsBalance);

					// Credit Current Year Earnings to zero it out
					entries.Add(new GeneralLedgerEntry
					{
						TransactionNumber = transactionNumber,
						TransactionDate = financialPeriod.EndDate,
						AccountId = currentYearEarningsAccount.Id,
						Description = $"Year-end closing: Transfer loss to Retained Earnings ({financialPeriod.PeriodName})",
						DebitAmount = 0,
						CreditAmount = lossAmount,
						ReferenceType = "YearEndClosing",
						ReferenceId = financialPeriod.Id,
						CreatedBy = createdBy ?? "System",
						CreatedDate = DateTime.Now
					});

					// Debit Retained Earnings
					entries.Add(new GeneralLedgerEntry
					{
						TransactionNumber = transactionNumber,
						TransactionDate = financialPeriod.EndDate,
						AccountId = retainedEarningsAccount.Id,
						Description = $"Year-end closing: Net loss transfer from {financialPeriod.PeriodName}",
						DebitAmount = lossAmount,
						CreditAmount = 0,
						ReferenceType = "YearEndClosing",
						ReferenceId = financialPeriod.Id,
						CreatedBy = createdBy ?? "System",
						CreatedDate = DateTime.Now
					});
				}
			}

			return entries;
		}

		public async Task<decimal> CalculateNetIncomeAsync(DateTime startDate, DateTime endDate)
		{
			// Get revenue total
			var revenueAccounts = await _context.Accounts
					.Where(a => a.AccountType == AccountType.Revenue && a.IsActive)
					.ToListAsync();

			decimal totalRevenue = 0;
			foreach (var account in revenueAccounts)
			{
				totalRevenue += await GetAccountBalanceForPeriodAsync(account.AccountCode, startDate, endDate);
			}

			// Get expense total
			var expenseAccounts = await _context.Accounts
					.Where(a => a.AccountType == AccountType.Expense && a.IsActive)
					.ToListAsync();

			decimal totalExpenses = 0;
			foreach (var account in expenseAccounts)
			{
				totalExpenses += await GetAccountBalanceForPeriodAsync(account.AccountCode, startDate, endDate);
			}

			return totalRevenue - totalExpenses;
		}

		public async Task<YearEndValidationResult> ValidateYearEndClosingAsync(FinancialPeriod financialPeriod)
		{
			var result = new YearEndValidationResult { IsValid = true };

			try
			{
				// Check if period is already closed
				if (financialPeriod.IsClosed)
				{
					result.Errors.Add("Financial period is already closed");
					result.IsValid = false;
				}

				// Check if required accounts exist
				var currentYearEarnings = await GetAccountByCodeAsync("3200");
				var retainedEarnings = await GetAccountByCodeAsync("3100");

				if (currentYearEarnings == null)
				{
					result.Errors.Add("Current Year Earnings account (3200) not found");
					result.IsValid = false;
				}

				if (retainedEarnings == null)
				{
					result.Errors.Add("Retained Earnings account (3100) not found");
					result.IsValid = false;
				}

				// Check trial balance
				var ledgerEntries = await GetAllLedgerEntriesAsync(financialPeriod.StartDate, financialPeriod.EndDate);
				result.TotalDebits = ledgerEntries.Sum(e => e.DebitAmount);
				result.TotalCredits = ledgerEntries.Sum(e => e.CreditAmount);
				result.TrialBalanceIsBalanced = Math.Abs(result.TotalDebits - result.TotalCredits) < 0.01m;

				if (!result.TrialBalanceIsBalanced)
				{
					result.Errors.Add($"Trial balance is not balanced. Debits: {result.TotalDebits:C}, Credits: {result.TotalCredits:C}");
					result.IsValid = false;
				}

				// Calculate net income
				result.NetIncome = await CalculateNetIncomeAsync(financialPeriod.StartDate, financialPeriod.EndDate);

				// Check for pending transactions (warnings)
				var endOfPeriod = financialPeriod.EndDate.Date.AddDays(1).AddSeconds(-1);
				var futureTransactions = await _context.GeneralLedgerEntries
						.Where(e => e.TransactionDate > endOfPeriod)
						.CountAsync();

				if (futureTransactions > 0)
				{
					result.Warnings.Add($"There are {futureTransactions} transactions dated after the period end");
				}

			}
			catch (Exception ex)
			{
				result.Errors.Add($"Validation error: {ex.Message}");
				result.IsValid = false;
			}

			return result;
		}

		public async Task<YearEndClosingSummary> GetYearEndClosingSummaryAsync(FinancialPeriod financialPeriod)
		{
			var summary = new YearEndClosingSummary();

			// Get revenue accounts and balances
			var revenueAccounts = await _context.Accounts
					.Where(a => a.AccountType == AccountType.Revenue && a.IsActive)
					.ToListAsync();

			foreach (var account in revenueAccounts)
			{
				var balance = await GetAccountBalanceAsync(account.AccountCode, financialPeriod.EndDate);
				if (balance > 0.01m)
				{
					summary.RevenueAccounts.Add(new AccountClosingSummary
					{
						AccountCode = account.AccountCode,
						AccountName = account.AccountName,
						Balance = balance
					});
					summary.TotalRevenue += balance;
				}
			}

			// Get expense accounts and balances
			var expenseAccounts = await _context.Accounts
					.Where(a => a.AccountType == AccountType.Expense && a.IsActive)
					.ToListAsync();

			foreach (var account in expenseAccounts)
			{
				var balance = await GetAccountBalanceAsync(account.AccountCode, financialPeriod.EndDate);
				if (balance > 0.01m)
				{
					summary.ExpenseAccounts.Add(new AccountClosingSummary
					{
						AccountCode = account.AccountCode,
						AccountName = account.AccountName,
						Balance = balance
					});
					summary.TotalExpenses += balance;
				}
			}

			// Calculate net income
			summary.NetIncome = summary.TotalRevenue - summary.TotalExpenses;

			// Get current account balances
			summary.CurrentYearEarningsBalance = await GetAccountBalanceAsync("3200", financialPeriod.EndDate);
			summary.RetainedEarningsBalanceBefore = await GetAccountBalanceAsync("3100", financialPeriod.EndDate);
			summary.RetainedEarningsBalanceAfter = summary.RetainedEarningsBalanceBefore + summary.NetIncome;

			return summary;
		}

		// Helper method to get account balance for a specific period
		private async Task<decimal> GetAccountBalanceForPeriodAsync(string accountCode, DateTime startDate, DateTime endDate)
		{
			var entries = await GetAccountLedgerEntriesAsync(accountCode, startDate, endDate);
			return entries.Sum(e => e.DebitAmount - e.CreditAmount);
		}

		// Add these methods to your existing AccountingService class

		// ============= ENHANCED CASH FLOW ANALYSIS METHODS =============

		public async Task<EnhancedCashFlowAnalysisViewModel> GetEnhancedCashFlowAnalysisAsync(DateTime startDate, DateTime endDate, bool includePriorPeriod = true)
		{
			try
			{
				var currentPeriod = await GetCashFlowStatementAsync(startDate, endDate);

				var analysis = new EnhancedCashFlowAnalysisViewModel
				{
					CurrentPeriod = currentPeriod
				};

				// Get prior period for comparison if requested
				if (includePriorPeriod)
				{
					var periodLength = endDate - startDate;
					var priorStartDate = startDate.Subtract(periodLength);
					var priorEndDate = startDate.AddDays(-1);

					analysis.PriorPeriod = await GetCashFlowStatementAsync(priorStartDate, priorEndDate);
				}

				// Calculate working capital analysis
				analysis.WorkingCapitalAnalysis = await GetWorkingCapitalAnalysisAsync(startDate, endDate);

				// Calculate free cash flow
				analysis.FreeCashFlow = await GetFreeCashFlowAnalysisAsync(startDate, endDate);

				// Calculate cash efficiency metrics
				await CalculateCashEfficiencyMetrics(analysis, startDate, endDate);

				// Get cash flow ratios
				analysis.CashFlowRatios = await CalculateCashFlowRatios(analysis.CurrentPeriod);

				// Get monthly trends
				analysis.MonthlyTrends = await GetMonthlyCashFlowTrendsAsync(12);

				return analysis;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting enhanced cash flow analysis");
				return new EnhancedCashFlowAnalysisViewModel();
			}
		}

		public async Task<CashFlowProjectionViewModel> GetCashFlowProjectionsAsync(int projectionMonths = 12)
		{
			try
			{
				var projection = new CashFlowProjectionViewModel();

				// Get historical data for pattern analysis
				var historicalData = await GetMonthlyCashFlowTrendsAsync(12);

				// Simple projection based on averages (can be enhanced with more sophisticated algorithms)
				var avgOperatingCashFlow = historicalData.Any() ? historicalData.Average(h => h.OperatingCashFlow) : 0;
				var avgInvestingCashFlow = historicalData.Any() ? historicalData.Average(h => h.InvestingCashFlow) : 0;
				var avgFinancingCashFlow = historicalData.Any() ? historicalData.Average(h => h.FinancingCashFlow) : 0;

				var currentCashBalance = await GetAccountBalanceAsync("1000");

				for (int i = 1; i <= projectionMonths; i++)
				{
					var projectionMonth = DateTime.Today.AddMonths(i);

					projection.Projections.Add(new MonthlyProjection
					{
						Month = projectionMonth,
						ProjectedOperatingCashFlow = avgOperatingCashFlow,
						ProjectedInvestingCashFlow = avgInvestingCashFlow,
						ProjectedFinancingCashFlow = avgFinancingCashFlow,
						ProjectedNetCashFlow = avgOperatingCashFlow + avgInvestingCashFlow + avgFinancingCashFlow,
						ProjectedCashBalance = currentCashBalance + (avgOperatingCashFlow + avgInvestingCashFlow + avgFinancingCashFlow) * i,
						ConfidenceLevel = Math.Max(30, 90 - (i * 5)) // Decreasing confidence over time
					});
				}

				// Create scenarios
				projection.OptimisticScenario = CreateProjectionScenario("Optimistic", projection.Projections, 1.2m);
				projection.MostLikelyScenario = CreateProjectionScenario("Most Likely", projection.Projections, 1.0m);
				projection.PessimisticScenario = CreateProjectionScenario("Pessimistic", projection.Projections, 0.8m);

				return projection;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting cash flow projections");
				return new CashFlowProjectionViewModel();
			}
		}

		public async Task<WorkingCapitalAnalysisViewModel> GetWorkingCapitalAnalysisAsync(DateTime startDate, DateTime endDate)
		{
			try
			{
				var analysis = new WorkingCapitalAnalysisViewModel();

				// Get current working capital components
				var currentAR = await GetAccountBalanceAsync("1100", endDate);
				var currentInventory = await GetAccountBalanceAsync("1220", endDate);
				var currentAP = await GetAccountBalanceAsync("2000", endDate);
				var currentAccruedLiabilities = await GetAccountBalanceAsync("2100", endDate);

				analysis.CurrentWorkingCapital = currentAR + currentInventory - currentAP - currentAccruedLiabilities;

				// Get prior period working capital
				var priorAR = await GetAccountBalanceAsync("1100", startDate.AddDays(-1));
				var priorInventory = await GetAccountBalanceAsync("1220", startDate.AddDays(-1));
				var priorAP = await GetAccountBalanceAsync("2000", startDate.AddDays(-1));
				var priorAccruedLiabilities = await GetAccountBalanceAsync("2100", startDate.AddDays(-1));

				analysis.PriorWorkingCapital = priorAR + priorInventory - priorAP - priorAccruedLiabilities;

				// Calculate changes
				analysis.AccountsReceivableChange = currentAR - priorAR;
				analysis.InventoryChange = currentInventory - priorInventory;
				analysis.AccountsPayableChange = currentAP - priorAP;
				analysis.AccruedLiabilitiesChange = currentAccruedLiabilities - priorAccruedLiabilities;

				// Calculate efficiency ratios
				var sales = await GetRevenueForPeriod(startDate, endDate);
				analysis.WorkingCapitalTurnover = sales > 0 ? sales / Math.Max(analysis.CurrentWorkingCapital, 1) : 0;
				analysis.WorkingCapitalToSalesRatio = sales > 0 ? analysis.CurrentWorkingCapital / sales : 0;

				// Create component analysis
				analysis.Components = new List<WorkingCapitalComponent>
				{
						new WorkingCapitalComponent
						{
								ComponentName = "Accounts Receivable",
								BeginningBalance = priorAR,
								EndingBalance = currentAR,
								ComponentType = "Asset",
								IsImprovement = currentAR < priorAR // Lower A/R is generally better for cash flow
            },
						new WorkingCapitalComponent
						{
								ComponentName = "Inventory",
								BeginningBalance = priorInventory,
								EndingBalance = currentInventory,
								ComponentType = "Asset",
								IsImprovement = currentInventory < priorInventory // Lower inventory is generally better for cash flow
            },
						new WorkingCapitalComponent
						{
								ComponentName = "Accounts Payable",
								BeginningBalance = priorAP,
								EndingBalance = currentAP,
								ComponentType = "Liability",
								IsImprovement = currentAP > priorAP // Higher A/P is generally better for cash flow
            }
				};

				return analysis;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting working capital analysis");
				return new WorkingCapitalAnalysisViewModel();
			}
		}

		public async Task<CustomerCashFlowAnalysisViewModel> GetCustomerCashFlowAnalysisAsync(DateTime startDate, DateTime endDate)
		{
			try
			{
				var analysis = new CustomerCashFlowAnalysisViewModel();

				// Get customer payment data
				var customerPayments = await _context.CustomerPayments
						.Include(p => p.Sale)
						.ThenInclude(s => s!.Customer)
						.Where(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate)
						.ToListAsync();

				var customerGroups = customerPayments
						.Where(p => p.Sale?.Customer != null)
						.GroupBy(p => p.Sale!.Customer!)
						.ToList();

				foreach (var group in customerGroups)
				{
					var customer = group.Key;
					var payments = group.ToList();

					var totalCollections = payments.Sum(p => p.Amount);
					var avgCollectionDays = await CalculateAverageCollectionDays(customer.Id, startDate, endDate);
					var collectionEfficiency = await CalculateCollectionEfficiency(customer.Id, startDate, endDate);

					analysis.CustomerCashFlows.Add(new CustomerCashFlow
					{
						CustomerId = customer.Id,
						CustomerName = customer.CustomerName,
						NetCashFlow = totalCollections,
						Collections = totalCollections,
						AverageCollectionDays = avgCollectionDays,
						CollectionEfficiency = collectionEfficiency,
						OutstandingBalance = customer.OutstandingBalance,
						CreditLimit = customer.CreditLimit,
						LastPaymentDate = payments.Max(p => p.PaymentDate),
						PaymentTrend = DeterminePaymentTrend(payments)
					});
				}

				analysis.TotalCollections = analysis.CustomerCashFlows.Sum(c => c.Collections);
				analysis.AverageCollectionPeriod = analysis.CustomerCashFlows.Any()
						? analysis.CustomerCashFlows.Average(c => c.AverageCollectionDays) : 0;
				analysis.CollectionEfficiency = analysis.CustomerCashFlows.Any()
						? analysis.CustomerCashFlows.Average(c => c.CollectionEfficiency) : 0;

				return analysis;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting customer cash flow analysis");
				return new CustomerCashFlowAnalysisViewModel();
			}
		}

		public async Task<FreeCashFlowAnalysisViewModel> GetFreeCashFlowAnalysisAsync(DateTime startDate, DateTime endDate)
		{
			try
			{
				var analysis = new FreeCashFlowAnalysisViewModel();

				// Get operating cash flow from the cash flow statement
				var cashFlowStatement = await GetCashFlowStatementAsync(startDate, endDate);
				analysis.OperatingCashFlow = cashFlowStatement.NetCashFromOperations;

				// Calculate capital expenditures from investing activities
				var capitalExpenditures = await GetCapitalExpenditures(startDate, endDate);
				analysis.TotalCapitalExpenditures = capitalExpenditures.Sum(c => c.Amount);
				analysis.CapitalExpenditureDetails = capitalExpenditures;

				// Calculate ratios
				var revenue = await GetRevenueForPeriod(startDate, endDate);
				analysis.FreeCashFlowMargin = revenue > 0 ? (analysis.FreeCashFlow / revenue) * 100 : 0;
				analysis.FreeCashFlowYield = analysis.FreeCashFlow > 0 ? (analysis.FreeCashFlow / Math.Max(revenue, 1)) * 100 : 0;

				// Calculate adequacy ratio (simplified)
				analysis.CashFlowAdequacyRatio = analysis.FreeCashFlow / Math.Max(analysis.TotalCapitalExpenditures, 1);

				return analysis;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting free cash flow analysis");
				return new FreeCashFlowAnalysisViewModel();
			}
		}

		public async Task<List<MonthlyCashFlowTrend>> GetMonthlyCashFlowTrendsAsync(int months = 12)
		{
			try
			{
				var trends = new List<MonthlyCashFlowTrend>();
				var endDate = DateTime.Today;

				for (int i = months - 1; i >= 0; i--)
				{
					var monthStart = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(-i);
					var monthEnd = monthStart.AddMonths(1).AddDays(-1);

					var monthlyEntries = await GetAllLedgerEntriesAsync(monthStart, monthEnd);

					var operatingCashFlow = await CalculateOperatingCashFlowForPeriod(monthStart, monthEnd);
					var investingCashFlow = await CalculateInvestingCashFlowForPeriod(monthStart, monthEnd);
					var financingCashFlow = await CalculateFinancingCashFlowForPeriod(monthStart, monthEnd);

					var trend = new MonthlyCashFlowTrend
					{
						Month = monthStart,
						OperatingCashFlow = operatingCashFlow,
						InvestingCashFlow = investingCashFlow,
						FinancingCashFlow = financingCashFlow,
						NetCashFlow = operatingCashFlow + investingCashFlow + financingCashFlow,
						EndingCashBalance = await GetAccountBalanceAsync("1000", monthEnd)
					};

					// Calculate growth rate
					if (trends.Any())
					{
						var previousTrend = trends.Last();
						trend.CashFlowGrowthRate = previousTrend.NetCashFlow != 0
								? ((trend.NetCashFlow - previousTrend.NetCashFlow) / Math.Abs(previousTrend.NetCashFlow)) * 100
								: 0;
					}

					trends.Add(trend);
				}

				return trends;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting monthly cash flow trends");
				return new List<MonthlyCashFlowTrend>();
			}
		}

		// ============= HELPER METHODS FOR ENHANCED CASH FLOW ANALYSIS =============

		private async Task CalculateCashEfficiencyMetrics(EnhancedCashFlowAnalysisViewModel analysis, DateTime startDate, DateTime endDate)
		{
			// Calculate Days in Accounts Receivable
			var avgAR = (await GetAccountBalanceAsync("1100", startDate) + await GetAccountBalanceAsync("1100", endDate)) / 2;
			var revenue = await GetRevenueForPeriod(startDate, endDate);
			var days = (endDate - startDate).Days;

			analysis.DaysInAccountsReceivable = revenue > 0 ? (avgAR / revenue) * days : 0;

			// Calculate Days in Inventory
			var avgInventory = (await GetAccountBalanceAsync("1220", startDate) + await GetAccountBalanceAsync("1220", endDate)) / 2;
			var cogs = await GetCOGSForPeriod(startDate, endDate);

			analysis.DaysInInventory = cogs > 0 ? (avgInventory / cogs) * days : 0;

			// Calculate Days in Accounts Payable
			var avgAP = (await GetAccountBalanceAsync("2000", startDate) + await GetAccountBalanceAsync("2000", endDate)) / 2;

			analysis.DaysInAccountsPayable = cogs > 0 ? (avgAP / cogs) * days : 0;

			// Calculate Cash Conversion Cycle
			analysis.CashConversionCycle = analysis.DaysInAccountsReceivable + analysis.DaysInInventory - analysis.DaysInAccountsPayable;
		}

		private async Task<List<CashFlowRatio>> CalculateCashFlowRatios(CashFlowStatementViewModel cashFlow)
		{
			var ratios = new List<CashFlowRatio>();

			// Operating Cash Flow Ratio
			ratios.Add(new CashFlowRatio
			{
				RatioName = "Operating Cash Flow Ratio",
				Value = cashFlow.NetIncome != 0 ? cashFlow.NetCashFromOperations / cashFlow.NetIncome : 0,
				FormattedValue = $"{(cashFlow.NetIncome != 0 ? cashFlow.NetCashFromOperations / cashFlow.NetIncome : 0):F2}",
				Interpretation = "Measures quality of earnings",
				Benchmark = cashFlow.NetIncome != 0 && cashFlow.NetCashFromOperations / cashFlow.NetIncome > 1 ? RatioBenchmark.Good : RatioBenchmark.Average
			});

			// Cash Flow Margin
			var revenue = await GetRevenueForPeriod(cashFlow.StartDate, cashFlow.EndDate);
			var cashFlowMargin = revenue > 0 ? (cashFlow.NetCashFromOperations / revenue) * 100 : 0;

			ratios.Add(new CashFlowRatio
			{
				RatioName = "Cash Flow Margin",
				Value = cashFlowMargin,
				FormattedValue = $"{cashFlowMargin:F1}%",
				Interpretation = "Operating cash flow as % of revenue",
				Benchmark = cashFlowMargin > 15 ? RatioBenchmark.Excellent :
										 cashFlowMargin > 10 ? RatioBenchmark.Good :
										 cashFlowMargin > 5 ? RatioBenchmark.Average : RatioBenchmark.Poor
			});

			return ratios;
		}

		private ProjectionScenario CreateProjectionScenario(string scenarioName, List<MonthlyProjection> baseProjections, decimal multiplier)
		{
			var scenario = new ProjectionScenario
			{
				ScenarioName = scenarioName,
				ProbabilityPercent = scenarioName switch
				{
					"Optimistic" => 20,
					"Most Likely" => 60,
					"Pessimistic" => 20,
					_ => 33
				}
			};

			scenario.Projections = baseProjections.Select(p => new MonthlyProjection
			{
				Month = p.Month,
				ProjectedOperatingCashFlow = p.ProjectedOperatingCashFlow * multiplier,
				ProjectedInvestingCashFlow = p.ProjectedInvestingCashFlow * multiplier,
				ProjectedFinancingCashFlow = p.ProjectedFinancingCashFlow * multiplier,
				ProjectedNetCashFlow = p.ProjectedNetCashFlow * multiplier,
				ProjectedCashBalance = p.ProjectedCashBalance * multiplier,
				ConfidenceLevel = p.ConfidenceLevel
			}).ToList();

			scenario.TotalProjectedCashFlow = scenario.Projections.Sum(p => p.ProjectedNetCashFlow);
			scenario.MinimumCashBalance = scenario.Projections.Min(p => p.ProjectedCashBalance);

			return scenario;
		}

		private async Task<decimal> GetRevenueForPeriod(DateTime startDate, DateTime endDate)
		{
			var revenueAccounts = await _context.Accounts
					.Where(a => a.AccountType == AccountType.Revenue && a.IsActive)
					.ToListAsync();

			decimal totalRevenue = 0;
			foreach (var account in revenueAccounts)
			{
				var entries = await GetAccountLedgerEntriesAsync(account.AccountCode, startDate, endDate);
				totalRevenue += entries.Sum(e => e.CreditAmount - e.DebitAmount);
			}

			return totalRevenue;
		}

		private async Task<decimal> GetCOGSForPeriod(DateTime startDate, DateTime endDate)
		{
			var cogsAccount = await GetAccountByCodeAsync("5000");
			if (cogsAccount == null) return 0;

			var entries = await GetAccountLedgerEntriesAsync("5000", startDate, endDate);
			return entries.Sum(e => e.DebitAmount - e.CreditAmount);
		}

		private async Task<List<CapitalExpenditureDetail>> GetCapitalExpenditures(DateTime startDate, DateTime endDate)
		{
			// This would typically look at fixed asset purchases
			// For now, return empty list - you can enhance this based on your chart of accounts
			return new List<CapitalExpenditureDetail>();
		}

		private async Task<decimal> CalculateOperatingCashFlowForPeriod(DateTime startDate, DateTime endDate)
		{
			// Simplified calculation - would need more sophisticated logic in production
			var revenue = await GetRevenueForPeriod(startDate, endDate);
			var expenses = await GetExpensesForPeriod(startDate, endDate);
			return revenue - expenses;
		}

		private async Task<decimal> CalculateInvestingCashFlowForPeriod(DateTime startDate, DateTime endDate)
		{
			// Simplified - would analyze fixed asset purchases/sales
			return 0;
		}

		private async Task<decimal> CalculateFinancingCashFlowForPeriod(DateTime startDate, DateTime endDate)
		{
			// Simplified - would analyze debt/equity transactions
			return 0;
		}

		private async Task<decimal> GetExpensesForPeriod(DateTime startDate, DateTime endDate)
		{
			var expenseAccounts = await _context.Accounts
					.Where(a => a.AccountType == AccountType.Expense && a.IsActive)
					.ToListAsync();

			decimal totalExpenses = 0;
			foreach (var account in expenseAccounts)
			{
				var entries = await GetAccountLedgerEntriesAsync(account.AccountCode, startDate, endDate);
				totalExpenses += entries.Sum(e => e.DebitAmount - e.CreditAmount);
			}

			return totalExpenses;
		}

		private async Task<decimal> CalculateAverageCollectionDays(int customerId, DateTime startDate, DateTime endDate)
		{
			// Simplified calculation - would need more sophisticated analysis
			return 30; // Default to 30 days
		}

		private async Task<decimal> CalculateCollectionEfficiency(int customerId, DateTime startDate, DateTime endDate)
		{
			// Simplified calculation - would analyze payment patterns
			return 85; // Default to 85%
		}

		private string DeterminePaymentTrend(List<CustomerPayment> payments)
		{
			if (payments.Count < 2) return "Insufficient Data";

			var recent = payments.OrderByDescending(p => p.PaymentDate).Take(3).Sum(p => p.Amount);
			var older = payments.OrderByDescending(p => p.PaymentDate).Skip(3).Take(3).Sum(p => p.Amount);

			if (recent > older * 1.1m) return "Improving";
			if (recent < older * 0.9m) return "Declining";
			return "Stable";
		}
	}
}