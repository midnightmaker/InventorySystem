// Services/BomService.cs
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Services
{
  public class BomService : IBomService
  {
    private readonly InventoryContext _context;
    private readonly ILogger<BomService> _logger;

    public BomService(InventoryContext context, ILogger<BomService> logger)
    {
      _context = context;
      _logger = logger;
    }

    #region Basic CRUD Operations

    public async Task<IEnumerable<Bom>> GetAllBomsAsync()
    {
      return await _context.Boms
          .Include(b => b.BomItems)
              .ThenInclude(bi => bi.Item)
          .Include(b => b.SubAssemblies)
          // REMOVED: .Where(b => b.ParentBomId == null) - This was filtering out sub-assemblies
          .OrderBy(b => b.BomNumber)
          .ToListAsync();
    }

    // If you want top level BOMs only
    public async Task<IEnumerable<Bom>> GetTopLevelBomsAsync()
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
          .Include(b => b.Documents)
          .Include(b => b.ParentBom)
          .Include(b => b.CreatedFromChangeOrder)
          .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Bom> CreateBomAsync(Bom bom)
    {
      try
      {
        // Set initial version if not provided
        if (string.IsNullOrEmpty(bom.Version))
        {
          bom.Version = "A";
        }

        // Set as current version for new BOMs
        bom.IsCurrentVersion = true;
        bom.CreatedDate = DateTime.Now;
        bom.ModifiedDate = DateTime.Now;

        _context.Boms.Add(bom);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new BOM {BomNumber} (ID: {BomId}) with version {Version}",
            bom.BomNumber, bom.Id, bom.Version);

        return bom;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating BOM {BomNumber}", bom.BomNumber);
        throw;
      }
    }

    public async Task<Bom> UpdateBomAsync(Bom bom)
    {
      try
      {
        bom.ModifiedDate = DateTime.Now;
        _context.Boms.Update(bom);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated BOM {BomNumber} (ID: {BomId})", bom.BomNumber, bom.Id);
        return bom;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating BOM {BomId}", bom.Id);
        throw;
      }
    }

    public async Task DeleteBomAsync(int id)
    {
      try
      {
        var bom = await _context.Boms
            .Include(b => b.BomItems)
            .Include(b => b.SubAssemblies)
            .Include(b => b.Documents)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bom != null)
        {
          // Check if BOM is being used in production
          var isUsedInProduction = await _context.Productions.AnyAsync(p => p.BomId == id);
          if (isUsedInProduction)
          {
            throw new InvalidOperationException("Cannot delete BOM that has been used in production.");
          }

          // Check if BOM has finished goods
          var hasFinishedGoods = await _context.FinishedGoods.AnyAsync(fg => fg.BomId == id);
          if (hasFinishedGoods)
          {
            throw new InvalidOperationException("Cannot delete BOM that has associated finished goods.");
          }

          _context.Boms.Remove(bom);
          await _context.SaveChangesAsync();

          _logger.LogInformation("Deleted BOM {BomNumber} (ID: {BomId})", bom.BomNumber, bom.Id);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting BOM {BomId}", id);
        throw;
      }
    }

    #endregion

    #region Production-Specific Methods (CRITICAL FOR PRODUCTION FIXES)

    /// <summary>
    /// Gets only current version BOMs for production dropdown
    /// </summary>
    public async Task<IEnumerable<Bom>> GetCurrentVersionBomsAsync()
    {
      return await _context.Boms
          .Include(b => b.BomItems)
              .ThenInclude(bi => bi.Item)
          .Include(b => b.SubAssemblies)
          .Where(b => b.ParentBomId == null && b.IsCurrentVersion == true)
          .OrderBy(b => b.BomNumber)
          .ToListAsync();
    }

    /// <summary>
    /// Gets current version BOM for production - CRITICAL for version consistency
    /// </summary>
    public async Task<Bom?> GetCurrentVersionBomByIdAsync(int bomId)
    {
      try
      {
        // First check if this IS the current version
        var directBom = await _context.Boms
            .Include(b => b.BomItems)
                .ThenInclude(bi => bi.Item)
            .Include(b => b.SubAssemblies)
                .ThenInclude(sa => sa.BomItems)
                    .ThenInclude(bi => bi.Item)
            .Include(b => b.Documents)
            .Include(b => b.ParentBom)
            .Include(b => b.CreatedFromChangeOrder)
            .FirstOrDefaultAsync(b => b.Id == bomId && b.IsCurrentVersion == true);

        if (directBom != null)
        {
          _logger.LogDebug("Found current version BOM directly: {BomId}", bomId);
          return directBom;
        }

        // If not, try to find the current version using BaseBomId
        var baseBomId = await _context.Boms
            .Where(b => b.Id == bomId)
            .Select(b => b.BaseBomId ?? bomId)
            .FirstOrDefaultAsync();

        if (baseBomId == 0)
        {
          _logger.LogWarning("No BOM found with ID {BomId}", bomId);
          return null;
        }

        var currentVersionBom = await _context.Boms
            .Include(b => b.BomItems)
                .ThenInclude(bi => bi.Item)
            .Include(b => b.SubAssemblies)
                .ThenInclude(sa => sa.BomItems)
                    .ThenInclude(bi => bi.Item)
            .Include(b => b.Documents)
            .Include(b => b.ParentBom)
            .Include(b => b.CreatedFromChangeOrder)
            .FirstOrDefaultAsync(b => (b.Id == baseBomId || b.BaseBomId == baseBomId) && b.IsCurrentVersion == true);

        if (currentVersionBom != null)
        {
          _logger.LogDebug("Found current version BOM via BaseBomId: {BomId} -> {CurrentBomId}", bomId, currentVersionBom.Id);
        }
        else
        {
          _logger.LogWarning("No current version found for BOM {BomId}", bomId);
        }

        return currentVersionBom;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting current version BOM {BomId}", bomId);
        throw;
      }
    }

    /// <summary>
    /// Validates if BOM is ready for production
    /// </summary>
    public async Task<ProductionValidationResult> ValidateBomForProductionAsync(int bomId)
    {
      var result = new ProductionValidationResult();

      try
      {
        var bom = await GetCurrentVersionBomByIdAsync(bomId);

        if (bom == null)
        {
          result.IsValid = false;
          result.Errors.Add("BOM not found or is not the current version");
          return result;
        }

        // Check if BOM has any items
        if (!bom.BomItems.Any())
        {
          result.IsValid = false;
          result.Errors.Add("BOM has no items defined");
        }

        // Check if all items are current versions and available
        foreach (var bomItem in bom.BomItems)
        {
          var item = await _context.Items
              .FirstOrDefaultAsync(i => i.Id == bomItem.ItemId);

          if (item == null)
          {
            result.IsValid = false;
            result.Errors.Add($"Item {bomItem.ItemId} not found");
            continue;
          }

          if (!item.IsCurrentVersion)
          {
            result.Warnings.Add($"Item {item.PartNumber} is not current version");
          }

          if (item.CurrentStock <= 0)
          {
            result.Warnings.Add($"Item {item.PartNumber} has zero stock");
          }
        }

        // Check for circular references in sub-assemblies
        var visited = new HashSet<int>();
        if (await HasCircularReferenceAsync(bom, visited))
        {
          result.IsValid = false;
          result.Errors.Add("Circular reference detected in sub-assemblies");
        }

        _logger.LogDebug("BOM {BomId} validation: Valid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
            bomId, result.IsValid, result.Errors.Count, result.Warnings.Count);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error validating BOM {BomId} for production", bomId);
        result.IsValid = false;
        result.Errors.Add($"Validation error: {ex.Message}");
      }

      return result;
    }

    #endregion

    #region BOM Item Management

    public async Task<BomItem> AddBomItemAsync(BomItem bomItem)
    {
      try
      {
        // Calculate unit cost using average purchase pricing
        var costs = await _context.Purchases
            .Where(p => p.ItemId == bomItem.ItemId)
            .Select(p => (decimal?)p.CostPerUnit)
            .ToListAsync();

        var averageCost = costs.Any() ? costs.Average() ?? 0 : 0;

        bomItem.UnitCost = averageCost;

        _context.BomItems.Add(bomItem);
        await _context.SaveChangesAsync();

        // Update the parent BOM's modified date
        var bom = await _context.Boms.FindAsync(bomItem.BomId);
        if (bom != null)
        {
          bom.ModifiedDate = DateTime.Now;
          await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Added item {ItemId} to BOM {BomId} with unit cost {UnitCost}",
            bomItem.ItemId, bomItem.BomId, bomItem.UnitCost);
        return bomItem;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error adding item {ItemId} to BOM {BomId}", bomItem.ItemId, bomItem.BomId);
        throw;
      }
    }

    public async Task<BomItem> UpdateBomItemAsync(BomItem bomItem)
    {
      try
      {
        _context.BomItems.Update(bomItem);

        // Update parent BOM modified date
        var bom = await _context.Boms.FindAsync(bomItem.BomId);
        if (bom != null)
        {
          bom.ModifiedDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated BOM item {BomItemId} in BOM {BomId}", bomItem.Id, bomItem.BomId);
        return bomItem;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating BOM item {BomItemId}", bomItem.Id);
        throw;
      }
    }

    public async Task DeleteBomItemAsync(int bomItemId)
    {
      try
      {
        var bomItem = await _context.BomItems.FindAsync(bomItemId);
        if (bomItem != null)
        {
          _context.BomItems.Remove(bomItem);

          // Update parent BOM modified date
          var bom = await _context.Boms.FindAsync(bomItem.BomId);
          if (bom != null)
          {
            bom.ModifiedDate = DateTime.Now;
          }

          await _context.SaveChangesAsync();
          _logger.LogInformation("Deleted BOM item {BomItemId} from BOM {BomId}", bomItemId, bomItem.BomId);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting BOM item {BomItemId}", bomItemId);
        throw;
      }
    }

    #endregion

    #region Cost Calculations

    public async Task<decimal> GetBomTotalCostAsync(int bomId)
    {
      try
      {
        var bom = await GetBomByIdAsync(bomId);
        if (bom == null) return 0;

        decimal totalCost = 0;

        // Calculate direct materials cost
        foreach (var bomItem in bom.BomItems)
        {
          totalCost += bomItem.ExtendedCost;
        }

        // Add sub-assembly costs recursively
        foreach (var subAssembly in bom.SubAssemblies)
        {
          totalCost += await GetBomTotalCostAsync(subAssembly.Id);
        }

        return totalCost;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error calculating total cost for BOM {BomId}", bomId);
        return 0;
      }
    }

    public async Task<decimal> GetBomMaterialCostAsync(int bomId, int quantity = 1)
    {
      try
      {
        var bom = await GetCurrentVersionBomByIdAsync(bomId);
        if (bom == null) return 0;

        decimal totalCost = 0;

        // Calculate direct materials cost using current stock values
        foreach (var bomItem in bom.BomItems)
        {
          var requiredQuantity = bomItem.Quantity * quantity;
          var item = await _context.Items.FindAsync(bomItem.ItemId);

          if (item != null && item.CurrentStock > 0)
          {
            // Use FIFO value calculation if available, otherwise use BOM item unit cost
            var averageCost = bomItem.UnitCost;

            // You can integrate with IInventoryService.GetFifoValueAsync here if needed
            // var fifoValue = await _inventoryService.GetFifoValueAsync(bomItem.ItemId);
            // averageCost = fifoValue / item.CurrentStock;

            totalCost += averageCost * requiredQuantity;
          }
        }

        // Add sub-assembly costs recursively
        foreach (var subAssembly in bom.SubAssemblies)
        {
          totalCost += await GetBomMaterialCostAsync(subAssembly.Id, quantity);
        }

        return totalCost;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error calculating material cost for BOM {BomId}", bomId);
        return 0;
      }
    }

    /// <summary>
    /// Gets exploded BOM cost data including all sub-assembly details
    /// </summary>
    public async Task<ExplodedBomCostData> GetExplodedBomCostDataAsync(int bomId)
    {
      try
      {
        var bom = await GetBomByIdAsync(bomId);
        if (bom == null)
        {
          return new ExplodedBomCostData();
        }

        var explodedData = new ExplodedBomCostData
        {
          BomId = bom.Id,
          BomNumber = bom.BomNumber,
          Description = bom.Description,
          Version = bom.Version
        };

        var allComponents = new List<ExplodedCostItem>();
        var subAssemblies = new List<ExplodedSubAssembly>();
        decimal totalDirectCost = 0;
        decimal totalSubAssemblyCost = 0;
        int maxDepth = 0;

        // Process direct components
        foreach (var bomItem in bom.BomItems)
        {
          var explodedItem = new ExplodedCostItem
          {
            PartNumber = bomItem.Item?.PartNumber ?? "Unknown",
            Description = bomItem.Item?.Description ?? "Unknown",
            Quantity = bomItem.Quantity,
            UnitCost = bomItem.UnitCost,
            ExtendedCost = bomItem.ExtendedCost,
            ReferenceDesignator = bomItem.ReferenceDesignator ?? "",
            Notes = bomItem.Notes ?? "",
            SourceBom = bom.BomNumber,
            Level = 0
          };

          allComponents.Add(explodedItem);
          totalDirectCost += explodedItem.ExtendedCost;
        }

        // Process sub-assemblies recursively
        foreach (var subAssembly in bom.SubAssemblies)
        {
          var explodedSub = await GetExplodedSubAssemblyDataAsync(subAssembly.Id, 1);
          if (explodedSub != null)
          {
            subAssemblies.Add(explodedSub);
            totalSubAssemblyCost += explodedSub.SubAssemblyCost;
            maxDepth = Math.Max(maxDepth, explodedSub.Level);

            // Add all components from sub-assemblies to the flat list
            allComponents.AddRange(GetAllComponentsFromSubAssembly(explodedSub));
          }
        }

        explodedData.AllComponents = allComponents.OrderBy(c => c.Level)
                                                 .ThenBy(c => c.SourceBom)
                                                 .ThenBy(c => c.PartNumber)
                                                 .ToList();
        explodedData.SubAssemblies = subAssemblies;
        explodedData.TotalCost = totalDirectCost + totalSubAssemblyCost;

        explodedData.Summary = new CostSummary
        {
          DirectComponentsCost = totalDirectCost,
          SubAssembliesCost = totalSubAssemblyCost,
          TotalCost = explodedData.TotalCost,
          TotalComponentCount = allComponents.Count,
          TotalSubAssemblyCount = subAssemblies.Count + subAssemblies.Sum(s => CountNestedSubAssemblies(s)),
          MaxDepthLevel = maxDepth
        };

        _logger.LogInformation("Generated exploded cost data for BOM {BomNumber} with {ComponentCount} total components",
            bom.BomNumber, allComponents.Count);

        return explodedData;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error generating exploded cost data for BOM {BomId}", bomId);
        return new ExplodedBomCostData();
      }
    }

    private async Task<ExplodedSubAssembly?> GetExplodedSubAssemblyDataAsync(int bomId, int level)
    {
      try
      {
        var bom = await GetBomByIdAsync(bomId);
        if (bom == null) return null;

        var explodedSub = new ExplodedSubAssembly
        {
          BomId = bom.Id,
          BomNumber = bom.BomNumber,
          Description = bom.Description,
          Version = bom.Version,
          AssemblyPartNumber = bom.AssemblyPartNumber ?? "",
          Level = level,
          ComponentCount = bom.BomItems.Count
        };

        decimal subAssemblyCost = 0;

        // Process components
        foreach (var bomItem in bom.BomItems)
        {
          var explodedItem = new ExplodedCostItem
          {
            PartNumber = bomItem.Item?.PartNumber ?? "Unknown",
            Description = bomItem.Item?.Description ?? "Unknown",
            Quantity = bomItem.Quantity,
            UnitCost = bomItem.UnitCost,
            ExtendedCost = bomItem.ExtendedCost,
            ReferenceDesignator = bomItem.ReferenceDesignator ?? "",
            Notes = bomItem.Notes ?? "",
            SourceBom = bom.BomNumber,
            Level = level
          };

          explodedSub.Components.Add(explodedItem);
          subAssemblyCost += explodedItem.ExtendedCost;
        }

        // Process nested sub-assemblies
        foreach (var nestedSubAssembly in bom.SubAssemblies)
        {
          var nestedExploded = await GetExplodedSubAssemblyDataAsync(nestedSubAssembly.Id, level + 1);
          if (nestedExploded != null)
          {
            explodedSub.NestedSubAssemblies.Add(nestedExploded);
            subAssemblyCost += nestedExploded.SubAssemblyCost;
          }
        }

        explodedSub.SubAssemblyCost = subAssemblyCost;
        return explodedSub;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting exploded sub-assembly data for BOM {BomId}", bomId);
        return null;
      }
    }

    private List<ExplodedCostItem> GetAllComponentsFromSubAssembly(ExplodedSubAssembly subAssembly)
    {
      var allComponents = new List<ExplodedCostItem>();
      
      // Add direct components
      allComponents.AddRange(subAssembly.Components);
      
      // Add components from nested sub-assemblies
      foreach (var nested in subAssembly.NestedSubAssemblies)
      {
        allComponents.AddRange(GetAllComponentsFromSubAssembly(nested));
      }
      
      return allComponents;
    }

    private int CountNestedSubAssemblies(ExplodedSubAssembly subAssembly)
    {
      int count = subAssembly.NestedSubAssemblies.Count;
      foreach (var nested in subAssembly.NestedSubAssemblies)
      {
        count += CountNestedSubAssemblies(nested);
      }
      return count;
    }

    #endregion

    #region Search and Filtering

    public async Task<IEnumerable<Bom>> SearchBomsAsync(string searchTerm)
    {
      if (string.IsNullOrEmpty(searchTerm))
      {
        return await GetAllBomsAsync();
      }

      return await _context.Boms
          .Include(b => b.BomItems)
              .ThenInclude(bi => bi.Item)
          .Include(b => b.SubAssemblies)
          .Where(b => b.ParentBomId == null &&
                     (b.BomNumber.Contains(searchTerm) ||
                      b.Description.Contains(searchTerm) ||
                      b.AssemblyPartNumber.Contains(searchTerm)))
          .OrderBy(b => b.BomNumber)
          .ToListAsync();
    }

    public async Task<IEnumerable<Bom>> GetBomsByAssemblyPartNumberAsync(string assemblyPartNumber)
    {
      return await _context.Boms
          .Include(b => b.BomItems)
              .ThenInclude(bi => bi.Item)
          .Include(b => b.SubAssemblies)
          .Where(b => b.AssemblyPartNumber == assemblyPartNumber)
          .OrderBy(b => b.Version)
          .ToListAsync();
    }

    public async Task<IEnumerable<Bom>> GetBomsByItemIdAsync(int itemId)
    {
      return await _context.Boms
          .Include(b => b.BomItems)
              .ThenInclude(bi => bi.Item)
          .Where(b => b.BomItems.Any(bi => bi.ItemId == itemId))
          .OrderBy(b => b.BomNumber)
          .ToListAsync();
    }

    #endregion

    #region Helper Methods

    private async Task<bool> HasCircularReferenceAsync(Bom bom, HashSet<int> visited)
    {
      if (visited.Contains(bom.Id))
      {
        return true;
      }

      visited.Add(bom.Id);

      foreach (var subAssembly in bom.SubAssemblies)
      {
        var subBom = await GetBomByIdAsync(subAssembly.Id);
        if (subBom != null && await HasCircularReferenceAsync(subBom, new HashSet<int>(visited)))
        {
          return true;
        }
      }

      return false;
    }

    public async Task<bool> BomExistsAsync(int bomId)
    {
      return await _context.Boms.AnyAsync(b => b.Id == bomId);
    }

    public async Task<bool> BomNumberExistsAsync(string bomNumber, int? excludeId = null)
    {
      var query = _context.Boms.Where(b => b.BomNumber == bomNumber);

      if (excludeId.HasValue)
      {
        query = query.Where(b => b.Id != excludeId.Value);
      }

      return await query.AnyAsync();
    }

    public async Task<string> GenerateNextBomNumberAsync(string prefix = "BOM")
    {
      var lastBom = await _context.Boms
          .Where(b => b.BomNumber.StartsWith(prefix))
          .OrderByDescending(b => b.BomNumber)
          .FirstOrDefaultAsync();

      if (lastBom == null)
      {
        return $"{prefix}-001";
      }

      // Extract number part and increment
      var lastNumber = lastBom.BomNumber.Substring(prefix.Length + 1);
      if (int.TryParse(lastNumber, out var number))
      {
        return $"{prefix}-{(number + 1):D3}";
      }

      return $"{prefix}-001";
    }

    public async Task<object> GetBomHierarchyAsync(int bomId)
    {
      try
      {
        var bom = await _context.Boms
            .Include(b => b.BomItems)
                .ThenInclude(bi => bi.Item)
            .Include(b => b.SubAssemblies)
                .ThenInclude(sa => sa.BomItems)
                    .ThenInclude(bi => bi.Item)
            .Include(b => b.SubAssemblies)
                .ThenInclude(sa => sa.SubAssemblies)
                    .ThenInclude(ssa => ssa.BomItems)
                        .ThenInclude(bi => bi.Item)
            .FirstOrDefaultAsync(b => b.Id == bomId);

        if (bom == null)
        {
          return new { };
        }

        return new
        {
          id = bom.Id,
          bomNumber = bom.BomNumber,
          description = bom.Description,
          version = bom.Version,
          assemblyPartNumber = bom.AssemblyPartNumber,
          bomItems = bom.BomItems?.Select(bi => new
          {
            id = bi.Id,
            quantity = bi.Quantity,
            unitCost = bi.UnitCost,
            referenceDesignator = bi.ReferenceDesignator,
            item = new
            {
              id = bi.Item?.Id,
              partNumber = bi.Item?.PartNumber,
              description = bi.Item?.Description,
              itemType = bi.Item?.ItemType,
              currentStock = bi.Item?.CurrentStock ?? 0
            }
          }).ToList(),
          subAssemblies = await GetSubAssemblyHierarchyAsync(bom.SubAssemblies)
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting BOM hierarchy for BOM {BomId}", bomId);
        return new { };
      }
    }

    private async Task<List<object>> GetSubAssemblyHierarchyAsync(IEnumerable<Bom> subAssemblies)
    {
      var result = new List<object>();

      foreach (var subAssembly in subAssemblies)
      {
        // Load the full sub-assembly with its items and nested sub-assemblies
        var fullSubAssembly = await _context.Boms
            .Include(b => b.BomItems)
                .ThenInclude(bi => bi.Item)
            .Include(b => b.SubAssemblies)
                .ThenInclude(sa => sa.BomItems)
                    .ThenInclude(bi => bi.Item)
            .FirstOrDefaultAsync(b => b.Id == subAssembly.Id);

        if (fullSubAssembly != null)
        {
          result.Add(new
          {
            id = fullSubAssembly.Id,
            bomNumber = fullSubAssembly.BomNumber,
            description = fullSubAssembly.Description,
            version = fullSubAssembly.Version,
            assemblyPartNumber = fullSubAssembly.AssemblyPartNumber,
            bomItems = fullSubAssembly.BomItems?.Select(bi => new
            {
              id = bi.Id,
              quantity = bi.Quantity,
              unitCost = bi.UnitCost,
              referenceDesignator = bi.ReferenceDesignator,
              item = new
              {
                id = bi.Item?.Id,
                partNumber = bi.Item?.PartNumber,
                description = bi.Item?.Description,
                itemType = bi.Item?.ItemType,
                currentStock = bi.Item?.CurrentStock ?? 0
              }
            }).ToList(),
            subAssemblies = await GetSubAssemblyHierarchyAsync(fullSubAssembly.SubAssemblies)
          });
        }
      }

      return result;
    }
    #endregion

    #region Statistics and Reporting

    public async Task<BomStatistics> GetBomStatisticsAsync()
    {
      try
      {
        var totalBoms = await _context.Boms.CountAsync(b => b.ParentBomId == null);
        var currentVersionBoms = await _context.Boms.CountAsync(b => b.ParentBomId == null && b.IsCurrentVersion);
        var totalBomItems = await _context.BomItems.CountAsync();
        var averageItemsPerBom = totalBoms > 0 ? (decimal)totalBomItems / totalBoms : 0;

        var totalValue = await _context.Boms
            .Where(b => b.ParentBomId == null && b.IsCurrentVersion)
            .SelectMany(b => b.BomItems)
            .SumAsync(bi => bi.ExtendedCost);

        return new BomStatistics
        {
          TotalBoms = totalBoms,
          CurrentVersionBoms = currentVersionBoms,
          TotalBomItems = totalBomItems,
          AverageItemsPerBom = averageItemsPerBom,
          TotalValue = totalValue
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error calculating BOM statistics");
        return new BomStatistics();
      }
    }

    #endregion
  }

  #region Supporting Classes

  public class ProductionValidationResult
  {
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
  }

  public class BomStatistics
  {
    public int TotalBoms { get; set; }
    public int CurrentVersionBoms { get; set; }
    public int TotalBomItems { get; set; }
    public decimal AverageItemsPerBom { get; set; }
    public decimal TotalValue { get; set; }
  }

  public class ExplodedBomCostData
  {
    public int BomId { get; set; }
    public string BomNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public List<ExplodedCostItem> AllComponents { get; set; } = new List<ExplodedCostItem>();
    public List<ExplodedSubAssembly> SubAssemblies { get; set; } = new List<ExplodedSubAssembly>();
    public decimal TotalCost { get; set; } = 0;

    public CostSummary Summary { get; set; } = new CostSummary();
  }

  public class ExplodedCostItem
  {
    public string PartNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 0;
    public decimal UnitCost { get; set; } = 0;
    public decimal ExtendedCost { get; set; } = 0;
    public string ReferenceDesignator { get; set; } = "";
    public string Notes { get; set; } = "";
    public string SourceBom { get; set; } = "";
    public int Level { get; set; } = 0;
  }

  public class ExplodedSubAssembly
  {
    public int BomId { get; set; }
    public string BomNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string AssemblyPartNumber { get; set; } = "";
    public List<ExplodedCostItem> Components { get; set; } = new List<ExplodedCostItem>();
    public List<ExplodedSubAssembly> NestedSubAssemblies { get; set; } = new List<ExplodedSubAssembly>();
    public decimal SubAssemblyCost { get; set; } = 0;
    public int Level { get; set; } = 0;
    public int ComponentCount { get; set; } = 0;
  }

  public class CostSummary
  {
    public decimal DirectComponentsCost { get; set; } = 0;
    public decimal SubAssembliesCost { get; set; } = 0;
    public decimal TotalCost { get; set; } = 0;
    public int TotalComponentCount { get; set; } = 0;
    public int TotalSubAssemblyCount { get; set; } = 0;
    public int MaxDepthLevel { get; set; } = 0;
  }

  #endregion
}