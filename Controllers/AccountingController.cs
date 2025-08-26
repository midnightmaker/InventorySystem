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
					// Enhance with reference info
					foreach (var entry in entries.Where(e => e.HasReference))
					{
						await EnhanceEntryReferenceInfo(entry);
					}
				}
				else
				{
					entries = await _accountingService.GetAllLedgerEntriesWithEnhancedReferencesAsync(defaultStartDate, defaultEndDate);
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
	}
}