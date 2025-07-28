// Services/VersionControlService.cs
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
  public class VersionControlService : IVersionControlService
  {
    private readonly InventoryContext _context;

    public VersionControlService(InventoryContext context)
    {
      _context = context;
    }

    #region Item Version Management

    public async Task<Item> CreateItemVersionAsync(int baseItemId, string newVersion, int changeOrderId)
    {
      var baseItem = await _context.Items.FindAsync(baseItemId);
      if (baseItem == null)
      {
        throw new InvalidOperationException("Base item not found");
      }

      // Check if version already exists
      var existingVersion = await GetItemVersionAsync(baseItemId, newVersion);
      if (existingVersion != null)
      {
        throw new InvalidOperationException($"Version {newVersion} already exists for this item");
      }

      // Mark all existing versions as not current
      var existingVersions = await GetItemVersionsAsync(baseItemId);
      foreach (var version in existingVersions)
      {
        version.IsCurrentVersion = false;
      }

      // Create new version (copy from base item but with no design documents)
      var newItem = new Item
      {
        PartNumber = baseItem.PartNumber,
        Description = baseItem.Description,
        Version = newVersion,
        ItemType = baseItem.ItemType,
        MinimumStock = baseItem.MinimumStock,
        Comments = baseItem.Comments,
        BaseItemId = baseItem.BaseItemId ?? baseItemId,
        IsCurrentVersion = true,
        CreatedFromChangeOrderId = changeOrderId,
        CreatedDate = DateTime.Now,
        CurrentStock = 0 // New versions start with no stock
                         // DesignDocuments collection starts empty as required
      };

      _context.Items.Add(newItem);
      await _context.SaveChangesAsync();

      return newItem;
    }

    public async Task<IEnumerable<Item>> GetItemVersionsAsync(int baseItemId)
    {
      var actualBaseId = await GetActualBaseItemIdAsync("Item", baseItemId);

      return await _context.Items
          .Where(i => i.Id == actualBaseId || i.BaseItemId == actualBaseId)
          .Include(i => i.DesignDocuments)
          .Include(i => i.CreatedFromChangeOrder)
          .OrderByDescending(i => i.IsCurrentVersion)
          .ThenBy(i => i.Version)
          .ToListAsync();
    }

    public async Task<Item?> GetItemVersionAsync(int baseItemId, string version)
    {
      var actualBaseId = await GetActualBaseItemIdAsync("Item", baseItemId);

      return await _context.Items
          .Include(i => i.DesignDocuments)
          .Include(i => i.Purchases)
          .Include(i => i.CreatedFromChangeOrder)
          .FirstOrDefaultAsync(i => (i.Id == actualBaseId || i.BaseItemId == actualBaseId) && i.Version == version);
    }

    public async Task<Item?> GetCurrentItemVersionAsync(int baseItemId)
    {
      var actualBaseId = await GetActualBaseItemIdAsync("Item", baseItemId);

      return await _context.Items
          .Include(i => i.DesignDocuments)
          .Include(i => i.Purchases)
          .Include(i => i.CreatedFromChangeOrder)
          .FirstOrDefaultAsync(i => (i.Id == actualBaseId || i.BaseItemId == actualBaseId) && i.IsCurrentVersion);
    }

    #endregion

    #region BOM Version Management

    public async Task<Bom> CreateBomVersionAsync(int baseBomId, string newVersion, int changeOrderId)
    {
      var baseBom = await _context.Boms.FindAsync(baseBomId);
      if (baseBom == null)
      {
        throw new InvalidOperationException("Base BOM not found");
      }

      // Check if version already exists
      var existingVersion = await GetBomVersionAsync(baseBomId, newVersion);
      if (existingVersion != null)
      {
        throw new InvalidOperationException($"Version {newVersion} already exists for this BOM");
      }

      // Mark all existing versions as not current
      var existingVersions = await GetBomVersionsAsync(baseBomId);
      foreach (var version in existingVersions)
      {
        version.IsCurrentVersion = false;
      }

      // Create new version (copy from base BOM but with no components)
      var newBom = new Bom
      {
        Name = baseBom.Name,
        Description = baseBom.Description,
        Version = newVersion,
        AssemblyPartNumber = baseBom.AssemblyPartNumber,
        ParentBomId = baseBom.ParentBomId,
        BaseBomId = baseBom.BaseBomId ?? baseBomId,
        IsCurrentVersion = true,
        CreatedFromChangeOrderId = changeOrderId,
        CreatedDate = DateTime.Now,
        ModifiedDate = DateTime.Now
        // BomItems collection starts empty as required
      };

      _context.Boms.Add(newBom);
      await _context.SaveChangesAsync();

      return newBom;
    }

    public async Task<IEnumerable<Bom>> GetBomVersionsAsync(int baseBomId)
    {
      var actualBaseId = await GetActualBaseItemIdAsync("BOM", baseBomId);

      return await _context.Boms
          .Where(b => b.Id == actualBaseId || b.BaseBomId == actualBaseId)
          .Include(b => b.BomItems)
          .ThenInclude(bi => bi.Item)
          .Include(b => b.CreatedFromChangeOrder)
          .OrderByDescending(b => b.IsCurrentVersion)
          .ThenBy(b => b.Version)
          .ToListAsync();
    }

    public async Task<Bom?> GetBomVersionAsync(int baseBomId, string version)
    {
      var actualBaseId = await GetActualBaseItemIdAsync("BOM", baseBomId);

      return await _context.Boms
          .Include(b => b.BomItems)
          .ThenInclude(bi => bi.Item)
          .Include(b => b.SubAssemblies)
          .Include(b => b.CreatedFromChangeOrder)
          .FirstOrDefaultAsync(b => (b.Id == actualBaseId || b.BaseBomId == actualBaseId) && b.Version == version);
    }

    public async Task<Bom?> GetCurrentBomVersionAsync(int baseBomId)
    {
      var actualBaseId = await GetActualBaseItemIdAsync("BOM", baseBomId);

      return await _context.Boms
          .Include(b => b.BomItems)
          .ThenInclude(bi => bi.Item)
          .Include(b => b.SubAssemblies)
          .Include(b => b.CreatedFromChangeOrder)
          .FirstOrDefaultAsync(b => (b.Id == actualBaseId || b.BaseBomId == actualBaseId) && b.IsCurrentVersion);
    }

    #endregion

    #region Change Order Management
    public async Task<bool> CancelChangeOrderAsync(int changeOrderId, string cancelledBy)
    {
      var changeOrder = await _context.ChangeOrders.FindAsync(changeOrderId);
      if (changeOrder == null || changeOrder.Status != "Pending")
      {
        return false;
      }

      changeOrder.Status = "Cancelled";
      changeOrder.ImplementedBy = cancelledBy;
      changeOrder.ImplementedDate = DateTime.Now;

      await _context.SaveChangesAsync();
      return true;
    }// Services/IVersionControlService.cs

    public async Task<ChangeOrder> CreateChangeOrderAsync(ChangeOrder changeOrder)
    {
      // Validate that new version is greater than previous version
      await ValidateVersionProgressionAsync(changeOrder.EntityType, changeOrder.BaseEntityId, changeOrder.NewVersion);

      // Generate change order number
      changeOrder.ChangeOrderNumber = await GenerateChangeOrderNumberAsync(changeOrder.EntityType, changeOrder.NewVersion);
      changeOrder.CreatedDate = DateTime.Now;
      changeOrder.Status = "Pending";

      _context.ChangeOrders.Add(changeOrder);
      await _context.SaveChangesAsync();

      return changeOrder;
    }

    public async Task<bool> ImplementChangeOrderAsync(int changeOrderId, string implementedBy)
    {
      var changeOrder = await _context.ChangeOrders.FindAsync(changeOrderId);
      if (changeOrder == null || changeOrder.Status != "Pending")
      {
        return false;
      }

      try
      {
        // Create the new version based on the change order
        if (changeOrder.EntityType == "Item")
        {
          await CreateItemVersionAsync(changeOrder.BaseEntityId, changeOrder.NewVersion, changeOrderId);
        }
        else if (changeOrder.EntityType == "BOM")
        {
          await CreateBomVersionAsync(changeOrder.BaseEntityId, changeOrder.NewVersion, changeOrderId);
        }

        // Mark change order as implemented
        changeOrder.Status = "Implemented";
        changeOrder.ImplementedBy = implementedBy;
        changeOrder.ImplementedDate = DateTime.Now;

        await _context.SaveChangesAsync();
        return true;
      }
      catch (Exception)
      {
        // If version creation fails, mark change order as failed
        changeOrder.Status = "Failed";
        await _context.SaveChangesAsync();
        return false;
      }
    }

    
    public async Task<IEnumerable<ChangeOrder>> GetAllChangeOrdersAsync()
    {
      return await _context.ChangeOrders
          .Include(co => co.RelatedItem)
          .Include(co => co.RelatedBom)
          .OrderByDescending(co => co.CreatedDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<ChangeOrder>> GetPendingChangeOrdersAsync()
    {
      return await _context.ChangeOrders
          .Include(co => co.RelatedItem)
          .Include(co => co.RelatedBom)
          .Where(co => co.Status == "Pending")
          .OrderByDescending(co => co.CreatedDate)
          .ToListAsync();
    }

    public async Task<IEnumerable<ChangeOrder>> GetChangeOrdersByEntityAsync(string entityType, int entityId)
    {
      return await _context.ChangeOrders
          .Include(co => co.RelatedItem)
          .Include(co => co.RelatedBom)
          .Where(co => co.EntityType == entityType && co.BaseEntityId == entityId)
          .OrderByDescending(co => co.CreatedDate)
          .ToListAsync();
    }

    public async Task<ChangeOrder?> GetChangeOrderByIdAsync(int changeOrderId)
    {
      return await _context.ChangeOrders
          .Include(co => co.RelatedItem)
          .Include(co => co.RelatedBom)
          .FirstOrDefaultAsync(co => co.Id == changeOrderId);
    }

    #endregion

    #region Helper Methods

    private async Task<int> GetActualBaseItemIdAsync(string entityType, int entityId)
    {
      if (entityType == "Item")
      {
        var item = await _context.Items.FindAsync(entityId);
        return item?.BaseItemId ?? entityId;
      }
      else if (entityType == "BOM")
      {
        var bom = await _context.Boms.FindAsync(entityId);
        return bom?.BaseBomId ?? entityId;
      }

      return entityId;
    }

    private async Task<string> GenerateChangeOrderNumberAsync(string entityType, string version)
    {
      var today = DateTime.Now;
      var prefix = entityType == "Item" ? "ICO" : "BCO"; // Item Change Order / BOM Change Order
      var dateStr = today.ToString("yyyyMMdd");

      // Find the next sequential number for today
      var existingCount = await _context.ChangeOrders
          .CountAsync(co => co.CreatedDate.Date == today.Date && co.EntityType == entityType);

      var sequence = (existingCount + 1).ToString("D3");

      return $"{prefix}-{dateStr}-{sequence}-{version}";
    }

    private async Task ValidateVersionProgressionAsync(string entityType, int entityId, string newVersion)
    {
      string? currentVersion = null;

      // Get current version
      if (entityType == "Item")
      {
        var currentItem = await GetCurrentItemVersionAsync(entityId);
        currentVersion = currentItem?.Version;
      }
      else if (entityType == "BOM")
      {
        var currentBom = await GetCurrentBomVersionAsync(entityId);
        currentVersion = currentBom?.Version;
      }

      if (string.IsNullOrEmpty(currentVersion))
      {
        // No current version, any version is valid for new entities
        return;
      }

      // Check if version already exists
      var existingVersions = entityType == "Item"
          ? (await GetItemVersionsAsync(entityId)).Select(v => v.Version)
          : (await GetBomVersionsAsync(entityId)).Select(v => v.Version);

      if (existingVersions.Contains(newVersion))
      {
        throw new InvalidOperationException($"Version {newVersion} already exists. Please choose a different version number.");
      }

      // Validate version progression
      if (!IsVersionGreater(newVersion, currentVersion))
      {
        throw new InvalidOperationException($"New version '{newVersion}' must be greater than current version '{currentVersion}'. " +
            "Examples: A → B, 1.0 → 1.1, 1.9 → 2.0");
      }
    }

    private bool IsVersionGreater(string newVersion, string currentVersion)
    {
      // Handle numeric versions (1.0, 1.1, 2.0, etc.)
      if (IsNumericVersion(newVersion) && IsNumericVersion(currentVersion))
      {
        if (Version.TryParse(PadVersionString(newVersion), out var newVer) &&
            Version.TryParse(PadVersionString(currentVersion), out var currentVer))
        {
          return newVer > currentVer;
        }
      }

      // Handle alphabetic versions (A, B, C, etc.)
      if (IsAlphabeticVersion(newVersion) && IsAlphabeticVersion(currentVersion))
      {
        return string.Compare(newVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
      }

      // Handle mixed or complex versions - use string comparison as fallback
      // This allows for custom versioning schemes
      return string.Compare(newVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private bool IsNumericVersion(string version)
    {
      // Check if version follows numeric pattern like 1.0, 1.1, 2.0, etc.
      return System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+(\.\d+)*$");
    }

    private bool IsAlphabeticVersion(string version)
    {
      // Check if version is single letter or simple alphabetic
      return System.Text.RegularExpressions.Regex.IsMatch(version, @"^[A-Za-z]+$");
    }

    private string PadVersionString(string version)
    {
      // Ensure version has at least major.minor format for Version.Parse
      var parts = version.Split('.');
      if (parts.Length == 1)
      {
        return version + ".0";
      }
      return version;
    }

    #endregion
  }
}