using InventorySystem.Models;

namespace InventorySystem.Services
{
    public interface IInventoryService
    {
        Task<IEnumerable<Item>> GetAllItemsAsync();
        Task<Item?> GetItemByIdAsync(int id);
        Task<Item?> GetItemByPartNumberAsync(string partNumber);
        Task<Item> CreateItemAsync(Item item);
        Task<Item> UpdateItemAsync(Item item);
        Task DeleteItemAsync(int id);
        Task<decimal> GetAverageCostAsync(int itemId);
        Task<decimal> GetFifoValueAsync(int itemId);
        Task<IEnumerable<Item>> GetLowStockItemsAsync();
    }
}