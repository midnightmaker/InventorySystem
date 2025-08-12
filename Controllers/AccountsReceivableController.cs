using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Controllers
{
    public class AccountsReceivableController : Controller
    {
        private readonly ISalesService _salesService;
        private readonly ICustomerService _customerService;
        private readonly ILogger<AccountsReceivableController> _logger;

        public AccountsReceivableController(
            ISalesService salesService,
            ICustomerService customerService,
            ILogger<AccountsReceivableController> logger)
        {
            _salesService = salesService;
            _customerService = customerService;
            _logger = logger;
        }

        // GET: AccountsReceivable
        public async Task<IActionResult> Index()
        {
            try
        {
                var viewModel = await BuildDashboardViewModel();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Accounts Receivable dashboard");
                TempData["ErrorMessage"] = $"Error loading dashboard: {ex.Message}";
                return View(new AccountsReceivableDashboardViewModel());
            }
        }

        // GET: AccountsReceivable/AgingReport
        public async Task<IActionResult> AgingReport()
        {
            try
            {
                _logger.LogInformation("Loading A/R Aging Report");

                var allSales = await _salesService.GetAllSalesAsync();
                var unpaidSales = allSales.Where(s => s.PaymentStatus != PaymentStatus.Paid && s.SaleStatus != SaleStatus.Cancelled).ToList();

                _logger.LogInformation("Found {UnpaidSalesCount} unpaid sales to analyze", unpaidSales.Count);

                var agingReport = new AgingReportViewModel
                {
                    Current = new List<AgingReportItem>(),
                    Days1To30 = new List<AgingReportItem>(),
                    Days31To60 = new List<AgingReportItem>(),
                    Days61To90 = new List<AgingReportItem>(),
                    Over90Days = new List<AgingReportItem>()
                };

                foreach (var sale in unpaidSales)
                {
                    var daysOld = (DateTime.Today - sale.SaleDate).Days;
                    var item = new AgingReportItem
                    {
                        SaleId = sale.Id,
                        SaleNumber = sale.SaleNumber,
                        CustomerName = sale.Customer?.CustomerName ?? "Unknown",
                        CustomerId = sale.CustomerId,
                        SaleDate = sale.SaleDate,
                        DueDate = sale.PaymentDueDate,
                        Amount = sale.TotalAmount,
                        DaysOld = daysOld,
                        PaymentStatus = sale.PaymentStatus
                    };

                    // Categorize into aging buckets
                    if (daysOld <= 0)
                        agingReport.Current.Add(item);
                    else if (daysOld <= 30)
                        agingReport.Days1To30.Add(item);
                    else if (daysOld <= 60)
                        agingReport.Days31To60.Add(item);
                    else if (daysOld <= 90)
                        agingReport.Days61To90.Add(item);
                    else
                        agingReport.Over90Days.Add(item);
                }

                // Pre-calculate totals to prevent repeated calculations in the view
                agingReport.CurrentTotal = agingReport.Current.Sum(i => i.Amount);
                agingReport.Days1To30Total = agingReport.Days1To30.Sum(i => i.Amount);
                agingReport.Days31To60Total = agingReport.Days31To60.Sum(i => i.Amount);
                agingReport.Days61To90Total = agingReport.Days61To90.Sum(i => i.Amount);
                agingReport.Over90DaysTotal = agingReport.Over90Days.Sum(i => i.Amount);

                _logger.LogInformation("Aging report built successfully - Total A/R: {GrandTotal:C}", agingReport.GrandTotal);

                return View(agingReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading aging report");
                TempData["ErrorMessage"] = $"Error loading aging report: {ex.Message}";
                return View(new AgingReportViewModel());
            }
        }

        // GET: AccountsReceivable/Collections
        public async Task<IActionResult> Collections()
        {
            try
            {
                var allSales = await _salesService.GetAllSalesAsync();
                var overdueAndPartialSales = allSales
                    .Where(s => (s.PaymentStatus == PaymentStatus.Overdue || s.PaymentStatus == PaymentStatus.PartiallyPaid) 
                               && s.SaleStatus != SaleStatus.Cancelled)
                    .OrderByDescending(s => s.DaysOverdue)
                    .ThenByDescending(s => s.TotalAmount)
                    .ToList();

                var collectionItems = overdueAndPartialSales.Select(s => new CollectionItem
                {
                    SaleId = s.Id,
                    SaleNumber = s.SaleNumber,
                    CustomerId = s.CustomerId,
                    CustomerName = s.Customer?.CustomerName ?? "Unknown",
                    CustomerEmail = s.Customer?.Email ?? "",
                    CustomerPhone = s.Customer?.Phone ?? "",
                    SaleDate = s.SaleDate,
                    DueDate = s.PaymentDueDate,
                    Amount = s.TotalAmount,
                    DaysOverdue = s.DaysOverdue,
                    PaymentStatus = s.PaymentStatus,
                    Priority = CalculateCollectionPriority(s.TotalAmount, s.DaysOverdue)
                }).ToList();

                var viewModel = new CollectionsViewModel
                {
                    CollectionItems = collectionItems,
                    TotalOverdueAmount = collectionItems.Sum(c => c.Amount),
                    HighPriorityCount = collectionItems.Count(c => c.Priority == CollectionPriority.High),
                    MediumPriorityCount = collectionItems.Count(c => c.Priority == CollectionPriority.Medium),
                    LowPriorityCount = collectionItems.Count(c => c.Priority == CollectionPriority.Low)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading collections");
                TempData["ErrorMessage"] = $"Error loading collections: {ex.Message}";
                return View(new CollectionsViewModel());
            }
        }

        // GET: AccountsReceivable/CustomerStatements
        public async Task<IActionResult> CustomerStatements()
        {
            try
            {
                var customers = await _customerService.GetAllCustomersAsync();
                var customersWithBalance = customers.Where(c => c.OutstandingBalance > 0).ToList();

                var statements = new List<CustomerStatementSummary>();
                foreach (var customer in customersWithBalance)
                {
                    var customerSales = await _salesService.GetCustomerSalesAsync(customer.Id);
                    var unpaidSales = customerSales.Where(s => s.PaymentStatus != PaymentStatus.Paid && s.SaleStatus != SaleStatus.Cancelled).ToList();

                    statements.Add(new CustomerStatementSummary
                    {
                        CustomerId = customer.Id,
                        CustomerName = customer.CustomerName,
                        CustomerEmail = customer.Email,
                        OutstandingBalance = customer.OutstandingBalance,
                        CreditLimit = customer.CreditLimit,
                        UnpaidInvoiceCount = unpaidSales.Count,
                        OldestInvoiceDate = unpaidSales.Any() ? unpaidSales.Min(s => s.SaleDate) : (DateTime?)null,
                        LastPaymentDate = customer.LastPaymentDate
                    });
                }

                var viewModel = new CustomerStatementsViewModel
                {
                    CustomerStatements = statements.OrderByDescending(s => s.OutstandingBalance).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer statements");
                TempData["ErrorMessage"] = $"Error loading customer statements: {ex.Message}";
                return View(new CustomerStatementsViewModel());
            }
        }

        // GET: AccountsReceivable/CustomerStatement/5
        public async Task<IActionResult> CustomerStatement(int id)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(id);
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer not found.";
                    return RedirectToAction(nameof(CustomerStatements));
                }

                var customerSales = await _salesService.GetCustomerSalesAsync(id);
                var unpaidSales = customerSales.Where(s => s.PaymentStatus != PaymentStatus.Paid && s.SaleStatus != SaleStatus.Cancelled).ToList();

                var statement = new CustomerStatementViewModel
                {
                    Customer = customer,
                    UnpaidInvoices = unpaidSales.OrderBy(s => s.SaleDate).ToList(),
                    StatementDate = DateTime.Today,
                    TotalOutstanding = customer.OutstandingBalance
                };

                return View(statement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer statement for customer {CustomerId}", id);
                TempData["ErrorMessage"] = $"Error loading customer statement: {ex.Message}";
                return RedirectToAction(nameof(CustomerStatements));
            }
        }

        // GET: AccountsReceivable/Reports
        public async Task<IActionResult> Reports()
        {
            try
            {
                var viewModel = await BuildReportsViewModel();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading A/R reports");
                TempData["ErrorMessage"] = $"Error loading reports: {ex.Message}";
                return View(new ARReportsViewModel());
            }
        }

        // Helper Methods
        private async Task<AccountsReceivableDashboardViewModel> BuildDashboardViewModel()
        {
            var allSales = await _salesService.GetAllSalesAsync();
            var allCustomers = await _customerService.GetAllCustomersAsync();

            var unpaidSales = allSales.Where(s => s.PaymentStatus != PaymentStatus.Paid && s.SaleStatus != SaleStatus.Cancelled).ToList();
            var overdueSales = allSales.Where(s => s.IsOverdue && s.PaymentStatus != PaymentStatus.Paid).ToList();

            var totalAR = unpaidSales.Sum(s => s.TotalAmount);
            var totalOverdue = overdueSales.Sum(s => s.TotalAmount);

            // Aging buckets
            var current = unpaidSales.Where(s => (DateTime.Today - s.SaleDate).Days <= 0).Sum(s => s.TotalAmount);
            var days1To30 = unpaidSales.Where(s => (DateTime.Today - s.SaleDate).Days >= 1 && (DateTime.Today - s.SaleDate).Days <= 30).Sum(s => s.TotalAmount);
            var days31To60 = unpaidSales.Where(s => (DateTime.Today - s.SaleDate).Days >= 31 && (DateTime.Today - s.SaleDate).Days <= 60).Sum(s => s.TotalAmount);
            var days61To90 = unpaidSales.Where(s => (DateTime.Today - s.SaleDate).Days >= 61 && (DateTime.Today - s.SaleDate).Days <= 90).Sum(s => s.TotalAmount);
            var over90Days = unpaidSales.Where(s => (DateTime.Today - s.SaleDate).Days > 90).Sum(s => s.TotalAmount);

            var customersWithBalance = allCustomers.Where(c => c.OutstandingBalance > 0).ToList();

            return new AccountsReceivableDashboardViewModel
            {
                TotalAccountsReceivable = totalAR,
                TotalOverdue = totalOverdue,
                OverduePercentage = totalAR > 0 ? (totalOverdue / totalAR) * 100 : 0,
                UnpaidInvoiceCount = unpaidSales.Count,
                OverdueInvoiceCount = overdueSales.Count,
                CustomersWithBalance = customersWithBalance.Count,
                AverageCollectionPeriod = CalculateAverageCollectionPeriod(allSales),
                
                // Aging breakdown
                CurrentAmount = current,
                Days1To30Amount = days1To30,
                Days31To60Amount = days31To60,
                Days61To90Amount = days61To90,
                Over90DaysAmount = over90Days,

                // Recent activity
                RecentOverdueInvoices = overdueSales.OrderByDescending(s => s.DaysOverdue).Take(5).ToList(),
                TopCustomerBalances = customersWithBalance.OrderByDescending(c => c.OutstandingBalance).Take(5).ToList()
            };
        }

        private async Task<ARReportsViewModel> BuildReportsViewModel()
        {
            var allSales = await _salesService.GetAllSalesAsync();
            var unpaidSales = allSales.Where(s => s.PaymentStatus != PaymentStatus.Paid && s.SaleStatus != SaleStatus.Cancelled).ToList();
            var paidSales = allSales.Where(s => s.PaymentStatus == PaymentStatus.Paid).ToList();

            var totalAR = unpaidSales.Sum(s => s.TotalAmount);
            var totalCollected = paidSales.Sum(s => s.TotalAmount);

            return new ARReportsViewModel
            {
                TotalAccountsReceivable = totalAR,
                TotalCollected = totalCollected,
                CollectionEfficiency = totalCollected + totalAR > 0 ? (totalCollected / (totalCollected + totalAR)) * 100 : 0,
                AverageDaysToCollect = CalculateAverageCollectionPeriod(allSales),
                BadDebtPercentage = 0, // Implement bad debt tracking as needed
                MonthlyCollectionTrend = CalculateMonthlyCollectionTrend(paidSales)
            };
        }

        private CollectionPriority CalculateCollectionPriority(decimal amount, int daysOverdue)
        {
            if (daysOverdue > 60 || amount > 5000)
                return CollectionPriority.High;
            else if (daysOverdue > 30 || amount > 1000)
                return CollectionPriority.Medium;
            else
                return CollectionPriority.Low;
        }

        private decimal CalculateAverageCollectionPeriod(IEnumerable<Sale> sales)
        {
            var paidSales = sales.Where(s => s.PaymentStatus == PaymentStatus.Paid).ToList();
            if (!paidSales.Any()) return 0;

            var totalDays = paidSales.Sum(s => (s.PaymentDueDate - s.SaleDate).Days);
            return paidSales.Count > 0 ? totalDays / paidSales.Count : 0;
        }

        private List<MonthlyCollectionData> CalculateMonthlyCollectionTrend(IEnumerable<Sale> paidSales)
        {
            return paidSales
                .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                .Select(g => new MonthlyCollectionData
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    AmountCollected = g.Sum(s => s.TotalAmount),
                    InvoiceCount = g.Count()
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();
        }
    }
}