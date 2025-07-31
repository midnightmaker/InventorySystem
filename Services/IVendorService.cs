using InventorySystem.Models;

namespace InventorySystem.Services
{
  public interface IVendorService
  {
    Task<IEnumerable<Vendor>> GetAllVendorsAsync();
    Task<IEnumerable<Vendor>> GetActiveVendorsAsync();
    Task<IEnumerable<Vendor>> GetPreferredVendorsAsync();
    Task<Vendor?> GetVendorByIdAsync(int id);
    Task<Vendor?> GetVendorByNameAsync(string companyName);
    Task<Vendor> CreateVendorAsync(Vendor vendor);
    Task<Vendor> UpdateVendorAsync(Vendor vendor);
    Task<bool> DeleteVendorAsync(int id);
    Task<bool> DeactivateVendorAsync(int id);
    Task<bool> ActivateVendorAsync(int id);

    // Vendor-Item relationships
    Task<IEnumerable<VendorItem>> GetVendorItemsAsync(int vendorId);
    Task<IEnumerable<VendorItem>> GetItemVendorsAsync(int itemId);
    Task<VendorItem?> GetVendorItemAsync(int vendorId, int itemId);
    Task<VendorItem> CreateVendorItemAsync(VendorItem vendorItem);
    Task<VendorItem> UpdateVendorItemAsync(VendorItem vendorItem);
    Task<bool> DeleteVendorItemAsync(int vendorId, int itemId);

    // Business logic
    Task<IEnumerable<Vendor>> SearchVendorsAsync(string searchTerm);
    Task<IEnumerable<VendorItem>> GetCheapestVendorsForItemAsync(int itemId);
    Task<IEnumerable<VendorItem>> GetFastestVendorsForItemAsync(int itemId);
    Task UpdateVendorItemLastPurchaseAsync(int vendorId, int itemId, decimal cost, DateTime purchaseDate);
    Task<decimal> GetVendorTotalPurchasesAsync(int vendorId);
    Task<IEnumerable<Purchase>> GetVendorPurchaseHistoryAsync(int vendorId);
  }
}