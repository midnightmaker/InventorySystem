// Services/SalesService.cs
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
  public class SalesService : ISalesService
  {
    private readonly InventoryContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IProductionService _productionService;
    private readonly IPurchaseService _purchaseService;

    public SalesService(
        InventoryContext context,
        IInventoryService inventoryService,
        IProductionService productionService,
        IPurchaseService purchaseService)
    {
      _context = context;
      _inventoryService = inventoryService;
      _productionService = productionService;
      _purchaseService = purchaseService;
    }

    public async Task<IEnumerable<Sale>> GetAllSalesAsync()
    {
      return await _context.Sales
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

    public async Task<Sale> UpdateSaleAsync(Sale sale)
    {
      // Recalculate totals based on sale items
      var saleItems = await _context.SaleItems
          .Where(si => si.SaleId == sale.Id)
          .ToListAsync();

      sale.Subtotal = saleItems.Sum(si => si.TotalPrice);
      sale.TotalAmount = sale.Subtotal + sale.TaxAmount + sale.ShippingCost;

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

    public async Task<SaleItem> AddSaleItemAsync(SaleItem saleItem)
    {
      // Set unit cost based on item type
      if (saleItem.ItemId.HasValue)
      {
        var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
        if (item != null)
        {
          saleItem.UnitCost = await _inventoryService.GetAverageCostAsync(saleItem.ItemId.Value);
        }
      }
      else if (saleItem.FinishedGoodId.HasValue)
      {
        var finishedGood = await _productionService.GetFinishedGoodByIdAsync(saleItem.FinishedGoodId.Value);
        if (finishedGood != null)
        {
          saleItem.UnitCost = finishedGood.UnitCost;
        }
      }

      _context.SaleItems.Add(saleItem);
      await _context.SaveChangesAsync();

      // Update sale totals
      await UpdateSaleTotalsAsync(saleItem.SaleId);

      return saleItem;
    }

    public async Task<SaleItem> UpdateSaleItemAsync(SaleItem saleItem)
    {
      _context.SaleItems.Update(saleItem);
      await _context.SaveChangesAsync();

      // Update sale totals
      await UpdateSaleTotalsAsync(saleItem.SaleId);

      return saleItem;
    }

    public async Task DeleteSaleItemAsync(int saleItemId)
    {
      var saleItem = await _context.SaleItems.FindAsync(saleItemId);
      if (saleItem != null)
      {
        var saleId = saleItem.SaleId;
        _context.SaleItems.Remove(saleItem);
        await _context.SaveChangesAsync();

        // Update sale totals
        await UpdateSaleTotalsAsync(saleId);
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
            // Selling finished good
            var finishedGood = await _productionService.GetFinishedGoodByIdAsync(saleItem.FinishedGoodId.Value);
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
          var finishedGood = await _productionService.GetFinishedGoodByIdAsync(saleItem.FinishedGoodId.Value);
          if (finishedGood == null || finishedGood.CurrentStock < saleItem.QuantitySold)
            return false;
        }
      }

      return true;
    }

    private async Task UpdateSaleTotalsAsync(int saleId)
    {
      var sale = await _context.Sales.FindAsync(saleId);
      if (sale == null) return;

      var saleItems = await _context.SaleItems
          .Where(si => si.SaleId == saleId)
          .ToListAsync();

      sale.Subtotal = saleItems.Sum(si => si.TotalPrice);
      sale.TotalAmount = sale.Subtotal + sale.TaxAmount + sale.ShippingCost;

      await _context.SaveChangesAsync();
    }

    // Statistics methods
    public async Task<decimal> GetTotalSalesValueAsync()
    {
      var sales = await _context.Sales
          .Where(s => s.SaleStatus != SaleStatus.Cancelled)
          .ToListAsync();
      return sales.Sum(s => s.TotalAmount);
    }

    public async Task<decimal> GetTotalSalesValueByMonthAsync(int year, int month)
    {
      var sales = await _context.Sales
          .Where(s => s.SaleDate.Year == year &&
                     s.SaleDate.Month == month &&
                     s.SaleStatus != SaleStatus.Cancelled)
          .ToListAsync();
      return sales.Sum(s => s.TotalAmount);
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

    public async Task<IEnumerable<Sale>> GetSalesByCustomerAsync(string customerName)
    {
      return await _context.Sales
          .Include(s => s.SaleItems)
          .Where(s => s.CustomerName.ToLower().Contains(customerName.ToLower()))
          .OrderByDescending(s => s.SaleDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<Sale>> GetSalesByStatusAsync(SaleStatus status)
    {
      return await _context.Sales
          .Include(s => s.SaleItems)
          .Where(s => s.SaleStatus == status)
          .OrderByDescending(s => s.SaleDate)
          .ToListAsync();
    }
  }
}