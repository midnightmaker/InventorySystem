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

    // Enhanced search and filtering methods
    Task<IEnumerable<Vendor>> SearchVendorsAsync(string searchTerm);
    Task<IEnumerable<Vendor>> AdvancedSearchVendorsAsync(
        string? companyName = null,
        string? vendorCode = null,
        string? contactName = null,
        string? contactEmail = null,
        bool? isActive = null,
        bool? isPreferred = null);

    // NEW: Enhanced filtering methods for the index page
    Task<(IEnumerable<Vendor> vendors, int totalCount)> GetFilteredVendorsAsync(
        string? search = null,
        string? statusFilter = null,
        string? ratingFilter = null,
        string? locationFilter = null,
        string sortOrder = "companyName_asc",
        int page = 1,
        int pageSize = 25);

    Task<IEnumerable<Vendor>> GetVendorsByStatusAsync(string statusFilter);
    Task<IEnumerable<Vendor>> GetVendorsByRatingAsync(string ratingFilter);
    Task<IEnumerable<Vendor>> GetVendorsByLocationAsync(string locationFilter);

    // Vendor-Item relationships
    Task<IEnumerable<VendorItem>> GetVendorItemsAsync(int vendorId);
    Task<IEnumerable<VendorItem>> GetItemVendorsAsync(int itemId);
    Task<VendorItem?> GetVendorItemAsync(int vendorId, int itemId);
    Task<VendorItem> CreateVendorItemAsync(VendorItem vendorItem);
    Task<VendorItem> UpdateVendorItemAsync(VendorItem vendorItem);
    Task<bool> DeleteVendorItemAsync(int vendorId, int itemId);

    // Business logic
    Task<IEnumerable<VendorItem>> GetCheapestVendorsForItemAsync(int itemId);
    Task<IEnumerable<VendorItem>> GetFastestVendorsForItemAsync(int itemId);
    Task UpdateVendorItemLastPurchaseAsync(int vendorId, int itemId, decimal cost, DateTime purchaseDate);
    Task<decimal> GetVendorTotalPurchasesAsync(int vendorId);
    Task<IEnumerable<Purchase>> GetVendorPurchaseHistoryAsync(int vendorId);

    /// <summary>
    /// Gets the preferred vendor for an item based on priority:
    /// 1. Primary vendor from VendorItem relationship
    /// 2. Item's PreferredVendor property
    /// 3. Last purchase vendor
    /// </summary>
    Task<Vendor?> GetPreferredVendorForItemAsync(int itemId);

    /// <summary>
    /// Gets the primary vendor from VendorItem relationships
    /// </summary>
    Task<Vendor?> GetPrimaryVendorForItemAsync(int itemId);

    /// <summary>
    /// Gets vendor selection info for bulk purchase with priority logic
    /// </summary>
    Task<VendorSelectionInfo> GetVendorSelectionInfoForItemAsync(int itemId);

    // NEW: Statistics and analytics methods
    Task<VendorAnalytics> GetVendorAnalyticsAsync();
    Task<IEnumerable<Vendor>> GetTopVendorsByPurchasesAsync(int count = 10);
    Task<IEnumerable<Vendor>> GetTopVendorsByRatingAsync(int count = 10);
    Task<IEnumerable<Vendor>> GetRecentlyAddedVendorsAsync(int count = 5);
  }

  // NEW: Supporting classes for analytics
  public class VendorAnalytics
  {
    public int TotalVendors { get; set; }
    public int ActiveVendors { get; set; }
    public int PreferredVendors { get; set; }
    public int InactiveVendors { get; set; }
    public int VendorsWithPurchases { get; set; }
    public int VendorsWithItems { get; set; }
    public decimal AverageRating { get; set; }
    public decimal TotalPurchaseValue { get; set; }
    public int HighlyRatedVendors { get; set; }
  }
}