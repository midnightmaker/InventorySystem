// Services/AccountingService.CashFlowAnalysis.cs
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.ViewModels.Accounting;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
	public partial class AccountingService
	{
		// ============= Enhanced Cash Flow Analysis =============

		public async Task<EnhancedCashFlowAnalysisViewModel> GetEnhancedCashFlowAnalysisAsync(
			DateTime startDate, DateTime endDate, bool includePriorPeriod = true)
		{
			try
			{
				var currentPeriod = await GetCashFlowStatementAsync(startDate, endDate);
				var analysis = new EnhancedCashFlowAnalysisViewModel { CurrentPeriod = currentPeriod };

				if (includePriorPeriod)
				{
					var periodLength   = endDate - startDate;
					var priorStartDate = startDate.Subtract(periodLength);
					var priorEndDate   = startDate.AddDays(-1);
					analysis.PriorPeriod = await GetCashFlowStatementAsync(priorStartDate, priorEndDate);
				}

				analysis.WorkingCapitalAnalysis = await GetWorkingCapitalAnalysisAsync(startDate, endDate);
				analysis.FreeCashFlow           = await GetFreeCashFlowAnalysisAsync(startDate, endDate);

				await CalculateCashEfficiencyMetrics(analysis, startDate, endDate);

				analysis.CashFlowRatios = await CalculateCashFlowRatios(analysis.CurrentPeriod);
				analysis.MonthlyTrends  = await GetMonthlyCashFlowTrendsAsync(12);

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
				var projection    = new CashFlowProjectionViewModel();
				var historicalData = await GetMonthlyCashFlowTrendsAsync(12);

				var avgOperating  = historicalData.Any() ? historicalData.Average(h => h.OperatingCashFlow)  : 0;
				var avgInvesting  = historicalData.Any() ? historicalData.Average(h => h.InvestingCashFlow)  : 0;
				var avgFinancing  = historicalData.Any() ? historicalData.Average(h => h.FinancingCashFlow)  : 0;

				var currentCashBalance = await GetAccountBalanceAsync("1000");

				for (int i = 1; i <= projectionMonths; i++)
				{
					projection.Projections.Add(new MonthlyProjection
					{
						Month                        = DateTime.Today.AddMonths(i),
						ProjectedOperatingCashFlow   = avgOperating,
						ProjectedInvestingCashFlow   = avgInvesting,
						ProjectedFinancingCashFlow   = avgFinancing,
						ProjectedNetCashFlow         = avgOperating + avgInvesting + avgFinancing,
						ProjectedCashBalance         = currentCashBalance +
						                               (avgOperating + avgInvesting + avgFinancing) * i,
						ConfidenceLevel              = Math.Max(30, 90 - (i * 5))
					});
				}

				projection.OptimisticScenario  = CreateProjectionScenario("Optimistic",  projection.Projections, 1.2m);
				projection.MostLikelyScenario  = CreateProjectionScenario("Most Likely", projection.Projections, 1.0m);
				projection.PessimisticScenario = CreateProjectionScenario("Pessimistic", projection.Projections, 0.8m);

				return projection;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting cash flow projections");
				return new CashFlowProjectionViewModel();
			}
		}

		public async Task<WorkingCapitalAnalysisViewModel> GetWorkingCapitalAnalysisAsync(
			DateTime startDate, DateTime endDate)
		{
			try
			{
				var analysis = new WorkingCapitalAnalysisViewModel();

				var currentAR                = await GetAccountBalanceAsync("1100", endDate);
				var currentInventory         = await GetAccountBalanceAsync("1220", endDate);
				var currentAP                = await GetAccountBalanceAsync("2000", endDate);
				var currentAccruedLiabilities = await GetAccountBalanceAsync("2100", endDate);

				analysis.CurrentWorkingCapital = currentAR + currentInventory - currentAP - currentAccruedLiabilities;

				var priorAR                = await GetAccountBalanceAsync("1100", startDate.AddDays(-1));
				var priorInventory         = await GetAccountBalanceAsync("1220", startDate.AddDays(-1));
				var priorAP                = await GetAccountBalanceAsync("2000", startDate.AddDays(-1));
				var priorAccruedLiabilities = await GetAccountBalanceAsync("2100", startDate.AddDays(-1));

				analysis.PriorWorkingCapital = priorAR + priorInventory - priorAP - priorAccruedLiabilities;

				analysis.AccountsReceivableChange   = currentAR       - priorAR;
				analysis.InventoryChange            = currentInventory - priorInventory;
				analysis.AccountsPayableChange      = currentAP       - priorAP;
				analysis.AccruedLiabilitiesChange   = currentAccruedLiabilities - priorAccruedLiabilities;

				var sales = await GetRevenueForPeriod(startDate, endDate);
				analysis.WorkingCapitalTurnover    = sales > 0 ? sales / Math.Max(analysis.CurrentWorkingCapital, 1) : 0;
				analysis.WorkingCapitalToSalesRatio = sales > 0 ? analysis.CurrentWorkingCapital / sales : 0;

				analysis.Components = new List<WorkingCapitalComponent>
				{
					new WorkingCapitalComponent
					{
						ComponentName    = "Accounts Receivable",
						BeginningBalance = priorAR,
						EndingBalance    = currentAR,
						ComponentType    = "Asset",
						IsImprovement    = currentAR < priorAR
					},
					new WorkingCapitalComponent
					{
						ComponentName    = "Inventory",
						BeginningBalance = priorInventory,
						EndingBalance    = currentInventory,
						ComponentType    = "Asset",
						IsImprovement    = currentInventory < priorInventory
					},
					new WorkingCapitalComponent
					{
						ComponentName    = "Accounts Payable",
						BeginningBalance = priorAP,
						EndingBalance    = currentAP,
						ComponentType    = "Liability",
						IsImprovement    = currentAP > priorAP
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

		public async Task<CustomerCashFlowAnalysisViewModel> GetCustomerCashFlowAnalysisAsync(
			DateTime startDate, DateTime endDate)
		{
			try
			{
				var analysis = new CustomerCashFlowAnalysisViewModel();

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
					var customer          = group.Key;
					var payments          = group.ToList();
					var totalCollections  = payments.Sum(p => p.Amount);
					var avgCollectionDays = await CalculateAverageCollectionDays(customer.Id, startDate, endDate);
					var collectionEff     = await CalculateCollectionEfficiency(customer.Id, startDate, endDate);

					analysis.CustomerCashFlows.Add(new CustomerCashFlow
					{
						CustomerId           = customer.Id,
						CustomerName         = customer.CustomerName,
						NetCashFlow          = totalCollections,
						Collections          = totalCollections,
						AverageCollectionDays = avgCollectionDays,
						CollectionEfficiency = collectionEff,
						OutstandingBalance   = customer.OutstandingBalance,
						CreditLimit          = customer.CreditLimit,
						LastPaymentDate      = payments.Max(p => p.PaymentDate),
						PaymentTrend         = DeterminePaymentTrend(payments)
					});
				}

				analysis.TotalCollections       = analysis.CustomerCashFlows.Sum(c => c.Collections);
				analysis.AverageCollectionPeriod = analysis.CustomerCashFlows.Any()
					? analysis.CustomerCashFlows.Average(c => c.AverageCollectionDays) : 0;
				analysis.CollectionEfficiency   = analysis.CustomerCashFlows.Any()
					? analysis.CustomerCashFlows.Average(c => c.CollectionEfficiency) : 0;

				return analysis;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting customer cash flow analysis");
				return new CustomerCashFlowAnalysisViewModel();
			}
		}

		public async Task<FreeCashFlowAnalysisViewModel> GetFreeCashFlowAnalysisAsync(
			DateTime startDate, DateTime endDate)
		{
			try
			{
				var analysis            = new FreeCashFlowAnalysisViewModel();
				var cashFlowStatement   = await GetCashFlowStatementAsync(startDate, endDate);
				analysis.OperatingCashFlow = cashFlowStatement.NetCashFromOperations;

				var capitalExpenditures         = await GetCapitalExpenditures(startDate, endDate);
				analysis.TotalCapitalExpenditures = capitalExpenditures.Sum(c => c.Amount);
				analysis.CapitalExpenditureDetails = capitalExpenditures;

				var revenue = await GetRevenueForPeriod(startDate, endDate);
				analysis.FreeCashFlowMargin  = revenue > 0 ? (analysis.FreeCashFlow / revenue) * 100 : 0;
				analysis.FreeCashFlowYield   = analysis.FreeCashFlow > 0
					? (analysis.FreeCashFlow / Math.Max(revenue, 1)) * 100 : 0;
				analysis.CashFlowAdequacyRatio = analysis.FreeCashFlow /
				                                  Math.Max(analysis.TotalCapitalExpenditures, 1);

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
				var trends  = new List<MonthlyCashFlowTrend>();
				var endDate = DateTime.Today;

				for (int i = months - 1; i >= 0; i--)
				{
					var monthStart = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(-i);
					var monthEnd   = monthStart.AddMonths(1).AddDays(-1);

					var operatingCashFlow  = await CalculateOperatingCashFlowForPeriod(monthStart, monthEnd);
					var investingCashFlow  = await CalculateInvestingCashFlowForPeriod(monthStart, monthEnd);
					var financingCashFlow  = await CalculateFinancingCashFlowForPeriod(monthStart, monthEnd);

					var trend = new MonthlyCashFlowTrend
					{
						Month               = monthStart,
						OperatingCashFlow   = operatingCashFlow,
						InvestingCashFlow   = investingCashFlow,
						FinancingCashFlow   = financingCashFlow,
						NetCashFlow         = operatingCashFlow + investingCashFlow + financingCashFlow,
						EndingCashBalance   = await GetAccountBalanceAsync("1000", monthEnd)
					};

					if (trends.Any())
					{
						var prev = trends.Last();
						trend.CashFlowGrowthRate = prev.NetCashFlow != 0
							? ((trend.NetCashFlow - prev.NetCashFlow) / Math.Abs(prev.NetCashFlow)) * 100
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

		// ============= Cash Flow Helper Methods =============

		private async Task CalculateCashEfficiencyMetrics(
			EnhancedCashFlowAnalysisViewModel analysis, DateTime startDate, DateTime endDate)
		{
			var days    = (endDate - startDate).Days;
			var revenue = await GetRevenueForPeriod(startDate, endDate);
			var cogs    = await GetCOGSForPeriod(startDate, endDate);

			var avgAR        = (await GetAccountBalanceAsync("1100", startDate) + await GetAccountBalanceAsync("1100", endDate)) / 2;
			var avgInventory = (await GetAccountBalanceAsync("1220", startDate) + await GetAccountBalanceAsync("1220", endDate)) / 2;
			var avgAP        = (await GetAccountBalanceAsync("2000", startDate) + await GetAccountBalanceAsync("2000", endDate)) / 2;

			analysis.DaysInAccountsReceivable = revenue > 0 ? (avgAR        / revenue) * days : 0;
			analysis.DaysInInventory          = cogs    > 0 ? (avgInventory / cogs)    * days : 0;
			analysis.DaysInAccountsPayable    = cogs    > 0 ? (avgAP        / cogs)    * days : 0;

			analysis.CashConversionCycle =
				analysis.DaysInAccountsReceivable +
				analysis.DaysInInventory           -
				analysis.DaysInAccountsPayable;
		}

		private async Task<List<CashFlowRatio>> CalculateCashFlowRatios(CashFlowStatementViewModel cashFlow)
		{
			var ratios = new List<CashFlowRatio>();

			var operatingRatio = cashFlow.NetIncome != 0
				? cashFlow.NetCashFromOperations / cashFlow.NetIncome : 0;

			ratios.Add(new CashFlowRatio
			{
				RatioName       = "Operating Cash Flow Ratio",
				Value           = operatingRatio,
				FormattedValue  = $"{operatingRatio:F2}",
				Interpretation  = "Measures quality of earnings",
				Benchmark       = operatingRatio > 1 ? RatioBenchmark.Good : RatioBenchmark.Average
			});

			var revenue        = await GetRevenueForPeriod(cashFlow.StartDate, cashFlow.EndDate);
			var cashFlowMargin = revenue > 0 ? (cashFlow.NetCashFromOperations / revenue) * 100 : 0;

			ratios.Add(new CashFlowRatio
			{
				RatioName      = "Cash Flow Margin",
				Value          = cashFlowMargin,
				FormattedValue = $"{cashFlowMargin:F1}%",
				Interpretation = "Operating cash flow as % of revenue",
				Benchmark      = cashFlowMargin > 15 ? RatioBenchmark.Excellent :
				                 cashFlowMargin > 10 ? RatioBenchmark.Good      :
				                 cashFlowMargin >  5 ? RatioBenchmark.Average   : RatioBenchmark.Poor
			});

			return ratios;
		}

		private ProjectionScenario CreateProjectionScenario(
			string scenarioName, List<MonthlyProjection> baseProjections, decimal multiplier)
		{
			var scenario = new ProjectionScenario
			{
				ScenarioName     = scenarioName,
				ProbabilityPercent = scenarioName switch
				{
					"Optimistic"  => 20,
					"Most Likely" => 60,
					"Pessimistic" => 20,
					_             => 33
				}
			};

			scenario.Projections = baseProjections.Select(p => new MonthlyProjection
			{
				Month                      = p.Month,
				ProjectedOperatingCashFlow = p.ProjectedOperatingCashFlow * multiplier,
				ProjectedInvestingCashFlow = p.ProjectedInvestingCashFlow * multiplier,
				ProjectedFinancingCashFlow = p.ProjectedFinancingCashFlow * multiplier,
				ProjectedNetCashFlow       = p.ProjectedNetCashFlow       * multiplier,
				ProjectedCashBalance       = p.ProjectedCashBalance       * multiplier,
				ConfidenceLevel            = p.ConfidenceLevel
			}).ToList();

			scenario.TotalProjectedCashFlow = scenario.Projections.Sum(p => p.ProjectedNetCashFlow);
			scenario.MinimumCashBalance     = scenario.Projections.Min(p => p.ProjectedCashBalance);

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

		private async Task<decimal> CalculateOperatingCashFlowForPeriod(DateTime startDate, DateTime endDate)
		{
			var revenue  = await GetRevenueForPeriod(startDate, endDate);
			var expenses = await GetExpensesForPeriod(startDate, endDate);
			return revenue - expenses;
		}

		private async Task<decimal> CalculateInvestingCashFlowForPeriod(DateTime startDate, DateTime endDate)
		{
			// Placeholder — would analyse fixed asset purchases/sales
			await Task.CompletedTask;
			return 0;
		}

		private async Task<decimal> CalculateFinancingCashFlowForPeriod(DateTime startDate, DateTime endDate)
		{
			// Placeholder — would analyse debt/equity transactions
			await Task.CompletedTask;
			return 0;
		}

		private async Task<List<CapitalExpenditureDetail>> GetCapitalExpenditures(
			DateTime startDate, DateTime endDate)
		{
			// Placeholder — would look at fixed asset account movements
			await Task.CompletedTask;
			return new List<CapitalExpenditureDetail>();
		}

		private async Task<decimal> CalculateAverageCollectionDays(
			int customerId, DateTime startDate, DateTime endDate)
		{
			await Task.CompletedTask;
			return 30;
		}

		private async Task<decimal> CalculateCollectionEfficiency(
			int customerId, DateTime startDate, DateTime endDate)
		{
			await Task.CompletedTask;
			return 85;
		}

		private string DeterminePaymentTrend(List<CustomerPayment> payments)
		{
			if (payments.Count < 2) return "Insufficient Data";

			var recent = payments.OrderByDescending(p => p.PaymentDate).Take(3).Sum(p => p.Amount);
			var older  = payments.OrderByDescending(p => p.PaymentDate).Skip(3).Take(3).Sum(p => p.Amount);

			if (recent > older * 1.1m) return "Improving";
			if (recent < older * 0.9m) return "Declining";
			return "Stable";
		}
	}
}
