using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
    public class ExpensesController : Controller
    {
        private readonly InventoryContext _context;
        private readonly ILogger<ExpensesController> _logger;

        public ExpensesController(InventoryContext context, ILogger<ExpensesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Expenses/Reports
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate, string expenseCategory = "All")
        {
            try
            {
                // Default to current year if no dates provided
                var defaultStartDate = startDate ?? new DateTime(DateTime.Now.Year, 1, 1);
                var defaultEndDate = endDate ?? DateTime.Now;

                _logger.LogInformation("Generating expense reports from {StartDate} to {EndDate}, Category: {Category}", 
                    defaultStartDate, defaultEndDate, expenseCategory);

                // Get all expense-related purchases within date range
                var expensePurchasesQuery = _context.Purchases
                    .Include(p => p.Item)
                    .Include(p => p.Vendor)
                    .Where(p => p.PurchaseDate >= defaultStartDate && p.PurchaseDate <= defaultEndDate)
                    .Where(p => p.Item.ItemType == ItemType.Expense || 
                               p.Item.ItemType == ItemType.Utility || 
                               p.Item.ItemType == ItemType.Subscription ||
                               p.Item.ItemType == ItemType.Consumable ||
                               p.Item.ItemType == ItemType.Service);

                // Apply category filter
                if (expenseCategory != "All")
                {
                    if (Enum.TryParse<ItemType>(expenseCategory, out var categoryType))
                    {
                        expensePurchasesQuery = expensePurchasesQuery.Where(p => p.Item.ItemType == categoryType);
                    }
                }

                var expensePurchases = await expensePurchasesQuery.ToListAsync();

                // Calculate summary statistics
                var totalExpenses = expensePurchases.Sum(p => p.TotalCost);
                var expenseCount = expensePurchases.Count;
                var averageExpense = expenseCount > 0 ? totalExpenses / expenseCount : 0;

                // Group by category
                var expensesByCategory = expensePurchases
                    .GroupBy(p => p.Item.ItemType)
                    .Select(g => new ExpenseCategoryData
                    {
                        Category = g.Key.ToString(),
                        CategoryDisplayName = GetCategoryDisplayName(g.Key),
                        TotalAmount = g.Sum(p => p.TotalCost),
                        TransactionCount = g.Count(),
                        AverageAmount = g.Count() > 0 ? g.Sum(p => p.TotalCost) / g.Count() : 0,
                        Percentage = totalExpenses > 0 ? (g.Sum(p => p.TotalCost) / totalExpenses) * 100 : 0
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToList();

                // Group by month for trend analysis
                var monthlyExpenses = expensePurchases
                    .GroupBy(p => new { p.PurchaseDate.Year, p.PurchaseDate.Month })
                    .Select(g => new MonthlyExpenseData
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        TotalAmount = g.Sum(p => p.TotalCost),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(x => x.Year).ThenBy(x => x.Month)
                    .ToList();

                // Top vendors by expense amount
                var topVendors = expensePurchases
                    .GroupBy(p => p.Vendor)
                    .Select(g => new VendorExpenseData
                    {
                        VendorId = g.Key?.Id ?? 0,
                        VendorName = g.Key?.CompanyName ?? "Unknown",
                        TotalAmount = g.Sum(p => p.TotalCost),
                        TransactionCount = g.Count(),
                        AverageAmount = g.Count() > 0 ? g.Sum(p => p.TotalCost) / g.Count() : 0
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .Take(10)
                    .ToList();

                // Recent expense transactions
                var recentExpenses = expensePurchases
                    .OrderByDescending(p => p.PurchaseDate)
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
                    RecentExpenses = recentExpenses
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating expense reports");
                TempData["ErrorMessage"] = "An error occurred while generating the expense report.";
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

                // Get operating expenses
                var operatingExpenses = await _context.Purchases
                    .Include(p => p.Item)
                    .Where(p => p.PurchaseDate >= defaultStartDate && p.PurchaseDate <= defaultEndDate)
                    .Where(p => p.Item.ItemType == ItemType.Expense || 
                               p.Item.ItemType == ItemType.Utility || 
                               p.Item.ItemType == ItemType.Subscription ||
                               p.Item.ItemType == ItemType.Service)
                    .ToListAsync();

                var totalOperatingExpenses = operatingExpenses.Sum(p => p.TotalCost);

                // Calculate net income
                var grossProfit = totalRevenue - cogs;
                var netIncome = grossProfit - totalOperatingExpenses;

                // Break down operating expenses by category
                var expenseBreakdown = operatingExpenses
                    .GroupBy(p => p.Item.ItemType)
                    .Select(g => new ExpenseCategoryData
                    {
                        Category = g.Key.ToString(),
                        CategoryDisplayName = GetCategoryDisplayName(g.Key),
                        TotalAmount = g.Sum(p => p.TotalCost)
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
                TempData["ErrorMessage"] = "An error occurred while generating the income statement.";
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

                // Get all expense purchases for the tax year
                var expensePurchases = await _context.Purchases
                    .Include(p => p.Item)
                    .Include(p => p.Vendor)
                    .Where(p => p.PurchaseDate >= startDate && p.PurchaseDate <= endDate)
                    .Where(p => p.Item.ItemType == ItemType.Expense || 
                               p.Item.ItemType == ItemType.Utility || 
                               p.Item.ItemType == ItemType.Subscription ||
                               p.Item.ItemType == ItemType.Service ||
                               p.Item.ItemType == ItemType.Consumable ||
                               p.Item.ItemType == ItemType.RnDMaterials)
                    .ToListAsync();

                // Map to tax categories
                var taxCategories = expensePurchases
                    .GroupBy(p => GetTaxCategory(p.Item.ItemType))
                    .Select(g => new TaxCategoryData
                    {
                        TaxCategory = g.Key,
                        TotalAmount = g.Sum(p => p.TotalCost),
                        TransactionCount = g.Count(),
                        Purchases = g.ToList()
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToList();

                // Generate 1099-ready vendor summary (vendors with payments >= $600)
                var vendorSummary = expensePurchases
                    .Where(p => p.Vendor != null)
                    .GroupBy(p => p.Vendor)
                    .Select(g => new VendorTaxSummary
                    {
                        VendorId = g.Key.Id,
                        VendorName = g.Key.CompanyName,
                        TotalPaid = g.Sum(p => p.TotalCost),
                        TransactionCount = g.Count(),
                        RequiresForm1099 = g.Sum(p => p.TotalCost) >= 600,
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
                TempData["ErrorMessage"] = "An error occurred while generating the tax report.";
                return View(new TaxReportsViewModel());
            }
        }

        private string GetCategoryDisplayName(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.Expense => "General Business Expenses",
                ItemType.Utility => "Utilities",
                ItemType.Subscription => "Software & Technology",
                ItemType.Service => "Professional Services",
                ItemType.Consumable => "Office Supplies",
                ItemType.RnDMaterials => "Research & Development",
                _ => itemType.ToString()
            };
        }

        private string GetTaxCategory(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.Service => "Professional Services",
                ItemType.Subscription => "Software & Technology",
                ItemType.Utility => "Utilities",
                ItemType.Expense => "General Business Expenses",
                ItemType.Consumable => "Office Supplies",
                ItemType.RnDMaterials => "Research & Development",
                _ => "Other Business Expenses"
            };
        }
    }
}