using InventorySystem.Models;

namespace InventorySystem.Services
{
  public interface IPurchaseService
  {
    // Existing methods
    Task<IEnumerable<Purchase>> GetPurchasesByItemIdAsync(int itemId);
    Task<Purchase?> GetPurchaseByIdAsync(int id);
    Task<Purchase> CreatePurchaseAsync(Purchase purchase);
    Task<Purchase> UpdatePurchaseAsync(Purchase purchase);
    Task DeletePurchaseAsync(int id);
    Task ProcessInventoryConsumptionAsync(int itemId, int quantityUsed);

    // Enhanced methods for dashboard functionality
    Task<IEnumerable<Purchase>> GetAllPurchasesAsync();
    Task<IEnumerable<Purchase>> GetPurchasesByVendorAsync(string vendor);
    Task<IEnumerable<Purchase>> GetPurchasesWithDocumentsAsync();
    Task<decimal> GetTotalPurchaseValueAsync();
    Task<decimal> GetTotalPurchaseValueByItemAsync(int itemId);
    Task<decimal> GetPurchaseValueByMonthAsync(int year, int month);
    Task<int> GetPurchaseCountByMonthAsync(int year, int month);
  }
}