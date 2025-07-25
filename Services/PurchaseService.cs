using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
  public class PurchaseService : IPurchaseService
  {
    private readonly InventoryContext _context;

    public PurchaseService(InventoryContext context)
    {
      _context = context;
    }

    public async Task<IEnumerable<Purchase>> GetPurchasesByItemIdAsync(int itemId)
    {
      return await _context.Purchases
          .Include(p => p.PurchaseDocuments) // ADD THIS LINE
          .Where(p => p.ItemId == itemId)
          .OrderByDescending(p => p.PurchaseDate)
          .ToListAsync();
    }

    public async Task<Purchase?> GetPurchaseByIdAsync(int id)
    {
      return await _context.Purchases
          .Include(p => p.Item)
          .Include(p => p.PurchaseDocuments) // ADD THIS LINE
          .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Purchase> CreatePurchaseAsync(Purchase purchase)
    {
      // Initialize remaining quantity for FIFO tracking
      purchase.RemainingQuantity = purchase.QuantityPurchased;
      _context.Purchases.Add(purchase);

      // Update item stock levels
      var item = await _context.Items.FindAsync(purchase.ItemId);
      if (item != null)
      {
        item.CurrentStock += purchase.QuantityPurchased;
      }

      await _context.SaveChangesAsync();
      return purchase;
    }

    public async Task<Purchase> UpdatePurchaseAsync(Purchase purchase)
    {
      // Get the original purchase to check for quantity changes
      var originalPurchase = await _context.Purchases
          .AsNoTracking()
          .FirstOrDefaultAsync(p => p.Id == purchase.Id);

      if (originalPurchase != null)
      {
        // Calculate the difference in quantity
        var quantityDifference = purchase.QuantityPurchased - originalPurchase.QuantityPurchased;

        if (quantityDifference != 0)
        {
          // Update the item's current stock
          var item = await _context.Items.FindAsync(purchase.ItemId);
          if (item != null)
          {
            item.CurrentStock += quantityDifference;
          }

          // Update the remaining quantity proportionally
          if (originalPurchase.QuantityPurchased > 0)
          {
            var remainingRatio = (double)originalPurchase.RemainingQuantity / originalPurchase.QuantityPurchased;
            purchase.RemainingQuantity = (int)(purchase.QuantityPurchased * remainingRatio);
          }
          else
          {
            purchase.RemainingQuantity = purchase.QuantityPurchased;
          }
        }
      }

      _context.Purchases.Update(purchase);
      await _context.SaveChangesAsync();
      return purchase;
    }

    public async Task DeletePurchaseAsync(int id)
    {
      var purchase = await _context.Purchases
          .Include(p => p.PurchaseDocuments) // Include documents for cascade delete
          .FirstOrDefaultAsync(p => p.Id == id);

      if (purchase != null)
      {
        // Adjust item stock when deleting purchase
        var item = await _context.Items.FindAsync(purchase.ItemId);
        if (item != null)
        {
          item.CurrentStock -= purchase.RemainingQuantity;

          // Ensure stock doesn't go negative
          if (item.CurrentStock < 0)
          {
            throw new InvalidOperationException(
                $"Cannot delete purchase: would result in negative stock for item {item.PartNumber}. " +
                $"Current stock: {item.CurrentStock + purchase.RemainingQuantity}, " +
                $"Purchase remaining: {purchase.RemainingQuantity}");
          }
        }

        // Documents will be cascade deleted due to foreign key relationship
        _context.Purchases.Remove(purchase);
        await _context.SaveChangesAsync();
      }
    }

    public async Task ProcessInventoryConsumptionAsync(int itemId, int quantityUsed)
    {
      // FIFO consumption: consume from oldest purchases first
      var purchases = await _context.Purchases
          .Where(p => p.ItemId == itemId && p.RemainingQuantity > 0)
          .OrderBy(p => p.PurchaseDate) // FIFO order
          .ToListAsync();

      int remainingToConsume = quantityUsed;

      foreach (var purchase in purchases)
      {
        if (remainingToConsume <= 0) break;

        int consumeFromThisPurchase = Math.Min(remainingToConsume, purchase.RemainingQuantity);
        purchase.RemainingQuantity -= consumeFromThisPurchase;
        remainingToConsume -= consumeFromThisPurchase;
      }

      // Update item current stock
      var item = await _context.Items.FindAsync(itemId);
      if (item != null)
      {
        item.CurrentStock -= quantityUsed;
      }

      await _context.SaveChangesAsync();
    }

    // Additional methods for purchase document management
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
          .Include(p => p.PurchaseDocuments)
          .Where(p => p.Vendor.Contains(vendor))
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
      return await _context.Purchases.SumAsync(p => p.TotalPaid);
    }

    public async Task<decimal> GetTotalPurchaseValueByItemAsync(int itemId)
    {
      return await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .SumAsync(p => p.TotalPaid);
    }
  }
}