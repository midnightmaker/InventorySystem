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
                .Where(p => p.ItemId == itemId)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();
        }
        
        public async Task<Purchase?> GetPurchaseByIdAsync(int id)
        {
            return await _context.Purchases
                .Include(p => p.Item)
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
            _context.Purchases.Update(purchase);
            await _context.SaveChangesAsync();
            return purchase;
        }
        
        public async Task DeletePurchaseAsync(int id)
        {
            var purchase = await _context.Purchases.FindAsync(id);
            if (purchase != null)
            {
                // Adjust item stock when deleting purchase
                var item = await _context.Items.FindAsync(purchase.ItemId);
                if (item != null)
                {
                    item.CurrentStock -= purchase.RemainingQuantity;
                }
                
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
    }
}