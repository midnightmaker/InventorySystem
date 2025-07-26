// Services/ProductionService.cs
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
  public class ProductionService : IProductionService
  {
    private readonly InventoryContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IBomService _bomService;
    private readonly IPurchaseService _purchaseService;

    public ProductionService(
        InventoryContext context,
        IInventoryService inventoryService,
        IBomService bomService,
        IPurchaseService purchaseService)
    {
      _context = context;
      _inventoryService = inventoryService;
      _bomService = bomService;
      _purchaseService = purchaseService;
    }

    public async Task<IEnumerable<Production>> GetAllProductionsAsync()
    {
      return await _context.Productions
          .Include(p => p.FinishedGood)
          .Include(p => p.Bom)
          .Include(p => p.MaterialConsumptions)
              .ThenInclude(mc => mc.Item)
          .OrderByDescending(p => p.ProductionDate)
          .ToListAsync();
    }

    public async Task<Production?> GetProductionByIdAsync(int id)
    {
      return await _context.Productions
          .Include(p => p.FinishedGood)
          .Include(p => p.Bom)
          .Include(p => p.MaterialConsumptions)
              .ThenInclude(mc => mc.Item)
          .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Production> CreateProductionAsync(Production production)
    {
      _context.Productions.Add(production);
      await _context.SaveChangesAsync();
      return production;
    }

    public async Task<Production> UpdateProductionAsync(Production production)
    {
      _context.Productions.Update(production);
      await _context.SaveChangesAsync();
      return production;
    }

    public async Task DeleteProductionAsync(int id)
    {
      var production = await _context.Productions
          .Include(p => p.MaterialConsumptions)
          .FirstOrDefaultAsync(p => p.Id == id);

      if (production != null)
      {
        _context.Productions.Remove(production);
        await _context.SaveChangesAsync();
      }
    }

    public async Task<Production> BuildBomAsync(int bomId, int quantity, decimal laborCost = 0, decimal overheadCost = 0, string? notes = null)
    {
      var bom = await _bomService.GetBomByIdAsync(bomId);
      if (bom == null)
        throw new ArgumentException("BOM not found");

      if (!await CanBuildBomAsync(bomId, quantity))
        throw new InvalidOperationException("Insufficient materials to build BOM");

      // Calculate material cost
      var materialCost = await CalculateBomMaterialCostAsync(bomId, quantity);

      // Find or create finished good for this BOM
      var finishedGood = await _context.FinishedGoods
          .FirstOrDefaultAsync(fg => fg.BomId == bomId);

      if (finishedGood == null)
      {
        finishedGood = new FinishedGood
        {
          PartNumber = bom.AssemblyPartNumber ?? $"FG-{bom.Name}",
          Description = $"Finished: {bom.Description}",
          BomId = bomId,
          UnitCost = (materialCost + laborCost + overheadCost) / quantity,
          SellingPrice = 0, // To be set later
          CurrentStock = 0,
          MinimumStock = 1
        };
        _context.FinishedGoods.Add(finishedGood);
        await _context.SaveChangesAsync();
      }

      // Create production record
      var production = new Production
      {
        FinishedGoodId = finishedGood.Id,
        BomId = bomId,
        QuantityProduced = quantity,
        ProductionDate = DateTime.Now,
        MaterialCost = materialCost,
        LaborCost = laborCost,
        OverheadCost = overheadCost,
        Notes = notes
      };

      _context.Productions.Add(production);
      await _context.SaveChangesAsync();

      // Consume materials and record consumption
      await ConsumeMaterialsAsync(production.Id, bom, quantity);

      // Update finished good inventory and cost
      finishedGood.CurrentStock += quantity;
      finishedGood.UnitCost = await CalculateWeightedAverageCostAsync(finishedGood.Id, quantity, production.UnitCost);
      await _context.SaveChangesAsync();

      return production;
    }

    public async Task<bool> CanBuildBomAsync(int bomId, int quantity)
    {
      var bom = await _bomService.GetBomByIdAsync(bomId);
      if (bom == null) return false;

      foreach (var bomItem in bom.BomItems)
      {
        var requiredQuantity = bomItem.Quantity * quantity;
        var item = await _inventoryService.GetItemByIdAsync(bomItem.ItemId);

        if (item == null || item.CurrentStock < requiredQuantity)
          return false;
      }

      // Check sub-assemblies recursively
      foreach (var subAssembly in bom.SubAssemblies)
      {
        if (!await CanBuildBomAsync(subAssembly.Id, quantity))
          return false;
      }

      return true;
    }

    public async Task<decimal> CalculateBomMaterialCostAsync(int bomId, int quantity)
    {
      var bom = await _bomService.GetBomByIdAsync(bomId);
      if (bom == null) return 0;

      decimal totalCost = 0;

      // Calculate direct materials cost using FIFO
      foreach (var bomItem in bom.BomItems)
      {
        var requiredQuantity = bomItem.Quantity * quantity;
        var fifoValue = await _inventoryService.GetFifoValueAsync(bomItem.ItemId);
        var item = await _inventoryService.GetItemByIdAsync(bomItem.ItemId);

        if (item != null && item.CurrentStock > 0)
        {
          var averageCost = item.CurrentStock > 0 ? fifoValue / item.CurrentStock : 0;
          totalCost += requiredQuantity * averageCost;
        }
      }

      // Add sub-assembly costs recursively
      foreach (var subAssembly in bom.SubAssemblies)
      {
        totalCost += await CalculateBomMaterialCostAsync(subAssembly.Id, quantity);
      }

      return totalCost;
    }

    private async Task ConsumeMaterialsAsync(int productionId, Bom bom, int quantity)
    {
      // Consume direct materials
      foreach (var bomItem in bom.BomItems)
      {
        var requiredQuantity = bomItem.Quantity * quantity;
        var item = await _inventoryService.GetItemByIdAsync(bomItem.ItemId);

        if (item != null)
        {
          // Get average cost for this consumption
          var fifoValue = await _inventoryService.GetFifoValueAsync(bomItem.ItemId);
          var unitCost = item.CurrentStock > 0 ? fifoValue / item.CurrentStock : 0;

          // Record consumption
          var consumption = new ProductionConsumption
          {
            ProductionId = productionId,
            ItemId = bomItem.ItemId,
            QuantityConsumed = requiredQuantity,
            UnitCostAtConsumption = unitCost,
            ConsumedDate = DateTime.Now
          };

          _context.ProductionConsumptions.Add(consumption);

          // Update item stock using FIFO consumption
          await _purchaseService.ProcessInventoryConsumptionAsync(bomItem.ItemId, requiredQuantity);

          // Update item current stock
          item.CurrentStock -= requiredQuantity;
        }
      }

      await _context.SaveChangesAsync();
    }

    private async Task<decimal> CalculateWeightedAverageCostAsync(int finishedGoodId, int newQuantity, decimal newUnitCost)
    {
      var finishedGood = await _context.FinishedGoods.FindAsync(finishedGoodId);
      if (finishedGood == null) return newUnitCost;

      var currentValue = finishedGood.CurrentStock * finishedGood.UnitCost;
      var newValue = newQuantity * newUnitCost;
      var totalQuantity = finishedGood.CurrentStock + newQuantity;

      return totalQuantity > 0 ? (currentValue + newValue) / totalQuantity : 0;
    }

    // Finished Goods methods
    public async Task<IEnumerable<FinishedGood>> GetAllFinishedGoodsAsync()
    {
      return await _context.FinishedGoods
          .Include(fg => fg.Bom)
          .Include(fg => fg.Productions)
          .Include(fg => fg.SaleItems)
          .OrderBy(fg => fg.PartNumber)
          .ToListAsync();
    }

    public async Task<FinishedGood?> GetFinishedGoodByIdAsync(int id)
    {
      return await _context.FinishedGoods
          .Include(fg => fg.Bom)
          .Include(fg => fg.Productions)
          .Include(fg => fg.SaleItems)
          .FirstOrDefaultAsync(fg => fg.Id == id);
    }

    public async Task<FinishedGood> CreateFinishedGoodAsync(FinishedGood finishedGood)
    {
      _context.FinishedGoods.Add(finishedGood);
      await _context.SaveChangesAsync();
      return finishedGood;
    }

    public async Task<FinishedGood> UpdateFinishedGoodAsync(FinishedGood finishedGood)
    {
      _context.FinishedGoods.Update(finishedGood);
      await _context.SaveChangesAsync();
      return finishedGood;
    }

    public async Task DeleteFinishedGoodAsync(int id)
    {
      var finishedGood = await _context.FinishedGoods.FindAsync(id);
      if (finishedGood != null)
      {
        _context.FinishedGoods.Remove(finishedGood);
        await _context.SaveChangesAsync();
      }
    }

    public async Task<IEnumerable<FinishedGood>> GetLowStockFinishedGoodsAsync()
    {
      return await _context.FinishedGoods
          .Where(fg => fg.CurrentStock <= fg.MinimumStock)
          .OrderBy(fg => fg.PartNumber)
          .ToListAsync();
    }

    // Statistics
    public async Task<decimal> GetTotalFinishedGoodsValueAsync()
    {
      var finishedGoods = await _context.FinishedGoods.ToListAsync();
      return finishedGoods.Sum(fg => fg.TotalValue);
    }

    public async Task<int> GetTotalFinishedGoodsCountAsync()
    {
      return await _context.FinishedGoods.CountAsync();
    }

    public async Task<decimal> GetTotalProductionCostAsync()
    {
      var productions = await _context.Productions.ToListAsync();
      return productions.Sum(p => p.TotalCost);
    }
  }
}