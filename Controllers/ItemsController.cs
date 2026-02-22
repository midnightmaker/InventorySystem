using InventorySystem.Data;
using InventorySystem.Helpers;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
  public class ItemsController : Controller
  {
    private readonly IInventoryService _inventoryService;
    private readonly IPurchaseService _purchaseService;
    private readonly InventoryContext _context;
    private readonly IVersionControlService _versionService;
    private readonly IVendorService _vendorService;
    private readonly IBomService _bomService;
    private readonly ILogger<ItemsController> _logger;
    private readonly IProductionService _productionService;

    // Pagination constants
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };

    public ItemsController(
      IInventoryService inventoryService, 
      IPurchaseService purchaseService, 
      IVendorService vendorService, 
      InventoryContext context, 
      IVersionControlService versionService,
      IBomService bomService,
      ILogger<ItemsController> logger,
      IProductionService productionService)
    {
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
      _versionService = versionService;
      _vendorService = vendorService;
      _context = context;
      _bomService = bomService;
      _logger = logger;
      _productionService = productionService;
    }

    public async Task<IActionResult> Index(
        string search,
        string itemTypeFilter,
        string stockLevelFilter,
        string vendorFilter,
        bool? isSellable,
        string sortOrder = "partNumber_asc",
        int page = 1,
        int pageSize = DefaultPageSize)
    {
      try
      {
        // Validate and constrain pagination parameters
        page = Math.Max(1, page);
        pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

        _logger.LogInformation("=== ITEMS INDEX DEBUG ===");
        _logger.LogInformation("Search: {Search}", search);
        _logger.LogInformation("ItemType Filter: {ItemTypeFilter}", itemTypeFilter);
        _logger.LogInformation("Stock Level Filter: {StockLevelFilter}", stockLevelFilter);
        _logger.LogInformation("Vendor Filter: {VendorFilter}", vendorFilter);
        _logger.LogInformation("Is Sellable: {IsSellable}", isSellable);
        _logger.LogInformation("Sort Order: {SortOrder}", sortOrder);
        _logger.LogInformation("Page: {Page}, PageSize: {PageSize}", page, pageSize);

        // ✅ FIXED: Include VendorItems and Vendor navigation properties for Primary Vendor display
        var query = _context.Items
            .Include(i => i.VendorItems.Where(vi => vi.IsActive))
                .ThenInclude(vi => vi.Vendor)
            .AsQueryable();

        // Apply search filter with wildcard support
        if (!string.IsNullOrWhiteSpace(search))
        {
          var searchTerm = search.Trim();
          _logger.LogInformation("Applying search filter: {SearchTerm}", searchTerm);

          if (searchTerm.Contains('*') || searchTerm.Contains('?'))
          {
            var likePattern = ConvertWildcardToLike(searchTerm);
            _logger.LogInformation("Using LIKE pattern: {LikePattern}", likePattern);

            query = query.Where(i =>
              EF.Functions.Like(i.PartNumber, likePattern) ||
              EF.Functions.Like(i.Description, likePattern) ||
              (i.Comments != null && EF.Functions.Like(i.Comments, likePattern)) ||
              (i.VendorPartNumber != null && EF.Functions.Like(i.VendorPartNumber, likePattern)) ||
              EF.Functions.Like(i.Id.ToString(), likePattern)
            );
          }
          else
          {
            query = query.Where(i =>
              i.PartNumber.Contains(searchTerm) ||
              i.Description.Contains(searchTerm) ||
              (i.Comments != null && i.Comments.Contains(searchTerm)) ||
              (i.VendorPartNumber != null && i.VendorPartNumber.Contains(searchTerm)) ||
              i.Id.ToString().Contains(searchTerm)
            );
          }
        }

        // UPDATED: Apply item type filter - only operational types
        if (!string.IsNullOrWhiteSpace(itemTypeFilter))
        {
          _logger.LogInformation("Applying item type filter: {ItemTypeFilter}", itemTypeFilter);
          
          var itemTypeStrings = itemTypeFilter.Split(',', StringSplitOptions.RemoveEmptyEntries);
          var validItemTypes = new List<ItemType>();
          
          foreach (var itemTypeString in itemTypeStrings)
          {
            if (Enum.TryParse<ItemType>(itemTypeString.Trim(), out var itemType))
            {
              // Only allow operational item types
              if (itemType == ItemType.Inventoried || 
                  itemType == ItemType.Consumable || 
                  itemType == ItemType.RnDMaterials)
              {
                validItemTypes.Add(itemType);
              }
            }
          }
          
          if (validItemTypes.Any())
          {
            query = query.Where(i => validItemTypes.Contains(i.ItemType));
            _logger.LogInformation("Filtered by item types: {ItemTypes}", string.Join(", ", validItemTypes));
          }
        }

        // UPDATED: Apply stock level filter - simplified
        if (!string.IsNullOrWhiteSpace(stockLevelFilter))
        {
          _logger.LogInformation("Applying stock level filter: {StockLevelFilter}", stockLevelFilter);
          query = stockLevelFilter switch
          {
            "low" => query.Where(i => i.CurrentStock <= i.MinimumStock),
            "out" => query.Where(i => i.CurrentStock == 0),
            "overstock" => query.Where(i => i.CurrentStock > (i.MinimumStock * 2)),
            "instock" => query.Where(i => i.CurrentStock > 0),
            _ => query
          };
        }

        // ✅ ENHANCED: Apply vendor filter using VendorItems relationship
        if (!string.IsNullOrWhiteSpace(vendorFilter))
        {
          _logger.LogInformation("Applying vendor filter: {VendorFilter}", vendorFilter);
          query = query.Where(i => i.VendorItems.Any(vi => vi.IsActive && vi.Vendor.CompanyName.Contains(vendorFilter)));
        }

        // Apply sellable filter
        if (isSellable.HasValue)
        {
          _logger.LogInformation("Applying sellable filter: {IsSellable}", isSellable.Value);
          query = query.Where(i => i.IsSellable == isSellable.Value);
        }

        // ✅ ENHANCED: Apply sorting with proper vendor sorting
        query = sortOrder switch
        {
          "partNumber_asc" => query.OrderBy(i => i.PartNumber),
          "partNumber_desc" => query.OrderByDescending(i => i.PartNumber),
          "description_asc" => query.OrderBy(i => i.Description),
          "description_desc" => query.OrderByDescending(i => i.Description),
          "itemType_asc" => query.OrderBy(i => i.ItemType),
          "itemType_desc" => query.OrderByDescending(i => i.ItemType),
          "stock_asc" => query.OrderBy(i => i.CurrentStock),
          "stock_desc" => query.OrderByDescending(i => i.CurrentStock),
          "vendor_asc" => query.OrderBy(i => i.VendorItems.Where(vi => vi.IsPrimary && vi.IsActive).Select(vi => vi.Vendor.CompanyName).FirstOrDefault() ?? "ZZZ"),
          "vendor_desc" => query.OrderByDescending(i => i.VendorItems.Where(vi => vi.IsPrimary && vi.IsActive).Select(vi => vi.Vendor.CompanyName).FirstOrDefault() ?? ""),
          "created_asc" => query.OrderBy(i => i.CreatedDate),
          "created_desc" => query.OrderByDescending(i => i.CreatedDate),
          _ => query.OrderBy(i => i.PartNumber)
        };

        var totalCount = await query.CountAsync();
        _logger.LogInformation("Total filtered records: {TotalCount}", totalCount);

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation("Retrieved {ItemCount} items for page {Page}", items.Count, page);

        // UPDATED: Get only operational item types for dropdowns
        var itemTypes = new List<ItemType> 
        { 
          ItemType.Inventoried, 
          ItemType.Consumable, 
          ItemType.RnDMaterials 
        };

        // ✅ ENHANCED: Get vendors more efficiently using the already loaded data
        var allVendors = items
            .SelectMany(i => i.VendorItems)
            .Where(vi => vi.IsActive && vi.Vendor.IsActive)
            .Select(vi => vi.Vendor.CompanyName)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        // If no vendors found in current page, fall back to full query
        if (!allVendors.Any())
        {
          allVendors = await _context.VendorItems
            .Include(vi => vi.Vendor)
            .Where(vi => vi.IsActive && vi.Vendor.IsActive)
            .Select(vi => vi.Vendor.CompanyName)
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync();
        }

        // Prepare ViewBag data
        ViewBag.SearchTerm = search;
        ViewBag.ItemTypeFilter = itemTypeFilter;
        ViewBag.StockLevelFilter = stockLevelFilter;
        ViewBag.VendorFilter = vendorFilter;
        ViewBag.IsSellable = isSellable;
        ViewBag.SortOrder = sortOrder;

        // Pagination data
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.HasPreviousPage = page > 1;
        ViewBag.HasNextPage = page < totalPages;
        ViewBag.ShowingFrom = totalCount > 0 ? skip + 1 : 0;
        ViewBag.ShowingTo = Math.Min(skip + pageSize, totalCount);
        ViewBag.AllowedPageSizes = AllowedPageSizes;

        // UPDATED: Dropdown data for operational items only
        ViewBag.ItemTypeOptions = new SelectList(itemTypes.Select(t => new
        {
          Value = t.ToString(),
          Text = t.ToString().Replace("_", " ")
        }), "Value", "Text", itemTypeFilter);

        // UPDATED: Simplified stock level options
        ViewBag.StockLevelOptions = new SelectList(new[]
        {
          new { Value = "", Text = "All Stock Levels" },
          new { Value = "low", Text = "Low Stock" },
          new { Value = "out", Text = "Out of Stock" },
          new { Value = "overstock", Text = "Overstocked" },
          new { Value = "instock", Text = "In Stock" }
        }, "Value", "Text", stockLevelFilter);

        ViewBag.VendorOptions = new SelectList(allVendors.Select(v => new
        {
          Value = v,
          Text = v
        }), "Value", "Text", vendorFilter);

        // Search statistics
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
                           !string.IsNullOrWhiteSpace(itemTypeFilter) ||
                           !string.IsNullOrWhiteSpace(stockLevelFilter) ||
                           !string.IsNullOrWhiteSpace(vendorFilter) ||
                           isSellable.HasValue;

        if (ViewBag.IsFiltered)
        {
          var totalItems = await _context.Items.CountAsync();
          ViewBag.SearchResultsCount = totalCount;
          ViewBag.TotalItemsCount = totalItems;
        }

        return View(items);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in Items Index");

        ViewBag.ErrorMessage = $"Error loading items: {ex.Message}";
        ViewBag.AllowedPageSizes = AllowedPageSizes;

        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = 1;
        ViewBag.TotalCount = 0;
        ViewBag.HasPreviousPage = false;
        ViewBag.HasNextPage = false;
        ViewBag.ShowingFrom = 0;
        ViewBag.ShowingTo = 0;

        ViewBag.SearchTerm = search;
        ViewBag.ItemTypeFilter = itemTypeFilter;
        ViewBag.StockLevelFilter = stockLevelFilter;
        ViewBag.VendorFilter = vendorFilter;
        ViewBag.IsSellable = isSellable;
        ViewBag.SortOrder = sortOrder;
        ViewBag.IsFiltered = false;

        ViewBag.ItemTypeOptions = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.StockLevelOptions = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.VendorOptions = new SelectList(new List<object>(), "Value", "Text");

        return View(new List<Item>());
      }
    }

    private string ConvertWildcardToLike(string wildcardPattern)
    {
      var escaped = wildcardPattern
          .Replace("%", "[%]")
          .Replace("_", "[_]")
          .Replace("[", "[[]");

      escaped = escaped
          .Replace("*", "%")
          .Replace("?", "_");

      return escaped;
    }

    public async Task<IActionResult> Details(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null) return NotFound();

      var itemVersions = await _inventoryService.GetItemVersionsAsync(item.BaseItemId ?? item.Id);
      ViewBag.ItemVersions = itemVersions;

      var purchases = await _purchaseService.GetPurchasesByItemIdAsync(id);
      ViewBag.Purchases = purchases;

      var purchasesByVersion = purchases
          .GroupBy(p => p.ItemVersion ?? "N/A")
          .ToDictionary(g => g.Key, g => g.AsEnumerable());
      ViewBag.PurchasesByVersion = purchasesByVersion;

      var vendorService = HttpContext.RequestServices.GetRequiredService<IVendorService>();
      var vendorItems = await vendorService.GetItemVendorsAsync(id);
      ViewBag.VendorItems = vendorItems;

      ViewBag.AverageCost = await _inventoryService.GetAverageCostAsync(id);
      ViewBag.FifoValue = await _inventoryService.GetFifoValueAsync(id);

      // BOMs that reference this item
      var bomsUsingItem = await _bomService.GetBomsByItemIdAsync(id);
      ViewBag.BomsUsingItem = bomsUsingItem;

      if (item.IsCurrentVersion)
      {
          var pendingChangeOrders = await _versionService.GetPendingChangeOrdersForEntityAsync("Item", item.BaseItemId ?? item.Id);
          ViewBag.PendingChangeOrders = pendingChangeOrders;
      }

      return View(item);
    }

    public async Task<IActionResult> Create()
    {
      try
      {
        var viewModel = new CreateItemViewModel
        {
          ItemType = ItemType.Inventoried,
          Version = "A",
          IsSellable = true,
          UnitOfMeasure = UnitOfMeasure.Each,
          InitialPurchaseDate = DateTime.Today,
          MaterialType = MaterialType.Standard
        };

        ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList();
        await LoadRawMaterialsForView();
        await LoadVendorsForView();

        return View(viewModel);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading create item form");
        TempData["ErrorMessage"] = $"Error loading create form: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateItemViewModel viewModel)
    {
      if (viewModel == null)
      {
        ModelState.AddModelError("", "Item data is missing.");
        return View(new CreateItemViewModel());
      }

      try
      {
        if (!viewModel.IsMaterialItem)
        {
          viewModel.MaterialType = MaterialType.Standard;
          viewModel.ParentRawMaterialId = null;
          viewModel.YieldFactor = null;
          viewModel.WastePercentage = null;
        }

        if (ModelState.IsValid)
        {
          // UPDATED: Only operational items - no expense logic
          var item = new Item
          {
            PartNumber = viewModel.PartNumber,
            Description = viewModel.Description,
            Comments = viewModel.Comments ?? string.Empty,
            MinimumStock = viewModel.ShowStockFields ? viewModel.MinimumStock : 0,
            CurrentStock = 0,
            CreatedDate = DateTime.Now,
            UnitOfMeasure = viewModel.UnitOfMeasure,
            VendorPartNumber = viewModel.VendorPartNumber,
            IsSellable = viewModel.IsSellable,
            SalePrice = viewModel.SalePrice ?? 0,
            ItemType = viewModel.ItemType,
            Version = viewModel.Version,
            MaterialType = viewModel.MaterialType,
            ParentRawMaterialId = viewModel.ParentRawMaterialId,
            YieldFactor = viewModel.YieldFactor,
            WastePercentage = viewModel.WastePercentage
          };

          if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
          {
            using var memoryStream = new MemoryStream();
            await viewModel.ImageFile.CopyToAsync(memoryStream);
            item.ImageData = memoryStream.ToArray();
            item.ImageContentType = viewModel.ImageFile.ContentType;
            item.ImageFileName = viewModel.ImageFile.FileName;
          }

          await _inventoryService.CreateItemAsync(item);

          if (viewModel.PreferredVendorId.HasValue)
          {
            await CreateVendorItemRelationship(item.Id, viewModel.PreferredVendorId.Value, viewModel.VendorPartNumber);
          }

          TempData["SuccessMessage"] = "Item created successfully!";
          return RedirectToAction("Index");
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating item: {PartNumber}", viewModel.PartNumber);
        ModelState.AddModelError("", $"Error creating item: {ex.Message}");
      }

      ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList();
      await LoadRawMaterialsForView();
      await LoadVendorsForView();

      return View(viewModel);
    }

    public async Task<IActionResult> Edit(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null) return NotFound();

      // REMOVED: All expense checks - only operational items
      await LoadVendorsForEditView(item);
      return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Item model, IFormFile? newImageFile, int? preferredVendorId)
    {
      if (id != model.Id)
      {
        return NotFound();
      }

      try
      {
        // Remove all fields not submitted by the form to prevent false ModelState errors.
        // Image fields were removed from the view (byte[] can't round-trip via hidden fields).
        ModelState.Remove(nameof(Item.ImageData));
        ModelState.Remove(nameof(Item.ImageContentType));
        ModelState.Remove(nameof(Item.ImageFileName));
        // Navigation / collection properties - never posted by a form.
        ModelState.Remove(nameof(Item.Purchases));
        ModelState.Remove(nameof(Item.DesignDocuments));
        ModelState.Remove(nameof(Item.VendorItems));
        ModelState.Remove(nameof(Item.BaseItem));
        ModelState.Remove(nameof(Item.CreatedFromChangeOrder));
        ModelState.Remove(nameof(Item.ParentRawMaterial));
        ModelState.Remove(nameof(Item.Versions));
        ModelState.Remove(nameof(Item.TransformedItems));
        // Optional / computed fields not present in the form.
        ModelState.Remove(nameof(Item.VersionHistory));
        ModelState.Remove(nameof(Item.MaterialType));
        ModelState.Remove(nameof(Item.ParentRawMaterialId));
        ModelState.Remove(nameof(Item.YieldFactor));
        ModelState.Remove(nameof(Item.WastePercentage));
        ModelState.Remove(nameof(Item.RequiresSerialNumber));
        ModelState.Remove(nameof(Item.RequiresModelNumber));
        ModelState.Remove(nameof(Item.PreferredRevenueAccountCode));
        ModelState.Remove(nameof(Item.Comments));

        if (!ModelState.IsValid)
        {
          // Log every validation error so we can identify any remaining problem fields.
          var errors = ModelState
            .Where(kvp => kvp.Value!.Errors.Count > 0)
            .Select(kvp => $"{kvp.Key}: {string.Join("; ", kvp.Value!.Errors.Select(e => e.ErrorMessage))}");
          _logger.LogWarning("Edit Item ModelState invalid for item {ItemId}. Errors: {Errors}", id, string.Join(" | ", errors));
        }

        if (ModelState.IsValid)
        {
          var existingItem = await _inventoryService.GetItemByIdAsync(id);
          if (existingItem == null)
          {
            TempData["ErrorMessage"] = "Item not found.";
            return RedirectToAction("Index");
          }

          // UPDATED: Simple property updates for operational items
          existingItem.PartNumber = model.PartNumber;
          existingItem.Description = model.Description;
          existingItem.Comments = model.Comments ?? string.Empty;
          existingItem.ItemType = model.ItemType;
          existingItem.UnitOfMeasure = model.UnitOfMeasure;
          existingItem.MinimumStock = model.MinimumStock;
          existingItem.VendorPartNumber = model.VendorPartNumber;
          existingItem.SalePrice = model.SalePrice;
          existingItem.IsSellable = model.IsSellable;

          if (newImageFile != null && newImageFile.Length > 0)
          {
            using var memoryStream = new MemoryStream();
            await newImageFile.CopyToAsync(memoryStream);
            existingItem.ImageData = memoryStream.ToArray();
            existingItem.ImageContentType = newImageFile.ContentType;
            existingItem.ImageFileName = newImageFile.FileName;
          }

          await _inventoryService.UpdateItemAsync(existingItem);
          await UpdateVendorItemRelationship(existingItem.Id, preferredVendorId, model.VendorPartNumber);

          TempData["SuccessMessage"] = "Item updated successfully!";
          return RedirectToAction("Details", new { id = existingItem.Id });
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating item: {PartNumber}", model.PartNumber);
        ModelState.AddModelError("", $"Error updating item: {ex.Message}");
      }

      await LoadVendorsForEditView(model);
      return View(model);
    }

    public async Task<IActionResult> Delete(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null) return NotFound();

      // Surface BOM usage so the view can warn the user
      var bomsUsingItem = await _bomService.GetBomsByItemIdAsync(id);
      ViewBag.BomsUsingItem = bomsUsingItem;

      return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
      try
      {
        // Block deletion when item is referenced in one or more BOMs
        var bomsUsingItem = (await _bomService.GetBomsByItemIdAsync(id)).ToList();
        if (bomsUsingItem.Any())
        {
          var bomList = string.Join(", ", bomsUsingItem.Select(b => b.BomNumber));
          TempData["ErrorMessage"] = $"Cannot delete this item — it is used in {bomsUsingItem.Count} BOM(s): {bomList}. Remove it from those BOMs first.";
          return RedirectToAction("Delete", new { id });
        }

        await _inventoryService.DeleteItemAsync(id);
        TempData["SuccessMessage"] = "Item deleted successfully!";
        return RedirectToAction(nameof(Index));
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error deleting item: {ex.Message}";
        return RedirectToAction("Details", new { id });
      }
    }

    // Image handling actions
    public async Task<IActionResult> GetImage(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null || !item.HasImage) return NotFound();

      return File(item.ImageData!, item.ImageContentType!, item.ImageFileName);
    }

    public async Task<IActionResult> GetImageThumbnail(int id, int size = 150)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null || !item.HasImage) return NotFound();

      return File(item.ImageData!, item.ImageContentType!, item.ImageFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveImage(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null) return NotFound();

      item.ImageData = null;
      item.ImageContentType = null;
      item.ImageFileName = null;

      await _inventoryService.UpdateItemAsync(item);
      TempData["SuccessMessage"] = "Image removed successfully!";

      return RedirectToAction("Details", new { id });
    }

    public async Task<IActionResult> TestDocuments(int id)
    {
      try
      {
        var documentsInDb = await _context.ItemDocuments
            .Where(d => d.ItemId == id)
            .ToListAsync();

        var itemWithDocs = await _context.Items
            .Include(i => i.DesignDocuments)
            .FirstOrDefaultAsync(i => i.Id == id);

        var itemFromService = await _inventoryService.GetItemByIdAsync(id);

        return Json(new
        {
          Success = true,
          ItemId = id,
          DirectDbQuery = new
          {
            Count = documentsInDb.Count,
            Documents = documentsInDb.Select(d => new { d.Id, d.DocumentName, d.FileName })
          },
          ItemWithInclude = new
          {
            ItemFound = itemWithDocs != null,
            DocumentsNull = itemWithDocs?.DesignDocuments == null,
            Count = itemWithDocs?.DesignDocuments?.Count ?? 0
          },
          ServiceMethod = new
          {
            ItemFound = itemFromService != null,
            DocumentsNull = itemFromService?.DesignDocuments == null,
            Count = itemFromService?.DesignDocuments?.Count ?? 0
          }
        });
      }
      catch (Exception ex)
      {
        return Json(new
        {
          Success = false,
          Error = ex.Message,
          StackTrace = ex.StackTrace
        });
      }
    }

    public IActionResult BulkUpload()
    {
      return View(new BulkItemUploadViewModel());
    }

    // POST: Handle CSV file upload and validation
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpload(BulkItemUploadViewModel model)
    {
        if (model.CsvFile == null || model.CsvFile.Length == 0)
        {
            ModelState.AddModelError("CsvFile", "Please select a CSV file to upload.");
            return View(model);
        }

        try
        {
            // Get the bulk upload service
            var bulkUploadService = HttpContext.RequestServices.GetRequiredService<IBulkUploadService>();

            // Generate a unique session ID for this upload
            var uploadSessionId = Guid.NewGuid().ToString();

            // Store the uploaded file temporarily
            var tempFilePath = await SaveTempFileAsync(model.CsvFile, uploadSessionId);

            // Validate the CSV file
            var validationResults = await bulkUploadService.ValidateCsvFileAsync(model.CsvFile, model.SkipHeaderRow);
            
            // Parse the CSV file to get preview items for valid rows
            var allParsedItems = await bulkUploadService.ParseCsvFileAsync(model.CsvFile, model.SkipHeaderRow);
            var validItems = validationResults
                .Where(vr => vr.IsValid && vr.ItemData != null)
                .Select(vr => vr.ItemData!)
                .ToList();

            // Store validation results and file info in session/cache
            var uploadSession = new UploadSession
            {
                SessionId = uploadSessionId,
                TempFilePath = tempFilePath,
                SkipHeaderRow = model.SkipHeaderRow,
                ValidationResults = validationResults,
                ValidItemsCount = validItems.Count,
                CreatedAt = DateTime.Now
            };

            // Store in session (or you could use a cache/database)
            HttpContext.Session.SetString($"BulkUpload_{uploadSessionId}", 
                System.Text.Json.JsonSerializer.Serialize(uploadSession));

            // Update the model with results
            model.ValidationResults = validationResults;
            model.PreviewItems = validItems;
            model.UploadSessionId = uploadSessionId;

            if (validationResults.Any(vr => !vr.IsValid))
            {
                model.ErrorMessage = $"Found {validationResults.Count(vr => !vr.IsValid)} validation errors. Please review and correct them.";
            }
            else if (validItems.Any())
            {
                model.SuccessMessage = $"Successfully validated {validItems.Count} items. Review the items below and click 'Import' to proceed.";
            }
            else
            {
                model.ErrorMessage = "No valid items found in the CSV file.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bulk upload CSV file");
            model.ErrorMessage = $"Error processing CSV file: {ex.Message}";
        }

        return View(model);
    }

    // POST: Process the actual import using the stored file
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessBulkUpload(string uploadSessionId)
    {
        if (string.IsNullOrEmpty(uploadSessionId))
        {
            TempData["ErrorMessage"] = "Invalid upload session. Please upload a CSV file first.";
            return RedirectToAction("BulkUpload");
        }

        try
        {
            _logger.LogInformation("Processing bulk import for session: {SessionId}", uploadSessionId);
            
            // Retrieve the upload session
            var sessionData = HttpContext.Session.GetString($"BulkUpload_{uploadSessionId}");
            if (string.IsNullOrEmpty(sessionData))
            {
                TempData["ErrorMessage"] = "Upload session expired. Please upload the CSV file again.";
                return RedirectToAction("BulkUpload");
            }

            var uploadSession = System.Text.Json.JsonSerializer.Deserialize<UploadSession>(sessionData);
            
            // Verify the temp file still exists
            if (!System.IO.File.Exists(uploadSession.TempFilePath))
            {
                TempData["ErrorMessage"] = "Upload file not found. Please upload the CSV file again.";
                return RedirectToAction("BulkUpload");
            }

            // Get the bulk upload service
            var bulkUploadService = HttpContext.RequestServices.GetRequiredService<IBulkUploadService>();

            // Create a FormFile from the stored file
            var storedFileFormFile = await CreateFormFileFromStoredFile(uploadSession.TempFilePath);
            
            // Re-parse the stored file to get valid items
            var validItems = await bulkUploadService.ParseCsvFileAsync(storedFileFormFile, uploadSession.SkipHeaderRow);
            
            // Import the valid items
            var importResult = await bulkUploadService.ImportValidItemsAsync(validItems);

            // Clean up temp file and session
            await CleanupUploadSession(uploadSessionId, uploadSession.TempFilePath);

            if (importResult.IsSuccess)
            {
                TempData["SuccessMessage"] = importResult.GetSummary();
            }
            else
            {
                TempData["ErrorMessage"] = $"Import completed with errors: {importResult.GetSummary()}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk import process for session {SessionId}", uploadSessionId);
            TempData["ErrorMessage"] = $"Error during import: {ex.Message}";
        }

        return RedirectToAction("Index");
    }

    // ✅ ENHANCED: Add vendor creation confirmation step
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessBulkUploadWithVendorChoices(string uploadSessionId, Dictionary<string, bool> vendorCreationChoices)
    {
        if (string.IsNullOrEmpty(uploadSessionId))
        {
            TempData["ErrorMessage"] = "Invalid upload session. Please upload a CSV file first.";
            return RedirectToAction("BulkUpload");
        }

        try
        {
            _logger.LogInformation("Processing bulk import with vendor choices for session: {SessionId}", uploadSessionId);
            
            // Retrieve the upload session
            var sessionData = HttpContext.Session.GetString($"BulkUpload_{uploadSessionId}");
            if (string.IsNullOrEmpty(sessionData))
            {
                TempData["ErrorMessage"] = "Upload session expired. Please upload the CSV file again.";
                return RedirectToAction("BulkUpload");
            }

            var uploadSession = System.Text.Json.JsonSerializer.Deserialize<UploadSession>(sessionData);
            
            // Verify the temp file still exists
            if (!System.IO.File.Exists(uploadSession.TempFilePath))
            {
                TempData["ErrorMessage"] = "Upload file not found. Please upload the CSV file again.";
                return RedirectToAction("BulkUpload");
            }

            // Get the bulk upload service
            var bulkUploadService = HttpContext.RequestServices.GetRequiredService<IBulkUploadService>();

            // Create a FormFile from the stored file
            var storedFileFormFile = await CreateFormFileFromStoredFile(uploadSession.TempFilePath);
            
            // Re-parse the stored file to get valid items
            var validItems = await bulkUploadService.ParseCsvFileAsync(storedFileFormFile, uploadSession.SkipHeaderRow);
            
            // ✅ ENHANCED: Import with vendor creation choices
            var importResult = await bulkUploadService.ImportValidItemsAsync(validItems, vendorCreationChoices);

            // Clean up temp file and session
            await CleanupUploadSession(uploadSessionId, uploadSession.TempFilePath);

            if (importResult.IsSuccess)
            {
                TempData["SuccessMessage"] = importResult.GetSummary();
            }
            else
            {
                TempData["ErrorMessage"] = $"Import completed with errors: {importResult.GetSummary()}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk import process for session {SessionId}", uploadSessionId);
            TempData["ErrorMessage"] = $"Error during import: {ex.Message}";
        }

        return RedirectToAction("Index");
    }

    // Helper method to create an IFormFile from a stored file
    private async Task<IFormFile> CreateFormFileFromStoredFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var stream = new MemoryStream(fileBytes);
        
        var formFile = new FormFile(stream, 0, fileBytes.Length, "CsvFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
        
        return formFile;
    }

    private async Task<string> SaveTempFileAsync(IFormFile file, string sessionId)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "BulkUploads");
        Directory.CreateDirectory(tempDir);
        
        var tempFileName = $"upload_{sessionId}_{DateTime.Now:yyyyMMddHHmmss}.csv";
        var tempFilePath = Path.Combine(tempDir, tempFileName);
        
        using var stream = new FileStream(tempFilePath, FileMode.Create);
        await file.CopyToAsync(stream);
        
        return tempFilePath;
    }

    private async Task CleanupUploadSession(string sessionId, string tempFilePath)
    {
        try
        {
            // Remove session data
            HttpContext.Session.Remove($"BulkUpload_{sessionId}");
            
            // Delete temp file
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up upload session {SessionId}", sessionId);
        }
    }

    // Supporting class for upload session data
    public class UploadSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string TempFilePath { get; set; } = string.Empty;
        public bool SkipHeaderRow { get; set; }
        public List<ItemValidationResult> ValidationResults { get; set; } = new();
        public int ValidItemsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // UPDATED: Load only operational raw materials
    private async Task LoadRawMaterialsForView()
    {
      try
      {
        var rawMaterials = await _context.Items
          .Where(i => i.MaterialType == MaterialType.RawMaterial && 
                     i.ItemType == ItemType.Inventoried &&
                     i.IsCurrentVersion)
          .OrderBy(i => i.PartNumber)
          .Select(i => new { i.Id, DisplayText = $"{i.PartNumber} - {i.Description}" })
          .ToListAsync();

        ViewBag.ParentRawMaterials = new SelectList(rawMaterials, "Id", "DisplayText");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading raw materials for view");
        ViewBag.ParentRawMaterials = new SelectList(new List<object>(), "Id", "DisplayText");
      }
    }

    private async Task LoadVendorsForView()
    {
      try
      {
        var vendors = await _vendorService.GetActiveVendorsAsync();
        ViewBag.PreferredVendors = new SelectList(vendors.OrderBy(v => v.CompanyName), "Id", "CompanyName");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading vendors for view");
        ViewBag.PreferredVendors = new SelectList(new List<object>(), "Id", "CompanyName");
      }
    }

    private async Task LoadVendorsForEditView(Item item)
    {
      try
      {
        var vendors = await _vendorService.GetActiveVendorsAsync();

				// Find current primary vendor for this item
				var primaryVendorItem = await _context.VendorItems
						.Where(vi => vi.ItemId == item.Id && vi.IsPrimary && vi.IsActive)
						.FirstOrDefaultAsync();

				ViewBag.PreferredVendorId = new SelectList(vendors.OrderBy(v => v.CompanyName), "Id", "CompanyName", primaryVendorItem?.VendorId);
				ViewBag.CurrentPreferredVendorId = primaryVendorItem?.VendorId;
			}
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading vendors for edit view");
        ViewBag.PreferredVendorId = new SelectList(new List<object>(), "Id", "CompanyName");
        ViewBag.CurrentPreferredVendorId = null;
      }
    }

		// UPDATE CreateVendorItemRelationship method:
		private async Task CreateVendorItemRelationship(int itemId, int vendorId, string? vendorPartNumber)
		{
			try
			{
				// Clear any existing primary vendor for this item
				var existingPrimaryVendorItems = await _context.VendorItems
						.Where(vi => vi.ItemId == itemId && vi.IsPrimary)
						.ToListAsync();

				foreach (var existingPrimary in existingPrimaryVendorItems)
				{
					existingPrimary.IsPrimary = false;
					existingPrimary.LastUpdated = DateTime.Now;
				}

				// Find or create the vendor-item relationship
				var vendorItem = await _context.VendorItems
						.FirstOrDefaultAsync(vi => vi.ItemId == itemId && vi.VendorId == vendorId);

				if (vendorItem == null)
				{
					vendorItem = new VendorItem
					{
						ItemId = itemId,
						VendorId = vendorId,
						VendorPartNumber = vendorPartNumber,
						IsPrimary = true,
						IsActive = true,
						UnitCost = 0,
						MinimumOrderQuantity = 1,
						LeadTimeDays = 0,
						LastUpdated = DateTime.Now
					};
					_context.VendorItems.Add(vendorItem);
				}
				else
				{
					vendorItem.IsPrimary = true;
					vendorItem.IsActive = true;
					vendorItem.LastUpdated = DateTime.Now;
				}

				await _context.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating vendor-item relationship for Item {ItemId} and Vendor {VendorId}", itemId, vendorId);
			}
		}

		private async Task UpdateVendorItemRelationship(int itemId, int? newVendorId, string? vendorPartNumber)
		{
			try
			{
				// Clear any existing primary vendor for this item
				var existingPrimaryVendorItems = await _context.VendorItems
						.Where(vi => vi.ItemId == itemId && vi.IsPrimary)
						.ToListAsync();

				foreach (var existingPrimary in existingPrimaryVendorItems)
				{
					existingPrimary.IsPrimary = false;
					existingPrimary.LastUpdated = DateTime.Now;
				}

				if (!newVendorId.HasValue)
				{
					await _context.SaveChangesAsync();
					return;
				}

				// Find or create the vendor-item relationship
				var vendorItem = await _context.VendorItems
						.FirstOrDefaultAsync(vi => vi.ItemId == itemId && vi.VendorId == newVendorId.Value);

				if (vendorItem == null)
				{
					vendorItem = new VendorItem
					{
						ItemId = itemId,
						VendorId = newVendorId.Value,
						VendorPartNumber = vendorPartNumber,
						IsPrimary = true,
						IsActive = true,
						UnitCost = 0,
						MinimumOrderQuantity = 1,
						LeadTimeDays = 0,
						LastUpdated = DateTime.Now
					};

					_context.VendorItems.Add(vendorItem);
				}
				else
				{
					vendorItem.IsPrimary = true;
					vendorItem.IsActive = true;
					vendorItem.VendorPartNumber = vendorPartNumber;
					vendorItem.LastUpdated = DateTime.Now;
				}

				await _context.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating vendor-item relationship for Item {ItemId} and Vendor {VendorId}", itemId, newVendorId);
			}
		}

		private bool ItemExists(int id)
    {
      return _context.Items.Any(e => e.Id == id);
    }

    // Helper method to reload view data for bulk purchase form
    private async Task ReloadBulkPurchaseViewData(BulkPurchaseRequest model)
    {
        try
        {
            var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(model.BomId, model.Quantity);
            var vendors = await _vendorService.GetActiveVendorsAsync();
            ViewBag.ShortageAnalysis = shortageAnalysis;
            ViewBag.Vendors = vendors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading bulk purchase view data");
            ViewBag.ShortageAnalysis = null;
            ViewBag.Vendors = new List<Vendor>();
        }
    }
  }
}