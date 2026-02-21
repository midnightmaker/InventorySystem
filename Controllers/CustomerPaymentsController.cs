using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Services;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using InventorySystem.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using System.Text.RegularExpressions;

namespace InventorySystem.Controllers
{
    public class CustomerPaymentsController : Controller
    {
        private readonly ISalesService _salesService;
        private readonly ICustomerService _customerService;
        private readonly ILogger<CustomerPaymentsController> _logger;
        private readonly InventoryContext _context;

        // Pagination constants
        private const int DefaultPageSize = 25;
        private const int MaxPageSize = 100;
        private readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };

        public CustomerPaymentsController(
            ISalesService salesService,
            ICustomerService customerService,
            ILogger<CustomerPaymentsController> logger,
            InventoryContext context)
        {
            _salesService = salesService;
            _customerService = customerService;
            _logger = logger;
            _context = context;
        }

        // GET: CustomerPayments
        public async Task<IActionResult> Index(
            string search,
            string customerFilter,
            string paymentMethodFilter,
            DateTime? startDate,
            DateTime? endDate,
            string sortOrder = "date_desc",
            int page = 1,
            int pageSize = DefaultPageSize)
        {
            try
            {
                // Validate and constrain pagination parameters
                page = Math.Max(1, page);
                pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

                _logger.LogInformation("=== CUSTOMER PAYMENTS INDEX DEBUG ===");
                _logger.LogInformation("Search: {Search}", search);
                _logger.LogInformation("Customer Filter: {CustomerFilter}", customerFilter);
                _logger.LogInformation("Payment Method Filter: {PaymentMethodFilter}", paymentMethodFilter);
                _logger.LogInformation("Date Range: {StartDate} to {EndDate}", startDate, endDate);
                _logger.LogInformation("Sort Order: {SortOrder}", sortOrder);
                _logger.LogInformation("Page: {Page}, PageSize: {PageSize}", page, pageSize);

                // Get all sales that have been paid (fully or partially)
                var paidSalesQuery = _context.Sales
                    .Include(s => s.Customer)
                    .Include(s => s.CustomerPayments.Where(p => p.Status == Models.Enums.PaymentRecordStatus.Processed))
                    .Where(s => s.PaymentStatus == PaymentStatus.Paid || 
                               s.PaymentStatus == PaymentStatus.PartiallyPaid)
                    .Where(s => s.SaleStatus != SaleStatus.Cancelled)
                    .AsQueryable();

                // Extract individual payments from sales notes
                var allPayments = new List<CustomerPaymentRecord>();

                var paidSales = await paidSalesQuery.ToListAsync();

                foreach (var sale in paidSales)
                {
                    var payments = ExtractPaymentsFromSale(sale);
                    allPayments.AddRange(payments);
                }

                _logger.LogInformation("Extracted {PaymentCount} payment records from {SalesCount} sales", 
                    allPayments.Count, paidSales.Count);

                // Apply filtering
                var filteredPayments = allPayments.AsQueryable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchTerm = search.Trim().ToLower();
                    _logger.LogInformation("Applying search filter: {SearchTerm}", searchTerm);

                    filteredPayments = filteredPayments.Where(p =>
                        p.CustomerName.ToLower().Contains(searchTerm) ||
                        p.SaleNumber.ToLower().Contains(searchTerm) ||
                        (p.PaymentMethod != null && p.PaymentMethod.ToLower().Contains(searchTerm)) ||
                        (p.PaymentNotes != null && p.PaymentNotes.ToLower().Contains(searchTerm)) ||
                        p.Amount.ToString().Contains(searchTerm)
                    );
                }

                // Apply customer filter
                if (!string.IsNullOrWhiteSpace(customerFilter) && int.TryParse(customerFilter, out int customerId))
                {
                    _logger.LogInformation("Applying customer filter: {CustomerId}", customerId);
                    filteredPayments = filteredPayments.Where(p => p.CustomerId == customerId);
                }

                // Apply payment method filter
                if (!string.IsNullOrWhiteSpace(paymentMethodFilter))
                {
                    _logger.LogInformation("Applying payment method filter: {PaymentMethodFilter}", paymentMethodFilter);
                    filteredPayments = filteredPayments.Where(p => 
                        p.PaymentMethod != null && 
                        p.PaymentMethod.Equals(paymentMethodFilter, StringComparison.OrdinalIgnoreCase));
                }

                // Apply date range filter
                if (startDate.HasValue)
                {
                    _logger.LogInformation("Applying start date filter: {StartDate}", startDate.Value);
                    filteredPayments = filteredPayments.Where(p => p.PaymentDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    _logger.LogInformation("Applying end date filter: {EndDate}", endDate.Value);
                    var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    filteredPayments = filteredPayments.Where(p => p.PaymentDate <= endOfDay);
                }

                // Convert to list for sorting
                var filteredPaymentsList = filteredPayments.ToList();

                // Apply sorting
                filteredPaymentsList = sortOrder switch
                {
                    "date_asc" => filteredPaymentsList.OrderBy(p => p.PaymentDate).ToList(),
                    "date_desc" => filteredPaymentsList.OrderByDescending(p => p.PaymentDate).ToList(),
                    "customer_asc" => filteredPaymentsList.OrderBy(p => p.CustomerName).ToList(),
                    "customer_desc" => filteredPaymentsList.OrderByDescending(p => p.CustomerName).ToList(),
                    "amount_asc" => filteredPaymentsList.OrderBy(p => p.Amount).ToList(),
                    "amount_desc" => filteredPaymentsList.OrderByDescending(p => p.Amount).ToList(),
                    "method_asc" => filteredPaymentsList.OrderBy(p => p.PaymentMethod ?? "").ToList(),
                    "method_desc" => filteredPaymentsList.OrderByDescending(p => p.PaymentMethod ?? "").ToList(),
                    "sale_asc" => filteredPaymentsList.OrderBy(p => p.SaleNumber).ToList(),
                    "sale_desc" => filteredPaymentsList.OrderByDescending(p => p.SaleNumber).ToList(),
                    _ => filteredPaymentsList.OrderByDescending(p => p.PaymentDate).ToList()
                };

                // Get total count for pagination
                var totalCount = filteredPaymentsList.Count;
                _logger.LogInformation("Total filtered payment records: {TotalCount}", totalCount);

                // Calculate pagination values
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                var skip = (page - 1) * pageSize;

                // Get paginated results
                var paginatedPayments = filteredPaymentsList
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList();

                _logger.LogInformation("Retrieved {PaymentCount} payment records for page {Page}", 
                    paginatedPayments.Count, page);

                // Calculate summary statistics
                var totalPaymentsAmount = allPayments.Sum(p => p.Amount);
                var averagePaymentAmount = allPayments.Any() ? allPayments.Average(p => p.Amount) : 0;

                // Get payment methods for filter dropdown
                var paymentMethods = allPayments
                    .Where(p => !string.IsNullOrWhiteSpace(p.PaymentMethod))
                    .Select(p => p.PaymentMethod!)
                    .Distinct()
                    .OrderBy(pm => pm)
                    .ToList();

                // Get customers for filter dropdown
                var allCustomers = await _customerService.GetAllCustomersAsync();
                var activeCustomers = allCustomers.Where(c => c.IsActive).ToList();

                // Build dropdown entries showing CompanyName as primary (B2B)
                var customerDropdownItems = activeCustomers
                    .Select(c => new
                    {
                        c.Id,
                        DisplayName = !string.IsNullOrEmpty(c.CompanyName)
                            ? $"{c.CompanyName} ({c.CustomerName})"
                            : c.CustomerName
                    })
                    .OrderBy(c => c.DisplayName)
                    .ToList();

                // Prepare ViewBag data
                ViewBag.SearchTerm = search;
                ViewBag.CustomerFilter = customerFilter;
                ViewBag.PaymentMethodFilter = paymentMethodFilter;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
                ViewBag.SortOrder = sortOrder;

                // Pagination data
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = totalCount;
                ViewBag.HasPreviousPage = page > 1;
                ViewBag.HasNextPage = page < totalPages;
                ViewBag.ShowingFrom = totalCount > 0 ? skip + 1 : 0;
                ViewBag.ShowingTo = Math.Min(skip + pageSize, totalCount);
                ViewBag.AllowedPageSizes = AllowedPageSizes;

                // Summary statistics
                ViewBag.TotalPaymentsAmount = totalPaymentsAmount;
                ViewBag.AveragePaymentAmount = averagePaymentAmount;
                ViewBag.TotalPaymentCount = allPayments.Count;

                // Dropdown data
                ViewBag.CustomerOptions = new SelectList(customerDropdownItems, "Id", "DisplayName", customerFilter);
                ViewBag.PaymentMethodOptions = new SelectList(paymentMethods.Select(pm => new
                {
                    Value = pm,
                    Text = pm
                }), "Value", "Text", paymentMethodFilter);

                // Search statistics
                ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
                                   !string.IsNullOrWhiteSpace(customerFilter) ||
                                   !string.IsNullOrWhiteSpace(paymentMethodFilter) ||
                                   startDate.HasValue ||
                                   endDate.HasValue;

                if (ViewBag.IsFiltered)
                {
                    ViewBag.SearchResultsCount = totalCount;
                    ViewBag.TotalCustomerPaymentsCount = allPayments.Count;
                }

                return View(paginatedPayments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer payments");
                TempData["ErrorMessage"] = $"Error loading customer payments: {ex.Message}";
                
                // Set safe defaults
                SetDefaultViewBagValues(search, customerFilter, paymentMethodFilter, startDate, endDate, sortOrder, page, pageSize);
                
                return View(new List<CustomerPaymentRecord>());
            }
        }

        // GET: CustomerPayments/Details/5
        public async Task<IActionResult> Details(int saleId)
        {
            try
            {
                var sale = await _salesService.GetSaleByIdAsync(saleId);
                if (sale == null)
                {
                    TempData["ErrorMessage"] = "Sale not found.";
                    return RedirectToAction("Index");
                }

                var payments = ExtractPaymentsFromSale(sale);
                
                var viewModel = new CustomerPaymentDetailsViewModel
                {
                    Sale = sale,
                    Payments = payments,
                    TotalPaid = payments.Sum(p => p.Amount),
                    RemainingBalance = sale.TotalAmount - payments.Sum(p => p.Amount)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment details for sale {SaleId}", saleId);
                TempData["ErrorMessage"] = $"Error loading payment details: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // GET: CustomerPayments/Reports
        public async Task<IActionResult> Reports(
            DateTime? startDate,
            DateTime? endDate)
        {
            try
            {
                _logger.LogInformation("Loading customer payments reports");

                // Set default date range if not provided (last 3 months)
                var defaultEndDate = DateTime.Today;
                var defaultStartDate = defaultEndDate.AddMonths(-3);

                var reportStartDate = startDate ?? defaultStartDate;
                var reportEndDate = endDate ?? defaultEndDate;

                _logger.LogInformation("Report date range: {StartDate} to {EndDate}", reportStartDate, reportEndDate);

                // Get all sales that have been paid (fully or partially) within the date range
                var paidSalesQuery = _context.Sales
                    .Include(s => s.Customer)
                    .Include(s => s.CustomerPayments.Where(p => p.Status == Models.Enums.PaymentRecordStatus.Processed))
                    .Where(s => s.PaymentStatus == PaymentStatus.Paid || 
                               s.PaymentStatus == PaymentStatus.PartiallyPaid)
                    .Where(s => s.SaleStatus != SaleStatus.Cancelled)
                    .Where(s => s.SaleDate >= reportStartDate && s.SaleDate <= reportEndDate)
                    .AsQueryable();

                var paidSales = await paidSalesQuery.ToListAsync();

                // Extract all payments from the sales
                var allPayments = new List<CustomerPaymentRecord>();
                foreach (var sale in paidSales)
                {
                    var payments = ExtractPaymentsFromSale(sale);
                    // Filter payments by the report date range
                    var filteredPayments = payments.Where(p => 
                        p.PaymentDate >= reportStartDate && 
                        p.PaymentDate <= reportEndDate).ToList();
                    allPayments.AddRange(filteredPayments);
                }

                _logger.LogInformation("Found {PaymentCount} payments in date range", allPayments.Count);

                // Calculate summary statistics
                var totalPayments = allPayments.Sum(p => p.Amount);
                var paymentCount = allPayments.Count;
                var averagePayment = paymentCount > 0 ? totalPayments / paymentCount : 0;

                // Group payments by method
                var paymentsByMethod = allPayments
                    .GroupBy(p => p.PaymentMethod ?? "Unknown")
                    .Select(g => new PaymentMethodSummary
                    {
                        PaymentMethod = g.Key,
                        Count = g.Count(),
                        TotalAmount = g.Sum(p => p.Amount),
                        Percentage = totalPayments > 0 ? (g.Sum(p => p.Amount) / totalPayments) * 100 : 0
                    })
                    .OrderByDescending(pm => pm.TotalAmount)
                    .ToList();

                // Group payments by customer
                var topCustomers = allPayments
                    .GroupBy(p => new { p.CustomerId, p.CustomerName })
                    .Select(g => new ViewModels.CustomerPaymentSummary
                    {
                        CustomerId = g.Key.CustomerId,
                        CustomerName = g.Key.CustomerName,
                        PaymentCount = g.Count(),
                        TotalAmount = g.Sum(p => p.Amount),
                        AveragePayment = g.Average(p => p.Amount),
                        LastPaymentDate = g.Max(p => p.PaymentDate)
                    })
                    .OrderByDescending(c => c.TotalAmount)
                    .Take(10)
                    .ToList();

                // Group payments by month
                var monthlyTrends = allPayments
                    .GroupBy(p => new { Year = p.PaymentDate.Year, Month = p.PaymentDate.Month })
                    .Select(g => new MonthlyPaymentTrend
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        PaymentCount = g.Count(),
                        TotalAmount = g.Sum(p => p.Amount),
                        AveragePayment = g.Average(p => p.Amount)
                    })
                    .OrderBy(mt => mt.Year)
                    .ThenBy(mt => mt.Month)
                    .ToList();

                // Create the report view model
                var viewModel = new CustomerPaymentsReportViewModel
                {
                    StartDate = reportStartDate,
                    EndDate = reportEndDate,
                    TotalPayments = totalPayments,
                    PaymentCount = paymentCount,
                    AveragePayment = averagePayment,
                    PaymentsByMethod = paymentsByMethod,
                    TopCustomers = topCustomers,
                    MonthlyTrends = monthlyTrends
                };

                // Set ViewBag data for the form
                ViewBag.StartDate = reportStartDate.ToString("yyyy-MM-dd");
                ViewBag.EndDate = reportEndDate.ToString("yyyy-MM-dd");

                _logger.LogInformation("Customer payments report generated successfully - {PaymentCount} payments, {TotalAmount:C}", 
                    paymentCount, totalPayments);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer payments reports");
                TempData["ErrorMessage"] = $"Error loading reports: {ex.Message}";
                
                // Return a default model with safe date range
                var defaultModel = new CustomerPaymentsReportViewModel
                {
                    StartDate = startDate ?? DateTime.Today.AddMonths(-3),
                    EndDate = endDate ?? DateTime.Today
                };

                ViewBag.StartDate = defaultModel.StartDate.ToString("yyyy-MM-dd");
                ViewBag.EndDate = defaultModel.EndDate.ToString("yyyy-MM-dd");

                return View(defaultModel);
            }
        }

        // Helper method to extract payment records from a sale's notes
        // DEPRECATED: This method extracts payments from legacy notes format
        // TODO: Migrate to use CustomerPayment entities directly
        private List<CustomerPaymentRecord> ExtractPaymentsFromSale(Sale sale)
        {
            var payments = new List<CustomerPaymentRecord>();

            // FIRST: Try to get payments from the proper CustomerPayment entities
            if (sale.CustomerPayments?.Any() == true)
            {
                foreach (var customerPayment in sale.CustomerPayments.Where(p => p.Status == Models.Enums.PaymentRecordStatus.Processed))
                {
                    payments.Add(new CustomerPaymentRecord
                    {
                        SaleId = sale.Id,
                        SaleNumber = sale.SaleNumber,
                        CustomerId = sale.CustomerId,
                        CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer",
                        CompanyName = sale.Customer?.CompanyName,
                        PaymentDate = customerPayment.PaymentDate,
                        Amount = customerPayment.Amount,
                        PaymentMethod = customerPayment.PaymentMethod,
                        PaymentNotes = customerPayment.Notes
                    });
                }
                
                // If we have proper payment entities, return them (no need to parse notes)
                if (payments.Any())
                {
                    return payments;
                }
            }

            // FALLBACK: Parse legacy notes format for backward compatibility
            if (string.IsNullOrWhiteSpace(sale.Notes))
            {
                // If no payment notes but sale is marked as paid, create a default payment record
                if (sale.PaymentStatus == PaymentStatus.Paid)
                {
                    payments.Add(new CustomerPaymentRecord
                    {
                        SaleId = sale.Id,
                        SaleNumber = sale.SaleNumber,
                        CustomerId = sale.CustomerId,
                        CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer",
                        CompanyName = sale.Customer?.CompanyName,
                        PaymentDate = sale.SaleDate, // Default to sale date
                        Amount = sale.TotalAmount,
                        PaymentMethod = sale.PaymentMethod ?? "Unknown",
                        PaymentNotes = "Payment recorded (legacy - no detailed notes available)"
                    });
                }
                return payments;
            }

            var lines = sale.Notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Look for payment records in format: "Payment recorded: $123.45 via Credit Card on 12/31/2023"
                if (line.Contains("Payment recorded:", StringComparison.OrdinalIgnoreCase))
                {
                    var payment = ParsePaymentFromNoteLine(line, sale);
                    if (payment != null)
                    {
                        payments.Add(payment);
                    }
                }
            }

            // If no payments found in notes but sale is marked as paid, create a default record
            if (!payments.Any() && sale.PaymentStatus == PaymentStatus.Paid)
            {
                payments.Add(new CustomerPaymentRecord
                {
                    SaleId = sale.Id,
                    SaleNumber = sale.SaleNumber,
                    CustomerId = sale.CustomerId,
                    CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer",
                    CompanyName = sale.Customer?.CompanyName,
                    PaymentDate = sale.SaleDate,
                    Amount = sale.TotalAmount,
                    PaymentMethod = sale.PaymentMethod ?? "Unknown",
                    PaymentNotes = "Payment recorded (legacy - extracted from sale status)"
                });
            }

            return payments;
        }

        // Helper method to parse individual payment from notes line
        private CustomerPaymentRecord? ParsePaymentFromNoteLine(string line, Sale sale)
        {
            try
            {
                // Pattern: "Payment recorded: $123.45 via Credit Card on 12/31/2023 - Optional notes"
                var pattern = @"Payment recorded:\s*\$(\d+(?:\.\d{2})?)\s*via\s*([^o]+?)\s*on\s*(\d{1,2}/\d{1,2}/\d{4})(?:\s*-\s*(.+))?";
                var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var amountStr = match.Groups[1].Value;
                    var paymentMethod = match.Groups[2].Value.Trim();
                    var dateStr = match.Groups[3].Value;
                    var notes = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";

                    if (decimal.TryParse(amountStr, out decimal amount) &&
                        DateTime.TryParse(dateStr, out DateTime paymentDate))
                    {
                        return new CustomerPaymentRecord
                        {
                            SaleId = sale.Id,
                            SaleNumber = sale.SaleNumber,
                            CustomerId = sale.CustomerId,
                            CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer",
                            CompanyName = sale.Customer?.CompanyName,
                            PaymentDate = paymentDate,
                            Amount = amount,
                            PaymentMethod = paymentMethod,
                            PaymentNotes = string.IsNullOrWhiteSpace(notes) ? line : notes
                        };
                    }
                }

                // Fallback: try simpler pattern without date
                var simplePattern = @"Payment recorded:\s*\$(\d+(?:\.\d{2})?)\s*via\s*(.+?)(?:\s*-\s*(.+))?$";
                var simpleMatch = Regex.Match(line, simplePattern, RegexOptions.IgnoreCase);

                if (simpleMatch.Success)
                {
                    var amountStr = simpleMatch.Groups[1].Value;
                    var paymentMethod = simpleMatch.Groups[2].Value.Trim();
                    var notes = simpleMatch.Groups.Count > 3 ? simpleMatch.Groups[3].Value.Trim() : "";

                    if (decimal.TryParse(amountStr, out decimal amount))
                    {
                        return new CustomerPaymentRecord
                        {
                            SaleId = sale.Id,
                            SaleNumber = sale.SaleNumber,
                            CustomerId = sale.CustomerId,
                            CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer",
                            CompanyName = sale.Customer?.CompanyName,
                            PaymentDate = sale.SaleDate, // Default to sale date
                            Amount = amount,
                            PaymentMethod = paymentMethod,
                            PaymentNotes = string.IsNullOrWhiteSpace(notes) ? line : notes
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing payment line: {Line}", line);
                return null;
            }
        }

        // Helper method to set default ViewBag values
        private void SetDefaultViewBagValues(string? search, string? customerFilter, string? paymentMethodFilter,
            DateTime? startDate, DateTime? endDate, string sortOrder, int page, int pageSize)
        {
            ViewBag.SearchTerm = search;
            ViewBag.CustomerFilter = customerFilter;
            ViewBag.PaymentMethodFilter = paymentMethodFilter;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.SortOrder = sortOrder;

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = 1;
            ViewBag.TotalCount = 0;
            ViewBag.HasPreviousPage = false;
            ViewBag.HasNextPage = false;
            ViewBag.ShowingFrom = 0;
            ViewBag.ShowingTo = 0;
            ViewBag.AllowedPageSizes = AllowedPageSizes;

            ViewBag.TotalPaymentsAmount = 0m;
            ViewBag.AveragePaymentAmount = 0m;
            ViewBag.TotalPaymentCount = 0;

            ViewBag.CustomerOptions = new SelectList(new List<object>(), "Id", "CustomerName");
            ViewBag.PaymentMethodOptions = new SelectList(new List<object>(), "Value", "Text");

            ViewBag.IsFiltered = false;
        }
    }
}