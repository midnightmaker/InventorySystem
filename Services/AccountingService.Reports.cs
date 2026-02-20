// Services/AccountingService.Reports.cs
using InventorySystem.Models.Accounting;
using InventorySystem.ViewModels.Accounting;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public partial class AccountingService
	{
		// ============= Financial Reports =============

		public async Task<TrialBalanceViewModel> GetTrialBalanceAsync(DateTime asOfDate)
		{
			var accounts = await GetActiveAccountsAsync();
			var entries  = new List<TrialBalanceEntry>();

			foreach (var account in accounts)
			{
				var balance = await GetAccountBalanceAsync(account.Id, asOfDate);
				if (balance == 0) continue;

				var entry = new TrialBalanceEntry
				{
					AccountCode = account.AccountCode,
					AccountName = account.AccountName,
					AccountType = account.AccountType
				};

				if (account.IsDebitAccount)
				{
					entry.DebitBalance  = Math.Max(0, balance);
					entry.CreditBalance = Math.Max(0, -balance);
				}
				else
				{
					entry.CreditBalance = Math.Max(0, balance);
					entry.DebitBalance  = Math.Max(0, -balance);
				}

				entries.Add(entry);
			}

			return new TrialBalanceViewModel { AsOfDate = asOfDate, Entries = entries };
		}

		public async Task<BalanceSheetViewModel> GetBalanceSheetAsync(DateTime asOfDate)
		{
			var accounts     = await GetActiveAccountsAsync();
			var balanceSheet = new BalanceSheetViewModel { AsOfDate = asOfDate };

			foreach (var account in accounts)
			{
				var balance = await GetAccountBalanceAsync(account.Id, asOfDate);
				if (balance == 0) continue;

				var bsAccount = new BalanceSheetAccount
				{
					AccountCode = account.AccountCode,
					AccountName = account.AccountName,
					Balance     = balance
				};

				switch (account.AccountType)
				{
					case AccountType.Asset:
						if (account.AccountSubType == AccountSubType.CurrentAsset ||
							account.AccountSubType == AccountSubType.InventoryAsset)
							balanceSheet.CurrentAssets.Add(bsAccount);
						else if (account.AccountSubType == AccountSubType.FixedAsset)
							balanceSheet.FixedAssets.Add(bsAccount);
						else
							balanceSheet.OtherAssets.Add(bsAccount);
						break;

					case AccountType.Liability:
						if (account.AccountSubType == AccountSubType.CurrentLiability)
							balanceSheet.CurrentLiabilities.Add(bsAccount);
						else
							balanceSheet.LongTermLiabilities.Add(bsAccount);
						break;

					case AccountType.Equity:
						balanceSheet.EquityAccounts.Add(bsAccount);
						break;
				}
			}

			return balanceSheet;
		}

		public async Task<IncomeStatementViewModel> GetIncomeStatementAsync(DateTime startDate, DateTime endDate)
		{
			var accounts         = await GetActiveAccountsAsync();
			var incomeStatement  = new IncomeStatementViewModel { StartDate = startDate, EndDate = endDate };

			foreach (var account in accounts)
			{
				var entries = await _context.GeneralLedgerEntries
					.Where(e => e.AccountId == account.Id &&
								e.TransactionDate >= startDate &&
								e.TransactionDate <= endDate)
					.ToListAsync();

				if (!entries.Any()) continue;

				var totalDebits  = entries.Sum(e => e.DebitAmount);
				var totalCredits = entries.Sum(e => e.CreditAmount);
				var netAmount    = account.AccountType == AccountType.Revenue
					? totalCredits - totalDebits
					: totalDebits  - totalCredits;

				if (netAmount == 0) continue;

				var isAccount = new IncomeStatementAccount
				{
					AccountCode = account.AccountCode,
					AccountName = account.AccountName,
					Amount      = Math.Abs(netAmount)
				};

				switch (account.AccountType)
				{
					case AccountType.Revenue:
						incomeStatement.RevenueAccounts.Add(isAccount);
						break;

					case AccountType.Expense:
						if (account.AccountSubType == AccountSubType.CostOfGoodsSold)
							incomeStatement.COGSAccounts.Add(isAccount);
						else if (account.AccountSubType == AccountSubType.OperatingExpense ||
								 account.AccountSubType == AccountSubType.UtilityExpense   ||
								 account.AccountSubType == AccountSubType.SubscriptionExpense)
							incomeStatement.OperatingExpenses.Add(isAccount);
						else
							incomeStatement.OtherExpenses.Add(isAccount);
						break;
				}
			}

			return incomeStatement;
		}

		public async Task<CashFlowStatementViewModel> GetCashFlowStatementAsync(DateTime startDate, DateTime endDate)
		{
			// Stub — see AccountingService.CashFlowAnalysis.cs for the enhanced analysis
			return new CashFlowStatementViewModel
			{
				StartDate            = startDate,
				EndDate              = endDate,
				NetIncome            = 0,
				BeginningCashBalance = await GetAccountBalanceAsync("1000", startDate.AddDays(-1))
			};
		}

		// ============= Dashboard =============

		public async Task<AccountingDashboardViewModel> GetAccountingDashboardAsync()
		{
			var dashboard = new AccountingDashboardViewModel
			{
				CashBalance              = await GetAccountBalanceAsync("1000"),
				AccountsReceivableBalance = await GetAccountBalanceAsync("1100"),
				AccountsPayableBalance   = await GetAccountBalanceAsync("2000"),
				TotalAccounts            = await _context.Accounts.CountAsync(),
				ActiveAccounts           = await _context.Accounts.CountAsync(a => a.IsActive)
			};

			dashboard.RecentTransactions = (await GetAllLedgerEntriesAsync(DateTime.Today.AddDays(-30)))
				.Take(10)
				.ToList();

			dashboard.UpcomingPayments = (await GetUnpaidAccountsPayableAsync())
				.Where(ap => ap.DueDate <= DateTime.Today.AddDays(30))
				.Take(10)
				.ToList();

			var accounts = await GetActiveAccountsAsync();
			foreach (var account in accounts)
			{
				var balance = await GetAccountBalanceAsync(account.Id);
				switch (account.AccountType)
				{
					case AccountType.Asset:
						dashboard.TotalAssets += balance;
						if (account.AccountCode.StartsWith("12"))
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

			var startOfMonth   = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
			var incomeStatement = await GetIncomeStatementAsync(startOfMonth, DateTime.Today);

			dashboard.MonthlyRevenue  = incomeStatement.TotalRevenue;
			dashboard.MonthlyExpenses = incomeStatement.TotalOperatingExpenses + incomeStatement.TotalOtherExpenses;
			dashboard.NetIncome       = dashboard.MonthlyRevenue - dashboard.MonthlyExpenses;

			return dashboard;
		}
	}
}
