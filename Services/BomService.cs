using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
    public class BomService : IBomService
    {
        private readonly InventoryContext _context;
        private readonly IInventoryService _inventoryService;
        
        public BomService(InventoryContext context, IInventoryService inventoryService)
        {
            _context = context;
            _inventoryService = inventoryService;
        }
        
        public async Task<IEnumerable<Bom>> GetAllBomsAsync()
        {
            return await _context.Boms
                .Include(b => b.BomItems)
                    .ThenInclude(bi => bi.Item)
                .Include(b => b.SubAssemblies)
                .Where(b => b.ParentBomId == null) // Only top-level BOMs
                .OrderBy(b => b.Name)
                .ToListAsync();
        }
        
        public async Task<Bom?> GetBomByIdAsync(int id)
        {
            return await _context.Boms
                .Include(b => b.BomItems)
                    .ThenInclude(bi => bi.Item)
                .Include(b => b.SubAssemblies)
                    .ThenInclude(sa => sa.BomItems)
                        .ThenInclude(bi => bi.Item)
                .FirstOrDefaultAsync(b => b.Id == id);
        }
        
        public async Task<Bom> CreateBomAsync(Bom bom)
        {
            _context.Boms.Add(bom);
            await _context.SaveChangesAsync();
            return bom;
        }
        
        public async Task<Bom> UpdateBomAsync(Bom bom)
        {
            bom.ModifiedDate = DateTime.Now;
            _context.Boms.Update(bom);
            await _context.SaveChangesAsync();
            return bom;
        }
        
        public async Task DeleteBomAsync(int id)
        {
            var bom = await _context.Boms
                .Include(b => b.BomItems)
                .Include(b => b.SubAssemblies)
                .FirstOrDefaultAsync(b => b.Id == id);
                
            if (bom != null)
            {
                _context.Boms.Remove(bom);
                await _context.SaveChangesAsync();
            }
        }
        
        public async Task<decimal> GetBomTotalCostAsync(int bomId)
        {
            var bom = await GetBomByIdAsync(bomId);
            if (bom == null) return 0;
            
            decimal totalCost = 0;
            
            // Calculate cost of direct items using average purchase pricing
            foreach (var bomItem in bom.BomItems)
            {
                var avgCost = await _inventoryService.GetAverageCostAsync(bomItem.ItemId);
                bomItem.UnitCost = avgCost;
                totalCost += bomItem.ExtendedCost;
            }
            
            // Calculate cost of sub-assemblies recursively
            foreach (var subAssembly in bom.SubAssemblies)
            {
                totalCost += await GetBomTotalCostAsync(subAssembly.Id);
            }
            
            return totalCost;
        }
        
        public async Task<BomItem> AddBomItemAsync(BomItem bomItem)
        {
            // Set unit cost based on average cost from purchases
            bomItem.UnitCost = await _inventoryService.GetAverageCostAsync(bomItem.ItemId);
            
            _context.BomItems.Add(bomItem);
            await _context.SaveChangesAsync();
            return bomItem;
        }
        
        public async Task DeleteBomItemAsync(int bomItemId)
        {
            var bomItem = await _context.BomItems.FindAsync(bomItemId);
            if (bomItem != null)
            {
                _context.BomItems.Remove(bomItem);
                await _context.SaveChangesAsync();
            }
        }
    }
}