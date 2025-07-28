// Services/PurchaseService.cs - Complete Implementation
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
  public class PurchaseService : IPurchaseService
  {
    private readonly InventoryContext _context;
    private readonly IInventoryService _inventoryService;

    public PurchaseService(InventoryContext context, IInventoryService inventoryService)
    {
      _context = context;
      _inventoryService = inventoryService;
    }

    #region Core Purchase Methods

    public async Task<IEnumerable<Purchase>> GetPurchasesByItemIdAsync(int itemId)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.ItemId == itemId)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<Purchase?> GetPurchaseByIdAsync(int id)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.PurchaseDocuments)
          .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Purchase> CreatePurchaseAsync(Purchase purchase)
    {
      try
      {
        // Set ItemVersion to current item version when creating purchase
        var itemForVersion = await _context.Items.FindAsync(purchase.ItemId);
        if (itemForVersion != null)
        {
          purchase.ItemVersion = itemForVersion.Version;
          purchase.ItemVersionId = itemForVersion.Id;
        }

        // TotalCost is calculated automatically by the model

        purchase.RemainingQuantity = purchase.QuantityPurchased;
        purchase.CreatedDate = DateTime.Now;

        _context.Purchases.Add(purchase);
        await _context.SaveChangesAsync();

        // Update item inventory directly
        var itemForStock = await _context.Items.FindAsync(purchase.ItemId);
        if (itemForStock != null)
        {
          itemForStock.CurrentStock += purchase.QuantityPurchased;
          await _context.SaveChangesAsync();
        }

        return purchase;
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException($"Error creating purchase: {ex.Message}", ex);
      }
    }

    public async Task<Purchase> UpdatePurchaseAsync(Purchase purchase)
    {
      try
      {
        var existingPurchase = await _context.Purchases.FindAsync(purchase.Id);
        if (existingPurchase == null)
        {
          throw new InvalidOperationException("Purchase not found");
        }

        // Calculate inventory adjustment
        var quantityDifference = purchase.QuantityPurchased - existingPurchase.QuantityPurchased;
        var remainingDifference = purchase.QuantityPurchased - existingPurchase.QuantityPurchased;

        // Update purchase properties
        existingPurchase.Vendor = purchase.Vendor;
        existingPurchase.PurchaseDate = purchase.PurchaseDate;
        existingPurchase.QuantityPurchased = purchase.QuantityPurchased;
        existingPurchase.CostPerUnit = purchase.CostPerUnit;
        existingPurchase.ShippingCost = purchase.ShippingCost;
        existingPurchase.TaxAmount = purchase.TaxAmount;
        existingPurchase.PurchaseOrderNumber = purchase.PurchaseOrderNumber;
        existingPurchase.Notes = purchase.Notes;
        existingPurchase.RemainingQuantity += remainingDifference;
        // TotalCost is calculated automatically by the model

        await _context.SaveChangesAsync();

        // Adjust inventory if quantity changed
        if (quantityDifference != 0)
        {
          var item = await _context.Items.FindAsync(purchase.ItemId);
          if (item != null)
          {
            item.CurrentStock += quantityDifference;
            await _context.SaveChangesAsync();
          }
        }

        return existingPurchase;
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException($"Error updating purchase: {ex.Message}", ex);
      }
    }

    public async Task DeletePurchaseAsync(int id)
    {
      try
      {
        var purchase = await _context.Purchases.FindAsync(id);
        if (purchase == null)
        {
          throw new InvalidOperationException("Purchase not found");
        }

        // Remove remaining quantity from inventory
        if (purchase.RemainingQuantity > 0)
        {
          var item = await _context.Items.FindAsync(purchase.ItemId);
          if (item != null)
          {
            item.CurrentStock -= purchase.RemainingQuantity;
            await _context.SaveChangesAsync();
          }
        }

        _context.Purchases.Remove(purchase);
        await _context.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException($"Error deleting purchase: {ex.Message}", ex);
      }
    }

    public async Task ProcessInventoryConsumptionAsync(int itemId, int quantityUsed)
    {
      try
      {
        // FIFO consumption - use oldest purchases first
        var availablePurchases = await _context.Purchases
            .Where(p => p.ItemId == itemId && p.RemainingQuantity > 0)
            .OrderBy(p => p.PurchaseDate)
            .ToListAsync();

        var remainingToConsume = quantityUsed;

        foreach (var purchase in availablePurchases)
        {
          if (remainingToConsume <= 0) break;

          var consumeFromThis = Math.Min(purchase.RemainingQuantity, remainingToConsume);
          purchase.RemainingQuantity -= consumeFromThis;
          remainingToConsume -= consumeFromThis;
        }

        if (remainingToConsume > 0)
        {
          throw new InvalidOperationException($"Insufficient inventory. Missing {remainingToConsume} units.");
        }

        await _context.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException($"Error processing inventory consumption: {ex.Message}", ex);
      }
    }

    #endregion

    #region Enhanced Dashboard Methods

    public async Task<IEnumerable<Purchase>> GetAllPurchasesAsync()
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.PurchaseDocuments)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesByVendorAsync(string vendor)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Where(p => p.Vendor.ToLower().Contains(vendor.ToLower()))
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesWithDocumentsAsync()
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.PurchaseDocuments.Any())
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<decimal> GetTotalPurchaseValueAsync()
    {
      // Use TotalPaid instead of TotalCost for complete purchase value including shipping/tax
      var total = await _context.Purchases.SumAsync(p => p.TotalPaid);
      return total;
    }

    public async Task<decimal> GetTotalPurchaseValueByItemAsync(int itemId)
    {
      return await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .SumAsync(p => p.TotalPaid);
    }

    public async Task<decimal> GetPurchaseValueByMonthAsync(int year, int month)
    {
      return await _context.Purchases
          .Where(p => p.PurchaseDate.Year == year && p.PurchaseDate.Month == month)
          .SumAsync(p => p.TotalPaid);
    }

    public async Task<int> GetPurchaseCountByMonthAsync(int year, int month)
    {
      return await _context.Purchases
          .CountAsync(p => p.PurchaseDate.Year == year && p.PurchaseDate.Month == month);
    }

    #endregion

    #region Version Control Methods

    public async Task<IEnumerable<Purchase>> GetPurchasesByItemVersionAsync(int itemId, string? version = null)
    {
      var query = _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.ItemId == itemId);

      if (!string.IsNullOrEmpty(version))
      {
        query = query.Where(p => p.ItemVersion == version);
      }

      return await query
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<Dictionary<string, IEnumerable<Purchase>>> GetPurchasesGroupedByVersionAsync(int itemId)
    {
      // Get the base item ID to include all versions
      var item = await _context.Items.FindAsync(itemId);
      var baseItemId = item?.BaseItemId ?? itemId;

      // Get all versions of this item
      var allVersions = await _context.Items
          .Where(i => i.Id == baseItemId || i.BaseItemId == baseItemId)
          .Select(i => new { i.Id, i.Version })
          .ToListAsync();

      var allItemIds = allVersions.Select(v => v.Id).ToList();

      // Get all purchases for all versions
      var allPurchases = await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.PurchaseDocuments)
          .Where(p => allItemIds.Contains(p.ItemId))
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();

      // Group by version
      var grouped = new Dictionary<string, IEnumerable<Purchase>>();

      foreach (var version in allVersions.OrderBy(v => v.Version))
      {
        var versionPurchases = allPurchases
            .Where(p => p.ItemVersion == version.Version ||
                       (string.IsNullOrEmpty(p.ItemVersion) && p.ItemId == version.Id))
            .ToList();

        if (versionPurchases.Any())
        {
          grouped[version.Version] = versionPurchases;
        }
      }

      // Handle purchases without version info (legacy data)
      var unversionedPurchases = allPurchases
          .Where(p => string.IsNullOrEmpty(p.ItemVersion))
          .ToList();

      if (unversionedPurchases.Any())
      {
        grouped["Unversioned"] = unversionedPurchases;
      }

      return grouped;
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesByBaseItemIdAsync(int baseItemId)
    {
      // Get all item versions for this base item
      var allVersions = await _context.Items
          .Where(i => i.Id == baseItemId || i.BaseItemId == baseItemId)
          .Select(i => i.Id)
          .ToListAsync();

      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.PurchaseDocuments)
          .Where(p => allVersions.Contains(p.ItemId))
          .OrderByDescending(p => p.PurchaseDate)
          .ThenBy(p => p.ItemVersion)
          .ToListAsync();
    }

    public async Task SetPurchaseItemVersionAsync(int purchaseId, string itemVersion)
    {
      var purchase = await _context.Purchases.FindAsync(purchaseId);
      if (purchase != null)
      {
        purchase.ItemVersion = itemVersion;
        await _context.SaveChangesAsync();
      }
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesForItemVersionsAsync(IEnumerable<int> itemIds)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.PurchaseDocuments)
          .Where(p => itemIds.Contains(p.ItemId))
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    #endregion

    #region Helper Methods

    public async Task<decimal> GetAverageCostAsync(int itemId)
    {
      var purchases = await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .ToListAsync();

      if (!purchases.Any()) return 0;

      return purchases.Average(p => p.CostPerUnit);
    }

    public async Task<decimal> GetFifoValueAsync(int itemId)
    {
      var item = await _context.Items.FindAsync(itemId);
      if (item == null) return 0;

      var availablePurchases = await _context.Purchases
          .Where(p => p.ItemId == itemId && p.RemainingQuantity > 0)
          .OrderBy(p => p.PurchaseDate)
          .ToListAsync();

      decimal fifoValue = 0;
      var remainingStock = item.CurrentStock;

      foreach (var purchase in availablePurchases)
      {
        if (remainingStock <= 0) break;

        var quantityToValue = Math.Min(purchase.RemainingQuantity, remainingStock);
        fifoValue += quantityToValue * purchase.CostPerUnit;
        remainingStock -= quantityToValue;
      }

      return fifoValue;
    }

    public async Task<IEnumerable<Purchase>> GetLowStockPurchaseHistoryAsync()
    {
      // Get items that are low on stock and their recent purchase history
      var lowStockItems = await _context.Items
          .Where(i => i.CurrentStock <= i.MinimumStock)
          .Select(i => i.Id)
          .ToListAsync();

      return await _context.Purchases
          .Include(p => p.Item)
          .Where(p => lowStockItems.Contains(p.ItemId))
          .OrderByDescending(p => p.PurchaseDate)
          .Take(50) // Limit for performance
          .ToListAsync();
    }

    public async Task<Dictionary<string, decimal>> GetVendorSpendingAsync(DateTime fromDate, DateTime toDate)
    {
      return await _context.Purchases
          .Where(p => p.PurchaseDate >= fromDate && p.PurchaseDate <= toDate)
          .GroupBy(p => p.Vendor)
          .Select(g => new { Vendor = g.Key, Total = g.Sum(p => p.TotalPaid) })
          .ToDictionaryAsync(x => x.Vendor, x => x.Total);
    }

    public async Task<IEnumerable<Purchase>> GetRecentPurchasesAsync(int count = 10)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .OrderByDescending(p => p.PurchaseDate)
          .Take(count)
          .ToListAsync();
    }

    #endregion
  }
}