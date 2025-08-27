// Controllers/AccountingController.cs
using InventorySystem.Data;
using InventorySystem.Domain.Enums;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels.Accounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
	public class AccountingController : Controller
	{
		private readonly IAccountingService _accountingService;
		private readonly IFinancialPeriodService _financialPeriodService; // ADD THIS
		private readonly InventoryContext _context;
		private readonly ILogger<AccountingController> _logger;

		public AccountingController(
			IAccountingService accountingService, 
			IFinancialPeriodService financialPeriodService, // ADD THIS PARAMETER
			InventoryContext context, 
			ILogger<AccountingController> logger)
		{
			_accountingService = accountingService;
			_financialPeriodService = financialPeriodService; // ADD THIS
			_context = context;
			_logger = logger;
		}

		[HttpGet]
		public async Task<IActionResult> DebugCustomerBalance(int customerId)
		{
			try
			{
				var customerBalanceService = HttpContext.RequestServices.GetRequiredService<ICustomerBalanceService>();

				// If you added the debug method above
				var debugInfo = await ((CustomerBalanceService)customerBalanceService).DebugCustomerAdjustments(customerId);

				// Also check database directly
				var adjustments = await _context.CustomerBalanceAdjustments
						.Where(a => a.CustomerId == customerId)
						.OrderByDescending(a => a.AdjustmentDate)
						.ToListAsync();

				ViewBag.DebugInfo = debugInfo;
				ViewBag.DirectAdjustments = adjustments;

				return View("Debug", adjustments);
			}
			catch (Exception ex)
			{
				ViewBag.Error = ex.Message;
				return View("Debug");
			}
		}
		// ============= DASHBOARD =============

		// GET: Accounting
		public async Task<IActionResult> Index()
		{
			try
			{
				// Check if system is initialized
				if (!await _accountingService.IsSystemInitializedAsync())
				{
					return RedirectToAction(nameof(Setup));
				}

				var dashboard = await _accountingService.GetAccountingDashboardAsync();
				return View(dashboard);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading accounting dashboard");
				TempData["ErrorMessage"] = "Error loading accounting dashboard";
				return View(new AccountingDashboardViewModel());
			}
		}

		// ============= CHART OF ACCOUNTS =============

		// GET: Accounting/ChartOfAccounts
		public async Task<IActionResult> ChartOfAccounts()
		{
			try
			{
				var accounts = await _accountingService.GetAllAccountsAsync();
				var viewModel = new ChartOfAccountsViewModel
				{
					Accounts = accounts.ToList()
				};
				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading chart of accounts");
				TempData["ErrorMessage"] = "Error loading chart of accounts";
				return View(new ChartOfAccountsViewModel());
			}
		}

		// GET: Accounting/CreateAccount
		public IActionResult CreateAccount()
		{
			var viewModel = new CreateAccountViewModel();
			return View(viewModel);
		}

		// POST: Accounting/CreateAccount
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateAccount(CreateAccountViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			try
			{
				var account = new Account
				{
					AccountCode = model.AccountCode,
					AccountName = model.AccountName,
					Description = model.Description,
					AccountType = model.AccountType,
					AccountSubType = model.AccountSubType,
					IsActive = true,
					ParentAccountId = model.ParentAccountId,
					CreatedBy = User.Identity?.Name ?? "System"
				};

				await _accountingService.CreateAccountAsync(account);
				TempData["SuccessMessage"] = $"Account {account.AccountCode} - {account.AccountName} created successfully";
				return RedirectToAction(nameof(ChartOfAccounts));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating account");
				TempData["ErrorMessage"] = $"Error creating account: {ex.Message}";
				return View(model);
			}
		}

		// GET: Accounting/EditAccount/5
		public async Task<IActionResult> EditAccount(int id)
		{
			try
			{
				var account = await _accountingService.GetAccountByIdAsync(id);
				if (account == null)
				{
					TempData["ErrorMessage"] = "Account not found";
					return RedirectToAction(nameof(ChartOfAccounts));
				}

				// Check if account has activity to determine what can be edited
				var hasActivity = await _accountingService.HasAccountActivityAsync(id);

				// Get available parent accounts for dropdown
				var allAccounts = await _accountingService.GetActiveAccountsAsync();
				var availableParentAccounts = allAccounts
					.Where(a => a.Id != id && a.ParentAccountId != id) // Exclude self and children
					.OrderBy(a => a.AccountCode)
					.ToList();

				var viewModel = new EditAccountViewModel
				{
					Id = account.Id,
					AccountCode = account.AccountCode,
					AccountName = account.AccountName,
					Description = account.Description,
					AccountType = account.AccountType,
					AccountSubType = account.AccountSubType,
					IsActive = account.IsActive,
					IsSystemAccount = account.IsSystemAccount,
					ParentAccountId = account.ParentAccountId,
					CurrentBalance = account.CurrentBalance,
					LastTransactionDate = account.LastTransactionDate,
					AvailableParentAccounts = availableParentAccounts
				};

				// Add debug information to ViewBag for troubleshooting
				ViewBag.AccountTypeValue = (int)account.AccountType;
				ViewBag.AccountSubTypeValue = (int)account.AccountSubType;
				ViewBag.HasActivity = hasActivity;

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading account {AccountId}", id);
				TempData["ErrorMessage"] = "Error loading account";
				return RedirectToAction(nameof(ChartOfAccounts));
			}
		}

		// POST: Accounting/EditAccount/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditAccount(int id, EditAccountViewModel model)
		{
			if (id != model.Id)
			{
				return NotFound();
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			try
			{
				var account = await _accountingService.GetAccountByIdAsync(id);
				if (account == null)
				{
					TempData["ErrorMessage"] = "Account not found";
					return RedirectToAction(nameof(ChartOfAccounts));
				}

				// Check if account has activity
				var hasActivity = await _accountingService.HasAccountActivityAsync(id);

				// Only allow safe field updates
				account.AccountName = model.AccountName;
				account.Description = model.Description;
				account.IsActive = model.IsActive;
				account.ParentAccountId = model.ParentAccountId;

				// RESTRICTED: Only allow account code changes for accounts without activity
				if (!hasActivity && !account.IsSystemAccount)
				{
					account.AccountCode = model.AccountCode;
				}
				else if (account.AccountCode != model.AccountCode)
				{
					TempData["WarningMessage"] = "Account code cannot be changed for accounts with transaction history";
				}

				// RESTRICTED: Never allow AccountType changes
				// account.AccountType remains unchanged

				// RESTRICTED: Only allow AccountSubType changes for accounts without activity
				if (!hasActivity && !account.IsSystemAccount)
				{
					account.AccountSubType = model.AccountSubType;
				}

				await _accountingService.UpdateAccountAsync(account);
				TempData["SuccessMessage"] = $"Account {account.AccountCode} updated successfully";
				return RedirectToAction(nameof(ChartOfAccounts));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating account {AccountId}", id);
				TempData["ErrorMessage"] = $"Error updating account: {ex.Message}";
				return View(model);
			}
		}

		// ============= GENERAL LEDGER =============

		// GET: Accounting/GeneralLedger
		public async Task<IActionResult> GeneralLedger(string? accountCode = null, DateTime? startDate = null, DateTime? endDate = null, string? period = null)
		{
			try
			{
				// Get default date range based on financial period settings
				(DateTime defaultStart, DateTime defaultEnd) = period switch
				{
					"current-fy" => await _financialPeriodService.GetCurrentFinancialYearRangeAsync(),
					"previous-fy" => await _financialPeriodService.GetPreviousFinancialYearRangeAsync(),
					"calendar-year" => await _financialPeriodService.GetCalendarYearRangeAsync(),
					"all-time" => (new DateTime(2020, 1, 1), DateTime.Today),
					_ => await _financialPeriodService.GetDefaultReportDateRangeAsync()
				};

				var actualStartDate = startDate ?? defaultStart;
				var actualEndDate = endDate ?? defaultEnd;

				IEnumerable<GeneralLedgerEntry> entries;

				if (!string.IsNullOrEmpty(accountCode))
				{
					entries = await _accountingService.GetAccountLedgerEntriesAsync(accountCode, actualStartDate, actualEndDate);
				}
				else
				{
					entries = await _accountingService.GetAllLedgerEntriesWithEnhancedReferencesAsync(actualStartDate, actualEndDate);
				}

				var accounts = await _accountingService.GetActiveAccountsAsync();
				var financialPeriodInfo = await _financialPeriodService.GetCompanySettingsAsync();
				var currentPeriod = await _financialPeriodService.GetCurrentFinancialPeriodAsync();

				var viewModel = new GeneralLedgerViewModel
				{
					Entries = entries.OrderByDescending(e => e.TransactionDate).ThenBy(e => e.TransactionNumber).ToList(),
					SelectedAccountCode = accountCode,
					StartDate = actualStartDate,
					EndDate = actualEndDate,
					Accounts = accounts.ToList(),
					// Add financial period info
					CurrentFinancialPeriod = currentPeriod,
					SelectedPeriod = period,
					IsAllTimeView = period == "all-time"
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading general ledger");
				TempData["ErrorMessage"] = "Error loading general ledger";
				return View(new GeneralLedgerViewModel());
			}
		}

		// Helper method for enhancing individual entries
		private async Task EnhanceEntryReferenceInfo(GeneralLedgerEntry entry)
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
							entry.EnhancedReferenceText = $"Sale {sale.SaleNumber}";
						}
						break;
					// Add other cases as needed
				}
			}
			catch
			{
				// Ignore enhancement errors
			}
		}

		// ============= FINANCIAL REPORTS =============

		// GET: Accounting/TrialBalance
		[HttpGet("Accounting/TrialBalance")]
		public async Task<IActionResult> TrialBalance(DateTime? asOfDate = null, string? period = null)
		{
			try
			{
				DateTime defaultDate;
				
				if (!string.IsNullOrEmpty(period))
				{
					defaultDate = period switch
					{
						"current-fy-end" => (await _financialPeriodService.GetCurrentFinancialYearRangeAsync()).end,
						"previous-fy-end" => (await _financialPeriodService.GetPreviousFinancialYearRangeAsync()).end,
						"calendar-year-end" => new DateTime(DateTime.Today.Year, 12, 31),
						_ => asOfDate ?? DateTime.Today
					};
				}
				else
				{
					defaultDate = asOfDate ?? DateTime.Today;
				}

				var trialBalance = await _accountingService.GetTrialBalanceAsync(defaultDate);
				
				// Add financial period info
				trialBalance.CurrentFinancialPeriod = await _financialPeriodService.GetCurrentFinancialPeriodAsync();
				trialBalance.SelectedPeriod = period;
				
				return View(trialBalance);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating trial balance");
				TempData["ErrorMessage"] = "Error generating trial balance";
				return View(new TrialBalanceViewModel());
			}
		}

		// GET: Accounting/BalanceSheet
		[HttpGet("Accounting/BalanceSheet")]
		public async Task<IActionResult> BalanceSheet(DateTime? asOfDate = null, string? period = null)
		{
			try
			{
				DateTime defaultDate;
				
				if (!string.IsNullOrEmpty(period))
				{
					defaultDate = period switch
					{
						"current-fy-end" => (await _financialPeriodService.GetCurrentFinancialYearRangeAsync()).end,
						"previous-fy-end" => (await _financialPeriodService.GetPreviousFinancialYearRangeAsync()).end,
						"calendar-year-end" => new DateTime(DateTime.Today.Year, 12, 31),
						_ => asOfDate ?? DateTime.Today
					};
				}
				else
				{
					defaultDate = asOfDate ?? DateTime.Today;
				}

				var balanceSheet = await _accountingService.GetBalanceSheetAsync(defaultDate);
				
				// Add financial period info
				balanceSheet.CurrentFinancialPeriod = await _financialPeriodService.GetCurrentFinancialPeriodAsync();
				balanceSheet.SelectedPeriod = period;
				
				return View(balanceSheet);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating balance sheet");
				TempData["ErrorMessage"] = "Error generating balance sheet";
				return View(new BalanceSheetViewModel());
			}
		}

		// GET: Accounting/IncomeStatement
		[HttpGet("Accounting/IncomeStatement")]
		public async Task<IActionResult> IncomeStatement(DateTime? startDate = null, DateTime? endDate = null, string? period = null)
		{
			try
			{
				DateTime defaultStartDate;
				DateTime defaultEndDate;
				
				if (!string.IsNullOrEmpty(period))
				{
					(defaultStartDate, defaultEndDate) = period switch
					{
						"current-fy" => await _financialPeriodService.GetCurrentFinancialYearRangeAsync(),
						"previous-fy" => await _financialPeriodService.GetPreviousFinancialYearRangeAsync(),
						"calendar-year" => await _financialPeriodService.GetCalendarYearRangeAsync(),
						"ytd" => (new DateTime(DateTime.Today.Year, 1, 1), DateTime.Today),
						_ => (startDate ?? new DateTime(DateTime.Today.Year, 1, 1), endDate ?? DateTime.Today)
					};
				}
				else
				{
					defaultStartDate = startDate ?? new DateTime(DateTime.Today.Year, 1, 1);
					defaultEndDate = endDate ?? DateTime.Today;
				}

				var incomeStatement = await _accountingService.GetIncomeStatementAsync(defaultStartDate, defaultEndDate);
				
				// Add financial period info
				incomeStatement.CurrentFinancialPeriod = await _financialPeriodService.GetCurrentFinancialPeriodAsync();
				incomeStatement.SelectedPeriod = period;
				
				return View(incomeStatement);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating income statement");
				TempData["ErrorMessage"] = "Error generating income statement";
				return View(new IncomeStatementViewModel());
			}
		}

		// ============= ACCOUNTS PAYABLE =============

		// GET: Accounting/AccountsPayable
		public async Task<IActionResult> AccountsPayable()
		{
			try
			{
				var unpaidAP = await _accountingService.GetUnpaidAccountsPayableAsync();
				var overdueAP = await _accountingService.GetOverdueAccountsPayableAsync();
				var totalAP = await _accountingService.GetTotalAccountsPayableAsync();
				var totalOverdue = await _accountingService.GetTotalOverdueAccountsPayableAsync();

				var viewModel = new AccountsPayableViewModel
				{
					UnpaidAccountsPayable = unpaidAP.ToList(),
					OverdueAccountsPayable = overdueAP.ToList(),
					TotalAccountsPayable = totalAP,
					TotalOverdueAmount = totalOverdue
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading accounts payable");
				TempData["ErrorMessage"] = "Error loading accounts payable";
				return View(new AccountsPayableViewModel());
			}
		}

		// ============= SETUP & INITIALIZATION =============

		// GET: Accounting/Setup
		public async Task<IActionResult> Setup()
		{
			var isInitialized = await _accountingService.IsSystemInitializedAsync();
			var viewModel = new SetupViewModel
			{
				IsSystemInitialized = isInitialized
			};
			return View(viewModel);
		}

		// POST: Accounting/Setup
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Setup(SetupViewModel model)
		{
			try
			{
				if (model.SeedDefaultAccounts)
				{
					await _accountingService.SeedDefaultAccountsAsync();
					TempData["SuccessMessage"] = "Default chart of accounts created successfully";
				}

				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during accounting setup");
				TempData["ErrorMessage"] = $"Error during setup: {ex.Message}";
				return View(model);
			}
		}

		// ============= UTILITIES =============

		// POST: Accounting/GenerateJournalEntries
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> GenerateJournalEntries()
		{
			try
			{
				_logger.LogInformation("Synchronization: Starting journal entry generation for existing transactions");

				var results = new
				{
					PurchaseEntriesGenerated = 0,
					SaleEntriesGenerated = 0,
					ProductionEntriesGenerated = 0,
					Errors = new List<string>()
				};

				// Generate journal entries for purchases that don't have them
				var purchasesWithoutEntries = await _context.Purchases
								.Include(p => p.Item)
								.Include(p => p.Vendor)
								.Where(p => !p.IsJournalEntryGenerated &&
								(p.Status == PurchaseStatus.Received ||
								 p.Status == PurchaseStatus.Paid) && // Include paid status for expenses
								p.Item != null)
		.ToListAsync();

				foreach (var purchase in purchasesWithoutEntries)
				{
					try
					{
						var success = await _accountingService.GenerateJournalEntriesForPurchaseAsync(purchase);
						if (success)
						{
							results = new
							{
								PurchaseEntriesGenerated = results.PurchaseEntriesGenerated + 1,
								SaleEntriesGenerated = results.SaleEntriesGenerated,
								ProductionEntriesGenerated = results.ProductionEntriesGenerated,
								Errors = results.Errors
							};
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error generating journal entry for purchase {PurchaseId}", purchase.Id);
						results.Errors.Add($"Purchase {purchase.Id}: {ex.Message}");
					}
				}

				// Generate journal entries for sales that don't have them
				var salesWithoutEntries = await _context.Sales
						.Include(s => s.Customer)
						.Include(s => s.SaleItems)
						.ThenInclude(si => si.Item)
						.Where(s => !s.IsJournalEntryGenerated && s.SaleStatus != SaleStatus.Cancelled)
						.ToListAsync();

				foreach (var sale in salesWithoutEntries)
				{
					try
					{
						var success = await _accountingService.GenerateJournalEntriesForSaleAsync(sale);
						if (success)
						{
							results = new
							{
								PurchaseEntriesGenerated = results.PurchaseEntriesGenerated,
								SaleEntriesGenerated = results.SaleEntriesGenerated + 1,
								ProductionEntriesGenerated = results.ProductionEntriesGenerated,
								Errors = results.Errors
							};
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error generating journal entry for sale {SaleId}", sale.Id);
						results.Errors.Add($"Sale {sale.Id}: {ex.Message}");
					}
				}

				// Generate journal entries for productions that don't have them
				var productionsWithoutEntries = await _context.Productions
						.Include(p => p.Bom)
						.Include(p => p.MaterialConsumptions)
						.Include(p => p.ProductionWorkflow) // Include the workflow
						.Where(p => p.ProductionWorkflow != null &&
												p.ProductionWorkflow.Status == ProductionStatus.Completed)
						.ToListAsync();

				foreach (var production in productionsWithoutEntries)
				{
					try
					{
						var success = await _accountingService.GenerateJournalEntriesForProductionAsync(production);
						if (success)
						{
							results = new
							{
								PurchaseEntriesGenerated = results.PurchaseEntriesGenerated,
								SaleEntriesGenerated = results.SaleEntriesGenerated,
								ProductionEntriesGenerated = results.ProductionEntriesGenerated + 1,
								Errors = results.Errors
							};
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error generating journal entry for production {ProductionId}", production.Id);
						results.Errors.Add($"Production {production.Id}: {ex.Message}");
					}
				}

				var totalEntries = results.PurchaseEntriesGenerated + results.SaleEntriesGenerated + results.ProductionEntriesGenerated;

				if (totalEntries > 0)
				{
					TempData["SuccessMessage"] = $"Successfully generated {totalEntries} journal entries " +
							$"({results.PurchaseEntriesGenerated} purchases, {results.SaleEntriesGenerated} sales, {results.ProductionEntriesGenerated} productions)";
				}
				else if (results.Errors.Any())
				{
					TempData["ErrorMessage"] = $"Encountered {results.Errors.Count} errors during generation. Please check the logs.";
				}
				else
				{
					TempData["SuccessMessage"] = "All transactions already have journal entries generated.";
				}

				if (results.Errors.Any())
				{
					_logger.LogWarning("Journal entry generation completed with {ErrorCount} errors: {Errors}",
							results.Errors.Count, string.Join("; ", results.Errors));
				}

				_logger.LogInformation("Journal entry generation completed. Generated {TotalEntries} entries", totalEntries);

				return RedirectToAction(nameof(GeneralLedger));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during journal entry generation");
				TempData["ErrorMessage"] = $"Error generating journal entries: {ex.Message}";
				return RedirectToAction(nameof(Index));
			}
		}

		// POST: Accounting/GenerateJournalEntriesForTransaction
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<JsonResult> GenerateJournalEntriesForTransaction(string transactionType, int transactionId)
		{
			try
			{
				bool success = false;
				string message = "";

				switch (transactionType.ToLower())
				{
					case "purchase":
						var purchase = await _context.Purchases
								.Include(p => p.Item)
								.Include(p => p.Vendor)
								.FirstOrDefaultAsync(p => p.Id == transactionId);

						if (purchase != null)
						{
							success = await _accountingService.GenerateJournalEntriesForPurchaseAsync(purchase);
							message = success ? "Journal entry generated for purchase" : "Failed to generate journal entry for purchase";
						}
						else
						{
							message = "Purchase not found";
						}
						break;

					case "sale":
						var sale = await _context.Sales
								.Include(s => s.Customer)
								.Include(s => s.SaleItems)
								.ThenInclude(si => si.Item)
								.FirstOrDefaultAsync(s => s.Id == transactionId);

						if (sale != null)
						{
							success = await _accountingService.GenerateJournalEntriesForSaleAsync(sale);
							message = success ? "Journal entry generated for sale" : "Failed to generate journal entry for sale";
						}
						else
						{
							message = "Sale not found";
						}
						break;

					case "production":
						var production = await _context.Productions
								.Include(p => p.Bom)
								.Include(p => p.MaterialConsumptions)
								.FirstOrDefaultAsync(p => p.Id == transactionId);

						if (production != null)
						{
							success = await _accountingService.GenerateJournalEntriesForProductionAsync(production);
							message = success ? "Journal entry generated for production" : "Failed to generate journal entry for production";
						}
						else
						{
							message = "Production not found";
						}
						break;

					default:
						message = "Invalid transaction type";
						break;
				}

				return Json(new { success, message });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entry for {TransactionType} {TransactionId}", transactionType, transactionId);
				return Json(new { success = false, message = ex.Message });
			}
		}

		// GET: Accounting/AccountDetails/5
		public async Task<IActionResult> AccountDetails(int id)
		{
			try
			{
				var account = await _accountingService.GetAccountByIdAsync(id);
				if (account == null)
				{
					TempData["ErrorMessage"] = "Account not found";
					return RedirectToAction(nameof(ChartOfAccounts));
				}

				var entries = await _accountingService.GetAccountLedgerEntriesAsync(account.AccountCode);
				var balance = await _accountingService.GetAccountBalanceAsync(account.AccountCode);

				var viewModel = new AccountDetailsViewModel
				{
					Account = account,
					LedgerEntries = entries.OrderByDescending(e => e.TransactionDate).ToList(),
					CurrentBalance = balance
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading account details for account {AccountId}", id);
				TempData["ErrorMessage"] = "Error loading account details";
				return RedirectToAction(nameof(ChartOfAccounts));
			}
		}

		// ============= MANUAL JOURNAL ENTRIES =============
		// GET: Accounting/CreateManualJournalEntry
		public async Task<IActionResult> CreateManualJournalEntry()
		{
			try
			{
				var accounts = await _accountingService.GetActiveAccountsAsync();
				var viewModel = new ManualJournalEntryViewModel
				{
					AvailableAccounts = accounts.OrderBy(a => a.AccountCode).ToList(),
					JournalEntries = new List<JournalEntryLineViewModel>
						{
								new JournalEntryLineViewModel { LineNumber = 1 },
								new JournalEntryLineViewModel { LineNumber = 2 }
						}
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading manual journal entry form");
				TempData["ErrorMessage"] = "Error loading journal entry form";
				return RedirectToAction(nameof(GeneralLedger));
			}
		}

		// POST: Accounting/CreateManualJournalEntry
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateManualJournalEntry(ManualJournalEntryViewModel model)
		{
			try
			{
				// Remove empty lines
				model.JournalEntries = model.JournalEntries.Where(e => e.AccountId > 0).ToList();

				// Validate the journal entry
				var validationResult = ValidateManualJournalEntry(model);
				if (!validationResult.IsValid)
				{
					foreach (var error in validationResult.Errors)
					{
						ModelState.AddModelError("", error);
					}
				}

				if (!ModelState.IsValid)
				{
					// Reload accounts for dropdown
					var accounts = await _accountingService.GetActiveAccountsAsync();
					model.AvailableAccounts = accounts.OrderBy(a => a.AccountCode).ToList();

					// Populate account display names
					foreach (var entry in model.JournalEntries)
					{
						var account = accounts.FirstOrDefault(a => a.Id == entry.AccountId);
						entry.AccountDisplay = account != null ? $"{account.AccountCode} - {account.AccountName}" : "";
					}

					return View(model);
				}

				// Create the manual journal entry
				var success = await _accountingService.CreateManualJournalEntryAsync(model);

				if (success)
				{
					TempData["SuccessMessage"] = $"Manual journal entry {model.ReferenceNumber} created successfully";
					return RedirectToAction(nameof(GeneralLedger));
				}
				else
				{
					TempData["ErrorMessage"] = "Failed to create manual journal entry";
					var accounts = await _accountingService.GetActiveAccountsAsync();
					model.AvailableAccounts = accounts.OrderBy(a => a.AccountCode).ToList();
					return View(model);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating manual journal entry");
				TempData["ErrorMessage"] = $"Error creating journal entry: {ex.Message}";

				var accounts = await _accountingService.GetActiveAccountsAsync();
				model.AvailableAccounts = accounts.OrderBy(a => a.AccountCode).ToList();
				return View(model);
			}
		}

		// POST: Accounting/PreviewManualJournalEntry
		[HttpPost]
		public async Task<IActionResult> PreviewManualJournalEntry([FromBody] ManualJournalEntryViewModel model)
		{
			try
			{
				var accounts = await _accountingService.GetActiveAccountsAsync();
				var nextJournalNumber = await _accountingService.GenerateNextJournalNumberAsync("JE-MAN");

				// Remove empty lines
				model.JournalEntries = model.JournalEntries.Where(e => e.AccountId > 0).ToList();

				// Populate account display names
				foreach (var entry in model.JournalEntries)
				{
					var account = accounts.FirstOrDefault(a => a.Id == entry.AccountId);
					entry.AccountDisplay = account != null ? $"{account.AccountCode} - {account.AccountName}" : "Invalid Account";
				}

				var preview = new JournalEntryPreviewViewModel
				{
					TransactionNumber = nextJournalNumber,
					TransactionDate = model.TransactionDate,
					ReferenceNumber = model.ReferenceNumber,
					Description = model.Description,
					JournalEntries = model.JournalEntries,
					TotalDebits = model.TotalDebits,
					TotalCredits = model.TotalCredits,
					IsBalanced = model.IsBalanced
				};

				return Json(new { success = true, preview });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating journal entry preview");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// Helper method for validation
		private (bool IsValid, List<string> Errors) ValidateManualJournalEntry(ManualJournalEntryViewModel model)
		{
			var errors = new List<string>();

			if (!model.JournalEntries.Any())
			{
				errors.Add("At least one journal entry line is required");
				return (false, errors);
			}

			if (model.JournalEntries.Count < 2)
			{
				errors.Add("At least two journal entry lines are required");
			}

			foreach (var entry in model.JournalEntries)
			{
				if (entry.AccountId <= 0)
				{
					errors.Add($"Line {entry.LineNumber}: Account is required");
				}

				if (!entry.DebitAmount.HasValue && !entry.CreditAmount.HasValue)
				{
					errors.Add($"Line {entry.LineNumber}: Either debit or credit amount is required");
				}

				if (entry.DebitAmount.HasValue && entry.CreditAmount.HasValue &&
						entry.DebitAmount > 0 && entry.CreditAmount > 0)
				{
					errors.Add($"Line {entry.LineNumber}: Cannot have both debit and credit amounts");
				}

				if ((entry.DebitAmount ?? 0) < 0 || (entry.CreditAmount ?? 0) < 0)
				{
					errors.Add($"Line {entry.LineNumber}: Amounts cannot be negative");
				}
			}

			if (!model.IsBalanced)
			{
				errors.Add($"Journal entry is not balanced. Debits: {model.TotalDebits:C}, Credits: {model.TotalCredits:C}");
			}

			return (errors.Count == 0, errors);
		}
		// ============= CUSTOMER BALANCE ADJUSTMENTS =============

		// GET: Accounting/CreateCustomerAdjustment
		public async Task<IActionResult> CreateCustomerAdjustment(int? customerId = null, int? saleId = null)
		{
			try
			{
				var accounts = await _accountingService.GetActiveAccountsAsync();

				var customersWithBalance = await _context.Customers
		.Include(c => c.Sales)
		.Include(c => c.BalanceAdjustments)
		.Where(c => c.IsActive &&
								c.Sales.Any(s => s.PaymentStatus == PaymentStatus.Pending ||
															 s.PaymentStatus == PaymentStatus.Overdue))
		.ToListAsync();

				var unpaidSales = await _context.Sales
						.Include(s => s.Customer)
						.Where(s => s.PaymentStatus != PaymentStatus.Paid && s.SaleStatus != SaleStatus.Cancelled)
						.OrderByDescending(s => s.SaleDate)
						.ToListAsync();

				var viewModel = new EnhancedManualJournalEntryViewModel
				{
					IsCustomerAdjustment = true,
					CustomerId = customerId,
					SaleId = saleId,
					AvailableAccounts = accounts.OrderBy(a => a.AccountCode).ToList(),
					AvailableCustomers = customersWithBalance,
					AvailableSales = unpaidSales,
					JournalEntries = new List<JournalEntryLineViewModel>
						{
								new JournalEntryLineViewModel { LineNumber = 1 },
								new JournalEntryLineViewModel { LineNumber = 2 }
						}
				};

				// Pre-populate if specific customer/sale provided
				if (saleId.HasValue)
				{
					var sale = unpaidSales.FirstOrDefault(s => s.Id == saleId.Value);
					if (sale != null)
					{
						viewModel.CustomerId = sale.CustomerId;
						viewModel.Description = $"Adjustment for Invoice {sale.SaleNumber}";
						viewModel.ReferenceNumber = $"ADJ-{sale.SaleNumber}-{DateTime.Now:yyyyMMdd}";
					}
				}

				return View("CreateCustomerAdjustment", viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading customer adjustment form");
				TempData["ErrorMessage"] = "Error loading customer adjustment form";
				return RedirectToAction(nameof(GeneralLedger));
			}
		}

		// POST: Accounting/CreateCustomerAdjustment
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateCustomerAdjustment(EnhancedManualJournalEntryViewModel model)
		{
			try
			{
				// Validate customer adjustment specific fields
				if (model.IsCustomerAdjustment)
				{
					if (!model.CustomerId.HasValue)
					{
						ModelState.AddModelError("CustomerId", "Customer is required for customer adjustments");
					}

					if (string.IsNullOrWhiteSpace(model.AdjustmentType))
					{
						ModelState.AddModelError("AdjustmentType", "Adjustment type is required");
					}

					if (string.IsNullOrWhiteSpace(model.AdjustmentReason))
					{
						ModelState.AddModelError("AdjustmentReason", "Adjustment reason is required");
					}
				}

				// Remove empty lines
				model.JournalEntries = model.JournalEntries.Where(e => e.AccountId > 0).ToList();

				// Validate the journal entry
				var validationResult = ValidateManualJournalEntry(model);
				if (!validationResult.IsValid)
				{
					foreach (var error in validationResult.Errors)
					{
						ModelState.AddModelError("", error);
					}
				}

				if (!ModelState.IsValid)
				{
					// Reload dropdowns
					await ReloadCustomerAdjustmentDropdowns(model);
					return View("CreateCustomerAdjustment", model);
				}

				// Create the manual journal entry
				var success = await _accountingService.CreateManualJournalEntryAsync(model);

				if (success && model.IsCustomerAdjustment && model.CustomerId.HasValue)
				{
					// Update customer balance
					var customerBalanceService = HttpContext.RequestServices.GetRequiredService<ICustomerBalanceService>();

					// FIX: Get the A/R account ID first, then use it in the Where clause
					var arAccountId = await GetAccountsReceivableAccountId();
					var adjustmentAmount = model.JournalEntries
							.Where(e => e.AccountId == arAccountId) // ✅ Now using the pre-fetched value
							.Sum(e => e.CreditAmount ?? 0); // Credit to A/R reduces the balance

					if (adjustmentAmount > 0)
					{
						if (model.AdjustmentType == "Bad Debt Write-off")
						{
							await customerBalanceService.UpdateCustomerBalanceForBadDebtAsync(
									model.CustomerId.Value,
									model.SaleId ?? 0,
									adjustmentAmount,
									model.AdjustmentReason ?? "Manual adjustment");
						}
						else
						{
							await customerBalanceService.UpdateCustomerBalanceForAllowanceAsync(
									model.CustomerId.Value,
									model.SaleId ?? 0,
									adjustmentAmount,
									model.AdjustmentReason ?? "Manual adjustment");
						}
					}
				}

				if (success)
				{
					TempData["SuccessMessage"] = $"Customer adjustment {model.ReferenceNumber} created successfully. Customer balance updated.";
					return RedirectToAction("Details", "Customers", new { id = model.CustomerId });
				}
				else
				{
					TempData["ErrorMessage"] = "Failed to create customer adjustment";
					await ReloadCustomerAdjustmentDropdowns(model);
					return View("CreateCustomerAdjustment", model);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating customer adjustment");
				TempData["ErrorMessage"] = $"Error creating customer adjustment: {ex.Message}";

				await ReloadCustomerAdjustmentDropdowns(model);
				return View("CreateCustomerAdjustment", model);
			}
		}

		// POST: Accounting/LoadCustomerAdjustmentTemplate
		[HttpPost]
		public async Task<IActionResult> LoadCustomerAdjustmentTemplate([FromBody] CustomerAdjustmentTemplateRequest request)
		{
			try
			{
				var template = CustomerAdjustmentTemplate.GetTemplates()
						.FirstOrDefault(t => t.AdjustmentType == request.AdjustmentType);

				if (template == null)
				{
					return Json(new { success = false, message = "Template not found" });
				}

				var accounts = await _accountingService.GetActiveAccountsAsync();
				var debitAccount = accounts.FirstOrDefault(a => a.AccountCode == template.DebitAccount);
				var creditAccount = accounts.FirstOrDefault(a => a.AccountCode == template.CreditAccount);

				var response = new
				{
					success = true,
					template = new
					{
						debitAccountId = debitAccount?.Id,
						creditAccountId = creditAccount?.Id,
						debitDescription = template.DebitDescription,
						creditDescription = template.CreditDescription,
						referenceNumber = $"{request.AdjustmentType.Replace(" ", "").ToUpper()}-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
						description = template.Description
					}
				};

				return Json(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading customer adjustment template");
				return Json(new { success = false, message = ex.Message });
			}
		}

		// Helper methods
		private async Task<int> GetAccountsReceivableAccountId()
		{
			var arAccount = await _accountingService.GetAccountByCodeAsync("1100");
			return arAccount?.Id ?? 0;
		}

		private async Task ReloadCustomerAdjustmentDropdowns(EnhancedManualJournalEntryViewModel model)
		{
			var accounts = await _accountingService.GetActiveAccountsAsync();
			var customers = await _context.Customers.Where(c => c.OutstandingBalance > 0).ToListAsync();
			var unpaidSales = await _context.Sales
					.Include(s => s.Customer)
					.Where(s => s.PaymentStatus != PaymentStatus.Paid && s.SaleStatus != SaleStatus.Cancelled)
					.ToListAsync();

			model.AvailableAccounts = accounts.OrderBy(a => a.AccountCode).ToList();
			model.AvailableCustomers = customers.OrderBy(c => c.CustomerName).ToList();
			model.AvailableSales = unpaidSales.OrderByDescending(s => s.SaleDate).ToList();

			// Populate account display names
			foreach (var entry in model.JournalEntries)
			{
				var account = accounts.FirstOrDefault(a => a.Id == entry.AccountId);
				entry.AccountDisplay = account != null ? $"{account.AccountCode} - {account.AccountName}" : "";
			}
		}

		public class CustomerAdjustmentTemplateRequest
		{
			public string AdjustmentType { get; set; } = string.Empty;
			public int? CustomerId { get; set; }
			public int? SaleId { get; set; }
			public decimal? Amount { get; set; }
		}

		// POST: Accounting/ToggleAccountStatus/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ToggleAccountStatus(int id, [FromBody] ToggleAccountStatusRequest request)
		{
			try
			{
				var account = await _accountingService.GetAccountByIdAsync(id);
				if (account == null)
				{
					return Json(new { success = false, message = "Account not found" });
				}

				if (account.IsSystemAccount)
				{
					return Json(new { success = false, message = "Cannot modify system accounts" });
				}

				account.IsActive = request.IsActive;
				await _accountingService.UpdateAccountAsync(account);

				var status = request.IsActive ? "activated" : "deactivated";
				return Json(new { success = true, message = $"Account {status} successfully" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error toggling account status for account {AccountId}", id);
				return Json(new { success = false, message = "Error updating account status" });
			}
		}

		// ============= FINANCIAL PERIOD MANAGEMENT =============

		// GET: Accounting/ManageFinancialPeriods
		public async Task<IActionResult> ManageFinancialPeriods()
		{
			try
			{
				var companySettings = await _financialPeriodService.GetCompanySettingsAsync();
				var periods = await _financialPeriodService.GetAllFinancialPeriodsAsync();
				var currentPeriod = await _financialPeriodService.GetCurrentFinancialPeriodAsync();

				var viewModel = new FinancialPeriodViewModel
				{
					CompanySettings = companySettings,
					AvailablePeriods = periods.ToList(),
					CurrentPeriod = currentPeriod,
					CurrentFinancialYear = await _financialPeriodService.GetCurrentFinancialYearRangeAsync(),
					PreviousFinancialYear = await _financialPeriodService.GetPreviousFinancialYearRangeAsync(),
					CurrentCalendarYear = await _financialPeriodService.GetCalendarYearRangeAsync()
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading financial periods");
				TempData["ErrorMessage"] = "Error loading financial periods";
				return RedirectToAction(nameof(Index));
			}
		}

		// POST: Accounting/UpdateCompanySettings
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateCompanySettings(CompanySettingsViewModel model)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					TempData["ErrorMessage"] = "Please correct the errors and try again";
					return RedirectToAction(nameof(ManageFinancialPeriods));
				}

				var settings = await _financialPeriodService.GetCompanySettingsAsync();
				settings.CompanyName = model.CompanyName;
				settings.FinancialYearStartMonth = model.FinancialYearStartMonth;
				settings.FinancialYearStartDay = model.FinancialYearStartDay;
				settings.DefaultReportPeriod = model.DefaultReportPeriod;
				settings.AutoCreateFinancialPeriods = model.AutoCreateFinancialPeriods;
				settings.UpdatedBy = User.Identity?.Name ?? "System";

				await _financialPeriodService.UpdateCompanySettingsAsync(settings);
				TempData["SuccessMessage"] = "Company settings updated successfully";
				
				return RedirectToAction(nameof(ManageFinancialPeriods));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating company settings");
				TempData["ErrorMessage"] = $"Error updating company settings: {ex.Message}";
				return RedirectToAction(nameof(ManageFinancialPeriods));
			}
		}

		// POST: Accounting/CreatePeriod
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreatePeriod(CreateFinancialPeriodViewModel model)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					TempData["ErrorMessage"] = "Please correct the errors and try again";
					return RedirectToAction(nameof(ManageFinancialPeriods));
				}

				var period = new FinancialPeriod
				{
					PeriodName = model.PeriodName,
					StartDate = model.StartDate,
					EndDate = model.EndDate,
					PeriodType = model.PeriodType,
					Description = model.Description,
					IsCurrentPeriod = model.IsCurrentPeriod,
					CreatedBy = User.Identity?.Name ?? "System"
				};

				var createdPeriod = await _financialPeriodService.CreateFinancialPeriodAsync(period);

				if (model.IsCurrentPeriod)
				{
					await _financialPeriodService.SetCurrentFinancialPeriodAsync(createdPeriod.Id);
				}

				TempData["SuccessMessage"] = $"Financial period '{model.PeriodName}' created successfully";
				return RedirectToAction(nameof(ManageFinancialPeriods));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating financial period");
				TempData["ErrorMessage"] = $"Error creating financial period: {ex.Message}";
				return RedirectToAction(nameof(ManageFinancialPeriods));
			}
		}

		// POST: Accounting/SetCurrentPeriod
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetCurrentPeriod(int periodId)
		{
			try
			{
				var period = await _financialPeriodService.GetFinancialPeriodByIdAsync(periodId);
				if (period == null)
				{
					TempData["ErrorMessage"] = "Financial period not found";
					return RedirectToAction(nameof(ManageFinancialPeriods));
				}

				if (period.IsClosed)
				{
					TempData["ErrorMessage"] = "Cannot set a closed period as current";
					return RedirectToAction(nameof(ManageFinancialPeriods));
				}

				await _financialPeriodService.SetCurrentFinancialPeriodAsync(periodId);
				TempData["SuccessMessage"] = $"'{period.PeriodName}' is now the current financial period";
				return RedirectToAction(nameof(ManageFinancialPeriods));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error setting current financial period");
				TempData["ErrorMessage"] = $"Error setting current period: {ex.Message}";
				return RedirectToAction(nameof(ManageFinancialPeriods));
			}
		}

		// POST: Accounting/CreateNextFinancialYear
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateNextFinancialYear()
		{
			try
			{
				var nextYear = await _financialPeriodService.CreateNextFinancialYearAsync();
				TempData["SuccessMessage"] = $"Next financial year '{nextYear.PeriodName}' created successfully";
				return RedirectToAction(nameof(ManageFinancialPeriods));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating next financial year");
				TempData["ErrorMessage"] = $"Error creating next financial year: {ex.Message}";
				return RedirectToAction(nameof(ManageFinancialPeriods));
			}
		}

		// GET: Accounting/CloseFinancialYear
		public async Task<IActionResult> CloseFinancialYear(int? periodId = null)
		{
			try
			{
				var currentPeriod = periodId.HasValue 
					? await _financialPeriodService.GetFinancialPeriodByIdAsync(periodId.Value)
					: await _financialPeriodService.GetCurrentFinancialPeriodAsync();

				if (currentPeriod == null)
				{
					TempData["ErrorMessage"] = "No financial period found to close";
					return RedirectToAction(nameof(ManageFinancialPeriods));
				}

				// Get financial year summary data
				var (start, end) = (currentPeriod.StartDate, currentPeriod.EndDate);
				var ledgerEntries = await _accountingService.GetAllLedgerEntriesAsync(start, end);
				var totalDebits = ledgerEntries.Sum(e => e.DebitAmount);
				var totalCredits = ledgerEntries.Sum(e => e.CreditAmount);

				// Check for pending items
				var pendingTransactions = await GetPendingTransactionsAsync(currentPeriod);
				var unreconciledAccounts = await GetUnreconciledAccountsAsync(currentPeriod);

				// Get next financial year
				var nextYear = await GetNextFinancialYearAsync(currentPeriod);

				var viewModel = new FinancialYearCloseViewModel
				{
					CurrentPeriod = currentPeriod,
					NextFinancialYear = nextYear,
					TotalDebits = totalDebits,
					TotalCredits = totalCredits,
					TotalTransactions = ledgerEntries.Count(),
					IsBalanced = Math.Abs(totalDebits - totalCredits) < 0.01m,
					HasPendingTransactions = pendingTransactions.Any(),
					PendingTransactionCount = pendingTransactions.Count,
					AllAccountsReconciled = !unreconciledAccounts.Any(),
					UnreconciledAccountCount = unreconciledAccounts.Count
				};

				return View(viewModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading financial year close page");
				TempData["ErrorMessage"] = "Error loading financial year close page";
				return RedirectToAction(nameof(ManageFinancialPeriods));
			}
		}

		// POST: Accounting/CloseFinancialYear
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CloseFinancialYear(FinancialYearCloseViewModel model)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return View(model);
				}

				// Perform final validation
				var period = await _financialPeriodService.GetFinancialPeriodByIdAsync(model.CurrentPeriod.Id);
				if (period == null || period.IsClosed)
				{
					TempData["ErrorMessage"] = "Financial period not found or already closed";
					return RedirectToAction(nameof(ManageFinancialPeriods));
				}

				// Close the financial year
				var success = await _financialPeriodService.CloseFinancialYearAsync(
					period.Id, 
					model.ClosingNotes, 
					User.Identity?.Name ?? "System");

				if (success)
				{
					// Create next financial year if requested
					if (model.CreateNextYear)
					{
						await _financialPeriodService.CreateNextFinancialYearAsync();
					}

					TempData["SuccessMessage"] = $"Financial year {period.PeriodName} has been successfully closed";
					return RedirectToAction(nameof(ManageFinancialPeriods));
				}
				else
				{
					TempData["ErrorMessage"] = "Failed to close financial year";
					return View(model);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error closing financial year");
				TempData["ErrorMessage"] = $"Error closing financial year: {ex.Message}";
				return View(model);
			}
		}

		// Helper methods for financial year closing
		private async Task<List<object>> GetPendingTransactionsAsync(FinancialPeriod period)
		{
			return new List<object>();
		}

		private async Task<List<object>> GetUnreconciledAccountsAsync(FinancialPeriod period)
		{
			return new List<object>();
		}

		private async Task<FinancialPeriod?> GetNextFinancialYearAsync(FinancialPeriod currentPeriod)
		{
			var nextStart = currentPeriod.EndDate.AddDays(1);
			return await _financialPeriodService.GetFinancialPeriodForDateAsync(nextStart);
		}
		// Helper class for account status toggle
		public class ToggleAccountStatusRequest
		{
			public bool IsActive { get; set; }
		}
	}
}