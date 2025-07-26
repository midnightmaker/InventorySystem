// Add these methods to your existing PurchaseService.cs

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

    // ADD THESE NEW METHODS to your existing PurchaseService:

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
      var purchases = await _context.Purchases.ToListAsync();
      return purchases.Sum(p => p.TotalPaid);
    }

    public async Task<decimal> GetTotalPurchaseValueByItemAsync(int itemId)
    {
      var purchases = await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .ToListAsync();
      return purchases.Sum(p => p.TotalPaid);
    }

    // ADD these methods to get monthly purchase statistics
    public async Task<decimal> GetPurchaseValueByMonthAsync(int year, int month)
    {
      var purchases = await _context.Purchases
          .Where(p => p.PurchaseDate.Year == year && p.PurchaseDate.Month == month)
          .ToListAsync();
      return purchases.Sum(p => p.TotalPaid);
    }

    public async Task<int> GetPurchaseCountByMonthAsync(int year, int month)
    {
      return await _context.Purchases
          .CountAsync(p => p.PurchaseDate.Year == year && p.PurchaseDate.Month == month);
    }

    // Existing methods would remain unchanged...
    public async Task<IEnumerable<Purchase>> GetPurchasesByItemIdAsync(int itemId)
    {
      return await _context.Purchases
          .Where(p => p.ItemId == itemId)
          .OrderBy(p => p.PurchaseDate)
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
      purchase.RemainingQuantity = purchase.QuantityPurchased;
      _context.Purchases.Add(purchase);
      await _context.SaveChangesAsync();

      // Update item's current stock
      var item = await _context.Items.FindAsync(purchase.ItemId);
      if (item != null)
      {
        item.CurrentStock += purchase.QuantityPurchased;
        await _context.SaveChangesAsync();
      }

      return purchase;
    }

    public async Task<Purchase> UpdatePurchaseAsync(Purchase purchase)
    {
      _context.Purchases.Update(purchase);
      await _context.SaveChangesAsync();
      return purchase;
    }

    public async Task DeletePurchaseAsync(int id)
    {
      var purchase = await _context.Purchases
          .Include(p => p.PurchaseDocuments)
          .FirstOrDefaultAsync(p => p.Id == id);

      if (purchase != null)
      {
        _context.Purchases.Remove(purchase);
        await _context.SaveChangesAsync();
      }
    }

    public async Task ProcessInventoryConsumptionAsync(int itemId, int quantityUsed)
    {
      var purchases = await _context.Purchases
          .Where(p => p.ItemId == itemId && p.RemainingQuantity > 0)
          .OrderBy(p => p.PurchaseDate) // FIFO
          .ToListAsync();

      int remainingToConsume = quantityUsed;

      foreach (var purchase in purchases)
      {
        if (remainingToConsume <= 0) break;

        int consumeFromThis = Math.Min(purchase.RemainingQuantity, remainingToConsume);
        purchase.RemainingQuantity -= consumeFromThis;
        remainingToConsume -= consumeFromThis;
      }

      await _context.SaveChangesAsync();
    }
  }
}