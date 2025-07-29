// Add these methods to your existing BomService.cs

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

    // ADD THESE NEW METHODS for dashboard statistics:

    public async Task<decimal> GetTotalBomValueAsync()
    {
      var allBoms = await GetAllBomsAsync();
      decimal totalValue = 0;

      foreach (var bom in allBoms)
      {
        totalValue += await GetBomTotalCostAsync(bom.Id);
      }

      return totalValue;
    }

    public async Task<int> GetTotalBomItemsCountAsync()
    {
      return await _context.BomItems.CountAsync();
    }

    public async Task<int> GetCompleteBomCountAsync()
    {
      return await _context.Boms
          .Where(b => b.BomItems.Any())
          .CountAsync();
    }

    public async Task<int> GetIncompleteBomCountAsync()
    {
      return await _context.Boms
          .Where(b => !b.BomItems.Any())
          .CountAsync();
    }

    public async Task<IEnumerable<Bom>> GetBomsCreatedInMonthAsync(int year, int month)
    {
      return await _context.Boms
          .Where(b => b.CreatedDate.Year == year && b.CreatedDate.Month == month)
          .ToListAsync();
    }

    // Existing methods remain the same...
    public async Task<IEnumerable<Bom>> GetAllBomsAsync()
    {
      return await _context.Boms
          .Include(b => b.BomItems)
              .ThenInclude(bi => bi.Item)
          .Include(b => b.SubAssemblies)
          .Where(b => b.ParentBomId == null) // Only top-level BOMs
          .OrderBy(b => b.BomNumber)
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
          .Include(b => b.Documents) // Add this line to include Documents
          .Include(b => b.ParentBom)
          .Include(b => b.CreatedFromChangeOrder)
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
        var averageCost = await _inventoryService.GetAverageCostAsync(bomItem.ItemId);
        totalCost += bomItem.Quantity * averageCost;
      }

      // Recursively calculate sub-assembly costs
      foreach (var subAssembly in bom.SubAssemblies)
      {
        totalCost += await GetBomTotalCostAsync(subAssembly.Id);
      }

      return totalCost;
    }

    public async Task<BomItem> AddBomItemAsync(BomItem bomItem)
    {
      // Calculate unit cost using average purchase pricing
      bomItem.UnitCost = await _inventoryService.GetAverageCostAsync(bomItem.ItemId);

      _context.BomItems.Add(bomItem);
      await _context.SaveChangesAsync();

      // Update the parent BOM's modified date
      var bom = await _context.Boms.FindAsync(bomItem.BomId);
      if (bom != null)
      {
        bom.ModifiedDate = DateTime.Now;
        await _context.SaveChangesAsync();
      }

      return bomItem;
    }

    public async Task DeleteBomItemAsync(int bomItemId)
    {
      var bomItem = await _context.BomItems.FindAsync(bomItemId);
      if (bomItem != null)
      {
        _context.BomItems.Remove(bomItem);
        await _context.SaveChangesAsync();

        // Update the parent BOM's modified date
        var bom = await _context.Boms.FindAsync(bomItem.BomId);
        if (bom != null)
        {
          bom.ModifiedDate = DateTime.Now;
          await _context.SaveChangesAsync();
        }
      }
    }
  }
}