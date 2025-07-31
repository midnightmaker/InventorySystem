using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.ViewModels;

namespace InventorySystem.Services
{
  public interface IBackorderNotificationService
  {
    Task NotifyBackorderCreatedAsync(Sale sale, SaleItem saleItem);
    Task NotifyBackorderFulfilledAsync(Sale sale, SaleItem saleItem);
    Task<IEnumerable<BackorderAlert>> GetBackorderAlertsAsync();
  }

  public class BackorderNotificationService : IBackorderNotificationService
  {
    private readonly InventoryContext _context;
    private readonly ILogger<BackorderNotificationService> _logger;

    public BackorderNotificationService(
        InventoryContext context,
        ILogger<BackorderNotificationService> logger)
    {
      _context = context;
      _logger = logger;
    }

    public async Task NotifyBackorderCreatedAsync(Sale sale, SaleItem saleItem)
    {
      _logger.LogInformation(
          "Backorder created: Sale {SaleNumber}, Product {ProductName}, Quantity {Quantity}",
          sale.SaleNumber, saleItem.ProductName, saleItem.QuantityBackordered);

      // TODO: Add email notification logic here
      // TODO: Add system notification/alert creation
    }

    public async Task NotifyBackorderFulfilledAsync(Sale sale, SaleItem saleItem)
    {
      _logger.LogInformation(
          "Backorder fulfilled: Sale {SaleNumber}, Product {ProductName}",
          sale.SaleNumber, saleItem.ProductName);

      // TODO: Add email notification to customer
      // TODO: Update sale status if all backorders fulfilled
    }

    public async Task<IEnumerable<BackorderAlert>> GetBackorderAlertsAsync()
    {
      var alerts = await _context.SaleItems
          .Include(si => si.Sale)
          .Include(si => si.Item)
          .Include(si => si.FinishedGood)
          .Where(si => si.QuantityBackordered > 0)
          .GroupBy(si => new {
            ProductId = si.ItemId ?? si.FinishedGoodId,
            ProductType = si.ItemId.HasValue ? "Item" : "FinishedGood",
            ProductName = si.ItemId.HasValue ? si.Item!.PartNumber : si.FinishedGood!.PartNumber
          })
          .Select(g => new BackorderAlert
          {
            ProductId = g.Key.ProductId ?? 0,
            ProductType = g.Key.ProductType,
            ProductName = g.Key.ProductName,
            TotalBackorderQuantity = g.Sum(si => si.QuantityBackordered),
            CustomerCount = g.Select(si => si.Sale.CustomerName).Distinct().Count(),
            OldestBackorderDate = g.Min(si => si.Sale.SaleDate),
            TotalBackorderValue = g.Sum(si => si.QuantityBackordered * si.UnitPrice)
          })
          .ToListAsync();

      return alerts;
    }
  }

  
}