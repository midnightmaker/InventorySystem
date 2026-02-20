// Services/AccountingService.YearEndClosing.cs
using InventorySystem.Models.Accounting;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public partial class AccountingService
	{
		// ============= Year-End Closing =============

		public async Task<bool> PerformYearEndClosingAsync(
			FinancialPeriod financialPeriod, string closingNotes, string? createdBy = null)
		{
			using var transaction = await _context.Database.BeginTransactionAsync();

			try
			{
				_logger.LogInformation("Starting year-end closing for period {PeriodName} ({PeriodId})",
					financialPeriod.PeriodName, financialPeriod.Id);

				var validation = await ValidateYearEndClosingAsync(financialPeriod);
				if (!validation.IsValid)
				{
					_logger.LogError("Year-end closing validation failed: {Errors}",
						string.Join(", ", validation.Errors));
					return false;
				}

				var revenueClosingTxn          = await GenerateNextJournalNumberAsync("CLOSE-REV");
				var expenseClosingTxn          = await GenerateNextJournalNumberAsync("CLOSE-EXP");
				var retainedEarningsTransferTxn = await GenerateNextJournalNumberAsync("CLOSE-RE");

				var revenueEntries          = await CreateRevenueClosingEntriesAsync(financialPeriod, revenueClosingTxn, createdBy);
				var expenseEntries          = await CreateExpenseClosingEntriesAsync(financialPeriod, expenseClosingTxn, createdBy);
				var retainedEarningsEntries = await CreateRetainedEarningsTransferAsync(financialPeriod, retainedEarningsTransferTxn, createdBy);

				var allEntries = revenueEntries.Concat(expenseEntries).Concat(retainedEarningsEntries);
				_context.GeneralLedgerEntries.AddRange(allEntries);
				await _context.SaveChangesAsync();

				await transaction.CommitAsync();

				_logger.LogInformation(
					"Year-end closing completed for period {PeriodName}. " +
					"Revenue entries: {R}, Expense entries: {E}, Transfer entries: {T}",
					financialPeriod.PeriodName,
					revenueEntries.Count, expenseEntries.Count, retainedEarningsEntries.Count);

				return true;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError(ex, "Error performing year-end closing for period {PeriodId}", financialPeriod.Id);
				return false;
			}
		}

		public async Task<List<GeneralLedgerEntry>> CreateRevenueClosingEntriesAsync(
			FinancialPeriod financialPeriod, string transactionNumber, string? createdBy = null)
		{
			var entries = new List<GeneralLedgerEntry>();

			var revenueAccounts = await _context.Accounts
				.Where(a => a.AccountType == AccountType.Revenue && a.IsActive)
				.ToListAsync();

			var currentYearEarnings = await GetAccountByCodeAsync("3200")
				?? throw new InvalidOperationException("Current Year Earnings account (3200) not found");

			decimal totalRevenue = 0;

			foreach (var account in revenueAccounts)
			{
				var balance = await GetAccountBalanceAsync(account.AccountCode, financialPeriod.EndDate);
				if (balance <= 0.01m) continue;

				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate   = financialPeriod.EndDate,
					AccountId         = account.Id,
					Description       = $"Year-end closing: Close {account.AccountName} to Current Year Earnings",
					DebitAmount       = balance,
					CreditAmount      = 0,
					ReferenceType     = "YearEndClosing",
					ReferenceId       = financialPeriod.Id,
					CreatedBy         = createdBy ?? "System",
					CreatedDate       = DateTime.Now
				});

				totalRevenue += balance;
			}

			if (totalRevenue > 0)
			{
				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate   = financialPeriod.EndDate,
					AccountId         = currentYearEarnings.Id,
					Description       = $"Year-end closing: Transfer revenue to Current Year Earnings ({financialPeriod.PeriodName})",
					DebitAmount       = 0,
					CreditAmount      = totalRevenue,
					ReferenceType     = "YearEndClosing",
					ReferenceId       = financialPeriod.Id,
					CreatedBy         = createdBy ?? "System",
					CreatedDate       = DateTime.Now
				});
			}

			return entries;
		}

		public async Task<List<GeneralLedgerEntry>> CreateExpenseClosingEntriesAsync(
			FinancialPeriod financialPeriod, string transactionNumber, string? createdBy = null)
		{
			var entries = new List<GeneralLedgerEntry>();

			var expenseAccounts = await _context.Accounts
				.Where(a => a.AccountType == AccountType.Expense && a.IsActive)
				.ToListAsync();

			var currentYearEarnings = await GetAccountByCodeAsync("3200")
				?? throw new InvalidOperationException("Current Year Earnings account (3200) not found");

			decimal totalExpenses = 0;

			foreach (var account in expenseAccounts)
			{
				var balance = await GetAccountBalanceAsync(account.AccountCode, financialPeriod.EndDate);
				if (balance <= 0.01m) continue;

				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate   = financialPeriod.EndDate,
					AccountId         = account.Id,
					Description       = $"Year-end closing: Close {account.AccountName} to Current Year Earnings",
					DebitAmount       = 0,
					CreditAmount      = balance,
					ReferenceType     = "YearEndClosing",
					ReferenceId       = financialPeriod.Id,
					CreatedBy         = createdBy ?? "System",
					CreatedDate       = DateTime.Now
				});

				totalExpenses += balance;
			}

			if (totalExpenses > 0)
			{
				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate   = financialPeriod.EndDate,
					AccountId         = currentYearEarnings.Id,
					Description       = $"Year-end closing: Transfer expenses to Current Year Earnings ({financialPeriod.PeriodName})",
					DebitAmount       = totalExpenses,
					CreditAmount      = 0,
					ReferenceType     = "YearEndClosing",
					ReferenceId       = financialPeriod.Id,
					CreatedBy         = createdBy ?? "System",
					CreatedDate       = DateTime.Now
				});
			}

			return entries;
		}

		public async Task<List<GeneralLedgerEntry>> CreateRetainedEarningsTransferAsync(
			FinancialPeriod financialPeriod, string transactionNumber, string? createdBy = null)
		{
			var entries = new List<GeneralLedgerEntry>();

			var currentYearEarnings = await GetAccountByCodeAsync("3200")
				?? throw new InvalidOperationException("Current Year Earnings account (3200) not found");

			var retainedEarnings = await GetAccountByCodeAsync("3100")
				?? throw new InvalidOperationException("Retained Earnings account (3100) not found");

			var balance = await GetAccountBalanceAsync("3200", financialPeriod.EndDate);
			if (Math.Abs(balance) <= 0.01m) return entries;

			if (balance > 0) // Profit
			{
				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate   = financialPeriod.EndDate,
					AccountId         = currentYearEarnings.Id,
					Description       = $"Year-end closing: Transfer profit to Retained Earnings ({financialPeriod.PeriodName})",
					DebitAmount       = balance,
					CreditAmount      = 0,
					ReferenceType     = "YearEndClosing",
					ReferenceId       = financialPeriod.Id,
					CreatedBy         = createdBy ?? "System",
					CreatedDate       = DateTime.Now
				});

				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate   = financialPeriod.EndDate,
					AccountId         = retainedEarnings.Id,
					Description       = $"Year-end closing: Net income transfer from {financialPeriod.PeriodName}",
					DebitAmount       = 0,
					CreditAmount      = balance,
					ReferenceType     = "YearEndClosing",
					ReferenceId       = financialPeriod.Id,
					CreatedBy         = createdBy ?? "System",
					CreatedDate       = DateTime.Now
				});
			}
			else // Loss
			{
				var lossAmount = Math.Abs(balance);

				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate   = financialPeriod.EndDate,
					AccountId         = currentYearEarnings.Id,
					Description       = $"Year-end closing: Transfer loss to Retained Earnings ({financialPeriod.PeriodName})",
					DebitAmount       = 0,
					CreditAmount      = lossAmount,
					ReferenceType     = "YearEndClosing",
					ReferenceId       = financialPeriod.Id,
					CreatedBy         = createdBy ?? "System",
					CreatedDate       = DateTime.Now
				});

				entries.Add(new GeneralLedgerEntry
				{
					TransactionNumber = transactionNumber,
					TransactionDate   = financialPeriod.EndDate,
					AccountId         = retainedEarnings.Id,
					Description       = $"Year-end closing: Net loss transfer from {financialPeriod.PeriodName}",
					DebitAmount       = lossAmount,
					CreditAmount      = 0,
					ReferenceType     = "YearEndClosing",
					ReferenceId       = financialPeriod.Id,
					CreatedBy         = createdBy ?? "System",
					CreatedDate       = DateTime.Now
				});
			}

			return entries;
		}

		public async Task<decimal> CalculateNetIncomeAsync(DateTime startDate, DateTime endDate)
		{
			var revenueAccounts = await _context.Accounts
				.Where(a => a.AccountType == AccountType.Revenue && a.IsActive)
				.ToListAsync();

			decimal totalRevenue = 0;
			foreach (var account in revenueAccounts)
				totalRevenue += await GetAccountBalanceForPeriodAsync(account.AccountCode, startDate, endDate);

			var expenseAccounts = await _context.Accounts
				.Where(a => a.AccountType == AccountType.Expense && a.IsActive)
				.ToListAsync();

			decimal totalExpenses = 0;
			foreach (var account in expenseAccounts)
				totalExpenses += await GetAccountBalanceForPeriodAsync(account.AccountCode, startDate, endDate);

			return totalRevenue - totalExpenses;
		}

		public async Task<YearEndValidationResult> ValidateYearEndClosingAsync(FinancialPeriod financialPeriod)
		{
			var result = new YearEndValidationResult { IsValid = true };

			try
			{
				if (financialPeriod.IsClosed)
				{
					result.Errors.Add("Financial period is already closed");
					result.IsValid = false;
				}

				if (await GetAccountByCodeAsync("3200") == null)
				{
					result.Errors.Add("Current Year Earnings account (3200) not found");
					result.IsValid = false;
				}

				if (await GetAccountByCodeAsync("3100") == null)
				{
					result.Errors.Add("Retained Earnings account (3100) not found");
					result.IsValid = false;
				}

				var ledgerEntries = await GetAllLedgerEntriesAsync(financialPeriod.StartDate, financialPeriod.EndDate);
				result.TotalDebits  = ledgerEntries.Sum(e => e.DebitAmount);
				result.TotalCredits = ledgerEntries.Sum(e => e.CreditAmount);
				result.TrialBalanceIsBalanced = Math.Abs(result.TotalDebits - result.TotalCredits) < 0.01m;

				if (!result.TrialBalanceIsBalanced)
				{
					result.Errors.Add(
						$"Trial balance is not balanced. Debits: {result.TotalDebits:C}, Credits: {result.TotalCredits:C}");
					result.IsValid = false;
				}

				result.NetIncome = await CalculateNetIncomeAsync(financialPeriod.StartDate, financialPeriod.EndDate);

				var endOfPeriod       = financialPeriod.EndDate.Date.AddDays(1).AddSeconds(-1);
				var futureTransactions = await _context.GeneralLedgerEntries
					.Where(e => e.TransactionDate > endOfPeriod)
					.CountAsync();

				if (futureTransactions > 0)
					result.Warnings.Add($"There are {futureTransactions} transactions dated after the period end");
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

			var revenueAccounts = await _context.Accounts
				.Where(a => a.AccountType == AccountType.Revenue && a.IsActive)
				.ToListAsync();

			foreach (var account in revenueAccounts)
			{
				var balance = await GetAccountBalanceAsync(account.AccountCode, financialPeriod.EndDate);
				if (balance <= 0.01m) continue;

				summary.RevenueAccounts.Add(new AccountClosingSummary
				{
					AccountCode = account.AccountCode,
					AccountName = account.AccountName,
					Balance     = balance
				});
				summary.TotalRevenue += balance;
			}

			var expenseAccounts = await _context.Accounts
				.Where(a => a.AccountType == AccountType.Expense && a.IsActive)
				.ToListAsync();

			foreach (var account in expenseAccounts)
			{
				var balance = await GetAccountBalanceAsync(account.AccountCode, financialPeriod.EndDate);
				if (balance <= 0.01m) continue;

				summary.ExpenseAccounts.Add(new AccountClosingSummary
				{
					AccountCode = account.AccountCode,
					AccountName = account.AccountName,
					Balance     = balance
				});
				summary.TotalExpenses += balance;
			}

			summary.NetIncome                    = summary.TotalRevenue - summary.TotalExpenses;
			summary.CurrentYearEarningsBalance   = await GetAccountBalanceAsync("3200", financialPeriod.EndDate);
			summary.RetainedEarningsBalanceBefore = await GetAccountBalanceAsync("3100", financialPeriod.EndDate);
			summary.RetainedEarningsBalanceAfter  = summary.RetainedEarningsBalanceBefore + summary.NetIncome;

			return summary;
		}

		// ============= Shared Period Helper =============

		private async Task<decimal> GetAccountBalanceForPeriodAsync(string accountCode, DateTime startDate, DateTime endDate)
		{
			var entries = await GetAccountLedgerEntriesAsync(accountCode, startDate, endDate);
			return entries.Sum(e => e.DebitAmount - e.CreditAmount);
		}
	}
}
