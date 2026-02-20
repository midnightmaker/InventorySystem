// Services/AccountingService.GeneralLedger.cs
using InventorySystem.Models.Accounting;
using InventorySystem.ViewModels.Accounting;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public partial class AccountingService
	{
		// ============= General Ledger Management =============

		public async Task<GeneralLedgerEntry> CreateJournalEntryAsync(GeneralLedgerEntry entry)
		{
			entry.CreatedDate = DateTime.Now;
			_context.GeneralLedgerEntries.Add(entry);
			await _context.SaveChangesAsync();

			await UpdateAccountBalanceFromEntryAsync(entry);
			return entry;
		}

		public async Task<IEnumerable<GeneralLedgerEntry>> CreateJournalEntriesAsync(IEnumerable<GeneralLedgerEntry> entries)
		{
			var entryList = entries.ToList();

			if (!await IsValidJournalEntryAsync(entryList))
				throw new InvalidOperationException("Journal entry is not balanced - debits must equal credits");

			foreach (var entry in entryList)
			{
				entry.CreatedDate = DateTime.Now;
				_context.GeneralLedgerEntries.Add(entry);
			}

			await _context.SaveChangesAsync();

			foreach (var entry in entryList)
				await UpdateAccountBalanceFromEntryAsync(entry);

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
				return $"{datePrefix}-001";

			var lastNumber = lastEntry.TransactionNumber.Split('-').LastOrDefault();
			if (int.TryParse(lastNumber, out var number))
				return $"{datePrefix}-{(number + 1):D3}";

			return $"{datePrefix}-001";
		}

		// ============= Ledger Queries =============

		public async Task<IEnumerable<GeneralLedgerEntry>> GetAccountLedgerEntriesAsync(
			string accountCode, DateTime? startDate = null, DateTime? endDate = null)
		{
			var account = await GetAccountByCodeAsync(accountCode);
			if (account == null) return new List<GeneralLedgerEntry>();

			var query = _context.GeneralLedgerEntries
				.Include(e => e.Account)
				.Where(e => e.AccountId == account.Id);

			if (startDate.HasValue) query = query.Where(e => e.TransactionDate >= startDate.Value);
			if (endDate.HasValue)   query = query.Where(e => e.TransactionDate <= endDate.Value);

			return await query
				.OrderByDescending(e => e.TransactionDate)
				.ThenBy(e => e.TransactionNumber)
				.ToListAsync();
		}

		public async Task<IEnumerable<GeneralLedgerEntry>> GetAllLedgerEntriesAsync(
			DateTime? startDate = null, DateTime? endDate = null)
		{
			IQueryable<GeneralLedgerEntry> query = _context.GeneralLedgerEntries.Include(e => e.Account);

			if (startDate.HasValue) query = query.Where(e => e.TransactionDate >= startDate.Value);
			if (endDate.HasValue)   query = query.Where(e => e.TransactionDate <= endDate.Value);

			return await query
				.OrderByDescending(e => e.TransactionDate)
				.ThenBy(e => e.TransactionNumber)
				.ToListAsync();
		}

		public async Task<IEnumerable<GeneralLedgerEntry>> GetAllLedgerEntriesWithEnhancedReferencesAsync(
			DateTime? startDate = null, DateTime? endDate = null)
		{
			IQueryable<GeneralLedgerEntry> query = _context.GeneralLedgerEntries.Include(e => e.Account);

			if (startDate.HasValue) query = query.Where(e => e.TransactionDate >= startDate.Value);
			if (endDate.HasValue)   query = query.Where(e => e.TransactionDate <= endDate.Value);

			var entries = await query
				.OrderByDescending(e => e.TransactionDate)
				.ThenBy(e => e.TransactionNumber)
				.ToListAsync();

			foreach (var entry in entries.Where(e => e.HasReference))
				await EnhanceEntryWithReferenceInfoAsync(entry);

			return entries;
		}

		public async Task<(string displayText, string? url, string icon)> GetEnhancedReferenceInfoAsync(
			string? referenceType, int? referenceId)
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
							"fas fa-shopping-cart text-success");

					case "purchase":
						var purchase = await _context.Purchases.FindAsync(referenceId.Value);
						return (
							$"Purchase {purchase?.PurchaseOrderNumber ?? $"#{referenceId}"}",
							$"/Purchases/Details/{referenceId}",
							"fas fa-shopping-bag text-primary");

					case "customerpayment":
						var payment = await _context.CustomerPayments
							.Include(p => p.Sale)
							.FirstOrDefaultAsync(p => p.Id == referenceId.Value);
						return (
							$"Payment for {payment?.Sale?.SaleNumber ?? $"#{referenceId}"}",
							$"/Sales/Details/{payment?.SaleId}",
							"fas fa-credit-card text-success");

					default:
						return ($"{referenceType} #{referenceId}", null, "fas fa-link text-muted");
				}
			}
			catch
			{
				return ($"{referenceType} #{referenceId}", null, "fas fa-link text-muted");
			}
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
						if (sale != null) entry.EnhancedReferenceText = $"Sale {sale.SaleNumber}";
						break;

					case "purchase":
						var purchase = await _context.Purchases.FindAsync(entry.ReferenceId!.Value);
						if (purchase != null)
							entry.EnhancedReferenceText = $"Purchase {purchase.PurchaseOrderNumber ?? $"#{purchase.Id}"}";
						break;

					case "customerpayment":
						var payment = await _context.CustomerPayments
							.Include(p => p.Sale)
							.FirstOrDefaultAsync(p => p.Id == entry.ReferenceId!.Value);
						if (payment?.Sale != null)
						{
							entry.EnhancedReferenceText = $"Payment for {payment.Sale.SaleNumber}";
							entry.EnhancedReferenceUrl  = $"/Sales/Details/{payment.SaleId}";
						}
						break;
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to enhance reference info for entry {EntryId}", entry.Id);
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
			var query = _context.GeneralLedgerEntries.Where(e => e.AccountId == accountId);

			if (asOfDate.HasValue)
				query = query.Where(e => e.TransactionDate <= asOfDate.Value);

			var entries = await query.ToListAsync();
			var totalDebits  = entries.Sum(e => e.DebitAmount);
			var totalCredits = entries.Sum(e => e.CreditAmount);

			var account = await GetAccountByIdAsync(accountId);
			return account != null && account.IsDebitAccount
				? totalDebits - totalCredits
				: totalCredits - totalDebits;
		}

		public async Task UpdateAccountBalanceAsync(string accountCode, decimal amount, bool isDebit = true)
		{
			var account = await GetAccountByCodeAsync(accountCode);
			if (account == null) return;

			account.CurrentBalance += account.IsDebitAccount
				? (isDebit ? amount : -amount)
				: (isDebit ? -amount : amount);

			account.LastTransactionDate = DateTime.Now;
			await _context.SaveChangesAsync();
		}

		private async Task UpdateAccountBalanceFromEntryAsync(GeneralLedgerEntry entry)
		{
			var account = await GetAccountByIdAsync(entry.AccountId);
			if (account == null) return;

			account.CurrentBalance += account.IsDebitAccount
				? entry.DebitAmount - entry.CreditAmount
				: entry.CreditAmount - entry.DebitAmount;

			account.LastTransactionDate = entry.TransactionDate;
			await _context.SaveChangesAsync();
		}

		public async Task RecalculateAccountBalanceAsync(string accountCode)
		{
			var account = await GetAccountByCodeAsync(accountCode);
			if (account == null) return;

			var entries = await _context.GeneralLedgerEntries
				.Where(e => e.AccountId == account.Id)
				.ToListAsync();

			var totalDebits  = entries.Sum(e => e.DebitAmount);
			var totalCredits = entries.Sum(e => e.CreditAmount);

			account.CurrentBalance = account.IsDebitAccount
				? totalDebits - totalCredits
				: totalCredits - totalDebits;

			account.LastTransactionDate = entries.Any() ? entries.Max(e => e.TransactionDate) : DateTime.Now;
			await _context.SaveChangesAsync();
		}

		public async Task RecalculateAllAccountBalancesAsync()
		{
			var accounts = await GetAllAccountsAsync();
			foreach (var account in accounts)
				await RecalculateAccountBalanceAsync(account.AccountCode);
		}

		// ============= Validation =============

		public async Task<bool> IsValidJournalEntryAsync(IEnumerable<GeneralLedgerEntry> entries)
		{
			var entryList = entries.ToList();
			if (!entryList.Any()) return false;

			var totalDebits  = entryList.Sum(e => e.DebitAmount);
			var totalCredits = entryList.Sum(e => e.CreditAmount);

			return Math.Abs(totalDebits - totalCredits) < 0.01m;
		}

		// ============= Manual Journal Entries =============

		public async Task<bool> CreateManualJournalEntryAsync(ManualJournalEntryViewModel model)
		{
			try
			{
				var journalNumber = await GenerateNextJournalNumberAsync("JE-MAN");
				var entries = new List<GeneralLedgerEntry>();

				foreach (var line in model.JournalEntries.Where(e => e.AccountId > 0))
				{
					var account = await GetAccountByIdAsync(line.AccountId);
					if (account == null)
					{
						_logger.LogError("Account {AccountId} not found for manual journal entry", line.AccountId);
						throw new InvalidOperationException($"Account with ID {line.AccountId} not found");
					}

					entries.Add(new GeneralLedgerEntry
					{
						TransactionDate   = model.TransactionDate,
						TransactionNumber = journalNumber,
						AccountId         = line.AccountId,
						Description       = !string.IsNullOrWhiteSpace(line.LineDescription)
							? line.LineDescription
							: model.Description ?? "Manual journal entry",
						DebitAmount  = line.DebitAmount  ?? 0,
						CreditAmount = line.CreditAmount ?? 0,
						ReferenceType = "ManualJournalEntry",
						ReferenceId   = null,
						CreatedBy     = "Manual Entry",
						CreatedDate   = DateTime.Now
					});
				}

				var totalDebits  = entries.Sum(e => e.DebitAmount);
				var totalCredits = entries.Sum(e => e.CreditAmount);

				if (Math.Abs(totalDebits - totalCredits) > 0.01m)
					throw new InvalidOperationException(
						$"Journal entry is not balanced. Debits: {totalDebits:C}, Credits: {totalCredits:C}");

				await CreateJournalEntriesAsync(entries);

				_logger.LogInformation(
					"Created manual journal entry {JournalNumber} with {EntryCount} lines. Reference: {Reference}",
					journalNumber, entries.Count, model.ReferenceNumber);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating manual journal entry");
				throw;
			}
		}

		public async Task<List<GeneralLedgerEntry>> GetManualJournalEntriesAsync(
			DateTime? startDate = null, DateTime? endDate = null)
		{
			try
			{
				var query = _context.GeneralLedgerEntries
					.Include(e => e.Account)
					.Where(e => e.ReferenceType == "ManualJournalEntry");

				if (startDate.HasValue) query = query.Where(e => e.TransactionDate >= startDate.Value);
				if (endDate.HasValue)   query = query.Where(e => e.TransactionDate <= endDate.Value);

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
					.Where(e => e.TransactionNumber == transactionNumber &&
								e.ReferenceType == "ManualJournalEntry")
					.ToListAsync();

				if (!originalEntries.Any())
					throw new InvalidOperationException($"Manual journal entry {transactionNumber} not found");

				var reversalNumber  = await GenerateNextJournalNumberAsync("JE-REV");
				var reversalEntries = originalEntries.Select(orig => new GeneralLedgerEntry
				{
					TransactionDate   = DateTime.Today,
					TransactionNumber = reversalNumber,
					AccountId         = orig.AccountId,
					Description       = $"REVERSAL: {reason} (Original: {orig.TransactionNumber})",
					DebitAmount       = orig.CreditAmount, // swap
					CreditAmount      = orig.DebitAmount,  // swap
					ReferenceType     = "ManualJournalEntryReversal",
					ReferenceId       = null,
					CreatedBy         = "System Reversal",
					CreatedDate       = DateTime.Now
				}).ToList();

				await CreateJournalEntriesAsync(reversalEntries);

				_logger.LogInformation(
					"Created reversal journal entry {ReversalNumber} for original entry {OriginalNumber}. Reason: {Reason}",
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
				return await _context.GeneralLedgerEntries
					.Where(e => e.ReferenceType == "ManualJournalEntry" &&
								e.TransactionDate >= startDate &&
								e.TransactionDate <= endDate)
					.SumAsync(e => e.DebitAmount);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating manual journal entries total");
				return 0;
			}
		}
	}
}
