using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;

namespace InventorySystem.Services
{
  public class VersionControlService : IVersionControlService
  {
    private readonly InventoryContext _context;
    private readonly ILogger<VersionControlService> _logger;

    public VersionControlService(InventoryContext context, ILogger<VersionControlService> logger)
    {
      _context = context;
      _logger = logger;
    }

    #region Item Version Management

    public async Task<Item> CreateItemVersionAsync(int baseItemId, string newVersion, int changeOrderId)
    {
      var baseItem = await _context.Items
          .Include(i => i.DesignDocuments)
          .FirstOrDefaultAsync(i => i.Id == baseItemId);

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

      // Create new version (copy from base item but with no stock or documents)
      var newItem = new Item
      {
        PartNumber = baseItem.PartNumber,
        Description = baseItem.Description,
        ItemType = baseItem.ItemType,
        Version = newVersion,
        VendorPartNumber = baseItem.VendorPartNumber,
        Comments = baseItem.Comments,
        MinimumStock = baseItem.MinimumStock,
        IsSellable = baseItem.IsSellable,
        BaseItemId = baseItem.BaseItemId ?? baseItemId,
        IsCurrentVersion = true,
        CreatedFromChangeOrderId = changeOrderId,
        CreatedDate = DateTime.Now,
        CurrentStock = 0 // New versions start with no stock
                         // DesignDocuments collection starts empty as required
      };

      _context.Items.Add(newItem);
      await _context.SaveChangesAsync();

      _logger.LogInformation("Created new item version {Version} for item {PartNumber} (ID: {ItemId})",
          newVersion, baseItem.PartNumber, newItem.Id);

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
      // Load the base BOM with ALL related data that needs to be transferred
      var baseBom = await _context.Boms
          .Include(b => b.BomItems)
          .ThenInclude(bi => bi.Item)
          .Include(b => b.SubAssemblies)
          .Include(b => b.Documents) // Include BOM documents
          .FirstOrDefaultAsync(b => b.Id == baseBomId);

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

      // ✅ CREATE NEW VERSION WITH ALL TRANSFERRED DATA
      var newBom = new Bom
      {
        BomNumber = baseBom.BomNumber,
        Description = baseBom.Description,
        Version = newVersion,
        AssemblyPartNumber = baseBom.AssemblyPartNumber,
        ParentBomId = baseBom.ParentBomId,
        BaseBomId = baseBom.BaseBomId ?? baseBomId,
        IsCurrentVersion = true,
        CreatedFromChangeOrderId = changeOrderId,
        CreatedDate = DateTime.Now,
        ModifiedDate = DateTime.Now
      };

      _context.Boms.Add(newBom);
      await _context.SaveChangesAsync(); // Save to get the new BOM ID

      // ✅ TRANSFER ALL BOM ITEMS FROM CURRENT VERSION
      if (baseBom.BomItems?.Any() == true)
      {
        foreach (var originalBomItem in baseBom.BomItems)
        {
          var newBomItem = new BomItem
          {
            BomId = newBom.Id, // Link to new BOM version
            ItemId = originalBomItem.ItemId, // Same item
            Quantity = originalBomItem.Quantity, // Same quantity
            ReferenceDesignator = originalBomItem.ReferenceDesignator, // Same reference
            UnitCost = originalBomItem.UnitCost, // Copy current cost
            // Note: ExtendedCost will be calculated automatically by the model
          };

          _context.BomItems.Add(newBomItem);
        }
      }

      // ✅ TRANSFER ALL SUB-ASSEMBLIES (CHILD BOMS)
      if (baseBom.SubAssemblies?.Any() == true)
      {
        foreach (var originalSubAssembly in baseBom.SubAssemblies)
        {
          var newSubAssembly = new Bom
          {
            BomNumber = originalSubAssembly.BomNumber,
            Description = originalSubAssembly.Description,
            Version = originalSubAssembly.Version, // Keep same version for sub-assemblies
            AssemblyPartNumber = originalSubAssembly.AssemblyPartNumber,
            ParentBomId = newBom.Id, // Link to new parent BOM
            BaseBomId = originalSubAssembly.BaseBomId,
            IsCurrentVersion = originalSubAssembly.IsCurrentVersion,
            CreatedFromChangeOrderId = changeOrderId,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
          };

          _context.Boms.Add(newSubAssembly);
        }
      }

      // ✅ TRANSFER ALL BOM DOCUMENTS
      if (baseBom.Documents?.Any() == true)
      {
        foreach (var originalDocument in baseBom.Documents)
        {
          // Create a copy of the document for the new BOM version
          var newDocument = new ItemDocument
          {
            BomId = newBom.Id, // Link to new BOM version
            ItemId = null, // This is a BOM document, not an Item document
            DocumentName = originalDocument.DocumentName,
            DocumentType = originalDocument.DocumentType,
            FileName = originalDocument.FileName,
            ContentType = originalDocument.ContentType,
            FileSize = originalDocument.FileSize,
            DocumentData = originalDocument.DocumentData, // Copy the actual file data
            Description = originalDocument.Description,
            UploadedDate = DateTime.Now // Mark as uploaded now for the new version
          };

          _context.ItemDocuments.Add(newDocument);
        }
      }

      // Save all the transferred items, sub-assemblies, and documents
      await _context.SaveChangesAsync();

      _logger.LogInformation(
        "Created new BOM version {Version} for BOM {BomName} (ID: {BomId}) with {ItemCount} items, {SubAssemblyCount} sub-assemblies, and {DocumentCount} documents transferred",
        newVersion, 
        baseBom.BomNumber, 
        newBom.Id,
        baseBom.BomItems?.Count ?? 0,
        baseBom.SubAssemblies?.Count ?? 0,
        baseBom.Documents?.Count ?? 0);

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

    public async Task<ChangeOrder> CreateChangeOrderAsync(ChangeOrder changeOrder)
    {
      // Validate that no pending change orders exist for this entity
      await ValidateNoPendingChangeOrdersAsync(changeOrder.EntityType, changeOrder.BaseEntityId);

      // Set the previous version and navigation properties from the current entity
      if (changeOrder.EntityType == "Item")
      {
        var currentItem = await GetCurrentItemVersionAsync(changeOrder.BaseEntityId);
        if (currentItem != null)
        {
          changeOrder.PreviousVersion = currentItem.Version;
          changeOrder.BaseItemId = currentItem.Id;
          changeOrder.BaseEntityId = currentItem.BaseItemId ?? currentItem.Id; // Use the actual base ID
        }
        else
        {
          throw new InvalidOperationException($"Item with ID {changeOrder.BaseEntityId} not found");
        }
      }
      else if (changeOrder.EntityType == "BOM")
      {
        var currentBom = await GetCurrentBomVersionAsync(changeOrder.BaseEntityId);
        if (currentBom != null)
        {
          changeOrder.PreviousVersion = currentBom.Version;
          changeOrder.BaseBomId = currentBom.Id;
          changeOrder.BaseEntityId = currentBom.BaseBomId ?? currentBom.Id; // Use the actual base ID
        }
        else
        {
          throw new InvalidOperationException($"BOM with ID {changeOrder.BaseEntityId} not found");
        }
      }

      // Validate that new version is greater than previous version
      await ValidateVersionProgressionAsync(changeOrder.EntityType, changeOrder.BaseEntityId, changeOrder.NewVersion);

      // Generate change order number
      changeOrder.ChangeOrderNumber = await GenerateChangeOrderNumberAsync(changeOrder.EntityType, changeOrder.NewVersion);
      changeOrder.CreatedDate = DateTime.Now;
      changeOrder.Status = "Pending";

      _context.ChangeOrders.Add(changeOrder);
      await _context.SaveChangesAsync();

      // Reload the change order with all related entities properly loaded
      var createdChangeOrder = await LoadChangeOrderWithRelatedEntitiesAsync(changeOrder.Id);

      _logger.LogInformation("Created change order {ChangeOrderNumber} for {EntityType} {BaseEntityId}",
          createdChangeOrder.ChangeOrderNumber, changeOrder.EntityType, changeOrder.BaseEntityId);

      return createdChangeOrder;
    }

    public async Task<bool> ImplementChangeOrderAsync(int changeOrderId, string implementedBy)
    {
      var changeOrder = await _context.ChangeOrders
          .Include(co => co.ChangeOrderDocuments.OrderBy(d => d.UploadedDate))
          .Include(co => co.BaseItem)
          .Include(co => co.BaseBom)
          .FirstOrDefaultAsync(co => co.Id == changeOrderId);

      if (changeOrder == null || changeOrder.Status != "Pending")
      {
        return false;
      }

      try
      {
        // Create the new version based on the change order
        if (changeOrder.EntityType == "Item")
        {
          var newItem = await CreateItemVersionAsync(changeOrder.BaseEntityId, changeOrder.NewVersion, changeOrderId);
          changeOrder.NewItemId = newItem.Id;
        }
        else if (changeOrder.EntityType == "BOM")
        {
          var newBom = await CreateBomVersionAsync(changeOrder.BaseEntityId, changeOrder.NewVersion, changeOrderId);
          changeOrder.NewBomId = newBom.Id;
        }

        // Mark change order as implemented
        changeOrder.Status = "Implemented";
        changeOrder.ImplementedBy = implementedBy;
        changeOrder.ImplementedDate = DateTime.Now;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Implemented change order {ChangeOrderNumber} by {ImplementedBy}",
            changeOrder.ChangeOrderNumber, implementedBy);

        return true;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error implementing change order {ChangeOrderId}", changeOrderId);
        return false;
      }
    }

    public async Task<bool> CancelChangeOrderAsync(int changeOrderId, string cancelledBy)
    {
      var changeOrder = await _context.ChangeOrders.FindAsync(changeOrderId);
      if (changeOrder == null || changeOrder.Status != "Pending")
      {
        return false;
      }

      changeOrder.Status = "Cancelled";
      changeOrder.CancelledBy = cancelledBy;
      changeOrder.CancelledDate = DateTime.Now;

      await _context.SaveChangesAsync();

      _logger.LogInformation("Cancelled change order {ChangeOrderNumber} by {CancelledBy}",
          changeOrder.ChangeOrderNumber, cancelledBy);

      return true;
    }

    public async Task<IEnumerable<ChangeOrder>> GetPendingChangeOrdersAsync()
    {
      var changeOrders = await _context.ChangeOrders
          .Include(co => co.ChangeOrderDocuments)
          .Include(co => co.BaseItem) // ✅ Add this
          .Include(co => co.BaseBom)  // ✅ Add this
          .Where(co => co.Status == "Pending")
          .OrderByDescending(co => co.CreatedDate)
          .ToListAsync();

      return changeOrders;
    }

    public async Task<List<ChangeOrder>> GetAllChangeOrdersAsync()
    {
      var changeOrders = await _context.ChangeOrders
          .Include(co => co.ChangeOrderDocuments)
          .Include(co => co.BaseItem) // ✅ Add this
          .Include(co => co.BaseBom)  // ✅ Add this
          .OrderByDescending(co => co.CreatedDate)
          .ToListAsync();

      return changeOrders;
    }

    public async Task<List<ChangeOrder>> GetChangeOrdersByStatusAsync(string status)
    {
      var changeOrders = await _context.ChangeOrders
          .Include(co => co.ChangeOrderDocuments)
          .Include(co => co.BaseItem) // ✅ Add this
          .Include(co => co.BaseBom)  // ✅ Add this
          .Where(co => co.Status == status)
          .OrderByDescending(co => co.CreatedDate)
          .ToListAsync();

      return changeOrders;
    }

    public async Task<IEnumerable<ChangeOrder>> GetChangeOrdersByEntityAsync(string entityType, int entityId)
    {
      var changeOrders = await _context.ChangeOrders
          .Include(co => co.ChangeOrderDocuments)
          .Include(co => co.BaseItem) // ✅ Add this
          .Include(co => co.BaseBom)  // ✅ Add this
          .Where(co => co.EntityType == entityType && co.BaseEntityId == entityId)
          .OrderByDescending(co => co.CreatedDate)
          .ToListAsync();

      return changeOrders;
    }

    public async Task<List<ChangeOrder>> GetChangeOrdersForEntityAsync(string entityType, int entityId)
    {
      return (List<ChangeOrder>)await GetChangeOrdersByEntityAsync(entityType, entityId);
    }

    public async Task<ChangeOrder?> GetChangeOrderByIdAsync(int changeOrderId)
    {
      var changeOrder = await _context.ChangeOrders
          .Include(co => co.ChangeOrderDocuments)
          .Include(co => co.BaseItem) // ✅ Add this - this was missing!
          .Include(co => co.BaseBom)  // ✅ Add this - this was missing!
          .Include(co => co.NewItem)  // ✅ Add this for implementation results
          .Include(co => co.NewBom)   // ✅ Add this for implementation results
          .FirstOrDefaultAsync(co => co.Id == changeOrderId);

      return changeOrder;
    }

    public async Task<bool> HasPendingChangeOrdersAsync(string entityType, int entityId)
    {
      return await _context.ChangeOrders
          .AnyAsync(co => co.EntityType == entityType &&
                         co.BaseEntityId == entityId &&
                         co.Status == "Pending");
    }

    public async Task<List<ChangeOrder>> GetPendingChangeOrdersForEntityAsync(string entityType, int entityId)
    {
      return await _context.ChangeOrders
          .Include(co => co.ChangeOrderDocuments)
          .Include(co => co.BaseItem) // ✅ Add this
          .Include(co => co.BaseBom)  // ✅ Add this
          .Where(co => co.EntityType == entityType &&
                      co.BaseEntityId == entityId &&
                      co.Status == "Pending")
          .OrderByDescending(co => co.CreatedDate)
          .ToListAsync();
    }

    public async Task<ChangeOrderStatistics> GetChangeOrderStatisticsAsync()
    {
      var allChangeOrders = await _context.ChangeOrders
          .Include(co => co.ChangeOrderDocuments)
          .ToListAsync();

      return new ChangeOrderStatistics
      {
        TotalChangeOrders = allChangeOrders.Count,
        PendingCount = allChangeOrders.Count(co => co.Status == "Pending"),
        ImplementedCount = allChangeOrders.Count(co => co.Status == "Implemented"),
        CancelledCount = allChangeOrders.Count(co => co.Status == "Cancelled"),
        TotalDocuments = allChangeOrders.Sum(co => co.DocumentCount),
        ChangeOrdersWithDocuments = allChangeOrders.Count(co => co.HasDocuments),
        ItemChangeOrders = allChangeOrders.Count(co => co.EntityType == "Item"),
        BomChangeOrders = allChangeOrders.Count(co => co.EntityType == "BOM")
      };
    }

    /// <summary>
    /// Gets the related entity (Item or BOM) for a change order for display purposes
    /// </summary>
    /// <param name="changeOrder">The change order</param>
    /// <returns>A tuple containing the entity name and description</returns>
    public async Task<(string entityName, string entityDescription)> GetRelatedEntityInfoAsync(ChangeOrder changeOrder)
    {
      if (changeOrder.EntityType == "Item")
      {
        var item = await GetCurrentItemVersionAsync(changeOrder.BaseEntityId);
        if (item != null)
        {
          return (item.PartNumber, item.Description);
        }
      }
      else if (changeOrder.EntityType == "BOM")
      {
        var bom = await GetCurrentBomVersionAsync(changeOrder.BaseEntityId);
        if (bom != null)
        {
          return (bom.BomNumber, bom.Description);
        }
      }

      return ($"{changeOrder.EntityType} ID: {changeOrder.BaseEntityId}", "");
    }

    #endregion

    #region Private Helper Methods

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

      // Handle mixed or other versions - use string comparison as fallback
      return string.Compare(newVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private bool IsNumericVersion(string version)
    {
      return Version.TryParse(PadVersionString(version), out _);
    }

    private bool IsAlphabeticVersion(string version)
    {
      return version.All(c => char.IsLetter(c)) && version.Length <= 3;
    }

    private string PadVersionString(string version)
    {
      // Ensure version has at least major.minor format
      if (!version.Contains('.'))
      {
        return version + ".0";
      }
      return version;
    }

    private async Task ValidateNoPendingChangeOrdersAsync(string entityType, int entityId)
    {
      var pendingChangeOrders = await _context.ChangeOrders
          .Where(co => co.EntityType == entityType &&
                      co.BaseEntityId == entityId &&
                      co.Status == "Pending")
          .ToListAsync();

      if (pendingChangeOrders.Any())
      {
        var pendingNumbers = string.Join(", ", pendingChangeOrders.Select(co => co.ChangeOrderNumber));
        throw new InvalidOperationException(
            $"Cannot create a new change order for this {entityType.ToLower()} because there are pending change orders that must be implemented or cancelled first: {pendingNumbers}");
      }
    }

    private async Task<ChangeOrder> LoadChangeOrderWithRelatedEntitiesAsync(int changeOrderId)
    {
      var changeOrder = await _context.ChangeOrders
          .Include(co => co.BaseItem)
          .Include(co => co.BaseBom)
          .Include(co => co.NewItem)
          .Include(co => co.NewBom)
          .Include(co => co.ChangeOrderDocuments.OrderBy(d => d.UploadedDate))
          .FirstOrDefaultAsync(co => co.Id == changeOrderId);

      if (changeOrder == null)
      {
        throw new InvalidOperationException("Change order not found after creation");
      }

      return changeOrder;
    }

    private async Task LoadRelatedEntitiesForChangeOrdersAsync(IEnumerable<ChangeOrder> changeOrders)
    {
      // Navigation properties are now properly loaded via Include statements
      // This method is kept for interface compatibility but no longer needed
      await Task.CompletedTask;
    }

    #endregion
  }
}