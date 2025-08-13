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

    
		public async Task<Sale?> GetSaleByIdAsync(int id)
		{
			return await _context.Sales
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.FinishedGood)
					.Include(s => s.Customer)
							.ThenInclude(c => c.BalanceAdjustments) // ✅ Include customer adjustments
					.Include(s => s.RelatedAdjustments) // ✅ Include sale-specific adjustments
					.FirstOrDefaultAsync(s => s.Id == id);
		}

		// Also update other methods that load sales to include adjustments:

		public async Task<IEnumerable<Sale>> GetAllSalesAsync()
		{
			return await _context.Sales
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.FinishedGood)
					.Include(s => s.Customer)
					.Include(s => s.RelatedAdjustments) // ✅ Add this
					.OrderByDescending(s => s.SaleDate)
					.ToListAsync();
		}

		public async Task<IEnumerable<Sale>> GetSalesByCustomerAsync(int customerId)
		{
			return await _context.Sales
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.Item)
					.Include(s => s.SaleItems)
							.ThenInclude(si => si.FinishedGood)
					.Include(s => s.Customer)
					.Include(s => s.RelatedAdjustments) // ✅ Add this
					.Where(s => s.CustomerId == customerId)
					.OrderByDescending(s => s.SaleDate)
					.ToListAsync();
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

    // ALTERNATIVE: More robust update method using fresh entity loading
    public async Task<Sale> UpdateSaleAsync(Sale sale)
    {
        try
        {
            // Load the current entity from database to ensure we have the latest state
            var existingEntity = await _context.Sales
                .FirstOrDefaultAsync(s => s.Id == sale.Id);
            
            if (existingEntity == null)
            {
                throw new InvalidOperationException($"Sale with ID {sale.Id} not found");
            }

            // Update only the properties that should be modifiable
            existingEntity.SaleDate = sale.SaleDate;
            existingEntity.Terms = sale.Terms;
            existingEntity.PaymentDueDate = sale.PaymentDueDate;
            existingEntity.PaymentStatus = sale.PaymentStatus;
            existingEntity.SaleStatus = sale.SaleStatus;
            existingEntity.PaymentMethod = sale.PaymentMethod;
            existingEntity.ShippingAddress = sale.ShippingAddress;
            existingEntity.TaxAmount = sale.TaxAmount;
            existingEntity.ShippingCost = sale.ShippingCost;
            existingEntity.Notes = sale.Notes;
            existingEntity.OrderNumber = sale.OrderNumber;

            // Do NOT update these protected fields:
            // - SaleNumber (reference integrity)
            // - CreatedDate (audit trail)
            // - CustomerId (business rule - use transfer function if needed)
            // - Id (primary key)

            await _context.SaveChangesAsync();
            return existingEntity;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to update sale {sale.Id}: {ex.Message}", ex);
        }
    }

    public async Task DeleteSaleAsync(int id)
    {
      var sale = await _context.Sales
          .Include(s => s.SaleItems)
          .FirstOrDefaultAsync(s => s.Id == id);

      if (sale != null)
      {
        // Cascading delete for sale items
        _context.SaleItems.RemoveRange(sale.SaleItems);

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
      var saleItem = await _context.SaleItems
          .Include(si => si.Sale)
          .FirstOrDefaultAsync(si => si.Id == saleItemId);
      
      if (saleItem == null)
      {
        throw new InvalidOperationException("Sale item not found.");
      }

      // SECURITY: Check if sale allows modifications
      if (!CanModifySaleItems(saleItem.Sale.SaleStatus))
      {
        throw new InvalidOperationException($"Cannot remove items from a sale with status '{saleItem.Sale.SaleStatus}'. Only sales with 'Processing' or 'Backordered' status can be modified.");
      }

      _context.SaleItems.Remove(saleItem);
      await _context.SaveChangesAsync();

      // No need to manually update sale totals - they're computed properties
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
            if (item != null && item.TrackInventory)
            {
              // Only reduce stock for inventory-tracked items
              item.CurrentStock -= saleItem.QuantitySold;

              // Process FIFO consumption only for inventory-tracked items
              await _purchaseService.ProcessInventoryConsumptionAsync(
                  saleItem.ItemId.Value,
                  saleItem.QuantitySold);
            }
            // Non-inventory items (Service, Virtual, etc.) don't affect stock
          }
          else if (saleItem.FinishedGoodId.HasValue)
          {
            // Access finished goods directly from context
            // Finished goods always track inventory
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

    
    public async Task<IEnumerable<Sale>> GetCustomerSalesAsync(int customerId)
    {
        return await GetSalesByCustomerAsync(customerId);
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
      // SECURITY: Check if sale allows modifications
      var sale = await _context.Sales.FindAsync(saleItem.SaleId);
      if (sale == null)
      {
        throw new InvalidOperationException("Sale not found.");
      }

      if (!CanModifySaleItems(sale.SaleStatus))
      {
        throw new InvalidOperationException($"Cannot add items to a sale with status '{sale.SaleStatus}'. Only sales with 'Processing' or 'Backordered' status can be modified.");
      }

      // Enhanced logic to handle backorders - but only for inventory-tracked items
      int availableQuantity = 0;
      bool tracksInventory = false;

      if (saleItem.ItemId.HasValue)
      {
        var item = await _inventoryService.GetItemByIdAsync(saleItem.ItemId.Value);
        if (item != null)
        {
          tracksInventory = item.TrackInventory;
          
          if (tracksInventory)
          {
            // Only check stock for inventory-tracked items
            availableQuantity = item.CurrentStock;
          }
          
          // Always try to get cost information for pricing
          try
          {
            saleItem.UnitCost = await _inventoryService.GetAverageCostAsync(saleItem.ItemId.Value);
          }
          catch
          {
            // For non-inventory items, set minimal cost
            saleItem.UnitCost = item.ItemType switch
            {
              ItemType.Service => saleItem.UnitPrice * 0.3m, // 30% cost ratio for services
              ItemType.Virtual => saleItem.UnitPrice * 0.1m, // 10% cost ratio for virtual items
              ItemType.Subscription => saleItem.UnitPrice * 0.2m, // 20% cost ratio for subscriptions
              _ => 0 // No cost for other non-inventory items
            };
          }
        }
      }
      else if (saleItem.FinishedGoodId.HasValue)
      {
        var finishedGood = await _context.FinishedGoods
            .FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
        if (finishedGood != null)
        {
          // Finished goods always track inventory
          tracksInventory = true;
          availableQuantity = finishedGood.CurrentStock;
          saleItem.UnitCost = finishedGood.UnitCost;
        }
      }

      // Calculate backorder quantity only for inventory-tracked items
      if (tracksInventory)
      {
        if (availableQuantity < saleItem.QuantitySold)
        {
          saleItem.QuantityBackordered = saleItem.QuantitySold - availableQuantity;
        }
        else
        {
          saleItem.QuantityBackordered = 0;
        }
      }
      else
      {
        // Non-inventory items never have backorders
        saleItem.QuantityBackordered = 0;
        _logger.LogInformation("Added non-inventory item to sale - no backorder logic applied for Item ID: {ItemId}", saleItem.ItemId);
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
          .Where(s => s.SaleItems.Any(si => si.QuantityBackordered > 0)) // FIXED: Look for any items with backorders
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
          if (item == null) return false;
          
          // Only check stock for inventory-tracked items
          if (item.TrackInventory && item.CurrentStock < saleItem.QuantitySold)
            return false;
          
          // Non-inventory items (Service, Virtual, etc.) can always be processed
        }
        else if (saleItem.FinishedGoodId.HasValue)
        {
          // Finished goods always track inventory
          var finishedGood = await _context.FinishedGoods
              .FirstOrDefaultAsync(fg => fg.Id == saleItem.FinishedGoodId.Value);
          if (finishedGood == null || finishedGood.CurrentStock < saleItem.QuantitySold)
            return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Determines if sale items can be added or removed based on the sale status
    /// </summary>
    /// <param name="saleStatus">The current status of the sale</param>
    /// <returns>True if items can be modified, false otherwise</returns>
    private static bool CanModifySaleItems(SaleStatus saleStatus)
    {
      return saleStatus switch
      {
        SaleStatus.Processing => true,    // Can modify - sale is still being prepared
        SaleStatus.Backordered => true,   // Can modify - still waiting for inventory
        SaleStatus.Shipped => false,     // Cannot modify - sale has been shipped
        SaleStatus.Delivered => false,   // Cannot modify - sale has been delivered  
        SaleStatus.Cancelled => false,   // Cannot modify - sale has been cancelled
        SaleStatus.Returned => false,    // Cannot modify - sale has been returned
        _ => false                        // Default: Cannot modify unknown status
      };
    }

    /// <summary>
    /// Public method to check if a sale can have items modified
    /// </summary>
    /// <param name="saleId">The ID of the sale to check</param>
    /// <returns>True if items can be modified, false otherwise</returns>
    public async Task<bool> CanModifySaleItemsAsync(int saleId)
    {
      var sale = await _context.Sales.FindAsync(saleId);
      return sale != null && CanModifySaleItems(sale.SaleStatus);
    }
  }
}