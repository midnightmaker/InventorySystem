// Services/PurchaseService.cs - Clean implementation with vendor functionality
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
  public partial class PurchaseService : IPurchaseService
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
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.ItemId == itemId)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<Purchase?> GetPurchaseByIdAsync(int id)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
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

        // Update VendorItem relationship with last purchase info
        await UpdateVendorItemLastPurchaseInfoAsync(
            purchase.VendorId,
            purchase.ItemId,
            purchase.CostPerUnit,
            purchase.PurchaseDate);

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
        existingPurchase.VendorId = purchase.VendorId;
        existingPurchase.PurchaseDate = purchase.PurchaseDate;
        existingPurchase.QuantityPurchased = purchase.QuantityPurchased;
        existingPurchase.CostPerUnit = purchase.CostPerUnit;
        existingPurchase.ShippingCost = purchase.ShippingCost;
        existingPurchase.TaxAmount = purchase.TaxAmount;
        existingPurchase.PurchaseOrderNumber = purchase.PurchaseOrderNumber;
        existingPurchase.Notes = purchase.Notes;
        existingPurchase.RemainingQuantity += remainingDifference;
        existingPurchase.Status = purchase.Status;
        existingPurchase.ExpectedDeliveryDate = purchase.ExpectedDeliveryDate;
        existingPurchase.ActualDeliveryDate = purchase.ActualDeliveryDate;

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

        // Update VendorItem relationship with new purchase info
        await UpdateVendorItemLastPurchaseInfoAsync(
            purchase.VendorId,
            purchase.ItemId,
            purchase.CostPerUnit,
            purchase.PurchaseDate);

        return existingPurchase;
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException($"Error updating purchase: {ex.Message}", ex);
      }
    }

    public async Task DeletePurchaseAsync(int id)
    {
      var purchase = await _context.Purchases.FindAsync(id);
      if (purchase != null)
      {
        // Adjust inventory back
        var item = await _context.Items.FindAsync(purchase.ItemId);
        if (item != null)
        {
          item.CurrentStock -= purchase.QuantityPurchased;
          await _context.SaveChangesAsync();
        }

        _context.Purchases.Remove(purchase);
        await _context.SaveChangesAsync();
      }
    }

    #endregion

    #region Vendor-Related Methods

    // Get last vendor used for an item
    public async Task<int?> GetLastVendorIdForItemAsync(int itemId)
    {
      var lastPurchase = await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .OrderByDescending(p => p.PurchaseDate)
          .ThenByDescending(p => p.CreatedDate)
          .FirstOrDefaultAsync();

      return lastPurchase?.VendorId;
    }

    // Get vendors that have supplied a specific item
    public async Task<IEnumerable<Vendor>> GetVendorsForItemAsync(int itemId)
    {
      return await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .Include(p => p.Vendor)
          .Select(p => p.Vendor)
          .Distinct()
          .Where(v => v.IsActive)
          .OrderBy(v => v.CompanyName)
          .ToListAsync();
    }

    // Helper method to update VendorItem with last purchase info
    private async Task UpdateVendorItemLastPurchaseInfoAsync(int vendorId, int itemId, decimal cost, DateTime purchaseDate)
    {
      var vendorItem = await _context.VendorItems
          .FirstOrDefaultAsync(vi => vi.VendorId == vendorId && vi.ItemId == itemId);

      if (vendorItem != null)
      {
        vendorItem.LastPurchaseDate = purchaseDate;
        vendorItem.LastPurchaseCost = cost;
        vendorItem.LastUpdated = DateTime.Now;
        await _context.SaveChangesAsync();
      }
      else
      {
        // Create new VendorItem relationship if it doesn't exist
        var newVendorItem = new VendorItem
        {
          VendorId = vendorId,
          ItemId = itemId,
          UnitCost = cost,
          LastPurchaseDate = purchaseDate,
          LastPurchaseCost = cost,
          IsActive = true,
          IsPrimary = false,
          LastUpdated = DateTime.Now
        };

        _context.VendorItems.Add(newVendorItem);
        await _context.SaveChangesAsync();
      }
    }

    #endregion

    #region Other Required Methods

    public async Task<IEnumerable<Purchase>> GetAllPurchasesAsync()
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesByVendorAsync(string vendor)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Where(p => p.Vendor.CompanyName.Contains(vendor))
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesWithDocumentsAsync()
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.PurchaseDocuments.Any())
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<decimal> GetTotalPurchaseValueAsync()
    {
      return await _context.Purchases.SumAsync(p => p.TotalPaid);
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

    public async Task ProcessInventoryConsumptionAsync(int itemId, int quantityUsed)
    {
      // Implementation for inventory consumption logic
      var purchases = await _context.Purchases
          .Where(p => p.ItemId == itemId && p.RemainingQuantity > 0)
          .OrderBy(p => p.PurchaseDate) // FIFO
          .ToListAsync();

      var remainingToConsume = quantityUsed;

      foreach (var purchase in purchases)
      {
        if (remainingToConsume <= 0) break;

        var consumeFromThis = Math.Min(purchase.RemainingQuantity, remainingToConsume);
        purchase.RemainingQuantity -= consumeFromThis;
        remainingToConsume -= consumeFromThis;
      }

      await _context.SaveChangesAsync();
    }

    #endregion

    #region Version Control Methods

    public async Task<IEnumerable<Purchase>> GetPurchasesByItemVersionAsync(int itemId, string? version = null)
    {
      var query = _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Where(p => p.ItemId == itemId);

      if (!string.IsNullOrEmpty(version))
      {
        query = query.Where(p => p.ItemVersion == version);
      }

      return await query.OrderByDescending(p => p.PurchaseDate).ToListAsync();
    }

    public async Task<Dictionary<string, IEnumerable<Purchase>>> GetPurchasesGroupedByVersionAsync(int itemId)
    {
      var purchases = await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Where(p => p.ItemId == itemId)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();

      return purchases.GroupBy(p => p.ItemVersion ?? "N/A")
          .ToDictionary(g => g.Key, g => g.AsEnumerable());
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesByBaseItemIdAsync(int baseItemId)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.Vendor)
          .Where(p => p.Item.BaseItemId == baseItemId || p.ItemId == baseItemId)
          .OrderByDescending(p => p.PurchaseDate)
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
          .Include(p => p.Vendor)
          .Where(p => itemIds.Contains(p.ItemId))
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    #endregion

    #region Helper Methods for Cost Calculations

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

      if (!availablePurchases.Any()) return 0;

      decimal fifoValue = 0;
      int remainingStock = item.CurrentStock;

      foreach (var purchase in availablePurchases)
      {
        if (remainingStock <= 0) break;

        int quantityToValue = Math.Min(remainingStock, purchase.RemainingQuantity);
        fifoValue += quantityToValue * purchase.CostPerUnit;
        remainingStock -= quantityToValue;
      }

      return fifoValue;
    }

    #endregion
  }
}