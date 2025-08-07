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

    // NEW: Enhanced filtering method for the index page
    public async Task<(IEnumerable<Vendor> vendors, int totalCount)> GetFilteredVendorsAsync(
        string? search = null,
        string? statusFilter = null,
        string? ratingFilter = null,
        string? locationFilter = null,
        string sortOrder = "companyName_asc",
        int page = 1,
        int pageSize = 25)
    {
      var query = _context.Vendors
          .Include(v => v.Purchases)
          .Include(v => v.VendorItems)
          .AsQueryable();

      // Apply search filter with wildcard support
      if (!string.IsNullOrWhiteSpace(search))
      {
        var searchTerm = search.Trim();
        if (searchTerm.Contains('*') || searchTerm.Contains('?'))
        {
          var likePattern = ConvertWildcardToLike(searchTerm);
          query = query.Where(v =>
            EF.Functions.Like(v.CompanyName, likePattern) ||
            (v.VendorCode != null && EF.Functions.Like(v.VendorCode, likePattern)) ||
            (v.ContactName != null && EF.Functions.Like(v.ContactName, likePattern)) ||
            (v.ContactEmail != null && EF.Functions.Like(v.ContactEmail, likePattern)) ||
            EF.Functions.Like(v.Id.ToString(), likePattern)
          );
        }
        else
        {
          query = query.Where(v =>
            v.CompanyName.Contains(searchTerm) ||
            (v.VendorCode != null && v.VendorCode.Contains(searchTerm)) ||
            (v.ContactName != null && v.ContactName.Contains(searchTerm)) ||
            (v.ContactEmail != null && v.ContactEmail.Contains(searchTerm)) ||
            v.Id.ToString().Contains(searchTerm)
          );
        }
      }

      // Apply status filter
      if (!string.IsNullOrWhiteSpace(statusFilter))
      {
        query = statusFilter switch
        {
          "active" => query.Where(v => v.IsActive),
          "inactive" => query.Where(v => !v.IsActive),
          "preferred" => query.Where(v => v.IsPreferred),
          "nonpreferred" => query.Where(v => !v.IsPreferred),
          "withpurchases" => query.Where(v => v.Purchases.Any()),
          "nopurchases" => query.Where(v => !v.Purchases.Any()),
          "withitems" => query.Where(v => v.VendorItems.Any()),
          "noitems" => query.Where(v => !v.VendorItems.Any()),
          _ => query
        };
      }

      // Apply rating filter
      if (!string.IsNullOrWhiteSpace(ratingFilter))
      {
        query = ratingFilter switch
        {
          "excellent" => query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 >= 4.5),
          "good" => query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 >= 3.5 && (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 < 4.5),
          "average" => query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 >= 2.5 && (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 < 3.5),
          "poor" => query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 < 2.5),
          "unrated" => query.Where(v => v.QualityRating == 0 || v.DeliveryRating == 0 || v.ServiceRating == 0),
          _ => query
        };
      }

      // Apply location filter
      if (!string.IsNullOrWhiteSpace(locationFilter))
      {
        if (locationFilter.Contains('*') || locationFilter.Contains('?'))
        {
          var likePattern = ConvertWildcardToLike(locationFilter);
          query = query.Where(v => 
            (v.City != null && EF.Functions.Like(v.City, likePattern)) ||
            (v.State != null && EF.Functions.Like(v.State, likePattern)) ||
            (v.Country != null && EF.Functions.Like(v.Country, likePattern)));
        }
        else
        {
          query = query.Where(v => 
            (v.City != null && v.City.Contains(locationFilter)) ||
            (v.State != null && v.State.Contains(locationFilter)) ||
            (v.Country != null && v.Country.Contains(locationFilter)));
        }
      }

      // Apply sorting
      query = sortOrder switch
      {
        "companyName_asc" => query.OrderBy(v => v.CompanyName),
        "companyName_desc" => query.OrderByDescending(v => v.CompanyName),
        "vendorCode_asc" => query.OrderBy(v => v.VendorCode ?? ""),
        "vendorCode_desc" => query.OrderByDescending(v => v.VendorCode ?? ""),
        "contact_asc" => query.OrderBy(v => v.ContactName ?? ""),
        "contact_desc" => query.OrderByDescending(v => v.ContactName ?? ""),
        "rating_desc" => query.OrderByDescending(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0),
        "rating_asc" => query.OrderBy(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0),
        "purchases_desc" => query.OrderByDescending(v => v.Purchases.Count()),
        "purchases_asc" => query.OrderBy(v => v.Purchases.Count()),
        "created_desc" => query.OrderByDescending(v => v.CreatedDate),
        "created_asc" => query.OrderBy(v => v.CreatedDate),
        "location_asc" => query.OrderBy(v => v.City ?? "").ThenBy(v => v.State ?? ""),
        "location_desc" => query.OrderByDescending(v => v.City ?? "").ThenByDescending(v => v.State ?? ""),
        _ => query.OrderBy(v => v.CompanyName)
      };

      var totalCount = await query.CountAsync();
      var vendors = await query
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync();

      return (vendors, totalCount);
    }

    public async Task<IEnumerable<Vendor>> GetVendorsByStatusAsync(string statusFilter)
    {
      var query = _context.Vendors
          .Include(v => v.Purchases)
          .Include(v => v.VendorItems)
          .AsQueryable();

      return statusFilter switch
      {
        "active" => await query.Where(v => v.IsActive).ToListAsync(),
        "inactive" => await query.Where(v => !v.IsActive).ToListAsync(),
        "preferred" => await query.Where(v => v.IsPreferred).ToListAsync(),
        "withpurchases" => await query.Where(v => v.Purchases.Any()).ToListAsync(),
        "nopurchases" => await query.Where(v => !v.Purchases.Any()).ToListAsync(),
        "withitems" => await query.Where(v => v.VendorItems.Any()).ToListAsync(),
        "noitems" => await query.Where(v => !v.VendorItems.Any()).ToListAsync(),
        _ => await query.ToListAsync()
      };
    }

    public async Task<IEnumerable<Vendor>> GetVendorsByRatingAsync(string ratingFilter)
    {
      var query = _context.Vendors
          .Include(v => v.Purchases)
          .Include(v => v.VendorItems)
          .AsQueryable();

      return ratingFilter switch
      {
        "excellent" => await query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 >= 4.5).ToListAsync(),
        "good" => await query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 >= 3.5 && (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 < 4.5).ToListAsync(),
        "average" => await query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 >= 2.5 && (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 < 3.5).ToListAsync(),
        "poor" => await query.Where(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0 < 2.5).ToListAsync(),
        "unrated" => await query.Where(v => v.QualityRating == 0 || v.DeliveryRating == 0 || v.ServiceRating == 0).ToListAsync(),
        _ => await query.ToListAsync()
      };
    }

    public async Task<IEnumerable<Vendor>> GetVendorsByLocationAsync(string locationFilter)
    {
      var query = _context.Vendors
          .Include(v => v.Purchases)
          .Include(v => v.VendorItems)
          .AsQueryable();

      if (locationFilter.Contains('*') || locationFilter.Contains('?'))
      {
        var likePattern = ConvertWildcardToLike(locationFilter);
        return await query.Where(v => 
          (v.City != null && EF.Functions.Like(v.City, likePattern)) ||
          (v.State != null && EF.Functions.Like(v.State, likePattern)) ||
          (v.Country != null && EF.Functions.Like(v.Country, likePattern))
        ).ToListAsync();
      }
      else
      {
        return await query.Where(v => 
          (v.City != null && v.City.Contains(locationFilter)) ||
          (v.State != null && v.State.Contains(locationFilter)) ||
          (v.Country != null && v.Country.Contains(locationFilter))
        ).ToListAsync();
      }
    }

    // NEW: Analytics and statistics methods
    public async Task<VendorAnalytics> GetVendorAnalyticsAsync()
    {
      var vendors = await _context.Vendors
          .Include(v => v.Purchases)
          .Include(v => v.VendorItems)
          .ToListAsync();

      return new VendorAnalytics
      {
        TotalVendors = vendors.Count,
        ActiveVendors = vendors.Count(v => v.IsActive),
        PreferredVendors = vendors.Count(v => v.IsPreferred),
        InactiveVendors = vendors.Count(v => !v.IsActive),
        VendorsWithPurchases = vendors.Count(v => v.Purchases.Any()),
        VendorsWithItems = vendors.Count(v => v.VendorItems.Any()),
        AverageRating = vendors.Any() ? vendors.Average(v => v.OverallRating) : 0,
        TotalPurchaseValue = vendors.Sum(v => v.TotalPurchases),
        HighlyRatedVendors = vendors.Count(v => v.OverallRating >= 4.5m)
      };
    }

    public async Task<IEnumerable<Vendor>> GetTopVendorsByPurchasesAsync(int count = 10)
    {
      return await _context.Vendors
          .Include(v => v.Purchases)
          .Include(v => v.VendorItems)
          .OrderByDescending(v => v.Purchases.Sum(p => p.TotalCost))
          .Take(count)
          .ToListAsync();
    }

    public async Task<IEnumerable<Vendor>> GetTopVendorsByRatingAsync(int count = 10)
    {
      return await _context.Vendors
          .Include(v => v.Purchases)
          .Include(v => v.VendorItems)
          .Where(v => v.IsActive)
          .OrderByDescending(v => (v.QualityRating + v.DeliveryRating + v.ServiceRating) / 3.0)
          .Take(count)
          .ToListAsync();
    }

    public async Task<IEnumerable<Vendor>> GetRecentlyAddedVendorsAsync(int count = 5)
    {
      return await _context.Vendors
          .Include(v => v.Purchases)
          .Include(v => v.VendorItems)
          .OrderByDescending(v => v.CreatedDate)
          .Take(count)
          .ToListAsync();
    }

    /// <summary>
    /// Converts wildcard patterns (* and ?) to SQL LIKE patterns
    /// * matches any sequence of characters -> %
    /// ? matches any single character -> _
    /// </summary>
    /// <param name="wildcardPattern">The wildcard pattern to convert</param>
    /// <returns>A SQL LIKE pattern string</returns>
    private string ConvertWildcardToLike(string wildcardPattern)
    {
      // Escape existing SQL LIKE special characters first
      var escaped = wildcardPattern
          .Replace("%", "[%]")    // Escape existing % characters
          .Replace("_", "[_]")    // Escape existing _ characters
          .Replace("[", "[[]");   // Escape existing [ characters

      // Convert wildcards to SQL LIKE patterns
      escaped = escaped
          .Replace("*", "%")      // * becomes %
          .Replace("?", "_");     // ? becomes _

      return escaped;
    }
  }

}
