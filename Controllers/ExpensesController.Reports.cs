// Controllers/ExpensesController.Reports.cs
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
    public partial class ExpensesController
    {
        // ============= Reports =============

        // GET: /Expenses/Reports
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate, string expenseCategory = "All")
        {
            try
            {
                var defaultStartDate = startDate ?? new DateTime(DateTime.Now.Year, 1, 1);
                var defaultEndDate   = endDate   ?? DateTime.Now;

                _logger.LogInformation("Generating expense reports from {StartDate} to {EndDate}, Category: {Category}",
                    defaultStartDate, defaultEndDate, expenseCategory);

                var expensePaymentsQuery = _context.ExpensePayments
                    .Include(ep => ep.Expense)
                    .Include(ep => ep.Vendor)
                    .Include(ep => ep.Project)
                    .Include(ep => ep.Documents)
                    .Where(ep => ep.PaymentDate >= defaultStartDate && ep.PaymentDate <= defaultEndDate);

                if (expenseCategory != "All" && Enum.TryParse<ExpenseCategory>(expenseCategory, out var categoryType))
                    expensePaymentsQuery = expensePaymentsQuery.Where(ep => ep.Expense.Category == categoryType);

                var expensePayments = await expensePaymentsQuery.ToListAsync();

                var paymentsWithoutDocuments = expensePayments.Where(ep => !ep.Documents.Any()).ToList();
                var documentComplianceRate   = expensePayments.Count > 0
                    ? ((double)(expensePayments.Count - paymentsWithoutDocuments.Count) / expensePayments.Count) * 100
                    : 0;

                var totalExpenses  = expensePayments.Sum(ep => ep.TotalAmount);
                var expenseCount   = expensePayments.Count;
                var averageExpense = expenseCount > 0 ? totalExpenses / expenseCount : 0;

                var expensesByCategory = expensePayments
                    .GroupBy(ep => ep.Expense.Category)
                    .Select(g => new ExpenseCategoryData
                    {
                        Category            = g.Key.ToString(),
                        CategoryDisplayName = GetCategoryDisplayName(g.Key),
                        TotalAmount         = g.Sum(ep => ep.TotalAmount),
                        TransactionCount    = g.Count(),
                        AverageAmount       = g.Count() > 0 ? g.Sum(ep => ep.TotalAmount) / g.Count() : 0,
                        Percentage          = totalExpenses > 0 ? (g.Sum(ep => ep.TotalAmount) / totalExpenses) * 100 : 0
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToList();

                var monthlyExpenses = expensePayments
                    .GroupBy(ep => new { ep.PaymentDate.Year, ep.PaymentDate.Month })
                    .Select(g => new MonthlyExpenseData
                    {
                        Year             = g.Key.Year,
                        Month            = g.Key.Month,
                        MonthName        = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        TotalAmount      = g.Sum(ep => ep.TotalAmount),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(x => x.Year).ThenBy(x => x.Month)
                    .ToList();

                var topVendors = expensePayments
                    .GroupBy(ep => ep.Vendor)
                    .Select(g => new VendorExpenseData
                    {
                        VendorId         = g.Key?.Id ?? 0,
                        VendorName       = g.Key?.CompanyName ?? "Unknown",
                        TotalAmount      = g.Sum(ep => ep.TotalAmount),
                        TransactionCount = g.Count(),
                        AverageAmount    = g.Count() > 0 ? g.Sum(ep => ep.TotalAmount) / g.Count() : 0
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
                    StartDate                   = defaultStartDate,
                    EndDate                     = defaultEndDate,
                    ExpenseCategory             = expenseCategory,
                    TotalExpenses               = totalExpenses,
                    ExpenseCount                = expenseCount,
                    AverageExpense              = averageExpense,
                    ExpensesByCategory          = expensesByCategory,
                    MonthlyExpenses             = monthlyExpenses,
                    TopVendors                  = topVendors,
                    RecentExpensePayments       = recentExpenses,
                    PaymentsWithoutDocuments    = paymentsWithoutDocuments,
                    DocumentComplianceRate      = documentComplianceRate,
                    TotalPaymentsNeedingDocuments = paymentsWithoutDocuments.Count
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating expense reports");
                SetErrorMessage("An error occurred while generating the expense report.");
                return View(new ExpenseReportsViewModel());
            }
        }

        // GET: /Expenses/IncomeStatement
        public async Task<IActionResult> IncomeStatement(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var defaultStartDate = startDate ?? new DateTime(DateTime.Now.Year, 1, 1);
                var defaultEndDate   = endDate   ?? DateTime.Now;

                _logger.LogInformation("Generating income statement from {StartDate} to {EndDate}",
                    defaultStartDate, defaultEndDate);

                var sales = await _context.Sales
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Item)
                    .Where(s => s.SaleDate >= defaultStartDate && s.SaleDate <= defaultEndDate)
                    .ToListAsync();

                var totalRevenue = sales.Sum(s => s.TotalAmount);
                var cogs         = sales.Sum(s => s.SaleItems?.Sum(si => si.UnitCost * si.QuantitySold) ?? 0);

                var operatingExpenses = await _context.ExpensePayments
                    .Include(ep => ep.Expense)
                    .Where(ep => ep.PaymentDate >= defaultStartDate && ep.PaymentDate <= defaultEndDate)
                    .ToListAsync();

                var totalOperatingExpenses = operatingExpenses.Sum(ep => ep.TotalAmount);
                var grossProfit            = totalRevenue - cogs;
                var netIncome              = grossProfit - totalOperatingExpenses;

                var expenseBreakdown = operatingExpenses
                    .GroupBy(ep => ep.Expense.Category)
                    .Select(g => new ExpenseCategoryData
                    {
                        Category            = g.Key.ToString(),
                        CategoryDisplayName = GetCategoryDisplayName(g.Key),
                        TotalAmount         = g.Sum(ep => ep.TotalAmount)
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToList();

                var viewModel = new IncomeStatementViewModel
                {
                    StartDate               = defaultStartDate,
                    EndDate                 = defaultEndDate,
                    TotalRevenue            = totalRevenue,
                    CostOfGoodsSold         = cogs,
                    GrossProfit             = grossProfit,
                    TotalOperatingExpenses  = totalOperatingExpenses,
                    NetIncome               = netIncome,
                    ExpenseBreakdown        = expenseBreakdown,
                    GrossProfitMargin       = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0,
                    NetProfitMargin         = totalRevenue > 0 ? (netIncome  / totalRevenue) * 100 : 0
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating income statement");
                SetErrorMessage("An error occurred while generating the income statement.");
                return View(new IncomeStatementViewModel());
            }
        }

        // GET: /Expenses/TaxReports
        public async Task<IActionResult> TaxReports(int year = 0)
        {
            try
            {
                var taxYear   = year == 0 ? DateTime.Now.Year : year;
                var startDate = new DateTime(taxYear, 1, 1);
                var endDate   = new DateTime(taxYear, 12, 31);

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
                        TaxCategory      = g.Key,
                        TotalAmount      = g.Sum(ep => ep.TotalAmount),
                        TransactionCount = g.Count(),
                        ExpensePayments  = g.ToList()
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToList();

                var vendorSummary = expensePayments
                    .Where(ep => ep.Vendor != null)
                    .GroupBy(ep => ep.Vendor)
                    .Select(g => new VendorTaxSummary
                    {
                        VendorId          = g.Key!.Id,
                        VendorName        = g.Key.CompanyName,
                        TotalPaid         = g.Sum(ep => ep.TotalAmount),
                        TransactionCount  = g.Count(),
                        RequiresForm1099  = g.Sum(ep => ep.TotalAmount) >= 600,
                        VendorTaxId       = g.Key.TaxId
                    })
                    .OrderByDescending(x => x.TotalPaid)
                    .ToList();

                var viewModel = new TaxReportsViewModel
                {
                    TaxYear                  = taxYear,
                    StartDate                = startDate,
                    EndDate                  = endDate,
                    TaxCategories            = taxCategories,
                    VendorSummary            = vendorSummary,
                    TotalDeductibleExpenses  = taxCategories.Sum(tc => tc.TotalAmount),
                    Form1099Count            = vendorSummary.Count(v => v.RequiresForm1099)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating tax reports");
                SetErrorMessage("An error occurred while generating the tax report.");
                return View(new TaxReportsViewModel());
            }
        }
    }
}
