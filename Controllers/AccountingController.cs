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
		private readonly InventoryContext _context;
		private readonly ILogger<AccountingController> _logger;

		public AccountingController(IAccountingService accountingService, InventoryContext context, ILogger<AccountingController> logger)
		{
			_accountingService = accountingService;
			_context = context;
			_logger = logger;
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
					ParentAccountId = account.ParentAccountId
				};

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

				account.AccountCode = model.AccountCode;
				account.AccountName = model.AccountName;
				account.Description = model.Description;
				account.AccountType = model.AccountType;
				account.AccountSubType = model.AccountSubType;
				account.IsActive = model.IsActive;
				account.ParentAccountId = model.ParentAccountId;

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
		public async Task<IActionResult> GeneralLedger(string? accountCode = null, DateTime? startDate = null, DateTime? endDate = null)
		{
			try
			{
				var defaultStartDate = startDate ?? DateTime.Today.AddMonths(-1);
				var defaultEndDate = endDate ?? DateTime.Today;

				IEnumerable<GeneralLedgerEntry> entries;

				if (!string.IsNullOrEmpty(accountCode))
				{
					entries = await _accountingService.GetAccountLedgerEntriesAsync(accountCode, defaultStartDate, defaultEndDate);
				}
				else
				{
					entries = await _accountingService.GetAllLedgerEntriesAsync(defaultStartDate, defaultEndDate);
				}

				var accounts = await _accountingService.GetActiveAccountsAsync();

				var viewModel = new GeneralLedgerViewModel
				{
					Entries = entries.OrderByDescending(e => e.TransactionDate).ThenBy(e => e.TransactionNumber).ToList(),
					SelectedAccountCode = accountCode,
					StartDate = defaultStartDate,
					EndDate = defaultEndDate,
					Accounts = accounts.ToList()
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

		// ============= FINANCIAL REPORTS =============

		// GET: Accounting/TrialBalance
		public async Task<IActionResult> TrialBalance(DateTime? asOfDate = null)
		{
			try
			{
				var defaultDate = asOfDate ?? DateTime.Today;
				var trialBalance = await _accountingService.GetTrialBalanceAsync(defaultDate);
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
		public async Task<IActionResult> BalanceSheet(DateTime? asOfDate = null)
		{
			try
			{
				var defaultDate = asOfDate ?? DateTime.Today;
				var balanceSheet = await _accountingService.GetBalanceSheetAsync(defaultDate);
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
		public async Task<IActionResult> IncomeStatement(DateTime? startDate = null, DateTime? endDate = null)
		{
			try
			{
				var defaultStartDate = startDate ?? new DateTime(DateTime.Today.Year, 1, 1);
				var defaultEndDate = endDate ?? DateTime.Today;

				var incomeStatement = await _accountingService.GetIncomeStatementAsync(defaultStartDate, defaultEndDate);
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
				_logger.LogInformation("Starting journal entry generation for existing transactions");

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
						.Where(p => !p.IsJournalEntryGenerated && p.Status == PurchaseStatus.Received)
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
						.Where(p => p.Status == ProductionStatus.Completed)
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
	}
}