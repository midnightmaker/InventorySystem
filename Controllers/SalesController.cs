// Controllers/SalesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Services;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using InventorySystem.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;

namespace InventorySystem.Controllers
{
  public class SalesController : Controller
  {
    private readonly ISalesService _salesService;
    private readonly IInventoryService _inventoryService;
    private readonly IProductionService _productionService;
    private readonly ICustomerService _customerService;
    private readonly ILogger<SalesController> _logger;
    private readonly InventoryContext _context;

    public SalesController(
        ISalesService salesService,
        IInventoryService inventoryService,
        IProductionService productionService,
        ICustomerService customerService,
        ILogger<SalesController> logger,
        InventoryContext context)
    {
      _salesService = salesService;
      _inventoryService = inventoryService;
      _productionService = productionService;
      _customerService = customerService;
      _logger = logger;
      _context = context;
    }

    // Sales Index with pagination support
    public async Task<IActionResult> Index(
        string search,
        string customerFilter,
        string statusFilter,
        string paymentStatusFilter,
        DateTime? startDate,
        DateTime? endDate,
        string sortOrder = "date_desc",
        int page = 1,
        int pageSize = 25)
    {
      try
      {
        // Pagination constants
        const int DefaultPageSize = 25;
        const int MaxPageSize = 100;
        int[] AllowedPageSizes = { 10, 25, 50, 100 };

        // Validate and constrain pagination parameters
        page = Math.Max(1, page);
        pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

        _logger.LogInformation("=== SALES INDEX DEBUG ===");
        _logger.LogInformation("Search: {Search}", search);
        _logger.LogInformation("Customer Filter: {CustomerFilter}", customerFilter);
        _logger.LogInformation("Status Filter: {StatusFilter}", statusFilter);
        _logger.LogInformation("Payment Status Filter: {PaymentStatusFilter}", paymentStatusFilter);
        _logger.LogInformation("Date Range: {StartDate} to {EndDate}", startDate, endDate);
        _logger.LogInformation("Sort Order: {SortOrder}", sortOrder);
        _logger.LogInformation("Page: {Page}, PageSize: {PageSize}", page, pageSize);

        // Get all sales and apply filtering using database context
        var query = _context.Sales
            .Include(s => s.Customer)
            .Include(s => s.SaleItems)
            .AsQueryable();

        // Apply search filter with wildcard support
        if (!string.IsNullOrWhiteSpace(search))
        {
          var searchTerm = search.Trim();
          _logger.LogInformation("Applying search filter: {SearchTerm}", searchTerm);

          if (searchTerm.Contains('*') || searchTerm.Contains('?'))
          {
            var likePattern = ConvertWildcardToLike(searchTerm);
            _logger.LogInformation("Using LIKE pattern: {LikePattern}", likePattern);

            query = query.Where(s =>
              EF.Functions.Like(s.SaleNumber, likePattern) ||
              (s.OrderNumber != null && EF.Functions.Like(s.OrderNumber, likePattern)) ||
              (s.Customer != null && EF.Functions.Like(s.Customer.CustomerName, likePattern)) ||
              (s.Customer != null && s.Customer.CompanyName != null && EF.Functions.Like(s.Customer.CompanyName, likePattern)) ||
              (s.Customer != null && s.Customer.Email != null && EF.Functions.Like(s.Customer.Email, likePattern)) ||
              (s.Notes != null && EF.Functions.Like(s.Notes, likePattern)) ||
              EF.Functions.Like(s.Id.ToString(), likePattern)
            );
          }
          else
          {
            var searchTermLower = searchTerm.ToLower();
            query = query.Where(s =>
              s.SaleNumber.ToLower().Contains(searchTermLower) ||
              (s.OrderNumber != null && s.OrderNumber.ToLower().Contains(searchTermLower)) ||
              (s.Customer != null && s.Customer.CustomerName.ToLower().Contains(searchTermLower)) ||
              (s.Customer != null && s.Customer.CompanyName != null && s.Customer.CompanyName.ToLower().Contains(searchTermLower)) ||
              (s.Customer != null && s.Customer.Email != null && s.Customer.Email.ToLower().Contains(searchTermLower)) ||
              (s.Notes != null && s.Notes.ToLower().Contains(searchTermLower))
            );
          }
        }

        // Apply customer filter
        if (!string.IsNullOrWhiteSpace(customerFilter) && int.TryParse(customerFilter, out int customerId))
        {
          _logger.LogInformation("Applying customer filter: {CustomerId}", customerId);
          query = query.Where(s => s.CustomerId == customerId);
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<SaleStatus>(statusFilter, out var saleStatus))
        {
          _logger.LogInformation("Applying status filter: {SaleStatus}", saleStatus);
          query = query.Where(s => s.SaleStatus == saleStatus);
        }

        // Apply payment status filter
        if (!string.IsNullOrWhiteSpace(paymentStatusFilter) && Enum.TryParse<PaymentStatus>(paymentStatusFilter, out var paymentStatus))
        {
          _logger.LogInformation("Applying payment status filter: {PaymentStatus}", paymentStatus);
          query = query.Where(s => s.PaymentStatus == paymentStatus);
        }

        // Apply date range filter
        if (startDate.HasValue)
        {
          _logger.LogInformation("Applying start date filter: {StartDate}", startDate.Value);
          query = query.Where(s => s.SaleDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
          _logger.LogInformation("Applying end date filter: {EndDate}", endDate.Value);
          var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
          query = query.Where(s => s.SaleDate <= endOfDay);
        }

        // Apply sorting
        query = sortOrder switch
        {
          "date_asc" => query.OrderBy(s => s.SaleDate),
          "date_desc" => query.OrderByDescending(s => s.SaleDate),
          "customer_asc" => query.OrderBy(s => s.Customer != null ? s.Customer.CustomerName : ""),
          "customer_desc" => query.OrderByDescending(s => s.Customer != null ? s.Customer.CustomerName : ""),
          "amount_asc" => query.OrderBy(s => s.TotalAmount),
          "amount_desc" => query.OrderByDescending(s => s.TotalAmount),
          "status_asc" => query.OrderBy(s => s.SaleStatus),
          "status_desc" => query.OrderByDescending(s => s.SaleStatus),
          "payment_asc" => query.OrderBy(s => s.PaymentStatus),
          "payment_desc" => query.OrderByDescending(s => s.PaymentStatus),
          _ => query.OrderByDescending(s => s.SaleDate)
        };

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        _logger.LogInformation("Total filtered records: {TotalCount}", totalCount);

        // Calculate pagination values
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var skip = (page - 1) * pageSize;

        // Get paginated results
        var sales = await query.Skip(skip).Take(pageSize).ToListAsync();
        _logger.LogInformation("Retrieved {SalesCount} sales for page {Page}", sales.Count, page);

        // Get filter options for dropdowns
        var allCustomers = await _customerService.GetAllCustomersAsync();
        var saleStatuses = Enum.GetValues<SaleStatus>().ToList();
        var paymentStatuses = Enum.GetValues<PaymentStatus>().ToList();

        // Prepare ViewBag data
        ViewBag.SearchTerm = search;
        ViewBag.CustomerFilter = customerFilter;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.PaymentStatusFilter = paymentStatusFilter;
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

        // Dropdown data
        ViewBag.CustomerOptions = new SelectList(allCustomers.Where(c => c.IsActive), "Id", "CustomerName", customerFilter);
        ViewBag.StatusOptions = new SelectList(saleStatuses.Select(s => new
        {
          Value = s.ToString(),
          Text = s.ToString().Replace("_", " ")
        }), "Value", "Text", statusFilter);
        ViewBag.PaymentStatusOptions = new SelectList(paymentStatuses.Select(s => new
        {
          Value = s.ToString(),
          Text = s.ToString().Replace("_", " ")
        }), "Value", "Text", paymentStatusFilter);

        // Search statistics
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
                           !string.IsNullOrWhiteSpace(customerFilter) ||
                           !string.IsNullOrWhiteSpace(statusFilter) ||
                           !string.IsNullOrWhiteSpace(paymentStatusFilter) ||
                           startDate.HasValue ||
                           endDate.HasValue;

        if (ViewBag.IsFiltered)
        {
          var totalSales = await _context.Sales.CountAsync();
          ViewBag.SearchResultsCount = totalCount;
          ViewBag.TotalSalesCount = totalSales;
        }

        return View(sales);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in Sales Index");
        TempData["ErrorMessage"] = $"Error loading sales: {ex.Message}";
        return View(new List<Sale>());
      }
    }

    // Sale Details
    public async Task<IActionResult> Details(int id)
    {
      var sale = await _salesService.GetSaleByIdAsync(id);
      if (sale == null) return NotFound();
      return View(sale);
    }

    // GET: Sales/Create
    public async Task<IActionResult> Create(int? customerId)
    {
        try
        {
            _logger.LogInformation("Loading create sale form");

            // Create new sale with default values
            var sale = new Sale
            {
                SaleDate = DateTime.Today,
                PaymentStatus = PaymentStatus.Pending,
                SaleStatus = SaleStatus.Processing,
                Terms = PaymentTerms.Net30,
                PaymentDueDate = DateTime.Today.AddDays(30),
                ShippingCost = 0,
                TaxAmount = 0
            };

            // If customerId is provided, pre-select it
            if (customerId.HasValue)
            {
                sale.CustomerId = customerId.Value;
            }

            // Load customers for dropdown
            var customers = await _customerService.GetAllCustomersAsync();
            ViewBag.Customers = customers
                .Where(c => c.IsActive)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
                    Selected = c.Id == customerId
                })
                .OrderBy(c => c.Text)
                .ToList();

            _logger.LogInformation("Create sale form loaded with {CustomerCount} customers", ((List<SelectListItem>)ViewBag.Customers).Count);

            return View(sale);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading create sale form");
            TempData["ErrorMessage"] = $"Error loading create form: {ex.Message}";
            return RedirectToAction("Index");
        }
    }

    // POST: Sales/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Sale sale)
    {
        try
        {
            _logger.LogInformation("Creating new sale for customer {CustomerId}", sale.CustomerId);

            // Validate model state
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create sale form validation failed");
                
                // Reload customers for dropdown
                var customers = await _customerService.GetAllCustomersAsync();
                ViewBag.Customers = customers
                    .Where(c => c.IsActive)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
                        Selected = c.Id == sale.CustomerId
                    })
                    .OrderBy(c => c.Text)
                    .ToList();

                return View(sale);
            }

            // Set default values
            sale.CreatedDate = DateTime.Now;
            sale.SaleNumber = ""; // Will be auto-generated by service

            // Create the sale
            var createdSale = await _salesService.CreateSaleAsync(sale);

            _logger.LogInformation("Sale created successfully with ID {SaleId} and number {SaleNumber}", 
                createdSale.Id, createdSale.SaleNumber);

            TempData["SuccessMessage"] = $"Sale {createdSale.SaleNumber} created successfully! You can now add items to this sale.";
            
            // Redirect to sale details to add items
            return RedirectToAction("Details", new { id = createdSale.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sale for customer {CustomerId}", sale.CustomerId);
            TempData["ErrorMessage"] = $"Error creating sale: {ex.Message}";

            // Reload customers for dropdown
            try
            {
                var customers = await _customerService.GetAllCustomersAsync();
                ViewBag.Customers = customers
                    .Where(c => c.IsActive)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = $"{c.CustomerName} - {c.CompanyName ?? c.CustomerName}",
                        Selected = c.Id == sale.CustomerId
                    })
                    .OrderBy(c => c.Text)
                    .ToList();
            }
            catch (Exception loadEx)
            {
                _logger.LogError(loadEx, "Error reloading customers for dropdown");
                ViewBag.Customers = new List<SelectListItem>();
            }

            return View(sale);
        }
    }

    // Invoice Report - View invoice for a sale
    [HttpGet]
    public async Task<IActionResult> InvoiceReport(int saleId)
    {
      try
      {
        var sale = await _salesService.GetSaleByIdAsync(saleId);
        if (sale == null)
        {
          TempData["ErrorMessage"] = "Sale not found.";
          return RedirectToAction("Index");
        }

        // Use Customer entity instead of legacy fields
        var customer = new CustomerInfo
        {
          CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer",
          CustomerEmail = sale.Customer?.Email ?? string.Empty,
          CustomerPhone = sale.Customer?.Phone ?? string.Empty,
          BillingAddress = sale.Customer?.FullBillingAddress ?? string.Empty,
          ShippingAddress = sale.ShippingAddress ?? sale.Customer?.FullShippingAddress ?? string.Empty
        };

        var viewModel = new InvoiceReportViewModel
        {
          InvoiceNumber = sale.SaleNumber,
          InvoiceDate = sale.SaleDate,
          DueDate = sale.PaymentDueDate,
          SaleStatus = sale.SaleStatus,
          PaymentStatus = sale.PaymentStatus,
          PaymentTerms = sale.Terms,
          Notes = sale.Notes ?? string.Empty,
          Customer = customer,
          LineItems = sale.SaleItems.Select(si => new InvoiceLineItem
          {
            ItemId = si.ItemId ?? si.FinishedGoodId ?? 0,
            PartNumber = si.ProductPartNumber,
            Description = si.ProductName,
            Quantity = si.QuantitySold,
            UnitPrice = si.UnitPrice,
            Notes = si.Notes ?? string.Empty,
            ProductType = si.ItemId.HasValue ? "Item" : "FinishedGood",
            QuantityBackordered = si.QuantityBackordered
          }).ToList(),
          CompanyInfo = await GetCompanyInfo(),
          CustomerEmail = sale.Customer?.Email ?? string.Empty,
          EmailSubject = $"Invoice {sale.SaleNumber}",
          EmailMessage = $"Please find attached Invoice {sale.SaleNumber} for your recent purchase.",
          PaymentMethod = sale.PaymentMethod ?? string.Empty,
          IsOverdue = sale.IsOverdue,
          DaysOverdue = sale.DaysOverdue,
          ShippingAddress = sale.ShippingAddress ?? string.Empty,
          OrderNumber = sale.OrderNumber ?? string.Empty,
          TotalShipping = sale.ShippingCost,
          TotalTax = sale.TaxAmount,
          // Calculate amount paid based on payment status
          AmountPaid = sale.PaymentStatus switch
          {
            PaymentStatus.Paid => sale.TotalAmount, // Fully paid
            PaymentStatus.PartiallyPaid => ExtractAmountPaidFromNotes(sale.Notes), // Extract from notes
            _ => 0 // Pending or Overdue = no payment yet
          }
        };

        // Set ViewBag.SaleId for the view to use in links and forms
        ViewBag.SaleId = sale.Id;

        return View(viewModel);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error generating invoice: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // Print-friendly version of the invoice
    [HttpGet]
    public async Task<IActionResult> InvoiceReportPrint(int saleId)
    {
      try
      {
        var sale = await _salesService.GetSaleByIdAsync(saleId);
        if (sale == null)
        {
          return NotFound("Sale not found.");
        }

        // Use Customer entity instead of legacy fields
        var customer = new CustomerInfo
        {
          CustomerName = sale.Customer?.CustomerName ?? "Unknown Customer",
          CustomerEmail = sale.Customer?.Email ?? string.Empty,
          CustomerPhone = sale.Customer?.Phone ?? string.Empty,
          BillingAddress = sale.Customer?.FullBillingAddress ?? string.Empty,
          ShippingAddress = sale.ShippingAddress ?? sale.Customer?.FullShippingAddress ?? string.Empty
        };

        var viewModel = new InvoiceReportViewModel
        {
          InvoiceNumber = sale.SaleNumber,
          InvoiceDate = sale.SaleDate,
          DueDate = sale.PaymentDueDate,
          SaleStatus = sale.SaleStatus,
          PaymentStatus = sale.PaymentStatus,
          PaymentTerms = sale.Terms,
          Notes = sale.Notes ?? string.Empty,
          Customer = customer,
          LineItems = sale.SaleItems.Select(si => new InvoiceLineItem
          {
            ItemId = si.ItemId ?? si.FinishedGoodId ?? 0,
            PartNumber = si.ProductPartNumber,
            Description = si.ProductName,
            Quantity = si.QuantitySold,
            UnitPrice = si.UnitPrice,
            Notes = si.Notes ?? string.Empty,
            ProductType = si.ItemId.HasValue ? "Item" : "FinishedGood",
            QuantityBackordered = si.QuantityBackordered
          }).ToList(),
          CompanyInfo = await GetCompanyInfo(),
          PaymentMethod = sale.PaymentMethod ?? string.Empty,
          IsOverdue = sale.IsOverdue,
          DaysOverdue = sale.DaysOverdue,
          ShippingAddress = sale.ShippingAddress ?? string.Empty,
          OrderNumber = sale.OrderNumber ?? string.Empty,
          TotalShipping = sale.ShippingCost,
          TotalTax = sale.TaxAmount,
          // Calculate amount paid based on payment status
          AmountPaid = sale.PaymentStatus switch
          {
            PaymentStatus.Paid => sale.TotalAmount, // Fully paid
            PaymentStatus.PartiallyPaid => ExtractAmountPaidFromNotes(sale.Notes), // Extract from notes
            _ => 0 // Pending or Overdue = no payment yet
          }
        };

        return View("InvoiceReportPrint", viewModel);
      }
      catch (Exception ex)
      {
        return BadRequest($"Error generating invoice: {ex.Message}");
      }
    }

    // Record Payment - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(int saleId, decimal paymentAmount, string paymentMethod, DateTime paymentDate, string? paymentNotes)
    {
        try
        {
            _logger.LogInformation("Recording payment for Sale ID: {SaleId}, Amount: {PaymentAmount}, Method: {PaymentMethod}, Date: {PaymentDate}", 
                saleId, paymentAmount, paymentMethod, paymentDate);

            // Get the sale
            var sale = await _salesService.GetSaleByIdAsync(saleId);
            if (sale == null)
            {
                TempData["ErrorMessage"] = "Sale not found.";
                return RedirectToAction("Index");
            }

            // Validate payment amount
            if (paymentAmount <= 0)
            {
                TempData["ErrorMessage"] = "Payment amount must be greater than zero.";
                return RedirectToAction("Details", new { id = saleId });
            }

            if (paymentAmount > sale.TotalAmount)
            {
                TempData["ErrorMessage"] = $"Payment amount cannot exceed invoice total of ${sale.TotalAmount:F2}.";
                return RedirectToAction("Details", new { id = saleId });
            }

            // Validate payment method
            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                TempData["ErrorMessage"] = "Payment method is required.";
                return RedirectToAction("Details", new { id = saleId });
            }

            // Update payment information
            sale.PaymentMethod = paymentMethod;
            
            // Update payment status based on amount
            if (paymentAmount >= sale.TotalAmount)
            {
                sale.PaymentStatus = PaymentStatus.Paid;
                _logger.LogInformation("Sale {SaleId} marked as fully paid", saleId);
            }
            else
            {
                sale.PaymentStatus = PaymentStatus.PartiallyPaid;
                _logger.LogInformation("Sale {SaleId} marked as partially paid ({PaymentAmount} of {TotalAmount})", 
                    saleId, paymentAmount, sale.TotalAmount);
            }

            // Add payment notes to sale notes if provided
            var paymentNote = $"Payment recorded: ${paymentAmount:F2} via {paymentMethod} on {paymentDate:MM/dd/yyyy}";
            if (!string.IsNullOrWhiteSpace(paymentNotes))
            {
                paymentNote += $" - {paymentNotes}";
            }

            if (string.IsNullOrWhiteSpace(sale.Notes))
            {
                sale.Notes = paymentNote;
            }
            else
            {
                sale.Notes += Environment.NewLine + paymentNote;
            }

            // Save the updated sale
            var updatedSale = await _salesService.UpdateSaleAsync(sale);
            
            _logger.LogInformation("Payment recorded successfully for Sale {SaleId}. New status: {PaymentStatus}", 
                saleId, sale.PaymentStatus);

            // Set success message
            var successMessage = sale.PaymentStatus == PaymentStatus.Paid 
                ? $"Payment of ${paymentAmount:F2} recorded successfully! Sale is now fully paid."
                : $"Partial payment of ${paymentAmount:F2} recorded successfully. Remaining balance: ${sale.TotalAmount - paymentAmount:F2}";

            TempData["SuccessMessage"] = successMessage;

            // Return to sale details page instead of invoice report
            return RedirectToAction("Details", new { id = saleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording payment for Sale ID: {SaleId}", saleId);
            TempData["ErrorMessage"] = $"Error recording payment: {ex.Message}";
            return RedirectToAction("Details", new { id = saleId });
        }
    }

    // GET: Sales/Backorders
    public async Task<IActionResult> Backorders()
    {
        try
        {
            _logger.LogInformation("Loading backorders");

            // Get all sales with backorders
            var backorderedSales = await _salesService.GetBackorderedSalesAsync();

            _logger.LogInformation("Found {Count} backordered sales", backorderedSales.Count());

            return View(backorderedSales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading backorders");
            TempData["ErrorMessage"] = $"Error loading backorders: {ex.Message}";
            return View(new List<Sale>());
        }
    }

    // GET: Sales/PastDueReport - Simplified version
    public async Task<IActionResult> PastDueReport()
    {
        try
        {
            _logger.LogInformation("Loading past due sales report");

            // Get all sales that are overdue
            var allSales = await _salesService.GetAllSalesAsync();
            var overdueSales = allSales
                .Where(s => s.IsOverdue && s.PaymentStatus != PaymentStatus.Paid && s.SaleStatus != SaleStatus.Cancelled)
                .OrderByDescending(s => s.DaysOverdue)
                .ThenByDescending(s => s.TotalAmount)
                .ToList();

            // Calculate summary metrics for ViewBag
            var totalOverdueAmount = overdueSales.Sum(s => s.TotalAmount);
            var averageDaysOverdue = overdueSales.Any() ? overdueSales.Average(s => s.DaysOverdue) : 0;

            ViewBag.TotalOverdueAmount = totalOverdueAmount;
            ViewBag.AverageDaysOverdue = averageDaysOverdue;

            _logger.LogInformation("Found {Count} overdue sales totaling {Amount:C}", 
                overdueSales.Count, totalOverdueAmount);

            return View(overdueSales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading past due report");
            TempData["ErrorMessage"] = $"Error loading past due report: {ex.Message}";
            return View(new List<Sale>());
        }
    }

    // GET: Sales/Reports - Fixed to return proper SalesReportsViewModel
    public async Task<IActionResult> Reports()
    {
        try
        {
            _logger.LogInformation("Loading sales reports dashboard");

            var allSales = await _salesService.GetAllSalesAsync();
            var validSales = allSales.Where(s => s.SaleStatus != SaleStatus.Cancelled).ToList();

            // Calculate monthly data
            var currentMonth = DateTime.Today.Month;
            var currentYear = DateTime.Today.Year;
            var lastMonth = DateTime.Today.AddMonths(-1);
            
            var currentMonthSales = validSales.Where(s => s.SaleDate.Month == currentMonth && s.SaleDate.Year == currentYear);
            var lastMonthSales = validSales.Where(s => s.SaleDate.Month == lastMonth.Month && s.SaleDate.Year == lastMonth.Year);
            
            var currentMonthTotal = currentMonthSales.Sum(s => s.TotalAmount);
            var lastMonthTotal = lastMonthSales.Sum(s => s.TotalAmount);
            var currentMonthProfit = currentMonthSales.Sum(s => s.TotalProfit);
            var lastMonthProfitTotal = lastMonthSales.Sum(s => s.TotalProfit);

            // Calculate growth percentages
            var monthlyGrowthSales = lastMonthTotal > 0 ? ((currentMonthTotal - lastMonthTotal) / lastMonthTotal) * 100 : 0;
            var monthlyGrowthProfit = lastMonthProfitTotal > 0 ? ((currentMonthProfit - lastMonthProfitTotal) / lastMonthProfitTotal) * 100 : 0;

            // Calculate payment metrics
            var paidSales = validSales.Where(s => s.PaymentStatus == PaymentStatus.Paid);
            var pendingSales = validSales.Where(s => s.PaymentStatus == PaymentStatus.Pending || s.PaymentStatus == PaymentStatus.Overdue);

            var totalSales = validSales.Sum(s => s.TotalAmount);
            var totalProfit = validSales.Sum(s => s.TotalProfit);
            var paidAmount = paidSales.Sum(s => s.TotalAmount);
            var pendingAmount = pendingSales.Sum(s => s.TotalAmount);

            // Create the proper view model
            var viewModel = new SalesReportsViewModel
            {
                // Core metrics
                TotalSales = totalSales,
                TotalProfit = totalProfit,
                TotalSalesCount = validSales.Count,
                
                // Monthly data
                CurrentMonthSales = currentMonthTotal,
                CurrentMonthProfit = currentMonthProfit,
                LastMonthSales = lastMonthTotal,
                LastMonthProfit = lastMonthProfitTotal,
                MonthlyGrowthSales = monthlyGrowthSales,
                MonthlyGrowthProfit = monthlyGrowthProfit,
                
                // Calculated metrics
                AverageSaleValue = validSales.Any() ? validSales.Average(s => s.TotalAmount) : 0,
                ProfitMargin = totalSales > 0 ? (totalProfit / totalSales) * 100 : 0,
                PaymentCollectionRate = totalSales > 0 ? (paidAmount / totalSales) * 100 : 0,
                
                // Sales data
                RecentSales = validSales.OrderByDescending(s => s.SaleDate).Take(10).ToList(),
                PendingSales = pendingSales.Take(10).ToList(),
                
                // Payment status
                PaidSalesCount = paidSales.Count(),
                PendingSalesCount = pendingSales.Count(),
                PaidAmount = paidAmount,
                PendingAmount = pendingAmount,
                
                // Initialize empty collections to prevent null reference exceptions
                TopSellingItems = new List<TopSellingItem>(),
                TopProfitableItems = new List<TopSellingItem>(),
                TopCustomers = new List<TopCustomer>()
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sales reports");
            TempData["ErrorMessage"] = $"Error loading reports: {ex.Message}";
            
            // Return a default model to prevent null reference exceptions
            return View(new SalesReportsViewModel
            {
                TopSellingItems = new List<TopSellingItem>(),
                TopProfitableItems = new List<TopSellingItem>(),
                TopCustomers = new List<TopCustomer>(),
                RecentSales = new List<Sale>(),
                PendingSales = new List<Sale>()
            });
        }
    }

    // Helper method to extract payment amount from notes
    private decimal ExtractAmountPaidFromNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return 0;

        decimal totalPaid = 0;
        var lines = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            // Look for payment records in format: "Payment recorded: $123.45 via..."
            if (line.Contains("Payment recorded: $", StringComparison.OrdinalIgnoreCase))
            {
                var startIndex = line.IndexOf("$") + 1;
                var endIndex = line.IndexOf(" via", startIndex);
                
                if (startIndex > 0 && endIndex > startIndex)
                {
                    var amountStr = line.Substring(startIndex, endIndex - startIndex);
                    if (decimal.TryParse(amountStr, out decimal amount))
                    {
                        totalPaid += amount;
                    }
                }
            }
        }
        
        return totalPaid;
    }

    // Helper method to get sale ID from invoice number
    private async Task<int> GetSaleIdFromInvoiceNumber(string invoiceNumber)
    {
        try
        {
            // Get all sales and find the one with matching sale number
            var sales = await _salesService.GetAllSalesAsync();
            var sale = sales.FirstOrDefault(s => s.SaleNumber == invoiceNumber);
            return sale?.Id ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding sale ID for invoice number: {InvoiceNumber}", invoiceNumber);
            return 0;
        }
    }

    /// <summary>
    /// Converts wildcard patterns (* and ?) to SQL LIKE patterns
    /// * matches any sequence of characters -> %
    /// ? matches any single character -> _
    /// </summary>
    /// <param name="wildcardPattern">The wildcard pattern to convert</param>
    /// <returns>A SQL LIKE pattern string</returns>
    private string ConvertWildcardToLike(string wildcardPattern)
    {
        // Escape existing SQL LIKE special characters first
        var escaped = wildcardPattern
            .Replace("%", "[%]")    // Escape existing % characters
            .Replace("_", "[_]")    // Escape existing _ characters
            .Replace("[", "[[]");   // Escape existing [ characters

        // Convert wildcards to SQL LIKE patterns
        escaped = escaped
            .Replace("*", "%")      // * becomes %
            .Replace("?", "_");     // ? becomes _

        return escaped;
    }

    // Helper method to get company information
    private async Task<ViewModels.CompanyInfo> GetCompanyInfo()
    {
      try
      {
        // Try to get from the database first
        var companyInfoService = HttpContext.RequestServices.GetRequiredService<ICompanyInfoService>();
        var dbCompanyInfo = await companyInfoService.GetCompanyInfoAsync();

        // Convert to the ViewModel CompanyInfo with logo support
        return new ViewModels.CompanyInfo
        {
          CompanyName = dbCompanyInfo.CompanyName,
          Address = dbCompanyInfo.Address,
          City = dbCompanyInfo.City,
          State = dbCompanyInfo.State,
          ZipCode = dbCompanyInfo.ZipCode,
          Phone = dbCompanyInfo.Phone,
          Email = dbCompanyInfo.Email,
          Website = dbCompanyInfo.Website,
          // Add logo properties
          HasLogo = dbCompanyInfo.HasLogo,
          LogoData = dbCompanyInfo.LogoData,
          LogoContentType = dbCompanyInfo.LogoContentType,
          LogoFileName = dbCompanyInfo.LogoFileName
        };
      }
      catch
      {
        // Fallback to hardcoded values if database access fails
        return new ViewModels.CompanyInfo
        {
          CompanyName = "Your Inventory Management Company",
          Address = "123 Business Drive",
          City = "Business City",
          State = "NC",
          ZipCode = "27101",
          Phone = "(336) 555-0123",
          Email = "sales@yourcompany.com",
          Website = "www.yourcompany.com",
          HasLogo = false
        };
      }
    }
  }
}