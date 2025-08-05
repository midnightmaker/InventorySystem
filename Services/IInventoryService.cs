using InventorySystem.Models;

namespace InventorySystem.Services
{
  public interface IInventoryService
  {
    // NEW: Method for optimized indexing
    Task<IEnumerable<Item>> GetItemsForIndexAsync();

    // Existing core methods
    Task<IEnumerable<Item>> GetAllItemsAsync();
    Task<Item?> GetItemByIdAsync(int id);
    Task<Item?> GetItemByPartNumberAsync(string partNumber);
    Task<Item> CreateItemAsync(Item item);
    Task<Item> UpdateItemAsync(Item item);
    Task DeleteItemAsync(int id);
    Task<decimal> GetAverageCostAsync(int itemId);
    Task<decimal> GetFifoValueAsync(int itemId);
    Task<IEnumerable<Item>> GetLowStockItemsAsync();

    // Enhanced methods for dashboard functionality
    Task<int> GetItemsInStockCountAsync();
    Task<int> GetItemsNoStockCountAsync();
    Task<int> GetItemsOverstockedCountAsync();
    Task<decimal> GetTotalInventoryValueAsync();
    Task<IEnumerable<Item>> GetItemsCreatedInMonthAsync(int year, int month);

    // NEW: Version control methods
    Task<IEnumerable<Item>> GetItemVersionsAsync(int baseItemId);
    Task<Item?> GetItemVersionAsync(int baseItemId, string version);
    Task<Item?> GetCurrentItemVersionAsync(int baseItemId);
    Task<Item> CreateItemVersionAsync(int baseItemId, string newVersion, int changeOrderId);
    Task<IEnumerable<Item>> GetItemsByBaseItemIdAsync(int baseItemId);
    Task<bool> IsCurrentVersionAsync(int itemId);
    Task SetCurrentVersionAsync(int itemId);

    // Helper methods for stock management
    Task UpdateStockAsync(int itemId, int newStock);
    Task AdjustStockAsync(int itemId, int adjustment);
  }
}