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

        // Start with base query including necessary navigation properties
        var query = _context.Items
            .Include(i => i.PreferredVendorItem)
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

        // Apply item type filter
        if (!string.IsNullOrWhiteSpace(itemTypeFilter) && Enum.TryParse<ItemType>(itemTypeFilter, out var itemType))
        {
          _logger.LogInformation("Applying item type filter: {ItemType}", itemType);
          query = query.Where(i => i.ItemType == itemType);
        }

        // Apply stock level filter
        if (!string.IsNullOrWhiteSpace(stockLevelFilter))
        {
          _logger.LogInformation("Applying stock level filter: {StockLevelFilter}", stockLevelFilter);
          query = stockLevelFilter switch
          {
            "low" => query.Where(i => (i.ItemType == ItemType.Inventoried || i.ItemType == ItemType.Consumable || i.ItemType == ItemType.RnDMaterials) && i.CurrentStock <= i.MinimumStock),
            "out" => query.Where(i => (i.ItemType == ItemType.Inventoried || i.ItemType == ItemType.Consumable || i.ItemType == ItemType.RnDMaterials) && i.CurrentStock == 0),
            "overstock" => query.Where(i => (i.ItemType == ItemType.Inventoried || i.ItemType == ItemType.Consumable || i.ItemType == ItemType.RnDMaterials) && i.CurrentStock > (i.MinimumStock * 2)),
            "instock" => query.Where(i => i.ItemType == ItemType.Inventoried && i.CurrentStock > 0),
            "tracked" => query.Where(i => i.ItemType == ItemType.Inventoried),
            "nontracked" => query.Where(i => i.ItemType != ItemType.Inventoried),
            _ => query
          };
        }

        // Apply vendor filter
        if (!string.IsNullOrWhiteSpace(vendorFilter))
        {
          _logger.LogInformation("Applying vendor filter: {VendorFilter}", vendorFilter);
          if (vendorFilter.Contains('*') || vendorFilter.Contains('?'))
          {
            var likePattern = ConvertWildcardToLike(vendorFilter);
            query = query.Where(i => i.PreferredVendorItem != null && 
                                   i.PreferredVendorItem.Vendor != null && 
                                   EF.Functions.Like(i.PreferredVendorItem.Vendor.CompanyName, likePattern));
          }
          else
          {
            query = query.Where(i => i.PreferredVendorItem != null && 
                                   i.PreferredVendorItem.Vendor != null && 
                                   i.PreferredVendorItem.Vendor.CompanyName.Contains(vendorFilter));
          }
        }

        // Apply sellable filter
        if (isSellable.HasValue)
        {
          _logger.LogInformation("Applying sellable filter: {IsSellable}", isSellable.Value);
          query = query.Where(i => i.IsSellable == isSellable.Value);
        }

        // Apply sorting
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
          "vendor_asc" => query.OrderBy(i => i.PreferredVendorItem != null ? i.PreferredVendorItem.Vendor.CompanyName : ""),
          "vendor_desc" => query.OrderByDescending(i => i.PreferredVendorItem != null ? i.PreferredVendorItem.Vendor.CompanyName : ""),
          "created_asc" => query.OrderBy(i => i.CreatedDate),
          "created_desc" => query.OrderByDescending(i => i.CreatedDate),
          _ => query.OrderBy(i => i.PartNumber)
        };

        // Get total count for pagination (before Skip/Take)
        var totalCount = await query.CountAsync();
        _logger.LogInformation("Total filtered records: {TotalCount}", totalCount);

        // Calculate pagination values
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var skip = (page - 1) * pageSize;

        // Get paginated results
        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation("Retrieved {ItemCount} items for page {Page}", items.Count, page);

        // Get filter options for dropdowns
        var itemTypes = Enum.GetValues<ItemType>().ToList();
        
        // Fix the vendor query to use the correct navigation property
        var allVendors = await _context.Items
            .Include(i => i.PreferredVendorItem)
                .ThenInclude(vi => vi.Vendor)
            .Where(i => i.PreferredVendorItem != null && i.PreferredVendorItem.Vendor != null)
            .Select(i => i.PreferredVendorItem.Vendor.CompanyName)
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync();

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

        // Dropdown data
        ViewBag.ItemTypeOptions = new SelectList(itemTypes.Select(t => new
        {
          Value = t.ToString(),
          Text = t.ToString().Replace("_", " ")
        }), "Value", "Text", itemTypeFilter);

        ViewBag.StockLevelOptions = new SelectList(new[]
        {
          new { Value = "", Text = "All Stock Levels" },
          new { Value = "low", Text = "Low Stock" },
          new { Value = "out", Text = "Out of Stock" },
          new { Value = "overstock", Text = "Overstocked" },
          new { Value = "instock", Text = "In Stock" },
          new { Value = "tracked", Text = "Tracked Items" },
          new { Value = "nontracked", Text = "Non-Tracked Items" }
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

        // Set essential ViewBag properties that the view expects
        ViewBag.ErrorMessage = $"Error loading items: {ex.Message}";
        ViewBag.AllowedPageSizes = AllowedPageSizes;

        // Set pagination defaults to prevent null reference exceptions
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = 1;
        ViewBag.TotalCount = 0;
        ViewBag.HasPreviousPage = false;
        ViewBag.HasNextPage = false;
        ViewBag.ShowingFrom = 0;
        ViewBag.ShowingTo = 0;

        // Set filter defaults
        ViewBag.SearchTerm = search;
        ViewBag.ItemTypeFilter = itemTypeFilter;
        ViewBag.StockLevelFilter = stockLevelFilter;
        ViewBag.VendorFilter = vendorFilter;
        ViewBag.IsSellable = isSellable;
        ViewBag.SortOrder = sortOrder;
        ViewBag.IsFiltered = false;

        // Set empty dropdown options
        ViewBag.ItemTypeOptions = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.StockLevelOptions = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.VendorOptions = new SelectList(new List<object>(), "Value", "Text");

        return View(new List<Item>());
      }
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

    public async Task<IActionResult> Details(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null) return NotFound();

      // Load item versions for version dropdown
      var itemVersions = await _inventoryService.GetItemVersionsAsync(item.BaseItemId ?? item.Id);
      ViewBag.ItemVersions = itemVersions;

      // Load purchase history
      var purchases = await _purchaseService.GetPurchasesByItemIdAsync(id);
      ViewBag.Purchases = purchases;

      // Group purchases by item version for filtering
      var purchasesByVersion = purchases
          .GroupBy(p => p.ItemVersion ?? "N/A")
          .ToDictionary(g => g.Key, g => g.AsEnumerable());
      ViewBag.PurchasesByVersion = purchasesByVersion;

      // NEW: Load vendor relationships
      var vendorService = HttpContext.RequestServices.GetRequiredService<IVendorService>();
      var vendorItems = await vendorService.GetItemVendorsAsync(id);
      ViewBag.VendorItems = vendorItems;

      // Load financial data
      ViewBag.AverageCost = await _inventoryService.GetAverageCostAsync(id);
      ViewBag.FifoValue = await _inventoryService.GetFifoValueAsync(id);

      // Load pending change orders if applicable
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
          MaterialType = MaterialType.Standard // Set default
        };

        // Pass UOM options to the view
        ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList();
        
        // Load raw materials for parent selection
        await LoadRawMaterialsForView();

        // Load active vendors for preferred vendor selection
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
        // For non-material items, force MaterialType to Standard
        if (!viewModel.IsMaterialItem)
        {
          viewModel.MaterialType = MaterialType.Standard;
          viewModel.ParentRawMaterialId = null;
          viewModel.YieldFactor = null;
          viewModel.WastePercentage = null;
        }

        if (ModelState.IsValid)
        {
          // Create the Item entity from the ViewModel
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
            ItemType = viewModel.ItemType,
            Version = viewModel.Version,
            MaterialType = viewModel.MaterialType,
            ParentRawMaterialId = viewModel.ParentRawMaterialId,
            YieldFactor = viewModel.YieldFactor,
            WastePercentage = viewModel.WastePercentage
          };

          // Handle image upload if provided
          if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
          {
            using var memoryStream = new MemoryStream();
            await viewModel.ImageFile.CopyToAsync(memoryStream);
            item.ImageData = memoryStream.ToArray();
            item.ImageContentType = viewModel.ImageFile.ContentType;
            item.ImageFileName = viewModel.ImageFile.FileName;
          }

          // Create the item first
          await _inventoryService.CreateItemAsync(item);

          // Handle preferred vendor relationship if selected
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

      // Reload view data on error
      ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList();
      await LoadRawMaterialsForView();
      await LoadVendorsForView();

      return View(viewModel);
    }

    public async Task<IActionResult> Edit(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null) return NotFound();

      // Pass current item data to the view for editing
      return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Item item)
    {
      if (id != item.Id)
      {
        return NotFound();
      }

      try
      {
        if (ModelState.IsValid)
        {
          await _inventoryService.UpdateItemAsync(item);
          TempData["SuccessMessage"] = "Item updated successfully!";
          return RedirectToAction("Index");
        }
      }
      catch (DbUpdateConcurrencyException)
      {
        if (!ItemExists(item.Id))
        {
          return NotFound();
        }
        else
        {
          throw;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating item: {PartNumber}", item.PartNumber);
        ModelState.AddModelError("", $"Error updating item: {ex.Message}");
      }

      return View(item);
    }

    public async Task<IActionResult> Delete(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null) return NotFound();
      return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
      try
      {
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

      // For simplicity, return the original image
      // In a production system, you might want to generate actual thumbnails
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

    // Add this test action to ItemsController.cs
    public async Task<IActionResult> TestDocuments(int id)
    {
      try
      {
        // Test 1: Direct database query
        var documentsInDb = await _context.ItemDocuments
            .Where(d => d.ItemId == id)
            .ToListAsync();

        // Test 2: Item with documents loaded
        var itemWithDocs = await _context.Items
            .Include(i => i.DesignDocuments)
            .FirstOrDefaultAsync(i => i.Id == id);

        // Test 3: Service method
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

    // Add these new action methods to ItemsController

    [HttpGet]
    public IActionResult ImportVendorAssignments(string? importId)
    {
        // In a real implementation, you might store the ImportVendorAssignmentViewModel
        // in TempData, Session, or database temporarily
        if (TempData["VendorAssignments"] is string vendorAssignmentsJson)
        {
            var model = System.Text.Json.JsonSerializer.Deserialize<ImportVendorAssignmentViewModel>(vendorAssignmentsJson);
            return View(model);
        }

        // If no pending assignments, redirect to items
        TempData["InfoMessage"] = "No vendor assignments pending.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteVendorAssignments(ImportVendorAssignmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("ImportVendorAssignments", model);
        }

        try
        {
            var bulkUploadService = HttpContext.RequestServices.GetRequiredService<IBulkUploadService>();
            var result = await bulkUploadService.CompleteVendorAssignmentsAsync(model);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Summary;
                
                // Clear any stored vendor assignments
                TempData.Remove("VendorAssignments");
            }
            else
            {
                TempData["ErrorMessage"] = $"Assignment completed with errors: {string.Join(", ", result.Errors)}";
            }

            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error completing vendor assignments: {ex.Message}";
            return View("ImportVendorAssignments", model);
        }
    }

    // Add this new method to your ItemsController.cs for vendor-grouped bulk purchases
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVendorGroupedBulkPurchases(BulkPurchaseRequest model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                // Reload view data and return to form
                await ReloadBulkPurchaseViewData(model);
                return View("CreateBulkPurchaseRequest", model);
            }

            var selectedItems = model.ItemsToPurchase.Where(i => i.Selected).ToList();

            if (!selectedItems.Any())
            {
                TempData["ErrorMessage"] = "Please select at least one item to purchase.";
                await ReloadBulkPurchaseViewData(model);
                return View("CreateBulkPurchaseRequest", model);
            }

            // Validate that all selected items have vendors
            var itemsWithoutVendors = selectedItems.Where(i => !i.VendorId.HasValue).ToList();
            if (itemsWithoutVendors.Any())
            {
                TempData["ErrorMessage"] = "Please select a vendor for all selected items.";
                await ReloadBulkPurchaseViewData(model);
                return View("CreateBulkPurchaseRequest", model);
            }

            // FIX: Add null check before accessing .Value to prevent CS8629
            var vendorGroups = selectedItems
                .Where(i => i.VendorId.HasValue) // Additional safety check
                .GroupBy(i => i.VendorId!.Value) // Use null-forgiving operator since we've verified HasValue
                .ToList();
            
            var createdPurchaseOrders = new List<string>();

            foreach (var vendorGroup in vendorGroups)
            {
                var vendorId = vendorGroup.Key;
                var vendor = await _vendorService.GetVendorByIdAsync(vendorId);
                
                if (vendor == null)
                {
                    TempData["ErrorMessage"] = $"Vendor not found for ID {vendorId}.";
                    continue;
                }

                // Calculate totals for this vendor group
                var vendorItems = vendorGroup.ToList();
                var totalItemValue = vendorItems.Sum(i => i.QuantityToPurchase * i.EstimatedUnitCost);
                
                // Generate unique PO number for this vendor
                var purchaseOrderNumber = model.PurchaseOrderNumber ?? 
                    await _purchaseService.GeneratePurchaseOrderNumberAsync();

                // Apply vendor-specific shipping and tax (you may want to make these configurable)
                var shippingCost = CalculateShippingCost(totalItemValue, vendor);
                var taxAmount = CalculateTaxAmount(totalItemValue, vendor);

                // Create individual Purchase records for each item, with proportional costs
                foreach (var item in vendorItems)
                {
                    var itemValue = item.QuantityToPurchase * item.EstimatedUnitCost;
                    var proportionOfTotal = totalItemValue > 0 ? itemValue / totalItemValue : 0;
                    
                    // Calculate proportional shipping and tax for this item
                    var itemShippingCost = shippingCost * proportionOfTotal;
                    var itemTaxAmount = taxAmount * proportionOfTotal;

                    var purchase = new Purchase
                    {
                        ItemId = item.ItemId,
                        QuantityPurchased = item.QuantityToPurchase,
                        CostPerUnit = item.EstimatedUnitCost,
                        VendorId = vendorId,
                        PurchaseOrderNumber = purchaseOrderNumber,
                        Notes = $"{model.Notes} | Vendor Group PO | {item.Notes}".Trim(' ', '|'),
                        PurchaseDate = DateTime.Today,
                        RemainingQuantity = item.QuantityToPurchase,
                        CreatedDate = DateTime.Now,
                        
                        // NEW: Proportional shipping and tax allocation
                        ShippingCost = itemShippingCost,
                        TaxAmount = itemTaxAmount,
                        
                        Status = PurchaseStatus.Pending,
                        ExpectedDeliveryDate = model.ExpectedDeliveryDate
                    };

                    await _purchaseService.CreatePurchaseAsync(purchase);
                }

                createdPurchaseOrders.Add($"{vendor.CompanyName}: {purchaseOrderNumber}");
            }

            TempData["SuccessMessage"] = $"Successfully created {vendorGroups.Count} consolidated purchase orders: {string.Join(", ", createdPurchaseOrders)}";
            return RedirectToAction("Index", "Purchases");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error creating vendor-grouped bulk purchases: {ex.Message}";
            await ReloadBulkPurchaseViewData(model);
            return View("CreateBulkPurchaseRequest", model);
        }
    }

    // Helper method to calculate shipping costs based on order value and vendor
    private decimal CalculateShippingCost(decimal orderValue, Vendor vendor)
    {
        // Example shipping calculation logic - customize based on your business rules
        
        // Free shipping threshold check
        if (orderValue >= 500m) return 0m;
        
        // Flat rate for small orders
        if (orderValue < 100m) return 25m;
        
        // Percentage-based shipping for medium orders
        if (orderValue < 300m) return orderValue * 0.05m; // 5%
        
        // Reduced rate for larger orders
        return orderValue * 0.03m; // 3%
    }

    // Helper method to calculate tax based on order value and vendor location
    private decimal CalculateTaxAmount(decimal orderValue, Vendor vendor)
    {
        // Example tax calculation - customize based on your tax rules
        
        // You might want to store tax rate in Vendor entity or use a tax calculation service
        decimal taxRate = GetTaxRateForVendor(vendor);
        
        return orderValue * taxRate;
    }

    // Helper method to get tax rate for vendor (customize based on your needs)
    private decimal GetTaxRateForVendor(Vendor vendor)
    {
        // Example: Different tax rates based on vendor location
        // In a real implementation, you might:
        // 1. Store tax rate in Vendor entity
        // 2. Use a tax calculation service
        // 3. Look up rates by state/province
        
        // For now, return a default rate
        return 0.0725m; // 7.25 default tax rate for NC
    }

    // Helper methods
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

    private async Task CreateVendorItemRelationship(int itemId, int vendorId, string? vendorPartNumber)
    {
      try
      {
        // Check if relationship already exists
        var existingRelationship = await _context.VendorItems
          .FirstOrDefaultAsync(vi => vi.ItemId == itemId && vi.VendorId == vendorId);

        if (existingRelationship == null)
        {
          // Create new VendorItem relationship
          var vendorItem = new VendorItem
          {
            ItemId = itemId,
            VendorId = vendorId,
            VendorPartNumber = vendorPartNumber,
            IsPrimary = true,
            IsActive = true,
            UnitCost = 0, // Will be updated when purchases are made
            MinimumOrderQuantity = 1,
            LeadTimeDays = 0,
            LastUpdated = DateTime.Now
          };

          _context.VendorItems.Add(vendorItem);
          await _context.SaveChangesAsync();

          // Update the item's preferred vendor reference
          var item = await _context.Items.FindAsync(itemId);
          if (item != null)
          {
            item.PreferredVendorItemId = vendorItem.Id;
            await _context.SaveChangesAsync();
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating vendor-item relationship for Item {ItemId} and Vendor {VendorId}", itemId, vendorId);
        // Don't throw here as the item was already created successfully
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