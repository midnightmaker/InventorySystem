using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly InventoryContext _context;
        
        public InventoryService(InventoryContext context)
        {
            _context = context;
        }
        
        public async Task<IEnumerable<Item>> GetAllItemsAsync()
        {
            return await _context.Items
                .Include(i => i.Purchases)
                .OrderBy(i => i.PartNumber)
                .ToListAsync();
        }
        
        public async Task<Item?> GetItemByIdAsync(int id)
        {
            return await _context.Items
                .Include(i => i.Purchases.OrderBy(p => p.PurchaseDate))
                .FirstOrDefaultAsync(i => i.Id == id);
        }
        
        public async Task<Item?> GetItemByPartNumberAsync(string partNumber)
        {
            return await _context.Items
                .Include(i => i.Purchases)
                .FirstOrDefaultAsync(i => i.PartNumber == partNumber);
        }
        
        public async Task<Item> CreateItemAsync(Item item)
        {
            _context.Items.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }
        
        public async Task<Item> UpdateItemAsync(Item item)
        {
            _context.Items.Update(item);
            await _context.SaveChangesAsync();
            return item;
        }
        
        public async Task DeleteItemAsync(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item != null)
            {
                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
            }
        }
        
        public async Task<decimal> GetAverageCostAsync(int itemId)
        {
            var purchases = await _context.Purchases
                .Where(p => p.ItemId == itemId)
                .ToListAsync();
                
            if (!purchases.Any()) return 0;
            
            var totalQuantity = purchases.Sum(p => p.QuantityPurchased);
            var totalCost = purchases.Sum(p => p.TotalCost);
            
            return totalQuantity > 0 ? totalCost / totalQuantity : 0;
        }
        
        public async Task<decimal> GetFifoValueAsync(int itemId)
        {
            var item = await GetItemByIdAsync(itemId);
            if (item == null || item.CurrentStock == 0) return 0;
            
            var purchases = item.Purchases
                .Where(p => p.RemainingQuantity > 0)
                .OrderBy(p => p.PurchaseDate)
                .ToList();
                
            decimal fifoValue = 0;
            int remainingStock = item.CurrentStock;
            
            foreach (var purchase in purchases)
            {
                if (remainingStock <= 0) break;
                
                int quantityToUse = Math.Min(remainingStock, purchase.RemainingQuantity);
                fifoValue += quantityToUse * purchase.CostPerUnit;
                remainingStock -= quantityToUse;
            }
            
            return fifoValue;
        }
        
        public async Task<IEnumerable<Item>> GetLowStockItemsAsync()
        {
            return await _context.Items
                .Where(i => i.CurrentStock <= i.MinimumStock)
                .OrderBy(i => i.PartNumber)
                .ToListAsync();
        }
    }
}