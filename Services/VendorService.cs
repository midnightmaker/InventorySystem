using InventorySystem.Data;
using InventorySystem.Models;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
  public class VendorService : IVendorService
  {
    private readonly InventoryContext _context;
    private readonly ILogger<VendorService> _logger;

    public VendorService(InventoryContext context, ILogger<VendorService> logger)
    {
      _context = context;
      _logger = logger;
    }

    public async Task<IEnumerable<Vendor>> GetAllVendorsAsync()
    {
      return await _context.Vendors
        .Include(v => v.Purchases)
        .Include(v => v.VendorItems)
        .OrderBy(v => v.CompanyName)
        .ToListAsync();
    }

    public async Task<IEnumerable<Vendor>> GetActiveVendorsAsync()
    {
      return await _context.Vendors
        .Where(v => v.IsActive)
        .Include(v => v.Purchases)
        .Include(v => v.VendorItems)
        .OrderBy(v => v.CompanyName)
        .ToListAsync();
    }

    public async Task<IEnumerable<Vendor>> GetPreferredVendorsAsync()
    {
      return await _context.Vendors
        .Where(v => v.IsActive && v.IsPreferred)
        .Include(v => v.Purchases)
        .Include(v => v.VendorItems)
        .OrderBy(v => v.CompanyName)
        .ToListAsync();
    }

    public async Task<Vendor?> GetVendorByIdAsync(int id)
    {
      return await _context.Vendors
        .Include(v => v.Purchases)
          .ThenInclude(p => p.Item)
        .Include(v => v.VendorItems)
          .ThenInclude(vi => vi.Item)
        .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<Vendor?> GetVendorByNameAsync(string companyName)
    {
      return await _context.Vendors
        .FirstOrDefaultAsync(v => v.CompanyName.ToLower() == companyName.ToLower());
    }

    public async Task<Vendor> CreateVendorAsync(Vendor vendor)
    {
      vendor.CreatedDate = DateTime.Now;
      vendor.LastUpdated = DateTime.Now;

      _context.Vendors.Add(vendor);
      await _context.SaveChangesAsync();

      _logger.LogInformation("Created vendor: {VendorName} (ID: {VendorId})", vendor.CompanyName, vendor.Id);
      return vendor;
    }

    public async Task<Vendor> UpdateVendorAsync(Vendor vendor)
    {
      vendor.LastUpdated = DateTime.Now;

      _context.Vendors.Update(vendor);
      await _context.SaveChangesAsync();

      _logger.LogInformation("Updated vendor: {VendorName} (ID: {VendorId})", vendor.CompanyName, vendor.Id);
      return vendor;
    }

    public async Task<bool> DeleteVendorAsync(int id)
    {
      var vendor = await _context.Vendors.FindAsync(id);
      if (vendor == null) return false;

      // Check if vendor has any purchases - FIXED to use VendorId instead of Vendor string
      var hasPurchases = await _context.Purchases.AnyAsync(p => p.VendorId == id);
      if (hasPurchases)
      {
        throw new InvalidOperationException("Cannot delete vendor with existing purchases. Consider deactivating instead.");
      }

      _context.Vendors.Remove(vendor);
      await _context.SaveChangesAsync();

      _logger.LogInformation("Deleted vendor: {VendorName} (ID: {VendorId})", vendor.CompanyName, id);
      return true;
    }


    public async Task<bool> DeactivateVendorAsync(int id)
    {
      var vendor = await _context.Vendors.FindAsync(id);
      if (vendor == null) return false;

      vendor.IsActive = false;
      vendor.LastUpdated = DateTime.Now;
      await _context.SaveChangesAsync();

      _logger.LogInformation("Deactivated vendor: {VendorName} (ID: {VendorId})", vendor.CompanyName, id);
      return true;
    }

    public async Task<bool> ActivateVendorAsync(int id)
    {
      var vendor = await _context.Vendors.FindAsync(id);
      if (vendor == null) return false;

      vendor.IsActive = true;
      vendor.LastUpdated = DateTime.Now;
      await _context.SaveChangesAsync();

      _logger.LogInformation("Activated vendor: {VendorName} (ID: {VendorId})", vendor.CompanyName, id);
      return true;
    }

    // Vendor-Item relationship methods
    public async Task<IEnumerable<VendorItem>> GetVendorItemsAsync(int vendorId)
    {
      return await _context.VendorItems
        .Include(vi => vi.Item)
        .Where(vi => vi.VendorId == vendorId && vi.IsActive)
        .OrderBy(vi => vi.Item.PartNumber)
        .ToListAsync();
    }

    public async Task<IEnumerable<VendorItem>> GetItemVendorsAsync(int itemId)
    {
      // First fetch the data without decimal ordering
      var vendorItems = await _context.VendorItems
        .Include(vi => vi.Vendor)
        .Where(vi => vi.ItemId == itemId && vi.IsActive && vi.Vendor.IsActive)
        .ToListAsync();

      // Then order by computed properties in memory
      return vendorItems
        .OrderBy(vi => vi.IsPrimary ? 0 : 1)
        .ThenBy(vi => vi.UnitCost);
    }

    public async Task<VendorItem?> GetVendorItemAsync(int vendorId, int itemId)
    {
      return await _context.VendorItems
        .Include(vi => vi.Vendor)
        .Include(vi => vi.Item)
        .FirstOrDefaultAsync(vi => vi.VendorId == vendorId && vi.ItemId == itemId);
    }

    public async Task<VendorItem> CreateVendorItemAsync(VendorItem vendorItem)
    {
      vendorItem.LastUpdated = DateTime.Now;

      _context.VendorItems.Add(vendorItem);
      await _context.SaveChangesAsync();

      return vendorItem;
    }

    public async Task<VendorItem> UpdateVendorItemAsync(VendorItem vendorItem)
    {
      vendorItem.LastUpdated = DateTime.Now;

      _context.VendorItems.Update(vendorItem);
      await _context.SaveChangesAsync();

      return vendorItem;
    }

    public async Task<bool> DeleteVendorItemAsync(int vendorId, int itemId)
    {
      var vendorItem = await GetVendorItemAsync(vendorId, itemId);
      if (vendorItem == null) return false;

      _context.VendorItems.Remove(vendorItem);
      await _context.SaveChangesAsync();
      return true;
    }

    // Business logic methods
    public async Task<IEnumerable<Vendor>> SearchVendorsAsync(string searchTerm)
    {
      if (string.IsNullOrWhiteSpace(searchTerm))
      {
        return await GetActiveVendorsAsync();
      }

      // Convert wildcard pattern to SQL LIKE pattern
      var likePattern = ConvertWildcardToLikePattern(searchTerm.Trim());

      // Use EF.Functions.Like for proper wildcard support
      return await _context.Vendors
          .Where(v => EF.Functions.Like(v.CompanyName, likePattern) ||
                     EF.Functions.Like(v.ContactName ?? "", likePattern) ||
                     EF.Functions.Like(v.VendorCode ?? "", likePattern) ||
                     EF.Functions.Like(v.ContactEmail ?? "", likePattern))
          .OrderBy(v => v.CompanyName)
          .ToListAsync();
    }

    // Add this helper method:
    private string ConvertWildcardToLikePattern(string searchTerm)
    {
      if (string.IsNullOrEmpty(searchTerm))
        return "%";

      // Replace user wildcards with SQL LIKE wildcards
      // * = zero or more characters (becomes %)
      // ? = single character (becomes _)
      var pattern = searchTerm
          .Replace("%", "\\%")      // Escape existing % 
          .Replace("_", "\\_")      // Escape existing _
          .Replace("*", "%")        // Convert * to %
          .Replace("?", "_");       // Convert ? to _

      // If no wildcards were provided, add % at the end for "starts with" behavior
      if (!pattern.Contains("%") && !pattern.Contains("_"))
      {
        pattern += "%";
      }

      return pattern;
    }

    // Add advanced search method for more complex scenarios:
    public async Task<IEnumerable<Vendor>> AdvancedSearchVendorsAsync(
        string? companyName = null,
        string? vendorCode = null,
        string? contactName = null,
        string? contactEmail = null,
        bool? isActive = null,
        bool? isPreferred = null)
    {
      var query = _context.Vendors.AsQueryable();

      if (!string.IsNullOrWhiteSpace(companyName))
      {
        var pattern = ConvertWildcardToLikePattern(companyName);
        query = query.Where(v => EF.Functions.Like(v.CompanyName, pattern));
      }

      if (!string.IsNullOrWhiteSpace(vendorCode))
      {
        var pattern = ConvertWildcardToLikePattern(vendorCode);
        query = query.Where(v => EF.Functions.Like(v.VendorCode ?? "", pattern));
      }

      if (!string.IsNullOrWhiteSpace(contactName))
      {
        var pattern = ConvertWildcardToLikePattern(contactName);
        query = query.Where(v => EF.Functions.Like(v.ContactName ?? "", pattern));
      }

      if (!string.IsNullOrWhiteSpace(contactEmail))
      {
        var pattern = ConvertWildcardToLikePattern(contactEmail);
        query = query.Where(v => EF.Functions.Like(v.ContactEmail ?? "", pattern));
      }

      if (isActive.HasValue)
      {
        query = query.Where(v => v.IsActive == isActive.Value);
      }

      if (isPreferred.HasValue)
      {
        query = query.Where(v => v.IsPreferred == isPreferred.Value);
      }

      return await query
          .OrderBy(v => v.CompanyName)
          .ToListAsync();
    }

    public async Task<IEnumerable<VendorItem>> GetCheapestVendorsForItemAsync(int itemId)
    {
      return await _context.VendorItems
        .Include(vi => vi.Vendor)
        .Where(vi => vi.ItemId == itemId && vi.IsActive && vi.Vendor.IsActive)
        .OrderBy(vi => vi.UnitCost)
        .Take(5)
        .ToListAsync();
    }

    public async Task<IEnumerable<VendorItem>> GetFastestVendorsForItemAsync(int itemId)
    {
      return await _context.VendorItems
        .Include(vi => vi.Vendor)
        .Where(vi => vi.ItemId == itemId && vi.IsActive && vi.Vendor.IsActive)
        .OrderBy(vi => vi.LeadTimeDays)
        .Take(5)
        .ToListAsync();
    }

    public async Task UpdateVendorItemLastPurchaseAsync(int vendorId, int itemId, decimal cost, DateTime purchaseDate)
    {
      var vendorItem = await GetVendorItemAsync(vendorId, itemId);
      if (vendorItem != null)
      {
        vendorItem.LastPurchaseDate = purchaseDate;
        vendorItem.LastPurchaseCost = cost;
        await UpdateVendorItemAsync(vendorItem);
      }
    }

    public async Task<decimal> GetVendorTotalPurchasesAsync(int vendorId)
    {

      var purchases = await _context.Purchases
      .Where(p => p.VendorId == vendorId)
      .Select(p => new { p.QuantityPurchased, p.CostPerUnit, p.ShippingCost, p.TaxAmount })
      .ToListAsync();

      return purchases.Sum(p => (p.QuantityPurchased * p.CostPerUnit) + p.ShippingCost + p.TaxAmount);
    }

    public async Task<IEnumerable<Purchase>> GetVendorPurchaseHistoryAsync(int vendorId)
    {
      // FIXED to use VendorId instead of vendor name comparison  
      return await _context.Purchases
        .Include(p => p.Item)
        .Include(p => p.Vendor)
        .Where(p => p.VendorId == vendorId)
        .OrderByDescending(p => p.PurchaseDate)
        .ToListAsync();
    }
    public async Task<Vendor?> GetPrimaryVendorForItemAsync(int itemId)
    {
      var primaryVendorItem = await _context.VendorItems
        .Include(vi => vi.Vendor)
        .Where(vi => vi.ItemId == itemId && vi.IsPrimary && vi.IsActive && vi.Vendor.IsActive)
        .FirstOrDefaultAsync();

      return primaryVendorItem?.Vendor;
    }

    public async Task<Vendor?> GetPreferredVendorForItemAsync(int itemId)
    {
      // 1. First priority: Primary vendor from VendorItem relationship
      var primaryVendor = await GetPrimaryVendorForItemAsync(itemId);
      if (primaryVendor != null)
      {
        return primaryVendor;
      }

      // 2. Second priority: Item's PreferredVendor property
      var item = await _context.Items.FindAsync(itemId);
      if (!string.IsNullOrWhiteSpace(item?.PreferredVendor))
      {
        var preferredVendor = await GetVendorByNameAsync(item.PreferredVendor);
        if (preferredVendor?.IsActive == true)
        {
          return preferredVendor;
        }
      }

      // 3. Third priority: Last purchase vendor
      var lastPurchase = await _context.Purchases
        .Include(p => p.Vendor)
        .Where(p => p.ItemId == itemId)
        .OrderByDescending(p => p.PurchaseDate)
        .ThenByDescending(p => p.CreatedDate)
        .FirstOrDefaultAsync();

      if (lastPurchase?.Vendor?.IsActive == true)
      {
        return lastPurchase.Vendor;
      }

      return null;
    }

    public async Task<VendorSelectionInfo> GetVendorSelectionInfoForItemAsync(int itemId)
    {
      var info = new VendorSelectionInfo
      {
        ItemId = itemId
      };

      // Get primary vendor from VendorItem relationship
      var primaryVendorItem = await _context.VendorItems
        .Include(vi => vi.Vendor)
        .Where(vi => vi.ItemId == itemId && vi.IsPrimary && vi.IsActive && vi.Vendor.IsActive)
        .FirstOrDefaultAsync();

      if (primaryVendorItem != null)
      {
        info.PrimaryVendor = primaryVendorItem.Vendor;
        info.PrimaryVendorCost = primaryVendorItem.UnitCost;
      }

      // Get item's preferred vendor property
      var item = await _context.Items.FindAsync(itemId);
      if (!string.IsNullOrWhiteSpace(item?.PreferredVendor))
      {
        var preferredVendor = await GetVendorByNameAsync(item.PreferredVendor);
        if (preferredVendor?.IsActive == true)
        {
          info.ItemPreferredVendor = preferredVendor;
          info.ItemPreferredVendorName = item.PreferredVendor;
        }
      }

      // Get last purchase vendor
      var lastPurchase = await _context.Purchases
        .Include(p => p.Vendor)
        .Where(p => p.ItemId == itemId)
        .OrderByDescending(p => p.PurchaseDate)
        .ThenByDescending(p => p.CreatedDate)
        .FirstOrDefaultAsync();

      if (lastPurchase != null)
      {
        info.LastPurchaseVendor = lastPurchase.Vendor;
        info.LastPurchaseDate = lastPurchase.PurchaseDate;
        info.LastPurchaseCost = lastPurchase.CostPerUnit;
      }

      // Determine the recommended vendor based on priority
      info.RecommendedVendor = info.PrimaryVendor ?? info.ItemPreferredVendor ?? info.LastPurchaseVendor;
      info.RecommendedCost = info.PrimaryVendorCost ?? info.LastPurchaseCost ?? 0;

      // Set selection reason
      if (info.PrimaryVendor != null)
      {
        info.SelectionReason = "Primary vendor (VendorItem relationship)";
      }
      else if (info.ItemPreferredVendor != null)
      {
        info.SelectionReason = "Item's preferred vendor";
      }
      else if (info.LastPurchaseVendor != null)
      {
        info.SelectionReason = "Last purchase vendor";
      }
      else
      {
        info.SelectionReason = "No preferred vendor found";
      }

      return info;
    }
  }

}
