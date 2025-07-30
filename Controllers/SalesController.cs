
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
      if (ModelState.IsValid)
      {
        try
        {
          await _salesService.CreateSaleAsync(sale);
          TempData["SuccessMessage"] = "Sale created successfully!";
          return RedirectToAction("Details", new { id = sale.Id });
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Error updating sale: {ex.Message}";
        }
      }

      return View(sale);
    }

    // Add Item to Sale - GET
    public async Task<IActionResult> AddItem(int saleId)
    {
      var sale = await _salesService.GetSaleByIdAsync(saleId);
      if (sale == null) return NotFound();

      var items = await _inventoryService.GetAllItemsAsync();
      var finishedGoods = await _productionService.GetAllFinishedGoodsAsync();

      ViewBag.SaleId = saleId;
      ViewBag.SaleNumber = sale.SaleNumber;
      ViewBag.CustomerName = sale.CustomerName;
      ViewBag.Items = new SelectList(items.Where(i => i.CurrentStock > 0), "Id", "PartNumber");
      ViewBag.FinishedGoods = new SelectList(finishedGoods.Where(fg => fg.CurrentStock > 0), "Id", "PartNumber");

      var viewModel = new AddSaleItemViewModel
      {
        SaleId = saleId,
        Quantity = 1,
        ProductType = "Item" // Default to selling items
      };

      return View(viewModel);
    }

    // Add Item to Sale - POST
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
            if (item == null || item.CurrentStock < viewModel.Quantity)
            {
              TempData["ErrorMessage"] = "Insufficient stock for this item.";
              return await ReloadAddItemView(viewModel);
            }
          }
          else if (viewModel.ProductType == "FinishedGood" && viewModel.FinishedGoodId.HasValue)
          {
            saleItem.FinishedGoodId = viewModel.FinishedGoodId;
            var finishedGood = await _productionService.GetFinishedGoodByIdAsync(viewModel.FinishedGoodId.Value);
            if (finishedGood == null || finishedGood.CurrentStock < viewModel.Quantity)
            {
              TempData["ErrorMessage"] = "Insufficient stock for this finished good.";
              return await ReloadAddItemView(viewModel);
            }
          }

          await _salesService.AddSaleItemAsync(saleItem);
          TempData["SuccessMessage"] = "Item added to sale successfully!";
          return RedirectToAction("Details", new { id = viewModel.SaleId });
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Error adding item to sale: {ex.Message}";
        }
      }

      return await ReloadAddItemView(viewModel);
    }

    private async Task<IActionResult> ReloadAddItemView(AddSaleItemViewModel viewModel)
    {
      var sale = await _salesService.GetSaleByIdAsync(viewModel.SaleId);
      var items = await _inventoryService.GetAllItemsAsync();
      var finishedGoods = await _productionService.GetAllFinishedGoodsAsync();

      ViewBag.SaleId = viewModel.SaleId;
      ViewBag.SaleNumber = sale?.SaleNumber ?? "";
      ViewBag.CustomerName = sale?.CustomerName ?? "";
      ViewBag.Items = new SelectList(items.Where(i => i.CurrentStock > 0), "Id", "PartNumber", viewModel.ItemId);
      ViewBag.FinishedGoods = new SelectList(finishedGoods.Where(fg => fg.CurrentStock > 0), "Id", "PartNumber", viewModel.FinishedGoodId);

      return View("AddItem", viewModel);
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

    // Check Product Availability (AJAX)
    [HttpGet]
    public async Task<IActionResult> CheckProductAvailability(string productType, int productId, int quantity)
    {
      try
      {
        bool available = false;
        decimal suggestedPrice = 0;
        string productName = "";
        int currentStock = 0;

        if (productType == "Item")
        {
          var item = await _inventoryService.GetItemByIdAsync(productId);
          if (item != null)
          {
            available = item.CurrentStock >= quantity;
            suggestedPrice = await _inventoryService.GetAverageCostAsync(productId) * 1.3m; // 30% markup
            productName = $"{item.PartNumber} - {item.Description}";
            currentStock = item.CurrentStock;
          }
        }
        else if (productType == "FinishedGood")
        {
          var finishedGood = await _productionService.GetFinishedGoodByIdAsync(productId);
          if (finishedGood != null)
          {
            available = finishedGood.CurrentStock >= quantity;
            suggestedPrice = finishedGood.SellingPrice > 0 ? finishedGood.SellingPrice : finishedGood.UnitCost * 1.5m; // 50% markup if no selling price set
            productName = $"{finishedGood.PartNumber} - {finishedGood.Description}";
            currentStock = finishedGood.CurrentStock;
          }
        }

        return Json(new
        {
          success = true,
          available = available,
          suggestedPrice = suggestedPrice,
          productName = productName,
          currentStock = currentStock
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
            .GroupBy(si => new {
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
            .GroupBy(si => new {
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
  }
}