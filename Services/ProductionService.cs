// Services/ProductionService.cs
using InventorySystem.Data;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
  public class ProductionService : IProductionService
  {
    private readonly InventoryContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IBomService _bomService;
    private readonly IPurchaseService _purchaseService;
    private readonly ILogger<ProductionService> _logger;
    IBackorderFulfillmentService _backorderService;

    public ProductionService(
        InventoryContext context,
        IInventoryService inventoryService,
        IBomService bomService,
        IBackorderFulfillmentService backorderService,
        IPurchaseService purchaseService,
        ILogger<ProductionService> logger)
    {
      _logger = logger;
      _context = context;
      _backorderService = backorderService; 
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

    // Fixed BuildBomAsync method in ProductionService.cs
    public async Task<Production> BuildBomAsync(int bomId, int quantity, decimal laborCost = 0, decimal overheadCost = 0, string? notes = null)
    {
      // Validate inputs
      if (quantity <= 0)
        throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));

      if (laborCost < 0)
        throw new ArgumentException("Labor cost cannot be negative", nameof(laborCost));

      if (overheadCost < 0)
        throw new ArgumentException("Overhead cost cannot be negative", nameof(overheadCost));

      // Get BOM using current version
      var bom = await _bomService.GetCurrentVersionBomByIdAsync(bomId);
      if (bom == null)
        throw new ArgumentException("BOM not found or is not the current version");

      if (!await CanBuildBomAsync(bomId, quantity))
        throw new InvalidOperationException("Insufficient materials to build BOM");

      // Calculate material cost
      var materialCost = await CalculateBomMaterialCostAsync(bomId, quantity);

      // FIXED: Calculate unit costs safely
      var totalCost = materialCost + laborCost + overheadCost;
      var unitCost = quantity > 0 ? totalCost / quantity : 0;

      // Find or create finished good for this BOM
      var finishedGood = await _context.FinishedGoods
          .FirstOrDefaultAsync(fg => fg.BomId == bomId);

      if (finishedGood == null)
      {
        finishedGood = new FinishedGood
        {
          PartNumber = bom.AssemblyPartNumber ?? $"FG-{bom.BomNumber}",
          Description = $"Finished: {bom.Description}",
          BomId = bomId,
          UnitCost = unitCost, // FIXED: Use pre-calculated safe unit cost
          SellingPrice = 0, // To be set later
          CurrentStock = 0,
          MinimumStock = 1
        };
        _context.FinishedGoods.Add(finishedGood);
        await _context.SaveChangesAsync();
      }

      // Create production record with explicit values
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
        // NOTE: TotalCost and UnitCost are calculated properties in the model
      };

      _context.Productions.Add(production);
      await _context.SaveChangesAsync();

      // Consume materials and record consumption
      await ConsumeMaterialsAsync(production.Id, bom, quantity);

      // Update finished good inventory and cost using safe calculation
      finishedGood.CurrentStock += quantity;

      // FIXED: Use pre-calculated unit cost to avoid accessing production.UnitCost before it's safe
      finishedGood.UnitCost = await CalculateWeightedAverageCostAsync(finishedGood.Id, quantity, unitCost);
      await _context.SaveChangesAsync();
      // After creating the production and updating finished good inventory:
      if (finishedGood != null)
      {
        // Check for and fulfill any existing backorders
        var backordersFulfilled = await _backorderService.FulfillBackordersForProductAsync(
            null,
            finishedGood.Id,
            quantity);

        if (backordersFulfilled)
        {
          _logger.LogInformation(
              "BuildBom completed for BOM {BomId}. Backorders fulfilled for {Quantity} units of FinishedGood {FinishedGoodId}",
              bomId, quantity, finishedGood.Id);
        }
      }

      return production;
    }

    // FIXED: Safe weighted average calculation
    private async Task<decimal> CalculateWeightedAverageCostAsync(int finishedGoodId, int newQuantity, decimal newUnitCost)
    {
      try
      {
        var finishedGood = await _context.FinishedGoods.FindAsync(finishedGoodId);
        if (finishedGood == null) return newUnitCost;

        var currentValue = finishedGood.CurrentStock * finishedGood.UnitCost;
        var newValue = newQuantity * newUnitCost;
        var totalQuantity = finishedGood.CurrentStock + newQuantity;

        // FIXED: Safe division with validation
        if (totalQuantity <= 0)
        {
          return newUnitCost;
        }

        return (currentValue + newValue) / totalQuantity;
      }
      catch (Exception ex)
      {
        // Log error and return safe fallback
        // _logger.LogError(ex, "Error calculating weighted average cost for finished good {Id}", finishedGoodId);
        return newUnitCost;
      }
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


    public async Task<bool> ProcessProductionCompletionAsync(int productionId)
    {
      try
      {
        var production = await GetProductionByIdAsync(productionId);
        if (production == null)
        {
          _logger.LogWarning("Production {ProductionId} not found for completion processing", productionId);
          return false;
        }

        // Update finished good inventory (existing logic)
        var finishedGood = await GetFinishedGoodByIdAsync(production.FinishedGoodId);
        if (finishedGood != null)
        {
          finishedGood.CurrentStock += production.QuantityProduced;
          await _context.SaveChangesAsync();

          // Auto-fulfill backorders using the backorder fulfillment service
          var backordersFulfilled = await _backorderService.FulfillBackordersForProductAsync(
              null,
              finishedGood.Id,
              production.QuantityProduced);

          if (backordersFulfilled)
          {
            _logger.LogInformation(
                "Production {ProductionId} processed. Backorders automatically fulfilled for FinishedGood {FinishedGoodId}",
                productionId, finishedGood.Id);
          }
        }
        else
        {
          _logger.LogWarning(
              "FinishedGood {FinishedGoodId} not found for Production {ProductionId} processing",
              production.FinishedGoodId, productionId);
        }

        return true;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing production completion for Production {ProductionId}", productionId);
        return false;
      }
    }

    public async Task<bool> CompleteProductionAsync(int productionId)
    {
      using var transaction = await _context.Database.BeginTransactionAsync();
      try
      {
        var production = await GetProductionByIdAsync(productionId);
        if (production == null)
        {
          _logger.LogWarning("Production {ProductionId} not found", productionId);
          return false;
        }

        // Update finished good inventory
        var finishedGood = await GetFinishedGoodByIdAsync(production.FinishedGoodId);
        if (finishedGood != null)
        {
          var previousStock = finishedGood.CurrentStock;
          finishedGood.CurrentStock += production.QuantityProduced;

          _logger.LogInformation(
              "Updated finished good {FinishedGoodId} stock from {PreviousStock} to {NewStock} (added {Quantity})",
              finishedGood.Id, previousStock, finishedGood.CurrentStock, production.QuantityProduced);

          await _context.SaveChangesAsync();

          // Auto-fulfill backorders using the new service
          var backordersFulfilled = await _backorderService.FulfillBackordersForProductAsync(
              null,
              finishedGood.Id,
              production.QuantityProduced);

          if (backordersFulfilled)
          {
            _logger.LogInformation(
                "Production {ProductionId} completed. Backorders automatically fulfilled for FinishedGood {FinishedGoodId}",
                productionId, finishedGood.Id);
          }
          else
          {
            _logger.LogInformation(
                "Production {ProductionId} completed. No backorders to fulfill for FinishedGood {FinishedGoodId}",
                productionId, finishedGood.Id);
          }
        }
        else
        {
          _logger.LogWarning(
              "FinishedGood {FinishedGoodId} not found for Production {ProductionId}",
              production.FinishedGoodId, productionId);
        }

        await transaction.CommitAsync();
        return true;
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Error completing production {ProductionId}", productionId);
        return false;
      }
    }
    public async Task<List<BackorderInfo>> GetBackordersForFinishedGoodAsync(int finishedGoodId)
    {
      var backorders = await _context.SaleItems
          .Include(si => si.Sale)
          .Where(si => si.FinishedGoodId == finishedGoodId && si.QuantityBackordered > 0)
          .Select(si => new BackorderInfo
          {
            SaleId = si.SaleId,
            SaleNumber = si.Sale.SaleNumber,
            CustomerName = si.Sale.CustomerName,
            QuantityBackordered = si.QuantityBackordered,
            SaleDate = si.Sale.SaleDate,
            DaysWaiting = (DateTime.Now - si.Sale.SaleDate).Days
          })
          .OrderBy(b => b.SaleDate) // FIFO order
          .ToListAsync();

      return backorders;
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
      try
      {
        // Validate that part number is unique
        var existingFinishedGood = await _context.FinishedGoods
            .FirstOrDefaultAsync(fg => fg.PartNumber == finishedGood.PartNumber);

        if (existingFinishedGood != null)
        {
          throw new InvalidOperationException($"A finished good with part number '{finishedGood.PartNumber}' already exists.");
        }

        _context.FinishedGoods.Add(finishedGood);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created finished good {PartNumber} with ID {FinishedGoodId}",
            finishedGood.PartNumber, finishedGood.Id);

        return finishedGood;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating finished good {PartNumber}", finishedGood.PartNumber);
        throw;
      }
    }

    public async Task<FinishedGood> UpdateFinishedGoodAsync(FinishedGood finishedGood)
    {
      try
      {
        // Validate that part number is unique (excluding current record)
        var existingFinishedGood = await _context.FinishedGoods
            .FirstOrDefaultAsync(fg => fg.PartNumber == finishedGood.PartNumber && fg.Id != finishedGood.Id);

        if (existingFinishedGood != null)
        {
          throw new InvalidOperationException($"A finished good with part number '{finishedGood.PartNumber}' already exists.");
        }

        _context.FinishedGoods.Update(finishedGood);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated finished good {PartNumber} with ID {FinishedGoodId}",
            finishedGood.PartNumber, finishedGood.Id);

        return finishedGood;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating finished good {FinishedGoodId}", finishedGood.Id);
        throw;
      }
    }


    public async Task DeleteFinishedGoodAsync(int id)
    {
      try
      {
        var finishedGood = await _context.FinishedGoods
            .Include(fg => fg.Productions)
            .Include(fg => fg.SaleItems)
            .FirstOrDefaultAsync(fg => fg.Id == id);

        if (finishedGood == null)
        {
          throw new ArgumentException($"Finished good with ID {id} not found.");
        }

        // Check if finished good has been used in any productions
        if (finishedGood.Productions.Any())
        {
          throw new InvalidOperationException($"Cannot delete finished good '{finishedGood.PartNumber}' because it has been used in productions.");
        }

        // Check if finished good has been sold
        if (finishedGood.SaleItems.Any())
        {
          throw new InvalidOperationException($"Cannot delete finished good '{finishedGood.PartNumber}' because it has been sold.");
        }

        _context.FinishedGoods.Remove(finishedGood);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Deleted finished good {PartNumber} with ID {FinishedGoodId}",
            finishedGood.PartNumber, id);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting finished good {FinishedGoodId}", id);
        throw;
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

    public async Task<MaterialShortageViewModel> GetMaterialShortageAnalysisAsync(int bomId, int quantity)
    {
      var bom = await _bomService.GetBomByIdAsync(bomId);
      if (bom == null)
        throw new ArgumentException("BOM not found");

      var canBuild = await CanBuildBomAsync(bomId, quantity);
      var totalCost = await CalculateBomMaterialCostAsync(bomId, quantity);
      var requirements = await GetBomMaterialRequirementsAsync(bomId, quantity);
      var shortages = await GetBomMaterialShortagesAsync(bomId, quantity);

      return new MaterialShortageViewModel
      {
        BomId = bomId,
        BomName = bom.BomNumber,
        BomDescription = bom.Description,
        RequestedQuantity = quantity,
        CanBuild = canBuild,
        TotalCost = totalCost,
        ShortageValue = shortages.Sum(s => s.ShortageValue),
        MaterialRequirements = requirements,
        MaterialShortages = shortages
      };
    }

    public async Task<IEnumerable<MaterialShortageItem>> GetBomMaterialShortagesAsync(int bomId, int quantity)
    {
      var shortages = new List<MaterialShortageItem>();
      await GetBomMaterialShortagesRecursiveAsync(bomId, quantity, shortages, "Direct");
      return shortages;
    }

    private async Task GetBomMaterialShortagesRecursiveAsync(int bomId, int quantity, List<MaterialShortageItem> shortages, string context)
    {
      var bom = await _bomService.GetBomByIdAsync(bomId);
      if (bom == null) return;

      // Check direct materials
      foreach (var bomItem in bom.BomItems)
      {
        var requiredQuantity = bomItem.Quantity * quantity;
        var item = await _inventoryService.GetItemByIdAsync(bomItem.ItemId);

        if (item != null && item.CurrentStock < requiredQuantity)
        {
          var shortageQuantity = requiredQuantity - item.CurrentStock;
          var averageCost = await _inventoryService.GetAverageCostAsync(bomItem.ItemId);

          // Get last purchase info - FIXED to include Vendor navigation property
          var lastPurchase = await _context.Purchases
              .Include(p => p.Vendor) // Include the Vendor navigation property
              .Where(p => p.ItemId == bomItem.ItemId)
              .OrderByDescending(p => p.PurchaseDate)
              .FirstOrDefaultAsync();

          // Calculate suggested purchase quantity (shortage + safety stock)
          var suggestedQuantity = Math.Max(shortageQuantity, item.MinimumStock - item.CurrentStock);
          suggestedQuantity = Math.Max(suggestedQuantity, (int)(shortageQuantity * 1.2m)); // 20% safety buffer

          var shortage = new MaterialShortageItem
          {
            ItemId = bomItem.ItemId,
            PartNumber = item.PartNumber,
            Description = item.Description,
            RequiredQuantity = requiredQuantity,
            AvailableQuantity = item.CurrentStock,
            ShortageQuantity = shortageQuantity,
            EstimatedUnitCost = averageCost > 0 ? averageCost : (lastPurchase?.CostPerUnit ?? 0),
            ShortageValue = shortageQuantity * (averageCost > 0 ? averageCost : (lastPurchase?.CostPerUnit ?? 0)),
            PreferredVendor = lastPurchase?.Vendor?.CompanyName, // FIXED: Use CompanyName instead of Vendor object
            LastPurchaseDate = lastPurchase?.PurchaseDate,
            LastPurchasePrice = lastPurchase?.CostPerUnit,
            MinimumStock = item.MinimumStock,
            SuggestedPurchaseQuantity = suggestedQuantity,
            BomContext = context,
            QuantityPerAssembly = bomItem.Quantity
          };

          // Check if this item already exists in shortages (from sub-assemblies)
          var existingShortage = shortages.FirstOrDefault(s => s.ItemId == bomItem.ItemId);
          if (existingShortage != null)
          {
            // Aggregate the shortage
            existingShortage.RequiredQuantity += requiredQuantity;
            existingShortage.ShortageQuantity = Math.Max(0, existingShortage.RequiredQuantity - existingShortage.AvailableQuantity);
            existingShortage.ShortageValue = existingShortage.ShortageQuantity * existingShortage.EstimatedUnitCost;
            existingShortage.SuggestedPurchaseQuantity = Math.Max(existingShortage.SuggestedPurchaseQuantity, suggestedQuantity);
            existingShortage.BomContext += $", {context}";
          }
          else
          {
            shortages.Add(shortage);
          }
        }
      }

      // Check sub-assemblies recursively
      foreach (var subAssembly in bom.SubAssemblies)
      {
        await GetBomMaterialShortagesRecursiveAsync(subAssembly.Id, quantity, shortages, $"Sub-Assembly: {subAssembly.BomNumber}");
      }
    }

    public async Task<IEnumerable<MaterialRequirement>> GetBomMaterialRequirementsAsync(int bomId, int quantity)
    {
      var requirements = new List<MaterialRequirement>();
      await GetBomMaterialRequirementsRecursiveAsync(bomId, quantity, requirements, "Direct");
      return requirements;
    }

    private async Task GetBomMaterialRequirementsRecursiveAsync(int bomId, int quantity, List<MaterialRequirement> requirements, string context)
    {
      var bom = await _bomService.GetBomByIdAsync(bomId);
      if (bom == null) return;

      // Process direct materials
      foreach (var bomItem in bom.BomItems)
      {
        var requiredQuantity = bomItem.Quantity * quantity;
        var item = await _inventoryService.GetItemByIdAsync(bomItem.ItemId);

        if (item != null)
        {
          var averageCost = await _inventoryService.GetAverageCostAsync(bomItem.ItemId);
          var hasSufficientStock = item.CurrentStock >= requiredQuantity;

          var requirement = new MaterialRequirement
          {
            ItemId = bomItem.ItemId,
            PartNumber = item.PartNumber,
            Description = item.Description,
            RequiredQuantity = requiredQuantity,
            AvailableQuantity = item.CurrentStock,
            EstimatedUnitCost = averageCost,
            TotalCost = requiredQuantity * averageCost,
            HasSufficientStock = hasSufficientStock,
            BomContext = context,
            QuantityPerAssembly = bomItem.Quantity
          };

          // Check if this item already exists in requirements (from sub-assemblies)
          var existingRequirement = requirements.FirstOrDefault(r => r.ItemId == bomItem.ItemId);
          if (existingRequirement != null)
          {
            // Aggregate the requirement
            existingRequirement.RequiredQuantity += requiredQuantity;
            existingRequirement.TotalCost += requiredQuantity * averageCost;
            existingRequirement.HasSufficientStock = existingRequirement.AvailableQuantity >= existingRequirement.RequiredQuantity;
            existingRequirement.BomContext += $", {context}";
          }
          else
          {
            requirements.Add(requirement);
          }
        }
      }

      // Process sub-assemblies recursively
      foreach (var subAssembly in bom.SubAssemblies)
      {
        await GetBomMaterialRequirementsRecursiveAsync(subAssembly.Id, quantity, requirements, $"Sub-Assembly: {subAssembly.BomNumber}");
      }
    }

    public class BackorderInfo
    {
      public int SaleId { get; set; }
      public string SaleNumber { get; set; } = string.Empty;
      public string CustomerName { get; set; } = string.Empty;
      public int QuantityBackordered { get; set; }
      public DateTime SaleDate { get; set; }
      public int DaysWaiting { get; set; }
    }

  }
}