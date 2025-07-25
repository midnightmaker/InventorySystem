using InventorySystem.Models;

namespace InventorySystem.Services
{
    public interface IPurchaseService
    {
        Task<IEnumerable<Purchase>> GetPurchasesByItemIdAsync(int itemId);
        Task<Purchase?> GetPurchaseByIdAsync(int id);
        Task<Purchase> CreatePurchaseAsync(Purchase purchase);
        Task<Purchase> UpdatePurchaseAsync(Purchase purchase);
        Task DeletePurchaseAsync(int id);
        Task ProcessInventoryConsumptionAsync(int itemId, int quantityUsed);
    }
}