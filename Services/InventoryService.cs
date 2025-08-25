// Services/InventoryService.cs - CLEANED: Only operational item types
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.Services
{
  public class InventoryService : IInventoryService
  {
    private readonly InventoryContext _context;

    public InventoryService(InventoryContext context)
    {
      _context = context;
    }

		#region Existing Core Methods

		public async Task<IEnumerable<Item>> GetAllItemsAsync()
		{
			return await _context.Items
				.OrderBy(i => i.PartNumber)
				.ToListAsync();
		}

		public async Task<IEnumerable<Item>> GetItemsForIndexAsync()
		{
			return await _context.Items
					.Include(i => i.VendorItems)
							.ThenInclude(vi => vi.Vendor)
					.OrderBy(i => i.PartNumber)
					.ToListAsync();
		}

		public async Task<Item?> GetItemByIdAsync(int id)
    {
      return await _context.Items
          .Include(i => i.Purchases.OrderBy(p => p.PurchaseDate))
          .Include(i => i.DesignDocuments)
          .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Item?> GetItemByPartNumberAsync(string partNumber)
    {
      return await _context.Items
          .Include(i => i.Purchases)
          .Include(i => i.DesignDocuments)
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
      try
      {
        var existingItem = await _context.Items.FindAsync(item.Id);
        if (existingItem == null)
        {
          throw new InvalidOperationException($"Item with ID {item.Id} not found");
        }

        // Update only the properties that should be modified
        existingItem.PartNumber = item.PartNumber;
        existingItem.Description = item.Description;
        existingItem.Comments = item.Comments;
        existingItem.MinimumStock = item.MinimumStock;
        existingItem.UnitOfMeasure = item.UnitOfMeasure;
        existingItem.VendorPartNumber = item.VendorPartNumber;
        existingItem.IsSellable = item.IsSellable;
        existingItem.ItemType = item.ItemType;
        existingItem.Version = item.Version;
        existingItem.SalePrice = item.SalePrice;

        // Update image data if provided
        if (item.ImageData != null && item.ImageData.Length > 0)
        {
          existingItem.ImageData = item.ImageData;
          existingItem.ImageContentType = item.ImageContentType;
          existingItem.ImageFileName = item.ImageFileName;
        }

        await _context.SaveChangesAsync();
        return existingItem;
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException($"Error updating item: {ex.Message}", ex);
      }
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
        .Select(p => p.CostPerUnit)
        .ToListAsync();

      if (!purchases.Any()) return 0;

      return purchases.Average();
    }

    public async Task<decimal> GetFifoValueAsync(int itemId)
    {
      var item = await _context.Items.FindAsync(itemId);
      if (item == null) return 0;

      var availablePurchases = await _context.Purchases
          .Where(p => p.ItemId == itemId && p.RemainingQuantity > 0)
          .OrderBy(p => p.PurchaseDate)
          .Select(p => new { p.RemainingQuantity, p.CostPerUnit })
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

    // UPDATED: Only operational item types
    public async Task<IEnumerable<Item>> GetLowStockItemsAsync()
    {
      return await _context.Items
          .Where(i => (i.ItemType == ItemType.Inventoried || 
                      i.ItemType == ItemType.Consumable || 
                      i.ItemType == ItemType.RnDMaterials) && 
                     i.CurrentStock <= i.MinimumStock)
          .Include(i => i.Purchases)
          .OrderBy(i => i.PartNumber)
          .ToListAsync();
    }

    #endregion

    #region Dashboard Methods - UPDATED for operational items only

    public async Task<int> GetItemsInStockCountAsync()
    {
      return await _context.Items.CountAsync(i => (i.ItemType == ItemType.Inventoried || 
                                                   i.ItemType == ItemType.Consumable || 
                                                   i.ItemType == ItemType.RnDMaterials) && 
                                                 i.CurrentStock > 0);
    }

    public async Task<int> GetItemsNoStockCountAsync()
    {
      return await _context.Items.CountAsync(i => (i.ItemType == ItemType.Inventoried || 
                                                   i.ItemType == ItemType.Consumable || 
                                                   i.ItemType == ItemType.RnDMaterials) && 
                                                 i.CurrentStock == 0);
    }

    public async Task<int> GetItemsOverstockedCountAsync()
    {
      return await _context.Items.CountAsync(i => (i.ItemType == ItemType.Inventoried || 
                                                   i.ItemType == ItemType.Consumable || 
                                                   i.ItemType == ItemType.RnDMaterials) && 
                                                 i.CurrentStock > (i.MinimumStock * 2));
    }

    public async Task<decimal> GetTotalInventoryValueAsync()
    {
      var items = await _context.Items
        .Where(i => (i.ItemType == ItemType.Inventoried || 
                    i.ItemType == ItemType.Consumable || 
                    i.ItemType == ItemType.RnDMaterials) && 
                   i.CurrentStock > 0)
        .Select(i => new { i.Id, i.CurrentStock })
        .ToListAsync();

      decimal totalValue = 0;

      foreach (var item in items)
      {
        var averageCost = await GetAverageCostAsync(item.Id);
        totalValue += item.CurrentStock * averageCost;
      }

      return totalValue;
    }

    public async Task<IEnumerable<Item>> GetItemsCreatedInMonthAsync(int year, int month)
    {
      return await _context.Items
          .Where(i => i.CreatedDate.Year == year && i.CreatedDate.Month == month)
          .Include(i => i.Purchases)
          .ToListAsync();
    }

    #endregion

    #region Version Control Methods

    public async Task<IEnumerable<Item>> GetItemVersionsAsync(int baseItemId)
    {
      return await _context.Items
          .Where(i => i.Id == baseItemId || i.BaseItemId == baseItemId)
          .Include(i => i.DesignDocuments)
          .OrderBy(i => i.Version)
          .ToListAsync();
    }

    public async Task<Item?> GetItemVersionAsync(int baseItemId, string version)
    {
      return await _context.Items
          .Include(i => i.DesignDocuments)
          .Include(i => i.Purchases)
          .FirstOrDefaultAsync(i => (i.Id == baseItemId || i.BaseItemId == baseItemId) && i.Version == version);
    }

    public async Task<Item?> GetCurrentItemVersionAsync(int baseItemId)
    {
      return await _context.Items
          .Include(i => i.DesignDocuments)
          .Include(i => i.Purchases)
          .FirstOrDefaultAsync(i => (i.Id == baseItemId || i.BaseItemId == baseItemId) && i.IsCurrentVersion);
    }

    public async Task<Item> CreateItemVersionAsync(int baseItemId, string newVersion, int changeOrderId)
    {
      var baseItem = await _context.Items.FindAsync(baseItemId);
      if (baseItem == null)
      {
        throw new InvalidOperationException("Base item not found");
      }

      // Mark all existing versions as not current
      var existingVersions = await GetItemVersionsAsync(baseItemId);
      foreach (var version in existingVersions)
      {
        version.IsCurrentVersion = false;
      }

      // Create new version
      var newItem = new Item
      {
        PartNumber = baseItem.PartNumber,
        Description = baseItem.Description,
        Version = newVersion,
        ItemType = baseItem.ItemType,
        MinimumStock = baseItem.MinimumStock,
        Comments = baseItem.Comments,
        BaseItemId = baseItem.BaseItemId ?? baseItemId,
        IsCurrentVersion = true,
        CreatedFromChangeOrderId = changeOrderId,
        CreatedDate = DateTime.Now,
        CurrentStock = 0 // New versions start with no stock
      };

      _context.Items.Add(newItem);
      await _context.SaveChangesAsync();

      return newItem;
    }

    public async Task<IEnumerable<Item>> GetItemsByBaseItemIdAsync(int baseItemId)
    {
      return await _context.Items
          .Where(i => i.Id == baseItemId || i.BaseItemId == baseItemId)
          .Include(i => i.DesignDocuments)
          .Include(i => i.Purchases)
          .OrderBy(i => i.Version)
          .ToListAsync();
    }

    public async Task<bool> IsCurrentVersionAsync(int itemId)
    {
      var item = await _context.Items.FindAsync(itemId);
      return item?.IsCurrentVersion ?? false;
    }

    public async Task SetCurrentVersionAsync(int itemId)
    {
      var item = await _context.Items.FindAsync(itemId);
      if (item == null) return;

      var baseItemId = item.BaseItemId ?? itemId;

      // Mark all versions as not current
      var allVersions = await GetItemVersionsAsync(baseItemId);
      foreach (var version in allVersions)
      {
        version.IsCurrentVersion = false;
      }

      // Mark specified version as current
      item.IsCurrentVersion = true;
      await _context.SaveChangesAsync();
    }

    #endregion

    #region Helper Methods

    public async Task UpdateStockAsync(int itemId, int newStock)
    {
      var item = await _context.Items.FindAsync(itemId);
      if (item != null)
      {
        item.CurrentStock = newStock;
        await _context.SaveChangesAsync();
      }
    }

    public async Task AdjustStockAsync(int itemId, int adjustment)
    {
      var item = await _context.Items.FindAsync(itemId);
      if (item != null)
      {
        item.CurrentStock += adjustment;
        await _context.SaveChangesAsync();
      }
    }

    #endregion
  }
}