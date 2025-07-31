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

      // Check if vendor has any purchases
      var hasPurchases = await _context.Purchases.AnyAsync(p => p.Vendor == vendor.CompanyName);
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
      return await _context.VendorItems
        .Include(vi => vi.Vendor)
        .Where(vi => vi.ItemId == itemId && vi.IsActive && vi.Vendor.IsActive)
        .OrderBy(vi => vi.IsPrimary ? 0 : 1)
        .ThenBy(vi => vi.UnitCost)
        .ToListAsync();
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
      return await _context.Vendors
        .Where(v => v.CompanyName.Contains(searchTerm) ||
                   v.ContactName.Contains(searchTerm) ||
                   v.VendorCode.Contains(searchTerm))
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
      var vendor = await GetVendorByIdAsync(vendorId);
      return vendor?.TotalPurchases ?? 0;
    }

    public async Task<IEnumerable<Purchase>> GetVendorPurchaseHistoryAsync(int vendorId)
    {
      var vendor = await GetVendorByIdAsync(vendorId);
      if (vendor == null) return new List<Purchase>();

      return await _context.Purchases
        .Include(p => p.Item)
        .Where(p => p.Vendor == vendor.CompanyName)
        .OrderByDescending(p => p.PurchaseDate)
        .ToListAsync();
    }
  }
}
