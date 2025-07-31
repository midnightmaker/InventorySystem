using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.Services
{
  public interface IBackorderFulfillmentService
  {
    Task<bool> FulfillBackordersForProductAsync(int? itemId, int? finishedGoodId, int quantityAvailable);
    Task<bool> CheckAndUpdateSaleStatusAsync(int saleId);
  }

  public class BackorderFulfillmentService : IBackorderFulfillmentService
  {
    private readonly InventoryContext _context;
    private readonly ILogger<BackorderFulfillmentService> _logger;

    public BackorderFulfillmentService(
        InventoryContext context,
        ILogger<BackorderFulfillmentService> logger)
    {
      _context = context;
      _logger = logger;
    }

    public async Task<bool> FulfillBackordersForProductAsync(int? itemId, int? finishedGoodId, int quantityAvailable)
    {
      if (quantityAvailable <= 0) return false;

      var backorderQuery = _context.SaleItems
          .Include(si => si.Sale)
          .Where(si => si.QuantityBackordered > 0);

      if (itemId.HasValue)
      {
        backorderQuery = backorderQuery.Where(si => si.ItemId == itemId.Value);
      }
      else if (finishedGoodId.HasValue)
      {
        backorderQuery = backorderQuery.Where(si => si.FinishedGoodId == finishedGoodId.Value);
      }
      else
      {
        return false;
      }

      var backorderedItems = await backorderQuery
          .OrderBy(si => si.Sale.SaleDate) // FIFO fulfillment
          .ToListAsync();

      int remainingQuantity = quantityAvailable;
      bool anyUpdated = false;

      foreach (var saleItem in backorderedItems)
      {
        if (remainingQuantity <= 0) break;

        int fulfillQuantity = Math.Min(saleItem.QuantityBackordered, remainingQuantity);
        saleItem.QuantityBackordered -= fulfillQuantity;
        remainingQuantity -= fulfillQuantity;
        anyUpdated = true;

        // Update sale status if no more backorders
        await CheckAndUpdateSaleStatusAsync(saleItem.SaleId);

        _logger.LogInformation(
            "Fulfilled {FulfillQuantity} units of backorder for Sale {SaleId}, Product {ProductType} {ProductId}",
            fulfillQuantity, saleItem.SaleId, itemId.HasValue ? "Item" : "FinishedGood", itemId ?? finishedGoodId);
      }

      if (anyUpdated)
      {
        await _context.SaveChangesAsync();
      }

      return anyUpdated;
    }

    public async Task<bool> CheckAndUpdateSaleStatusAsync(int saleId)
    {
      var sale = await _context.Sales
          .Include(s => s.SaleItems)
          .FirstOrDefaultAsync(s => s.Id == saleId);

      if (sale == null) return false;

      bool hasBackorders = sale.SaleItems.Any(si => si.QuantityBackordered > 0);

      if (!hasBackorders && sale.SaleStatus == SaleStatus.Backordered)
      {
        sale.SaleStatus = SaleStatus.Processing;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Sale {SaleNumber} status updated from Backordered to Processing - all backorders fulfilled",
            sale.SaleNumber);

        return true;
      }

      return false;
    }
  }
}
