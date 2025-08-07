// Services/SalesService.cs
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.Services
{
  public class SalesService : ISalesService
  {
    private readonly InventoryContext _context;
    private readonly IInventoryService _inventoryService;
    
    private readonly IPurchaseService _purchaseService;
    private readonly IBackorderFulfillmentService _backorderService;
    private readonly ILogger<SalesService> _logger;

    public SalesService(
        InventoryContext context,
        IInventoryService inventoryService,
        IProductionService productionService,
        IPurchaseService purchaseService,
        IBackorderFulfillmentService backorderService,
        ILogger<SalesService> logger
        )
    {
      _context = context;
      _inventoryService = inventoryService;
      _backorderService = backorderService; 
      _logger = logger;
      _purchaseService = purchaseService;
    }

    public async Task<IEnumerable<Sale>> GetAllSalesAsync()
    {
      return await _context.Sales
          .Include(s => s.Customer) // ADDED: Include Customer for clean relationship
          .Include(s => s.SaleItems)
              .ThenInclude(si => si.Item)
          .Include(s => s.SaleItems)
              .ThenInclude(si => si.FinishedGood)
          .OrderByDescending(s => s.SaleDate)
          .ToListAsync();
    }

    public async Task<Sale?> GetSaleByIdAsync(int id)
    {
      return await _context.Sales
          .Include(s => s.Customer) // ADDED: Include Customer for clean relationship
          .Include(s => s.SaleItems)
              .ThenInclude(si => si.Item)
          .Include(s => s.SaleItems)
              .ThenInclude(si => si.FinishedGood)
          .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Sale> CreateSaleAsync(Sale sale)
    {
      if (string.IsNullOrEmpty(sale.SaleNumber))
      {
        sale.SaleNumber = await GenerateSaleNumberAsync();
      }

      _context.Sales.Add(sale);
      await _context.SaveChangesAsync();
      return sale;
    }

    // FIXED: Removed attempts to set computed properties
    public async Task<Sale> UpdateSaleAsync(Sale sale)
    {
      // The TotalAmount and SubtotalAmount are computed properties that calculate automatically
      // based on SaleItems, ShippingCost, and TaxAmount. No manual calculation needed.
      
      _context.Sales.Update(sale);
      await _context.SaveChangesAsync();
      return sale;
    }

    public async Task DeleteSaleAsync(int id)
    {
      var sale = await _context.Sales
          .Include(s => s.SaleItems)
          .FirstOrDefaultAsync(s => s.Id == id);

      if (sale != null)
      {
        _context.Sales.Remove(sale);
        await _context.SaveChangesAsync();
      }
    }

    public async Task<string> GenerateSaleNumberAsync()
    {
      var today = DateTime.Now;
      var prefix = $"SAL-{today:yyyyMMdd}";

      var lastSale = await _context.Sales
          .Where(s => s.SaleNumber.StartsWith(prefix))
          .OrderByDescending(s => s.SaleNumber)
          .FirstOrDefaultAsync();

      if (lastSale == null)
      {
        return $"{prefix}-001";
      }

      var lastNumber = lastSale.SaleNumber.Substring(prefix.Length + 1);
      if (int.TryParse(lastNumber, out int number))
      {
        return $"{prefix}-{(number + 1):D3}";
      }

      return $"{prefix}-001";
    }

    public async Task<SaleItem> UpdateSaleItemAsync(SaleItem saleItem)
    {
      _context.SaleItems.Update(saleItem);
      await _context.SaveChangesAsync();

      // No need to manually update sale totals - they're computed properties
      // The Sale.TotalAmount and Sale.SubtotalAmount will update automatically

      return saleItem;
    }

    public async Task DeleteSaleItemAsync(int saleItemId)
    {
      var saleItem = await _context.SaleItems.FindAsync(saleItemId);
      if (saleItem != null)
      {
        _context.SaleItems.Remove(saleItem);
        await _context.SaveChangesAsync();

        // No need to manually update sale totals - they're computed properties
      }
    }

    public async Task<bool> ProcessSaleAsync(int saleId)
    {
      var sale = await GetSaleByIdAsync(saleId);
      if (sale == null) return false;

      if (!await CanProcessSaleAsync(saleId))
        return false;

      using var transaction = await _context.Database.BeginTransactionAsync();
      try
      {
        foreach (var saleItem in sale.SaleItems)
        {
          if (saleItem.ItemId.HasValue)
          {
            // Selling raw inventory item
            var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
            if (item != null)
            {
              item.CurrentStock -= saleItem.QuantitySold;

              // Process FIFO consumption
              await _purchaseService.ProcessInventoryConsumptionAsync(
                  saleItem.ItemId.Value,
                  saleItem.QuantitySold);
            }
          }
          else if (saleItem.FinishedGoodId.HasValue)
          {
            // Access finished goods directly from context
            var finishedGood = await _context.FinishedGoods
                .FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
            if (finishedGood != null)
            {
              finishedGood.CurrentStock -= saleItem.QuantitySold;
            }
          }
        }

        // Update sale status
        sale.SaleStatus = SaleStatus.Shipped;
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
        return true;
      }
      catch
      {
        await transaction.RollbackAsync();
        return false;
      }
    }

    // REMOVED: UpdateSaleTotalsAsync method - no longer needed since totals are computed properties

    // Statistics methods
    public async Task<decimal> GetTotalSalesValueAsync()
    {
      var sales = await _context.Sales
          .Include(s => s.SaleItems) // ADDED: Include SaleItems for computed properties
          .Where(s => s.SaleStatus != SaleStatus.Cancelled)
          .ToListAsync();
      return sales.Sum(s => s.TotalAmount); // This works because TotalAmount is computed
    }

    public async Task<decimal> GetTotalSalesValueByMonthAsync(int year, int month)
    {
      var sales = await _context.Sales
          .Include(s => s.SaleItems) // ADDED: Include SaleItems for computed properties
          .Where(s => s.SaleDate.Year == year &&
                     s.SaleDate.Month == month &&
                     s.SaleStatus != SaleStatus.Cancelled)
          .ToListAsync();
      return sales.Sum(s => s.TotalAmount); // This works because TotalAmount is computed
    }

    public async Task<decimal> GetTotalProfitAsync()
    {
      var saleItems = await _context.SaleItems
          .Include(si => si.Sale)
          .Where(si => si.Sale.SaleStatus != SaleStatus.Cancelled)
          .ToListAsync();
      return saleItems.Sum(si => si.Profit);
    }

    public async Task<decimal> GetTotalProfitByMonthAsync(int year, int month)
    {
      var saleItems = await _context.SaleItems
          .Include(si => si.Sale)
          .Where(si => si.Sale.SaleDate.Year == year &&
                     si.Sale.SaleDate.Month == month &&
                     si.Sale.SaleStatus != SaleStatus.Cancelled)
          .ToListAsync();
      return saleItems.Sum(si => si.Profit);
    }

    public async Task<int> GetTotalSalesCountAsync()
    {
      return await _context.Sales
          .CountAsync(s => s.SaleStatus != SaleStatus.Cancelled);
    }

    // UPDATED: Clean method to use Customer relationship instead of legacy CustomerName field
    public async Task<IEnumerable<Sale>> GetSalesByCustomerAsync(int customerId)
    {
      return await _context.Sales
          .Include(s => s.Customer)
          .Include(s => s.SaleItems)
          .Where(s => s.CustomerId == customerId)
          .OrderByDescending(s => s.SaleDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<Sale>> GetSalesByStatusAsync(SaleStatus status)
    {
      return await _context.Sales
          .Include(s => s.Customer) // ADDED: Include Customer
          .Include(s => s.SaleItems)
          .Where(s => s.SaleStatus == status)
          .OrderByDescending(s => s.SaleDate)
          .ToListAsync();
    }

    public async Task<SaleItem> AddSaleItemAsync(SaleItem saleItem)
    {
      // Enhanced logic to handle backorders
      int availableQuantity = 0;

      if (saleItem.ItemId.HasValue)
      {
        var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
        if (item != null)
        {
          availableQuantity = item.CurrentStock;
          saleItem.UnitCost = await _inventoryService.GetAverageCostAsync(saleItem.ItemId.Value);
        }
      }
      else if (saleItem.FinishedGoodId.HasValue)
      {
        var finishedGood = await _context.FinishedGoods
            .FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
        if (finishedGood != null)
        {
          availableQuantity = finishedGood.CurrentStock;
          saleItem.UnitCost = finishedGood.UnitCost;
        }
      }

      // Calculate backorder quantity
      if (availableQuantity < saleItem.QuantitySold)
      {
        saleItem.QuantityBackordered = saleItem.QuantitySold - availableQuantity;
      }
      else
      {
        saleItem.QuantityBackordered = 0;
      }

      _context.SaleItems.Add(saleItem);
      await _context.SaveChangesAsync();

      // Update sale status (totals are computed automatically)
      await _backorderService.CheckAndUpdateSaleStatusAsync(saleItem.SaleId);

      return saleItem;
    }

    public async Task<bool> CheckAndUpdateBackorderStatusAsync(int saleId)
    {
      return await _backorderService.CheckAndUpdateSaleStatusAsync(saleId);
    }

    public async Task<IEnumerable<Sale>> GetBackorderedSalesAsync()
    {
      return await _context.Sales
          .Include(s => s.Customer) // ADDED: Include Customer
          .Include(s => s.SaleItems)
          .ThenInclude(si => si.Item)
          .Include(s => s.SaleItems)
          .ThenInclude(si => si.FinishedGood)
          .Where(s => s.SaleStatus == SaleStatus.Backordered)
          .OrderBy(s => s.SaleDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<SaleItem>> GetBackorderedItemsAsync()
    {
      return await _context.SaleItems
          .Include(si => si.Sale)
          .ThenInclude(s => s.Customer) // ADDED: Include Customer through Sale
          .Include(si => si.Item)
          .Include(si => si.FinishedGood)
          .Where(si => si.QuantityBackordered > 0)
          .OrderBy(si => si.Sale.SaleDate)
          .ToListAsync();
    }

    public async Task<bool> FulfillBackordersForProductAsync(int? itemId, int? finishedGoodId, int quantityAvailable)
    {
      return await _backorderService.FulfillBackordersForProductAsync(itemId, finishedGoodId, quantityAvailable);
    }

    public async Task<bool> CanProcessSaleAsync(int saleId)
    {
      var sale = await GetSaleByIdAsync(saleId);
      if (sale == null) return false;

      foreach (var saleItem in sale.SaleItems)
      {
        if (saleItem.ItemId.HasValue)
        {
          var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
          if (item == null || item.CurrentStock < saleItem.QuantitySold)
            return false;
        }
        else if (saleItem.FinishedGoodId.HasValue)
        {
          var finishedGood = await _context.FinishedGoods
              .FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
          if (finishedGood == null || finishedGood.CurrentStock < saleItem.QuantitySold)
            return false;
        }
      }

      return true;
    }
  }
}