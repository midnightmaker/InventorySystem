// Controllers/SalesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Services;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using InventorySystem.Models.Enums;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Controllers
{
  public class SalesController : Controller
  {
    private readonly ISalesService _salesService;
    private readonly IInventoryService _inventoryService;
    private readonly IProductionService _productionService;
    private readonly ICustomerService _customerService;
    private readonly ILogger<SalesController> _logger;

    public SalesController(
        ISalesService salesService,
        IInventoryService inventoryService,
        IProductionService productionService,
        ICustomerService customerService,
        ILogger<SalesController> logger)
    {
      _salesService = salesService;
      _inventoryService = inventoryService;
      _productionService = productionService;
      _customerService = customerService;
      _logger = logger;
    }

    // Sales Index
    public async Task<IActionResult> Index()
    {
      var sales = await _salesService.GetAllSalesAsync();
      return View(sales);
    }

    // Sale Details
    public async Task<IActionResult> Details(int id)
    {
      var sale = await _salesService.GetSaleByIdAsync(id);
      if (sale == null) return NotFound();
      return View(sale);
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