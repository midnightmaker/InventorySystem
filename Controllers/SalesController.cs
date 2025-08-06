// Controllers/SalesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Services;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using InventorySystem.Models.Enums;

namespace InventorySystem.Controllers
{
  public class SalesController : Controller
  {
    private readonly ISalesService _salesService;
    private readonly IInventoryService _inventoryService;
    private readonly IProductionService _productionService;

    public SalesController(
        ISalesService salesService,
        IInventoryService inventoryService,
        IProductionService productionService)
    {
      _salesService = salesService;
      _inventoryService = inventoryService;
      _productionService = productionService;
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

    // Create Sale - GET
    public async Task<IActionResult> Create()
    {
      var sale = new Sale
      {
        SaleNumber = await _salesService.GenerateSaleNumberAsync(),
        SaleDate = DateTime.Now,
        PaymentStatus = PaymentStatus.Pending,
        SaleStatus = SaleStatus.Processing
      };

      return View(sale);
    }

    // Create Sale - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Sale sale)
    {
      // Perform additional server-side validation
      ValidatePaymentDueDate(sale);

      if (ModelState.IsValid)
      {
        try
        {
          // Calculate payment due date before saving
          sale.CalculatePaymentDueDate();

          // Validate again after calculation
          ValidatePaymentDueDate(sale);

          if (ModelState.IsValid)
          {
            await _salesService.CreateSaleAsync(sale);
            TempData["SuccessMessage"] = "Sale created successfully!";
            return RedirectToAction("Details", new { id = sale.Id });
          }
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Error creating sale: {ex.Message}";
        }
      }

      return View(sale);
    }

    // Replace the AddItem GET method:
    public async Task<IActionResult> AddItem(int saleId)
    {
      var sale = await _salesService.GetSaleByIdAsync(saleId);
      if (sale == null) return NotFound();

      var items = await _inventoryService.GetAllItemsAsync();
      var finishedGoods = await _productionService.GetAllFinishedGoodsAsync();

      ViewBag.SaleId = saleId;
      ViewBag.SaleNumber = sale.SaleNumber;
      ViewBag.CustomerName = sale.CustomerName;

      // NEW - Remove stock filters to allow zero-stock items
      ViewBag.Items = new SelectList(items, "Id", "PartNumber");
      ViewBag.FinishedGoods = new SelectList(finishedGoods, "Id", "PartNumber");

      var viewModel = new AddSaleItemViewModel
      {
        SaleId = saleId,
        Quantity = 1,
        ProductType = "Item"
      };

      return View(viewModel);
    }

    // Replace the AddItem POST method:
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(AddSaleItemViewModel viewModel)
    {
      if (ModelState.IsValid)
      {
        try
        {
          var saleItem = new SaleItem
          {
            SaleId = viewModel.SaleId,
            QuantitySold = viewModel.Quantity,
            UnitPrice = viewModel.UnitPrice,
            Notes = viewModel.Notes
          };

          if (viewModel.ProductType == "Item" && viewModel.ItemId.HasValue)
          {
            saleItem.ItemId = viewModel.ItemId;
            var item = await _inventoryService.GetItemByIdAsync(viewModel.ItemId.Value);
            if (item == null)
            {
              TempData["ErrorMessage"] = "Selected item not found.";
              return await ReloadAddItemView(viewModel);
            }
            // NEW - Remove stock validation, allow backorders
          }
          else if (viewModel.ProductType == "FinishedGood" && viewModel.FinishedGoodId.HasValue)
          {
            saleItem.FinishedGoodId = viewModel.FinishedGoodId;
            var finishedGood = await _productionService.GetFinishedGoodByIdAsync(viewModel.FinishedGoodId.Value);
            if (finishedGood == null)
            {
              TempData["ErrorMessage"] = "Selected finished good not found.";
              return await ReloadAddItemView(viewModel);
            }
            // NEW - Remove stock validation, allow backorders
          }

          await _salesService.AddSaleItemAsync(saleItem);

          // NEW - Check if item was backordered and show appropriate message
          if (saleItem.QuantityBackordered > 0)
          {
            TempData["WarningMessage"] = $"Item added to sale! {saleItem.QuantityBackordered} units are backordered due to insufficient stock.";
          }
          else
          {
            TempData["SuccessMessage"] = "Item added to sale successfully!";
          }

          return RedirectToAction("Details", new { id = viewModel.SaleId });
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Error adding item to sale: {ex.Message}";
        }
      }

      return await ReloadAddItemView(viewModel);
    }

    // Update ReloadAddItemView method:
    private async Task<IActionResult> ReloadAddItemView(AddSaleItemViewModel viewModel)
    {
      var sale = await _salesService.GetSaleByIdAsync(viewModel.SaleId);
      var items = await _inventoryService.GetAllItemsAsync();
      var finishedGoods = await _productionService.GetAllFinishedGoodsAsync();

      ViewBag.SaleId = viewModel.SaleId;
      ViewBag.SaleNumber = sale?.SaleNumber ?? "";
      ViewBag.CustomerName = sale?.CustomerName ?? "";

      // NEW - Remove stock filters
      ViewBag.Items = new SelectList(items, "Id", "PartNumber", viewModel.ItemId);
      ViewBag.FinishedGoods = new SelectList(finishedGoods, "Id", "PartNumber", viewModel.FinishedGoodId);

      return View("AddItem", viewModel);
    }

    // Add new method for backorder management:
    public async Task<IActionResult> Backorders()
    {
      try
      {
        var backorderedSales = await _salesService.GetBackorderedSalesAsync();
        return View(backorderedSales);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading backorders: {ex.Message}";
        return View(new List<Sale>());
      }
    }

    // Add this action for past due sales report
    public async Task<IActionResult> PastDueReport()
    {
      try
      {
        var allSales = await _salesService.GetAllSalesAsync();

        var pastDueSales = allSales
            .Where(s => s.IsOverdue && s.PaymentStatus != PaymentStatus.Paid)
            .OrderByDescending(s => s.DaysOverdue)
            .ToList();

        ViewBag.TotalOverdueAmount = pastDueSales.Sum(s => s.TotalAmount);
        ViewBag.AverageDaysOverdue = pastDueSales.Any() ? pastDueSales.Average(s => s.DaysOverdue) : 0;

        return View(pastDueSales);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading past due report: {ex.Message}";
        return View(new List<Sale>());
      }
    }

    // Remove Item from Sale
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(int saleItemId, int saleId)
    {
      try
      {
        await _salesService.DeleteSaleItemAsync(saleItemId);
        TempData["SuccessMessage"] = "Item removed from sale successfully!";
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error removing item: {ex.Message}";
      }

      return RedirectToAction("Details", new { id = saleId });
    }

    // Process Sale (Ship and reduce inventory)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessSale(int id)
    {
      try
      {
        var canProcess = await _salesService.CanProcessSaleAsync(id);
        if (!canProcess)
        {
          TempData["ErrorMessage"] = "Cannot process sale - insufficient inventory.";
          return RedirectToAction("Details", new { id });
        }

        var success = await _salesService.ProcessSaleAsync(id);
        if (success)
        {
          TempData["SuccessMessage"] = "Sale processed successfully! Inventory has been updated.";
        }
        else
        {
          TempData["ErrorMessage"] = "Error processing sale.";
        }
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error processing sale: {ex.Message}";
      }

      return RedirectToAction("Details", new { id });
    }

    [HttpGet]
    public async Task<IActionResult> CheckProductAvailability(string productType, int productId, int quantity)
    {
      try
      {
        bool available = false;
        decimal suggestedPrice = 0;
        string productName = "";
        int currentStock = 0;
        int backorderQuantity = 0;
        string availabilityMessage = "";

        if (productType == "Item")
        {
          var item = await _inventoryService.GetItemByIdAsync(productId);
          if (item != null)
          {
            currentStock = item.CurrentStock;
            available = currentStock >= quantity;
            backorderQuantity = Math.Max(0, quantity - currentStock);
            suggestedPrice = await _inventoryService.GetAverageCostAsync(productId) * 1.3m;
            productName = $"{item.PartNumber} - {item.Description}";

            if (available)
            {
              availabilityMessage = "✅ Sufficient stock available";
            }
            else
            {
              availabilityMessage = $"⚠️ {backorderQuantity} units will be backordered";
            }
          }
        }
        else if (productType == "FinishedGood")
        {
          var finishedGood = await _productionService.GetFinishedGoodByIdAsync(productId);
          if (finishedGood != null)
          {
            currentStock = finishedGood.CurrentStock;
            available = currentStock >= quantity;
            backorderQuantity = Math.Max(0, quantity - currentStock);
            suggestedPrice = finishedGood.SellingPrice > 0 ? finishedGood.SellingPrice : finishedGood.UnitCost * 1.5m;
            productName = $"{finishedGood.PartNumber} - {finishedGood.Description}";

            if (available)
            {
              availabilityMessage = "✅ Sufficient stock available";
            }
            else
            {
              availabilityMessage = $"⚠️ {backorderQuantity} units will be backordered";
            }
          }
        }

        return Json(new
        {
          success = true,
          available = available,
          suggestedPrice = suggestedPrice,
          productName = productName,
          currentStock = currentStock,
          backorderQuantity = backorderQuantity,
          availabilityMessage = availabilityMessage
        });
      }
      catch (Exception ex)
      {
        return Json(new { success = false, error = ex.Message });
      }
    }

    // Sales Reports
    public async Task<IActionResult> Reports()
    {
      try
      {
        // Get all sales data
        var allSales = await _salesService.GetAllSalesAsync();
        var allSaleItems = allSales.SelectMany(s => s.SaleItems).ToList();

        // Calculate top selling products
        var topSellingItems = allSaleItems
            .GroupBy(si => new
            {
              ProductType = si.ItemId.HasValue ? "Item" : "FinishedGood",
              ProductId = si.ItemId ?? si.FinishedGoodId,
              ProductName = si.ProductName,
              ProductPartNumber = si.ProductPartNumber
            })
            .Select(g => new TopSellingProduct
            {
              ProductType = g.Key.ProductType,
              ProductName = g.Key.ProductName,
              ProductPartNumber = g.Key.ProductPartNumber,
              QuantitySold = g.Sum(si => si.QuantitySold),
              TotalRevenue = g.Sum(si => si.TotalPrice),
              TotalProfit = g.Sum(si => si.Profit),
              ProfitMargin = g.Sum(si => si.TotalPrice) > 0 ? (g.Sum(si => si.Profit) / g.Sum(si => si.TotalPrice)) * 100 : 0,
              SalesCount = g.Count()
            })
            .OrderByDescending(p => p.QuantitySold)
            .Take(10)
            .ToList();

        var topProfitableItems = allSaleItems
            .GroupBy(si => new
            {
              ProductType = si.ItemId.HasValue ? "Item" : "FinishedGood",
              ProductId = si.ItemId ?? si.FinishedGoodId,
              ProductName = si.ProductName,
              ProductPartNumber = si.ProductPartNumber
            })
            .Select(g => new TopSellingProduct
            {
              ProductType = g.Key.ProductType,
              ProductName = g.Key.ProductName,
              ProductPartNumber = g.Key.ProductPartNumber,
              QuantitySold = g.Sum(si => si.QuantitySold),
              TotalRevenue = g.Sum(si => si.TotalPrice),
              TotalProfit = g.Sum(si => si.Profit),
              ProfitMargin = g.Sum(si => si.TotalPrice) > 0 ? (g.Sum(si => si.Profit) / g.Sum(si => si.TotalPrice)) * 100 : 0,
              SalesCount = g.Count()
            })
            .OrderByDescending(p => p.TotalProfit)
            .Take(10)
            .ToList();

        // Calculate customer summaries
        var topCustomers = allSales
            .Where(s => s.SaleStatus != SaleStatus.Cancelled)
            .GroupBy(s => new { s.CustomerName, s.CustomerEmail })
            .Select(g => new CustomerSummary
            {
              CustomerName = g.Key.CustomerName,
              CustomerEmail = g.Key.CustomerEmail,
              SalesCount = g.Count(),
              TotalPurchases = g.Sum(s => s.TotalAmount),
              TotalProfit = g.SelectMany(s => s.SaleItems).Sum(si => si.Profit),
              LastPurchaseDate = g.Max(s => s.SaleDate)
            })
            .OrderByDescending(c => c.TotalPurchases)
            .Take(10)
            .ToList();

        // Calculate payment status
        var paidSales = allSales.Where(s => s.PaymentStatus == PaymentStatus.Paid && s.SaleStatus != SaleStatus.Cancelled);
        var pendingSales = allSales.Where(s => s.PaymentStatus == PaymentStatus.Pending && s.SaleStatus != SaleStatus.Cancelled);

        var viewModel = new SalesReportsViewModel
        {
          // Core metrics
          TotalSales = await _salesService.GetTotalSalesValueAsync(),
          TotalProfit = await _salesService.GetTotalProfitAsync(),
          TotalSalesCount = await _salesService.GetTotalSalesCountAsync(),
          CurrentMonthSales = await _salesService.GetTotalSalesValueByMonthAsync(DateTime.Now.Year, DateTime.Now.Month),
          CurrentMonthProfit = await _salesService.GetTotalProfitByMonthAsync(DateTime.Now.Year, DateTime.Now.Month),
          LastMonthSales = await _salesService.GetTotalSalesValueByMonthAsync(DateTime.Now.AddMonths(-1).Year, DateTime.Now.AddMonths(-1).Month),
          LastMonthProfit = await _salesService.GetTotalProfitByMonthAsync(DateTime.Now.AddMonths(-1).Year, DateTime.Now.AddMonths(-1).Month),

          // Recent activity
          RecentSales = allSales.OrderByDescending(s => s.SaleDate).Take(5),
          PendingSales = allSales.Where(s => s.SaleStatus == SaleStatus.Processing).OrderByDescending(s => s.SaleDate).Take(5),

          // Product analytics
          TopSellingItems = topSellingItems,
          TopProfitableItems = topProfitableItems,

          // Customer analytics
          TopCustomers = topCustomers,

          // Payment status
          PaidSalesCount = paidSales.Count(),
          PendingSalesCount = pendingSales.Count(),
          PaidAmount = paidSales.Sum(s => s.TotalAmount),
          PendingAmount = pendingSales.Sum(s => s.TotalAmount)
        };

        return View(viewModel);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading sales reports: {ex.Message}";
        return View(new SalesReportsViewModel());
      }
    }

    // Delete Sale
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
      try
      {
        await _salesService.DeleteSaleAsync(id);
        TempData["SuccessMessage"] = "Sale deleted successfully!";
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error deleting sale: {ex.Message}";
      }

      return RedirectToAction("Index");
    }

    // Helper method for payment due date validation
    private void ValidatePaymentDueDate(Sale sale)
    {
      // Check if payment due date is in the past
      if (sale.PaymentDueDate.Date < DateTime.Today)
      {
        ModelState.AddModelError(nameof(sale.PaymentDueDate),
            "Payment due date cannot be in the past.");
      }

      // Check if payment due date is before sale date
      if (sale.PaymentDueDate.Date < sale.SaleDate.Date)
      {
        ModelState.AddModelError(nameof(sale.PaymentDueDate),
            "Payment due date cannot be before the sale date.");
      }

      // Business rule: Immediate terms should have same due date as sale date
      if (sale.Terms == PaymentTerms.Immediate && sale.PaymentDueDate.Date != sale.SaleDate.Date)
      {
        ModelState.AddModelError(nameof(sale.PaymentDueDate),
            "Payment due date must be the same as sale date for Immediate terms.");
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

        // Create customer info from sale data
        var customer = new CustomerInfo
        {
          CustomerName = sale.CustomerName,
          CustomerEmail = sale.CustomerEmail ?? string.Empty,
          CustomerPhone = sale.CustomerPhone ?? string.Empty,
          BillingAddress = sale.ShippingAddress ?? string.Empty,
          ShippingAddress = sale.ShippingAddress ?? string.Empty
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
          CustomerEmail = sale.CustomerEmail ?? string.Empty,
          EmailSubject = $"Invoice {sale.SaleNumber}",
          EmailMessage = $"Please find attached Invoice {sale.SaleNumber} for your recent purchase.",
          PaymentMethod = sale.PaymentMethod ?? string.Empty,
          IsOverdue = sale.IsOverdue,
          DaysOverdue = sale.DaysOverdue,
          ShippingAddress = sale.ShippingAddress ?? string.Empty,
          OrderNumber = sale.OrderNumber ?? string.Empty,
          TotalShipping = sale.ShippingCost,
          TotalTax = sale.TaxAmount
        };

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

        // Create customer info from sale data
        var customer = new CustomerInfo
        {
          CustomerName = sale.CustomerName,
          CustomerEmail = sale.CustomerEmail ?? string.Empty,
          CustomerPhone = sale.CustomerPhone ?? string.Empty,
          BillingAddress = sale.ShippingAddress ?? string.Empty,
          ShippingAddress = sale.ShippingAddress ?? string.Empty
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
          TotalTax = sale.TaxAmount
        };

        return View("InvoiceReportPrint", viewModel);
      }
      catch (Exception ex)
      {
        return BadRequest($"Error generating invoice: {ex.Message}");
      }
    }

    // Email Invoice to Customer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailInvoiceReport(InvoiceReportViewModel model)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(model.CustomerEmail))
        {
          TempData["ErrorMessage"] = "Customer email address is required.";
          return RedirectToAction("InvoiceReport", new { saleId = GetSaleIdFromInvoiceNumber(model.InvoiceNumber) });
        }

        // Generate the HTML invoice
        var invoiceHtml = await RenderInvoiceToStringAsync("InvoiceReportEmail", model);

        // Send email (you'll need to implement IEmailService)
        // var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
        // var emailSuccess = await emailService.SendEmailAsync(
        //     model.CustomerEmail,
        //     model.EmailSubject,
        //     invoiceHtml,
        //     isHtml: true
        // );

        // if (emailSuccess)
        // {
        //     TempData["SuccessMessage"] = $"Invoice {model.InvoiceNumber} emailed successfully to {model.CustomerEmail}";
        // }
        // else
        // {
        //     TempData["ErrorMessage"] = "Failed to send email. Please try again or contact the customer directly.";
        // }

        // For now, show success message
        TempData["SuccessMessage"] = $"Invoice {model.InvoiceNumber} would be emailed to {model.CustomerEmail}";

        return RedirectToAction("InvoiceReport", new { saleId = GetSaleIdFromInvoiceNumber(model.InvoiceNumber) });
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error sending email: {ex.Message}";
        return RedirectToAction("InvoiceReport", new { saleId = GetSaleIdFromInvoiceNumber(model.InvoiceNumber) });
      }
    }

    // Helper method to get company information (reuse from PurchasesController)
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

    // Helper method to render invoice to string (for email HTML)
    private Task<string> RenderInvoiceToStringAsync(string viewName, InvoiceReportViewModel model)
    {
      // Simplified implementation - in production you'd use a proper view rendering service
      var html = $@"
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; margin: 20px; }}
                .header {{ text-align: center; border-bottom: 2px solid #333; padding-bottom: 10px; }}
                .company-info {{ margin: 20px 0; }}
                .customer-info {{ margin: 20px 0; }}
                table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
                th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                th {{ background-color: #f2f2f2; }}
                .total-row {{ font-weight: bold; background-color: #f9f9f9; }}
                .overdue {{ color: #dc3545; font-weight: bold; }}
            </style>
        </head>
        <body>
            <div class='header'>
                <h1>INVOICE</h1>
                <h2>Invoice# {model.InvoiceNumber}</h2>
                {(model.IsOverdue ? $"<p class='overdue'>⚠️ OVERDUE - {model.DaysOverdue} days past due</p>" : "")}
            </div>
            
            <div class='company-info'>
                <h3>From:</h3>
                <p><strong>{model.CompanyInfo.CompanyName}</strong><br/>
                {model.CompanyInfo.Address}<br/>
                {model.CompanyInfo.City}, {model.CompanyInfo.State} {model.CompanyInfo.ZipCode}<br/>
                Phone: {model.CompanyInfo.Phone}<br/>
                Email: {model.CompanyInfo.Email}</p>
            </div>
            
            <div class='customer-info'>
                <h3>Bill To:</h3>
                <p><strong>{model.Customer.CustomerName}</strong><br/>
                {model.Customer.BillingAddress}<br/>
                Phone: {model.Customer.CustomerPhone}<br/>
                Email: {model.Customer.CustomerEmail}</p>
            </div>
            
            <p><strong>Invoice Date:</strong> {model.InvoiceDate:MM/dd/yyyy}<br/>
            <strong>Due Date:</strong> {model.DueDate?.ToString("MM/dd/yyyy") ?? "Immediate"}<br/>
            <strong>Payment Terms:</strong> {model.PaymentTerms}<br/>
            <strong>Status:</strong> {model.PaymentStatus}</p>
            
            <table>
                <thead>
                    <tr>
                        <th>Line #</th>
                        <th>Item #</th>
                        <th>Description</th>
                        <th>Qty</th>
                        <th>Unit Price</th>
                        <th>Total</th>
                    </tr>
                </thead>
                <tbody>";

      int lineNumber = 1;
      foreach (var item in model.LineItems)
      {
        html += $@"
                    <tr>
                        <td>{lineNumber}</td>
                        <td>{item.PartNumber}</td>
                        <td>{item.Description}{(item.IsBackordered ? $" <em>({item.BackorderStatus})</em>" : "")}</td>
                        <td>{item.Quantity}</td>
                        <td>${item.UnitPrice:F2}</td>
                        <td>${item.LineTotal:F2}</td>
                    </tr>";
        lineNumber++;
      }

      html += $@"
                    <tr class='total-row'>
                        <td colspan='5'><strong>Subtotal</strong></td>
                        <td><strong>${model.SubtotalAmount:F2}</strong></td>
                    </tr>";

      if (model.TotalShipping > 0)
      {
        html += $@"
                    <tr class='total-row'>
                        <td colspan='5'><strong>Shipping</strong></td>
                        <td><strong>${model.TotalShipping:F2}</strong></td>
                    </tr>";
      }

      if (model.TotalTax > 0)
      {
        html += $@"
                    <tr class='total-row'>
                        <td colspan='5'><strong>Tax</strong></td>
                        <td><strong>${model.TotalTax:F2}</strong></td>
                    </tr>";
      }

      html += $@"
                    <tr class='total-row' style='font-size: 1.2em;'>
                        <td colspan='5'><strong>TOTAL DUE</strong></td>
                        <td><strong>${model.GrandTotal:F2}</strong></td>
                    </tr>
                </tbody>
            </table>
            
            {(string.IsNullOrEmpty(model.Notes) ? "" : $"<p><strong>Notes:</strong> {model.Notes}</p>")}
            
            <p><em>Thank you for your business!</em></p>
            <p><small>Please include invoice number {model.InvoiceNumber} with your payment.</small></p>
        </body>
        </html>";

      return Task.FromResult(html);
    }

    // Helper method to get sale ID from invoice number
    private int GetSaleIdFromInvoiceNumber(string invoiceNumber)
    {
      // This would need to be implemented based on how you store sale numbers
      // For now, return a placeholder
      return 1;
    }
  }
}