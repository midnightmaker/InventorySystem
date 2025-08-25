﻿using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Models.Accounting;
using InventorySystem.Extensions;
using InventorySystem.ViewModels;
using InventorySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
    public class ExpensesController : BaseController // ✅ Changed from Controller to BaseController
    {
        private readonly InventoryContext _context;
        private readonly ILogger<ExpensesController> _logger;
        private readonly IAccountingService _accountingService;

        public ExpensesController(InventoryContext context, ILogger<ExpensesController> logger, IAccountingService accountingService)
        {
            _context = context;
            _logger = logger;
            _accountingService = accountingService; // Add this
        }

        // ✅ FIXED: GET: /Expenses/Pay (Route alias for PayExpenses)
        [HttpGet]
        public async Task<IActionResult> Pay()
        {
            try
            {
                var expenses = await _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.ExpenseCode)
                    .ToListAsync();

                var vendors = await _context.Vendors
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.CompanyName)
                    .ToListAsync();

                var categories = Enum.GetValues<ExpenseCategory>()
                    .Select(c => GetCategoryDisplayName(c))
                    .Distinct()
                    .ToList();

                var viewModel = new PayExpensesViewModel
                {
                    AvailableExpenses = expenses,
                    AvailableVendors = vendors,
                    AvailableCategories = categories,
                    PaymentDate = DateTime.Today,
                    PaymentMethod = "Check"
                };

                // ✅ Explicitly specify to use the PayExpenses view
                return View("PayExpenses", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pay expenses page");
                SetErrorMessage("Error loading expenses for payment.");
                return RedirectToAction("Index");
            }
        }

        // GET: /Expenses
        public async Task<IActionResult> Index(
            string search,
            string categoryFilter,
            DateTime? startDate,
            DateTime? endDate,
            string sortOrder = "code_asc",
            int page = 1,
            int pageSize = 25)
        {
            try
            {
                // ✅ FIXED: Include DefaultVendor navigation property
                var query = _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .Where(e => e.IsActive)
                    .AsQueryable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchTerm = search.Trim();
                    query = query.Where(e =>
                        e.ExpenseCode.Contains(searchTerm) ||
                        e.Description.Contains(searchTerm) ||
                        (e.Comments != null && e.Comments.Contains(searchTerm))
                    );
                }

                // Apply category filter
                if (!string.IsNullOrWhiteSpace(categoryFilter) && 
                    Enum.TryParse<ExpenseCategory>(categoryFilter, out var category))
                {
                    query = query.Where(e => e.Category == category);
                }

                // Apply sorting
                query = sortOrder switch
                {
                    "code_asc" => query.OrderBy(e => e.ExpenseCode),
                    "code_desc" => query.OrderByDescending(e => e.ExpenseCode),
                    "description_asc" => query.OrderBy(e => e.Description),
                    "description_desc" => query.OrderByDescending(e => e.Description),
                    "category_asc" => query.OrderBy(e => e.Category),
                    "category_desc" => query.OrderByDescending(e => e.Category),
                    _ => query.OrderBy(e => e.ExpenseCode)
                };

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                var skip = (page - 1) * pageSize;

                var expenses = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                // Prepare ViewBag data
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = totalCount;
                ViewBag.PageSize = pageSize;
                ViewBag.SearchTerm = search;
                ViewBag.CategoryFilter = categoryFilter;
                ViewBag.SortOrder = sortOrder;
                ViewBag.ShowingFrom = totalCount > 0 ? skip + 1 : 0;
                ViewBag.ShowingTo = Math.Min(skip + pageSize, totalCount);

                // Category options for dropdown
                var categories = Enum.GetValues<ExpenseCategory>().ToList();
                ViewBag.CategoryOptions = new SelectList(categories.Select(c => new
                {
                    Value = c.ToString(),
                    Text = GetCategoryDisplayName(c)
                }), "Value", "Text", categoryFilter);

                return View(expenses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expenses");
                SetErrorMessage("Error loading expenses."); // ✅ Using BaseController method
                return View(new List<Expense>());
            }
        }

        // GET: /Expenses/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                var vendors = await _context.Vendors
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.CompanyName)
                    .ToListAsync();

                var categories = Enum.GetValues<ExpenseCategory>()
                    .Select(c => new SelectListItem
                    {
                        Value = ((int)c).ToString(),
                        Text = c.GetDisplayName()
                    }).ToList();

                var taxCategories = Enum.GetValues<TaxCategory>()
                    .Select(tc => new SelectListItem
                    {
                        Value = ((int)tc).ToString(),
                        Text = tc.GetDisplayName()
                    }).ToList();

                // ✅ NEW: Get expense accounts and create category-to-account mapping
                var expenseAccounts = await _accountingService.GetExpenseAccountsAsync();
                var expenseAccountList = expenseAccounts.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.AccountCode} - {a.AccountName}"
                }).ToList();

                // Create suggestions mapping for JavaScript
                var accountSuggestions = new Dictionary<string, int?>();
                foreach (ExpenseCategory category in Enum.GetValues<ExpenseCategory>())
                {
                    var suggestedAccount = await _accountingService.GetSuggestedAccountForExpenseCategoryAsync(category);
                    accountSuggestions[((int)category).ToString()] = suggestedAccount?.Id;
                }

                ViewBag.Vendors = new SelectList(vendors, "Id", "CompanyName");
                ViewBag.Categories = new SelectList(categories, "Value", "Text");
                ViewBag.TaxCategories = new SelectList(taxCategories, "Value", "Text");
                ViewBag.ExpenseAccounts = new SelectList(expenseAccountList, "Value", "Text");
                ViewBag.AccountSuggestions = accountSuggestions;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create expense form");
                SetErrorMessage("Error loading form."); // ✅ Using BaseController method
                return RedirectToAction("Index");
            }
        }

        // POST: /Expenses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateExpenseViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validate that the selected account is an expense account
                var selectedAccount = await _accountingService.GetAccountByIdAsync(model.LedgerAccountId);
                if (selectedAccount == null || selectedAccount.AccountType != AccountType.Expense)
                {
                    ModelState.AddModelError("LedgerAccountId", "Please select a valid expense account.");
                }
                else
                {
                    var expense = new Expense
                    {
                        ExpenseCode = model.ExpenseCode,
                        Description = model.Description,
                        Category = model.Category,
                        LedgerAccountId = model.LedgerAccountId, // ✅ NEW
                        TaxCategory = model.TaxCategory,
                        UnitOfMeasure = model.UnitOfMeasure,
                        DefaultAmount = model.DefaultAmount,
                        DefaultVendorId = model.DefaultVendorId,
                        IsRecurring = model.IsRecurring,
                        RecurringFrequency = model.RecurringFrequency,
                        Comments = model.Comments,
                        IsActive = model.IsActive,
                        CreatedDate = DateTime.Now
                    };

                    _context.Expenses.Add(expense);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Expense type '{expense.ExpenseCode}' created successfully and linked to account '{selectedAccount.AccountCode} - {selectedAccount.AccountName}'.";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Reload ViewBag data on validation failure
            await LoadCreateViewBagData();
            return View(model);
        }

        private async Task LoadCreateViewBagData()
        {
            var vendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            var categories = Enum.GetValues<ExpenseCategory>()
                .Select(c => new SelectListItem
                {
                    Value = ((int)c).ToString(),
                    Text = c.GetDisplayName()
                }).ToList();

            var taxCategories = Enum.GetValues<TaxCategory>()
                .Select(tc => new SelectListItem
                {
                    Value = ((int)tc).ToString(),
                    Text = tc.GetDisplayName()
                }).ToList();

            var expenseAccounts = await _accountingService.GetExpenseAccountsAsync();
            var expenseAccountList = expenseAccounts.Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.AccountCode} - {a.AccountName}"
            }).ToList();

            var accountSuggestions = new Dictionary<string, int?>();
            foreach (ExpenseCategory category in Enum.GetValues<ExpenseCategory>())
            {
                var suggestedAccount = await _accountingService.GetSuggestedAccountForExpenseCategoryAsync(category);
                accountSuggestions[((int)category).ToString()] = suggestedAccount?.Id;
            }

            ViewBag.Vendors = new SelectList(vendors, "Id", "CompanyName");
            ViewBag.Categories = new SelectList(categories, "Value", "Text");
            ViewBag.TaxCategories = new SelectList(taxCategories, "Value", "Text");
            ViewBag.ExpenseAccounts = new SelectList(expenseAccountList, "Value", "Text");
            ViewBag.AccountSuggestions = accountSuggestions;
        }

        // GET: /Expenses/Reports
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate, string expenseCategory = "All")
        {
            try
            {
                var defaultStartDate = startDate ?? new DateTime(DateTime.Now.Year, 1, 1);
                var defaultEndDate = endDate ?? DateTime.Now;

                _logger.LogInformation("Generating expense reports from {StartDate} to {EndDate}, Category: {Category}", 
                    defaultStartDate, defaultEndDate, expenseCategory);

                // Include Documents in the query
                var expensePaymentsQuery = _context.ExpensePayments
                    .Include(ep => ep.Expense)
                    .Include(ep => ep.Vendor)
                    .Include(ep => ep.Project)
                    .Include(ep => ep.Documents) // Add this line
                    .Where(ep => ep.PaymentDate >= defaultStartDate && ep.PaymentDate <= defaultEndDate);

                if (expenseCategory != "All")
                {
                    if (Enum.TryParse<ExpenseCategory>(expenseCategory, out var categoryType))
                    {
                        expensePaymentsQuery = expensePaymentsQuery.Where(ep => ep.Expense.Category == categoryType);
                    }
                }

                var expensePayments = await expensePaymentsQuery.ToListAsync();

                // Add document status analysis
                var paymentsWithoutDocuments = expensePayments.Where(ep => !ep.Documents.Any()).ToList();
                var documentComplianceRate = expensePayments.Count > 0 
                    ? ((double)(expensePayments.Count - paymentsWithoutDocuments.Count) / expensePayments.Count) * 100 
                    : 0;

                var totalExpenses = expensePayments.Sum(ep => ep.TotalAmount);
                var expenseCount = expensePayments.Count;
                var averageExpense = expenseCount > 0 ? totalExpenses / expenseCount : 0;

                var expensesByCategory = expensePayments
                    .GroupBy(ep => ep.Expense.Category)
                    .Select(g => new ExpenseCategoryData
                    {
                        Category = g.Key.ToString(),
                        CategoryDisplayName = GetCategoryDisplayName(g.Key),
                        TotalAmount = g.Sum(ep => ep.TotalAmount),
                        TransactionCount = g.Count(),
                        AverageAmount = g.Count() > 0 ? g.Sum(ep => ep.TotalAmount) / g.Count() : 0,
                        Percentage = totalExpenses > 0 ? (g.Sum(ep => ep.TotalAmount) / totalExpenses) * 100 : 0
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToList();

                var monthlyExpenses = expensePayments
                    .GroupBy(ep => new { ep.PaymentDate.Year, ep.PaymentDate.Month })
                    .Select(g => new MonthlyExpenseData
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        TotalAmount = g.Sum(ep => ep.TotalAmount),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(x => x.Year).ThenBy(x => x.Month)
                    .ToList();

                var topVendors = expensePayments
                    .GroupBy(ep => ep.Vendor)
                    .Select(g => new VendorExpenseData
                    {
                        VendorId = g.Key?.Id ?? 0,
                        VendorName = g.Key?.CompanyName ?? "Unknown",
                        TotalAmount = g.Sum(ep => ep.TotalAmount),
                        TransactionCount = g.Count(),
                        AverageAmount = g.Count() > 0 ? g.Sum(ep => ep.TotalAmount) / g.Count() : 0
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .Take(10)
                    .ToList();

                var recentExpenses = expensePayments
                    .OrderByDescending(ep => ep.PaymentDate)
                    .Take(20)
                    .ToList();

                var viewModel = new ExpenseReportsViewModel
                {
                    StartDate = defaultStartDate,
                    EndDate = defaultEndDate,
                    ExpenseCategory = expenseCategory,
                    TotalExpenses = totalExpenses,
                    ExpenseCount = expenseCount,
                    AverageExpense = averageExpense,
                    ExpensesByCategory = expensesByCategory,
                    MonthlyExpenses = monthlyExpenses,
                    TopVendors = topVendors,
                    RecentExpensePayments = recentExpenses,
                    
                    // Add new properties for document tracking
                    PaymentsWithoutDocuments = paymentsWithoutDocuments,
                    DocumentComplianceRate = documentComplianceRate,
                    TotalPaymentsNeedingDocuments = paymentsWithoutDocuments.Count
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating expense reports");
                SetErrorMessage("An error occurred while generating the expense report."); // ✅ Using BaseController method
                return View(new ExpenseReportsViewModel());
            }
        }

        // GET: /Expenses/IncomeStatement
        public async Task<IActionResult> IncomeStatement(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                // Default to current year if no dates provided
                var defaultStartDate = startDate ?? new DateTime(DateTime.Now.Year, 1, 1);
                var defaultEndDate = endDate ?? DateTime.Now;

                _logger.LogInformation("Generating income statement from {StartDate} to {EndDate}", 
                    defaultStartDate, defaultEndDate);

                // Get all sales within date range
                var sales = await _context.Sales
                    .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Item)
                    .Where(s => s.SaleDate >= defaultStartDate && s.SaleDate <= defaultEndDate)
                    .ToListAsync();

                // Calculate revenue
                var totalRevenue = sales.Sum(s => s.TotalAmount);
                var totalProfit = sales.Sum(s => s.SaleItems?.Sum(si => si.Profit) ?? 0);

                // Calculate Cost of Goods Sold (COGS)
                var cogs = sales.Sum(s => s.SaleItems?.Sum(si => si.UnitCost * si.QuantitySold) ?? 0);

                // UPDATED: Get operating expenses from ExpensePayments table
                var operatingExpenses = await _context.ExpensePayments
                    .Include(ep => ep.Expense)
                    .Where(ep => ep.PaymentDate >= defaultStartDate && ep.PaymentDate <= defaultEndDate)
                    .ToListAsync();

                var totalOperatingExpenses = operatingExpenses.Sum(ep => ep.TotalAmount);

                // Calculate net income
                var grossProfit = totalRevenue - cogs;
                var netIncome = grossProfit - totalOperatingExpenses;

                // UPDATED: Break down operating expenses by ExpenseCategory
                var expenseBreakdown = operatingExpenses
                    .GroupBy(ep => ep.Expense.Category)
                    .Select(g => new ExpenseCategoryData
                    {
                        Category = g.Key.ToString(),
                        CategoryDisplayName = GetCategoryDisplayName(g.Key),
                        TotalAmount = g.Sum(ep => ep.TotalAmount)
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToList();

                var viewModel = new IncomeStatementViewModel
                {
                    StartDate = defaultStartDate,
                    EndDate = defaultEndDate,
                    TotalRevenue = totalRevenue,
                    CostOfGoodsSold = cogs,
                    GrossProfit = grossProfit,
                    TotalOperatingExpenses = totalOperatingExpenses,
                    NetIncome = netIncome,
                    ExpenseBreakdown = expenseBreakdown,
                    GrossProfitMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0,
                    NetProfitMargin = totalRevenue > 0 ? (netIncome / totalRevenue) * 100 : 0
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating income statement");
                SetErrorMessage("An error occurred while generating the income statement."); // ✅ Using BaseController method
                return View(new IncomeStatementViewModel());
            }
        }

        // GET: /Expenses/TaxReports  
        public async Task<IActionResult> TaxReports(int year = 0)
        {
            try
            {
                var taxYear = year == 0 ? DateTime.Now.Year : year;
                var startDate = new DateTime(taxYear, 1, 1);
                var endDate = new DateTime(taxYear, 12, 31);

                _logger.LogInformation("Generating tax reports for year {Year}", taxYear);

                var expensePayments = await _context.ExpensePayments
                    .Include(ep => ep.Expense)
                    .Include(ep => ep.Vendor)
                    .Where(ep => ep.PaymentDate >= startDate && ep.PaymentDate <= endDate)
                    .ToListAsync();

                var taxCategories = expensePayments
                    .GroupBy(ep => GetTaxCategory(ep.Expense.Category))
                    .Select(g => new TaxCategoryData
                    {
                        TaxCategory = g.Key,
                        TotalAmount = g.Sum(ep => ep.TotalAmount),
                        TransactionCount = g.Count(),
                        ExpensePayments = g.ToList() // Store ExpensePayment objects directly
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToList();

                var vendorSummary = expensePayments
                    .Where(ep => ep.Vendor != null)
                    .GroupBy(ep => ep.Vendor)
                    .Select(g => new VendorTaxSummary
                    {
                        VendorId = g.Key.Id,
                        VendorName = g.Key.CompanyName,
                        TotalPaid = g.Sum(ep => ep.TotalAmount),
                        TransactionCount = g.Count(),
                        RequiresForm1099 = g.Sum(ep => ep.TotalAmount) >= 600,
                        VendorTaxId = g.Key.TaxId
                    })
                    .OrderByDescending(x => x.TotalPaid)
                    .ToList();

                var viewModel = new TaxReportsViewModel
                {
                    TaxYear = taxYear,
                    StartDate = startDate,
                    EndDate = endDate,
                    TaxCategories = taxCategories,
                    VendorSummary = vendorSummary,
                    TotalDeductibleExpenses = taxCategories.Sum(tc => tc.TotalAmount),
                    Form1099Count = vendorSummary.Count(v => v.RequiresForm1099)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating tax reports");
                SetErrorMessage("An error occurred while generating the tax report."); // ✅ Using BaseController method
                return View(new TaxReportsViewModel());
            }
        }

        // GET: /Expenses/PayExpenses
        public async Task<IActionResult> PayExpenses()
        {
            try
            {
                var expenses = await _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.ExpenseCode)
                    .ToListAsync();

                var vendors = await _context.Vendors
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.CompanyName)
                    .ToListAsync();

                var categories = Enum.GetValues<ExpenseCategory>()
                    .Select(c => GetCategoryDisplayName(c))
                    .Distinct()
                    .ToList();

                var viewModel = new PayExpensesViewModel
                {
                    AvailableExpenses = expenses,
                    AvailableVendors = vendors,
                    AvailableCategories = categories,
                    PaymentDate = DateTime.Today,
                    PaymentMethod = "Check"
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pay expenses page");
                SetErrorMessage("Error loading expenses for payment."); // ✅ Using BaseController method
                return RedirectToAction("Index");
            }
        }

        // POST: /Expenses/ProcessPayments
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayments(PayExpensesViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await ReloadPayExpensesData(model);
                return View("PayExpenses", model);
            }

            try
            {
                var selectedExpenses = model.SelectedExpenses?.Where(e => e.IsSelected) ?? Enumerable.Empty<SelectedExpenseViewModel>();
                
                if (!selectedExpenses.Any())
                {
                    SetErrorMessage("Please select at least one expense to pay."); // ✅ Using BaseController method
                    await ReloadPayExpensesData(model);
                    return View("PayExpenses", model);
                }

                var paymentsCreated = 0;
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    foreach (var selectedExpense in selectedExpenses)
                    {
                        if (!selectedExpense.VendorId.HasValue || selectedExpense.Amount <= 0)
                            continue;

                        var expensePayment = new ExpensePayment
                        {
                            ExpenseId = selectedExpense.ExpenseId,
                            VendorId = selectedExpense.VendorId.Value,
                            PaymentDate = model.PaymentDate,
                            Amount = selectedExpense.Amount,
                            PaymentMethod = model.PaymentMethod,
                            PaymentReference = model.PaymentReference,
                            Notes = selectedExpense.Notes,
                            CreatedDate = DateTime.Now,
                            CreatedBy = User.Identity?.Name ?? "System"
                        };

                        _context.ExpensePayments.Add(expensePayment);
                        await _context.SaveChangesAsync(); // Save to get the ID

                        // ✅ Generate journal entries for the expense payment
                        var journalSuccess = await _accountingService.GenerateJournalEntriesForExpensePaymentAsync(expensePayment);
                        if (!journalSuccess)
                        {
                            _logger.LogWarning("Failed to generate journal entries for expense payment {PaymentId}", expensePayment.Id);
                            // Continue anyway - don't fail the payment recording due to journal entry issues
                        }

                        paymentsCreated++;
                    }

                    await transaction.CommitAsync();

                    SetSuccessMessage($"Successfully processed {paymentsCreated} expense payment(s)!"); // ✅ Using BaseController method
                    return RedirectToAction("Index");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expense payments");
                SetErrorMessage("Error processing payments. Please try again."); // ✅ Using BaseController method
                await ReloadPayExpensesData(model);
                return View("PayExpenses", model);
            }
        }

        // GET: /Expenses/Details/5 - Use correct navigation property name
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var expense = await _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .Include(e => e.Payments)
                        .ThenInclude(ep => ep.Vendor)
                    .Include(e => e.Payments)
                        .ThenInclude(ep => ep.Documents) // Add this line to include documents
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (expense == null)
                {
                    SetErrorMessage("Expense not found.");
                    return RedirectToAction("Index");
                }

                return View(expense);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expense details: {ExpenseId}", id);
                SetErrorMessage("Error loading expense details.");
                return RedirectToAction("Index");
            }
        }

        // GET: /Expenses/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var expense = await _context.Expenses.FindAsync(id);
                if (expense == null)
                {
                    SetErrorMessage("Expense not found."); // ✅ Using BaseController method
                    return RedirectToAction("Index");
                }

                var vendors = await _context.Vendors
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.CompanyName)
                    .ToListAsync();

                var viewModel = new EditExpenseViewModel
                {
                    Id = expense.Id,
                    ExpenseCode = expense.ExpenseCode,
                    Description = expense.Description,
                    Category = expense.Category,
                    UnitOfMeasure = expense.UnitOfMeasure,
                    DefaultAmount = expense.DefaultAmount,
                    DefaultVendorId = expense.DefaultVendorId,
                    IsRecurring = expense.IsRecurring,
                    RecurringFrequency = expense.RecurringFrequency,
                    Comments = expense.Comments,
                    IsActive = expense.IsActive,
                    CreatedDate = expense.CreatedDate // ✅ Add this for display
                };

                ViewBag.Categories = new SelectList(Enum.GetValues<ExpenseCategory>().Select(c => new
                {
                    Value = c,
                    Text = GetCategoryDisplayName(c)
                }), "Value", "Text", expense.Category);

                ViewBag.Vendors = new SelectList(vendors, "Id", "CompanyName", expense.DefaultVendorId);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expense for edit: {ExpenseId}", id);
                SetErrorMessage("Error loading expense."); // ✅ Using BaseController method
                return RedirectToAction("Index");
            }
        }

        // POST: /Expenses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditExpenseViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                await ReloadEditViewData(model);
                return View(model);
            }

            try
            {
                var expense = await _context.Expenses.FindAsync(id);
                if (expense == null)
                {
                    SetErrorMessage("Expense not found."); // ✅ Using BaseController method
                    return RedirectToAction("Index");
                }

                expense.ExpenseCode = model.ExpenseCode;
                expense.Description = model.Description;
                expense.Category = model.Category;
                expense.UnitOfMeasure = model.UnitOfMeasure;
                expense.DefaultAmount = model.DefaultAmount;
                expense.DefaultVendorId = model.DefaultVendorId;
                expense.IsRecurring = model.IsRecurring;
                expense.RecurringFrequency = model.RecurringFrequency;
                expense.Comments = model.Comments;
                expense.IsActive = model.IsActive;
                expense.LastModified = DateTime.Now;

                await _context.SaveChangesAsync();

                SetSuccessMessage("Expense updated successfully!"); // ✅ Using BaseController method
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expense: {ExpenseId}", id);
                SetErrorMessage("Error updating expense."); // ✅ Using BaseController method
                await ReloadEditViewData(model);
                return View(model);
            }
        }

        // GET: /Expenses/RecordPayment?expenseId=1 (Record payment for a specific expense)
        [HttpGet]
        public async Task<IActionResult> RecordPayment(int expenseId)
        {
            try
            {
                // Get the specific expense to record payment for
                var expense = await _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .FirstOrDefaultAsync(e => e.Id == expenseId && e.IsActive);

                if (expense == null)
                {
                    SetErrorMessage("Expense not found or is inactive.");
                    return RedirectToAction("Index");
                }

                // Get all vendors for the dropdown
                var vendors = await _context.Vendors
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.CompanyName)
                    .ToListAsync();

                var categories = Enum.GetValues<ExpenseCategory>()
                    .Select(c => GetCategoryDisplayName(c))
                    .Distinct()
                    .ToList();

                // Create view model with just this one expense pre-selected
                var selectedExpenses = new List<SelectedExpenseViewModel>
                {
                    new SelectedExpenseViewModel
                    {
                        ExpenseId = expense.Id,
                        IsSelected = true,
                        VendorId = expense.DefaultVendorId,
                        Amount = expense.DefaultAmount ?? 0,
                        Notes = $"Payment for {expense.ExpenseCode}"
                    }
                };

                var viewModel = new RecordExpensePaymentsViewModel
                {
                    AvailableExpenses = new List<Expense> { expense },
                    AvailableVendors = vendors,
                    AvailableCategories = categories,
                    PaymentDate = DateTime.Today,
                    PaymentMethod = "Check",
                    SelectedExpenses = selectedExpenses
                };

                // Use the RecordPayments view with pre-populated data
                return View("RecordPayments", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading specific expense for payment recording: {ExpenseId}", expenseId);
                SetErrorMessage("Error loading expense for payment recording.");
                return RedirectToAction("Index");
            }
        }

        // GET: /Expenses/RecordPayments
        [HttpGet]
        public async Task<IActionResult> RecordPayments()
        {
            try
            {
                var expenses = await _context.Expenses
                    .Include(e => e.DefaultVendor)
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.ExpenseCode)
                    .ToListAsync();

                var vendors = await _context.Vendors
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.CompanyName)
                    .ToListAsync();

                var categories = Enum.GetValues<ExpenseCategory>()
                    .Select(c => GetCategoryDisplayName(c))
                    .Distinct()
                    .ToList();

                var viewModel = new RecordExpensePaymentsViewModel
                {
                    AvailableExpenses = expenses,
                    AvailableVendors = vendors,
                    AvailableCategories = categories,
                    PaymentDate = DateTime.Today,
                    PaymentMethod = "Check"
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading record expense payments page");
                SetErrorMessage("Error loading expenses for payment recording.");
                return RedirectToAction("Index");
            }
        }

        // POST: /Expenses/RecordPayments
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52428800)] // 50MB limit to accommodate documents
        public async Task<IActionResult> RecordPayments(RecordExpensePaymentsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await ReloadRecordPaymentsData(model);
                return View(model);
            }

            try
            {
                var selectedExpenses = model.SelectedExpenses?.Where(e => e.IsSelected) ?? Enumerable.Empty<SelectedExpenseViewModel>();
                
                if (!selectedExpenses.Any())
                {
                    SetErrorMessage("Please select at least one expense to record payment for.");
                    await ReloadRecordPaymentsData(model);
                    return View(model);
                }

                var paymentsCreated = 0;
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    foreach (var selectedExpense in selectedExpenses)
                    {
                        if (!selectedExpense.VendorId.HasValue || selectedExpense.Amount <= 0)
                            continue;

                        var expensePayment = new ExpensePayment
                        {
                            ExpenseId = selectedExpense.ExpenseId,
                            VendorId = selectedExpense.VendorId.Value,
                            PaymentDate = model.PaymentDate,
                            Amount = selectedExpense.Amount,
                            PaymentMethod = model.PaymentMethod,
                            PaymentReference = model.PaymentReference,
                            Notes = selectedExpense.Notes,
                            CreatedDate = DateTime.Now,
                            CreatedBy = User.Identity?.Name ?? "System"
                        };

                        _context.ExpensePayments.Add(expensePayment);
                        await _context.SaveChangesAsync(); // Save to get the ID

                        // ✅ Generate journal entries for the expense payment
                        var journalSuccess = await _accountingService.GenerateJournalEntriesForExpensePaymentAsync(expensePayment);
                        if (!journalSuccess)
                        {
                            _logger.LogWarning("Failed to generate journal entries for expense payment {PaymentId}", expensePayment.Id);
                            // Continue anyway - don't fail the payment recording due to journal entry issues
                        }

                        // Handle document upload if provided
                        if (selectedExpense.DocumentFile != null && selectedExpense.DocumentFile.Length > 0)
                        {
                            await ProcessDocumentUpload(selectedExpense.DocumentFile, expensePayment.Id, selectedExpense.DocumentName, selectedExpense.DocumentType);
                        }

                        paymentsCreated++;
                    }

                    await transaction.CommitAsync();

                    SetSuccessMessage($"Successfully recorded {paymentsCreated} expense payment(s)!");
                    return RedirectToAction("Index");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording expense payments");
                SetErrorMessage("Error recording payments. Please try again.");
                await ReloadRecordPaymentsData(model);
                return View(model);
            }
        }

        // Add these methods to the ExpensesController class (after the RecordPayments methods)

        // GET: /Expenses/UploadDocument?expensePaymentId=1
        [HttpGet]
        public async Task<IActionResult> UploadDocument(int expensePaymentId)
        {
            try
            {
                var expensePayment = await _context.ExpensePayments
                    .Include(ep => ep.Expense)
                    .Include(ep => ep.Vendor)
                    .FirstOrDefaultAsync(ep => ep.Id == expensePaymentId);

                if (expensePayment == null)
                {
                    SetErrorMessage("Expense payment not found.");
                    return RedirectToAction("Reports");
                }

                var viewModel = new ExpenseDocumentUploadViewModel
                {
                    ExpensePaymentId = expensePaymentId,
                    ExpenseDetails = $"{expensePayment.Expense.ExpenseCode} - {expensePayment.PaymentDate:MM/dd/yyyy}",
                    VendorName = expensePayment.Vendor?.CompanyName ?? "Unknown",
                    Amount = expensePayment.TotalAmount
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading upload document page for ExpensePaymentId: {ExpensePaymentId}", expensePaymentId);
                SetErrorMessage("Error loading upload page.");
                return RedirectToAction("Reports");
            }
        }

        // POST: /Expenses/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(26214400)] // 25MB limit
        public async Task<IActionResult> UploadDocument(ExpenseDocumentUploadViewModel viewModel)
        {
            try
            {
                var expensePayment = await _context.ExpensePayments
                    .Include(ep => ep.Expense)
                    .Include(ep => ep.Vendor)
                    .FirstOrDefaultAsync(ep => ep.Id == viewModel.ExpensePaymentId);

                if (expensePayment == null)
                {
                    SetErrorMessage("Expense payment not found.");
                    return RedirectToAction("Reports");
                }

                if (viewModel.DocumentFile == null || viewModel.DocumentFile.Length == 0)
                {
                    ModelState.AddModelError("DocumentFile", "Please select a file to upload.");
                    viewModel.ExpenseDetails = $"{expensePayment.Expense.ExpenseCode} - {expensePayment.PaymentDate:MM/dd/yyyy}";
                    viewModel.VendorName = expensePayment.Vendor?.CompanyName ?? "Unknown";
                    viewModel.Amount = expensePayment.TotalAmount;
                    return View(viewModel);
                }

                // Validate file size (25MB limit)
                var maxFileSize = 25 * 1024 * 1024;
                if (viewModel.DocumentFile.Length > maxFileSize)
                {
                    ModelState.AddModelError("DocumentFile", "File size cannot exceed 25MB.");
                }

                // Validate file type
                var fileExtension = Path.GetExtension(viewModel.DocumentFile.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip" };
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("DocumentFile", "File type not allowed. Please check the supported file types.");
                }

                if (!ModelState.IsValid)
                {
                    viewModel.ExpenseDetails = $"{expensePayment.Expense.ExpenseCode} - {expensePayment.PaymentDate:MM/dd/yyyy}";
                    viewModel.VendorName = expensePayment.Vendor?.CompanyName ?? "Unknown";
                    viewModel.Amount = expensePayment.TotalAmount;
                    return View(viewModel);
                }

                // Read file data
                byte[] fileData;
                using (var memoryStream = new MemoryStream())
                {
                    await viewModel.DocumentFile.CopyToAsync(memoryStream);
                    fileData = memoryStream.ToArray();
                }

                // Create document using existing PurchaseDocument model
                var document = new PurchaseDocument
                {
                    ExpensePaymentId = viewModel.ExpensePaymentId, // Link to expense payment
                    DocumentName = viewModel.DocumentName,
                    DocumentType = viewModel.DocumentType,
                    Description = viewModel.Description,
                    FileName = viewModel.DocumentFile.FileName,
                    ContentType = viewModel.DocumentFile.ContentType,
                    FileSize = viewModel.DocumentFile.Length,
                    DocumentData = fileData,
                    UploadedDate = DateTime.Now
                };

                _context.PurchaseDocuments.Add(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Expense document uploaded successfully: {DocumentId} for ExpensePaymentId: {ExpensePaymentId}", 
                    document.Id, viewModel.ExpensePaymentId);

                SetSuccessMessage($"Document '{viewModel.DocumentName}' uploaded successfully!");
                return RedirectToAction("Reports");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading expense document for ExpensePaymentId: {ExpensePaymentId}", viewModel.ExpensePaymentId);
                SetErrorMessage("An error occurred while uploading the document. Please try again.");
                return RedirectToAction("Reports");
            }
        }

        // GET: /Expenses/PreviewDocument/5
        public async Task<IActionResult> PreviewDocument(int id)
        {
            try
            {
                var document = await _context.PurchaseDocuments.FindAsync(id);

                if (document == null)
                {
                    SetErrorMessage("Document not found.");
                    return RedirectToAction("Reports");
                }

                // Check if the document is previewable
                var fileExtension = Path.GetExtension(document.FileName).ToLowerInvariant();
                var previewableTypes = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff" };

                if (!previewableTypes.Contains(fileExtension))
                {
                    SetErrorMessage("This file type cannot be previewed. Please download the file instead.");
                    return RedirectToAction("Reports");
                }

                // For images, return the image directly
                if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff" }.Contains(fileExtension))
                {
                    return File(document.DocumentData, document.ContentType);
                }

                // For PDFs, return with inline disposition
                if (document.ContentType == "application/pdf")
                {
                    Response.Headers.Add("Content-Disposition", "inline");
                }

                return File(document.DocumentData, document.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing document: {DocumentId}", id);
                SetErrorMessage("Error loading document preview.");
                return RedirectToAction("Reports");
            }
        }

        // GET: /Expenses/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id)
        {
            try
            {
                var document = await _context.PurchaseDocuments.FindAsync(id);

                if (document == null)
                {
                    SetErrorMessage("Document not found.");
                    return RedirectToAction("Reports");
                }

                return File(document.DocumentData, document.ContentType, document.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document: {DocumentId}", id);
                SetErrorMessage("Error downloading document.");
                return RedirectToAction("Reports");
            }
        }

        // POST: /Expenses/DeleteDocument/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            try
            {
                var document = await _context.PurchaseDocuments.FindAsync(id);
                if (document == null)
                {
                    SetErrorMessage("Document not found.");
                    return RedirectToAction("Reports");
                }

                var expensePaymentId = document.ExpensePaymentId;
                
                // Get the expense ID for redirect
                var expensePayment = await _context.ExpensePayments
                    .FirstOrDefaultAsync(ep => ep.Id == expensePaymentId);

                _context.PurchaseDocuments.Remove(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Expense document deleted: {DocumentId} from ExpensePaymentId: {ExpensePaymentId}", id, expensePaymentId);
                SetSuccessMessage("Document deleted successfully.");
                
                // Redirect back to the expense details if we have an expense, otherwise to reports
                if (expensePayment?.ExpenseId != null)
                {
                    return RedirectToAction("Details", new { id = expensePayment.ExpenseId });
                }
                
                return RedirectToAction("Reports");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expense document: {DocumentId}", id);
                SetErrorMessage("An error occurred while deleting the document.");
                return RedirectToAction("Reports");
            }
        }

        // Helper method for document processing
        private async Task ProcessDocumentUpload(IFormFile documentFile, int expensePaymentId, string? documentName, string? documentType)
        {
            try
            {
                // Validate file
                var maxFileSize = 25 * 1024 * 1024; // 25MB
                if (documentFile.Length > maxFileSize)
                {
                    _logger.LogWarning("Document file too large: {Size} bytes", documentFile.Length);
                    return;
                }

                var fileExtension = Path.GetExtension(documentFile.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".doc", ".docx", ".xls", ".xlsx", ".txt" };
                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Invalid file type: {Extension}", fileExtension);
                    return;
                }

                // Read file data
                byte[] fileData;
                using (var memoryStream = new MemoryStream())
                {
                    await documentFile.CopyToAsync(memoryStream);
                    fileData = memoryStream.ToArray();
                }

                // Create document
                var document = new PurchaseDocument
                {
                    ExpensePaymentId = expensePaymentId,
                    DocumentName = documentName ?? Path.GetFileNameWithoutExtension(documentFile.FileName),
                    DocumentType = documentType ?? "Receipt",
                    FileName = documentFile.FileName,
                    ContentType = documentFile.ContentType,
                    FileSize = documentFile.Length,
                    DocumentData = fileData,
                    UploadedDate = DateTime.Now
                };

                _context.PurchaseDocuments.Add(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Document uploaded for expense payment: {ExpensePaymentId}", expensePaymentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for expense payment: {ExpensePaymentId}", expensePaymentId);
                // Don't throw - we don't want document upload issues to fail the payment recording
            }
        }

        // Update the helper method name
        private async Task ReloadRecordPaymentsData(RecordExpensePaymentsViewModel model)
        {
            model.AvailableExpenses = await _context.Expenses
                .Include(e => e.DefaultVendor)
                .Where(e => e.IsActive)
                .OrderBy(e => e.ExpenseCode)
                .ToListAsync();

            model.AvailableVendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            model.AvailableCategories = Enum.GetValues<ExpenseCategory>()
                .Select(c => GetCategoryDisplayName(c))
                .Distinct()
                .ToList();
        }

        //// Keep the legacy methods for backward compatibility but mark as obsolete
        //[Obsolete("Use RecordPayment instead")]
        //public async Task<IActionResult> PayExpense(int expenseId) => await RecordPayment(expenseId);

        //[Obsolete("Use RecordPayments instead")]
        //public async Task<IActionResult> PayExpenses() => await RecordPayments();

        //[Obsolete("Use RecordPayments instead")]
        //public async Task<IActionResult> ProcessPayments(PayExpensesViewModel model)
        //{
        //    // Convert old model to new model and redirect
        //    var newModel = new RecordExpensePaymentsViewModel
        //    {
        //        AvailableExpenses = model.AvailableExpenses,
        //        AvailableVendors = model.AvailableVendors,
        //        AvailableCategories = model.AvailableCategories,
        //        PaymentDate = model.PaymentDate,
        //        PaymentMethod = model.PaymentMethod,
        //        PaymentReference = model.PaymentReference,
        //        SelectedExpenses = model.SelectedExpenses
        //    };
            
        //    return await RecordPayments(newModel);
        //}

        // Helper methods
        private string GetCategoryDisplayName(ExpenseCategory category)
        {
            return category switch
            {
                ExpenseCategory.OfficeSupplies => "Office Supplies",
                ExpenseCategory.Utilities => "Utilities",
                ExpenseCategory.ProfessionalServices => "Professional Services",
                ExpenseCategory.SoftwareLicenses => "Software & Technology",
                ExpenseCategory.Travel => "Travel & Transportation",
                ExpenseCategory.Equipment => "Equipment & Maintenance",
                ExpenseCategory.Marketing => "Marketing & Advertising",
                ExpenseCategory.Research => "Research & Development",
                ExpenseCategory.Insurance => "Insurance",
                ExpenseCategory.GeneralBusiness => "General Business",
                _ => category.ToString()
            };
        }

        private string GetTaxCategory(ExpenseCategory category)
        {
            return category switch
            {
                ExpenseCategory.ProfessionalServices => "Professional Services",
                ExpenseCategory.SoftwareLicenses => "Software & Technology",
                ExpenseCategory.Utilities => "Utilities",
                ExpenseCategory.GeneralBusiness => "General Business Expenses",
                ExpenseCategory.OfficeSupplies => "Office Supplies",
                ExpenseCategory.Research => "Research & Development",
                ExpenseCategory.Travel => "Travel & Transportation",
                ExpenseCategory.Equipment => "Equipment & Maintenance",
                ExpenseCategory.Marketing => "Marketing & Advertising",
                ExpenseCategory.Insurance => "Insurance",
                _ => "Other Business Expenses"
            };
        }

        private async Task ReloadCreateViewData()
        {
            var vendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            ViewBag.Categories = new SelectList(Enum.GetValues<ExpenseCategory>().Select(c => new
            {
                Value = c,
                Text = GetCategoryDisplayName(c)
            }), "Value", "Text");

            ViewBag.Vendors = new SelectList(vendors, "Id", "CompanyName");
        }

        // Helper method to reload pay expenses data
        private async Task ReloadPayExpensesData(PayExpensesViewModel model)
        {
            model.AvailableExpenses = await _context.Expenses
                .Include(e => e.DefaultVendor)
                .Where(e => e.IsActive)
                .OrderBy(e => e.ExpenseCode)
                .ToListAsync();

            model.AvailableVendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            model.AvailableCategories = Enum.GetValues<ExpenseCategory>()
                .Select(c => GetCategoryDisplayName(c))
                .Distinct()
                .ToList();
        }

        // Helper method to reload edit view data  
        private async Task ReloadEditViewData(EditExpenseViewModel model)
        {
            var vendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.CompanyName)
                .ToListAsync();

            ViewBag.Categories = new SelectList(Enum.GetValues<ExpenseCategory>().Select(c => new
            {
                Value = c,
                Text = GetCategoryDisplayName(c)
            }), "Value", "Text", model.Category);

            ViewBag.Vendors = new SelectList(vendors, "Id", "CompanyName", model.DefaultVendorId);
        }
    }
}