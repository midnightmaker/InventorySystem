using InventorySystem.Models;

namespace InventorySystem.Services
{
  public interface IPurchaseService
  {
    // Existing core methods
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

    // Version control methods
    Task<IEnumerable<Purchase>> GetPurchasesByItemVersionAsync(int itemId, string? version = null);
    Task<Dictionary<string, IEnumerable<Purchase>>> GetPurchasesGroupedByVersionAsync(int itemId);
    Task<IEnumerable<Purchase>> GetPurchasesByBaseItemIdAsync(int baseItemId);
    Task SetPurchaseItemVersionAsync(int purchaseId, string itemVersion);
    Task<IEnumerable<Purchase>> GetPurchasesForItemVersionsAsync(IEnumerable<int> itemIds);

    // Vendor-related methods
    Task<int?> GetLastVendorIdForItemAsync(int itemId);
    Task<IEnumerable<Vendor>> GetVendorsForItemAsync(int itemId);

    // Helper methods for cost calculations
    Task<decimal> GetAverageCostAsync(int itemId);
    Task<decimal> GetFifoValueAsync(int itemId);

    /// <summary>
    /// Generates a unique Purchase Order Number
    /// </summary>
    /// <returns>Generated purchase order number</returns>
    Task<string> GeneratePurchaseOrderNumberAsync();

    /// <summary>
    /// Gets all purchases for a specific purchase order number (grouped purchases)
    /// </summary>
    /// <param name="purchaseOrderNumber">The purchase order number</param>
    /// <returns>List of purchases with the same PO number</returns>
    Task<IEnumerable<Purchase>> GetPurchasesByOrderNumberAsync(string purchaseOrderNumber);

    /// <summary>
    /// Gets summary information for a purchase order (vendor grouping)
    /// </summary>
    /// <param name="purchaseOrderNumber">The purchase order number</param>
    /// <returns>Purchase order summary with totals</returns>
    Task<PurchaseOrderSummary> GetPurchaseOrderSummaryAsync(string purchaseOrderNumber);

    // ? NEW: Receiving workflow methods
    Task<Purchase> ReceivePurchaseAsync(int purchaseId, DateTime? receivedDate = null, 
        string? receivedBy = null, string? notes = null);
    Task<Purchase> CancelPurchaseAsync(int purchaseId, string reason, string? cancelledBy = null);

    // ? NEW: Purchase order management
    Task<IEnumerable<Purchase>> GetPendingPurchaseOrdersAsync();
    Task<IEnumerable<Purchase>> GetOverduePurchaseOrdersAsync();
  }
}