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
        bool? isExpense,
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
        _logger.LogInformation("Is Expense: {IsExpense}", isExpense);
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

        // Apply item type filter - ENHANCED to handle comma-separated values
        if (!string.IsNullOrWhiteSpace(itemTypeFilter))
        {
          _logger.LogInformation("Applying item type filter: {ItemTypeFilter}", itemTypeFilter);
          
          // Handle comma-separated ItemType filters (e.g., "Inventoried,Consumable")
          var itemTypeStrings = itemTypeFilter.Split(',', StringSplitOptions.RemoveEmptyEntries);
          var validItemTypes = new List<ItemType>();
          
          foreach (var itemTypeString in itemTypeStrings)
          {
            if (Enum.TryParse<ItemType>(itemTypeString.Trim(), out var itemType))
            {
              validItemTypes.Add(itemType);
            }
          }
          
          if (validItemTypes.Any())
          {
            query = query.Where(i => validItemTypes.Contains(i.ItemType));
            _logger.LogInformation("Filtered by item types: {ItemTypes}", string.Join(", ", validItemTypes));
          }
        }

        // Apply stock level filter
        if (!string.IsNullOrWhiteSpace(stockLevelFilter))
        {
          _logger.LogInformation("Applying stock level filter: {StockLevelFilter}", stockLevelFilter);
          query = stockLevelFilter switch
          {
            "low" => query.Where(i => !i.IsExpense && 
                                     (i.ItemType == ItemType.Inventoried || 
                                      i.ItemType == ItemType.Consumable || 
                                      i.ItemType == ItemType.RnDMaterials) && 
                                     i.CurrentStock <= i.MinimumStock),
            "out" => query.Where(i => !i.IsExpense && 
                                     (i.ItemType == ItemType.Inventoried || 
                                      i.ItemType == ItemType.Consumable || 
                                      i.ItemType == ItemType.RnDMaterials) && 
                                     i.CurrentStock == 0),
            "overstock" => query.Where(i => !i.IsExpense && 
                                           (i.ItemType == ItemType.Inventoried || 
                                            i.ItemType == ItemType.Consumable || 
                                            i.ItemType == ItemType.RnDMaterials) && 
                                           i.CurrentStock > (i.MinimumStock * 2)),
            "instock" => query.Where(i => !i.IsExpense && 
                                         (i.ItemType == ItemType.Inventoried || 
                                          i.ItemType == ItemType.Consumable || 
                                          i.ItemType == ItemType.RnDMaterials) && 
                                         i.CurrentStock > 0),
            "tracked" => query.Where(i => !i.IsExpense && 
                                         (i.ItemType == ItemType.Inventoried || 
                                          i.ItemType == ItemType.Consumable || 
                                          i.ItemType == ItemType.RnDMaterials)),
            "nontracked" => query.Where(i => i.IsExpense || 
                                            !(i.ItemType == ItemType.Inventoried || 
                                              i.ItemType == ItemType.Consumable || 
                                              i.ItemType == ItemType.RnDMaterials)),
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

        // Apply expense filter
        if (isExpense.HasValue)
        {
          _logger.LogInformation("Applying expense filter: {IsExpense}", isExpense.Value);
          query = query.Where(i => i.IsExpense == isExpense.Value);
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
        ViewBag.IsExpense = isExpense;
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
                           isSellable.HasValue ||
                           isExpense.HasValue;

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
					bool isExpenseItem = IsExpenseItemType(viewModel.ItemType);
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
						IsSellable = isExpenseItem ? false : viewModel.IsSellable,
						IsExpense = isExpenseItem,
						SalePrice = isExpenseItem ? null : viewModel.SalePrice,
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
		private static bool IsExpenseItemType(ItemType itemType)
		{
			return itemType == ItemType.Expense ||
						 itemType == ItemType.Utility ||
						 itemType == ItemType.Subscription ||
						 itemType == ItemType.Service ||
						 itemType == ItemType.Virtual;
		}
		public async Task<IActionResult> Edit(int id)
		{
			var item = await _inventoryService.GetItemByIdAsync(id);
			if (item == null) return NotFound();

			// ✅ FIX: Use IsExpense flag directly instead of inferring
			if (item.IsExpense)
			{
				return RedirectToAction("EditExpense", new { id });
			}

			// Load vendors for the preferred vendor dropdown
			await LoadVendorsForEditView(item);
			return View(item);
		}

		// GET: /Items/EditExpense/5 - Dedicated expense item editing
		public async Task<IActionResult> EditExpense(int id)
    {
      try
      {
        var item = await _inventoryService.GetItemByIdAsync(id);
        if (item == null) 
        {
          TempData["ErrorMessage"] = "Item not found.";
          return RedirectToAction("Index");
        }

        // Verify this is actually an expense item
        if (!item.IsExpense)
        {
          TempData["ErrorMessage"] = "This item is not an expense item. Redirecting to standard edit view.";
          return RedirectToAction("Edit", new { id });
        }

        // Create view model from item
        var viewModel = new EditExpenseItemViewModel
        {
          Id = item.Id,
          PartNumber = item.PartNumber,
          Description = item.Description,
          Comments = item.Comments,
          UnitOfMeasure = item.UnitOfMeasure,
          VendorPartNumber = item.VendorPartNumber,
          ItemType = item.ItemType,
          Version = item.Version,
          PreferredVendorId = item.PreferredVendorItem?.VendorId,
          HasImage = item.HasImage,
          ImageFileName = item.ImageFileName,
          CreatedDate = item.CreatedDate
        };

        // Load active vendors for preferred vendor selection
        await LoadVendorsForView();

        return View(viewModel);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading expense item for edit: {ItemId}", id);
        TempData["ErrorMessage"] = $"Error loading expense item: {ex.Message}";
        return RedirectToAction("Index", new { isExpense = true });
      }
    }

    // POST: /Items/EditExpense/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditExpense(int id, EditExpenseItemViewModel viewModel)
    {
      if (id != viewModel.Id)
      {
        return NotFound();
      }

      try
      {
        // Validate that this is indeed an expense type
        if (viewModel.ItemType != ItemType.Expense && 
            viewModel.ItemType != ItemType.Utility && 
            viewModel.ItemType != ItemType.Subscription &&
            viewModel.ItemType != ItemType.Service &&
            viewModel.ItemType != ItemType.Virtual)
        {
          ModelState.AddModelError("ItemType", "Invalid expense type selected.");
        }

        if (ModelState.IsValid)
        {
          // Get the existing item
          var existingItem = await _inventoryService.GetItemByIdAsync(id);
          if (existingItem == null)
          {
            TempData["ErrorMessage"] = "Item not found.";
            return RedirectToAction("Index", new { isExpense = true });
          }

          // Update the item properties
          existingItem.PartNumber = viewModel.PartNumber;
          existingItem.Description = viewModel.Description;
          existingItem.Comments = viewModel.Comments ?? string.Empty;
          existingItem.UnitOfMeasure = viewModel.UnitOfMeasure;
          existingItem.VendorPartNumber = viewModel.VendorPartNumber;
          existingItem.ItemType = viewModel.ItemType;
          // Note: Version, CreatedDate, and expense status should not be changed

          // Handle image upload if provided
          if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
          {
            using var memoryStream = new MemoryStream();
            await viewModel.ImageFile.CopyToAsync(memoryStream);
            existingItem.ImageData = memoryStream.ToArray();
            existingItem.ImageContentType = viewModel.ImageFile.ContentType;
            existingItem.ImageFileName = viewModel.ImageFile.FileName;
          }

          // Update the item
          await _inventoryService.UpdateItemAsync(existingItem);

          // Handle preferred vendor relationship changes
          await UpdateVendorItemRelationship(existingItem.Id, viewModel.PreferredVendorId, viewModel.VendorPartNumber);

          TempData["SuccessMessage"] = $"{viewModel.ItemType} item updated successfully!";
          
          // Redirect back to the appropriate expense filter
          return RedirectToAction("Index", new { itemTypeFilter = viewModel.ItemType.ToString() });
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating expense item: {PartNumber}", viewModel.PartNumber);
        ModelState.AddModelError("", $"Error updating expense item: {ex.Message}");
      }

      // Reload view data on error
      await LoadVendorsForView();
      return View(viewModel);
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

    // Helper methods
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

    // Helper method to load raw materials for parent selection
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
        
        // Set up the preferred vendor dropdown
        ViewBag.PreferredVendorId = new SelectList(vendors.OrderBy(v => v.CompanyName), "Id", "CompanyName", item.PreferredVendorItem?.VendorId);
        
        // Store the current preferred vendor info for display
        ViewBag.CurrentPreferredVendorId = item.PreferredVendorItem?.VendorId;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading vendors for edit view");
        ViewBag.PreferredVendorId = new SelectList(new List<object>(), "Id", "CompanyName");
        ViewBag.CurrentPreferredVendorId = null;
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

    private async Task UpdateVendorItemRelationship(int itemId, int? newVendorId, string? vendorPartNumber)
    {
      try
      {
        // Get current item to check existing preferred vendor
        var item = await _context.Items
          .Include(i => i.PreferredVendorItem)
          .FirstOrDefaultAsync(i => i.Id == itemId);
        
        if (item == null) return;

        // If no new vendor selected, remove existing preferred vendor relationship
        if (!newVendorId.HasValue)
        {
          if (item.PreferredVendorItem != null)
          {
            // Don't delete the VendorItem, just remove the preferred reference
            item.PreferredVendorItemId = null;
            await _context.SaveChangesAsync();
          }
          return;
        }

        // Check if vendor relationship already exists
        var existingVendorItem = await _context.VendorItems
          .FirstOrDefaultAsync(vi => vi.ItemId == itemId && vi.VendorId == newVendorId.Value);

        if (existingVendorItem == null)
        {
          // Create new VendorItem relationship
          existingVendorItem = new VendorItem
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

          _context.VendorItems.Add(existingVendorItem);
          await _context.SaveChangesAsync();
        }
        else
        {
          // Update existing vendor item with new part number
          existingVendorItem.VendorPartNumber = vendorPartNumber;
          existingVendorItem.LastUpdated = DateTime.Now;
        }

        // Update the item's preferred vendor reference
        item.PreferredVendorItemId = existingVendorItem.Id;
        await _context.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating vendor-item relationship for Item {ItemId} and Vendor {VendorId}", itemId, newVendorId);
        // Don't throw here as the item update was already successful
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

    // GET: /Items/CreateExpense - Dedicated expense item creation
    public async Task<IActionResult> CreateExpense(string? itemType = "Expense")
    {
        try
        {
            // Validate the itemType is an expense type
            if (!Enum.TryParse<ItemType>(itemType, out var expenseType) || 
                (expenseType != ItemType.Expense && expenseType != ItemType.Utility && 
                 expenseType != ItemType.Subscription && expenseType != ItemType.Service && 
                 expenseType != ItemType.Virtual))
            {
                expenseType = ItemType.Expense;
            }

            var viewModel = new CreateExpenseItemViewModel
            {
                ItemType = expenseType,
                Version = "A",
                UnitOfMeasure = UnitOfMeasure.Each
            };

            // Load active vendors for preferred vendor selection
            await LoadVendorsForView();

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading create expense item form");
            TempData["ErrorMessage"] = $"Error loading create form: {ex.Message}";
            return RedirectToAction("Index", new { itemTypeFilter = itemType });
        }
    }

    // POST: /Items/CreateExpense
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExpense(CreateExpenseItemViewModel viewModel)
    {
        if (viewModel == null)
        {
            ModelState.AddModelError("", "Expense item data is missing.");
            return View(new CreateExpenseItemViewModel());
        }

        try
        {
            // Validate that this is indeed an expense type
            if (viewModel.ItemType != ItemType.Expense && 
                viewModel.ItemType != ItemType.Utility && 
                viewModel.ItemType != ItemType.Subscription &&
                viewModel.ItemType != ItemType.Service &&
                viewModel.ItemType != ItemType.Virtual)
            {
                ModelState.AddModelError("ItemType", "Invalid expense type selected.");
            }

            if (ModelState.IsValid)
            {
                // Create the Item entity from the expense ViewModel
                var item = new Item
                {
                    PartNumber = viewModel.PartNumber,
                    Description = viewModel.Description,
                    Comments = viewModel.Comments ?? string.Empty,
                    MinimumStock = 0, // Expenses don't track stock
                    CurrentStock = 0,
                    CreatedDate = DateTime.Now,
                    UnitOfMeasure = viewModel.UnitOfMeasure,
                    VendorPartNumber = viewModel.VendorPartNumber,
                    IsSellable = false, // Expenses are not sellable
                    IsExpense = true, // Mark as expense item
                    ItemType = viewModel.ItemType,
                    Version = viewModel.Version,
                    MaterialType = MaterialType.Standard, // Expenses use standard material type
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

                // Create the item
                await _inventoryService.CreateItemAsync(item);

                // Handle preferred vendor relationship if selected
                if (viewModel.PreferredVendorId.HasValue)
                {
                    await CreateVendorItemRelationship(item.Id, viewModel.PreferredVendorId.Value, viewModel.VendorPartNumber);
                }

                TempData["SuccessMessage"] = $"{viewModel.ItemType} item created successfully!";
                
                // Redirect back to the appropriate expense filter
                return RedirectToAction("Index", new { itemTypeFilter = viewModel.ItemType.ToString() });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense item: {PartNumber}", viewModel.PartNumber);
            ModelState.AddModelError("", $"Error creating expense item: {ex.Message}");
        }

        // Reload view data on error
        await LoadVendorsForView();
        return View(viewModel);
    }

    // GET: Test endpoint to verify ProcessBulkUpload is working
    [HttpGet]
    public IActionResult TestProcessBulkUpload()
    {
        return Json(new
        {
            Success = true,
            Message = "ProcessBulkUpload endpoint is accessible",
            Route = "Items/ProcessBulkUpload",
            Method = "POST",
            RequiredFields = new[]
            {
                "PreviewItems[].PartNumber",
                "PreviewItems[].Description",
                "PreviewItems[].Comments",
                "PreviewItems[].MinimumStock",
                "PreviewItems[].RowNumber",
                "PreviewItems[].VendorPartNumber",
                "PreviewItems[].PreferredVendor",
                "PreviewItems[].Manufacturer",
                "PreviewItems[].ManufacturerPartNumber",
                "PreviewItems[].IsSellable",
                "PreviewItems[].IsExpense",
                "PreviewItems[].ItemType",
                "PreviewItems[].Version",
                "PreviewItems[].UnitOfMeasure",
                "PreviewItems[].InitialQuantity",
                "PreviewItems[].InitialCostPerUnit",
                "PreviewItems[].InitialVendor",
                "PreviewItems[].InitialPurchaseDate",
                "PreviewItems[].InitialPurchaseOrderNumber"
            },
            Timestamp = DateTime.Now
        });
    }

    // POST: Test endpoint to verify ProcessBulkUpload model binding
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestProcessBulkUpload(BulkItemUploadViewModel model)
    {
        _logger.LogInformation("TestProcessBulkUpload called. Model is null: {IsNull}", model == null);
        _logger.LogInformation("PreviewItems count: {Count}", model?.PreviewItems?.Count ?? 0);

        return Json(new
        {
            Success = true,
            Message = "Test ProcessBulkUpload received data successfully",
            ModelIsNull = model == null,
            PreviewItemsCount = model?.PreviewItems?.Count ?? 0,
            PreviewItemsData = model?.PreviewItems?.Take(3)?.Select(p => new
            {
                p.PartNumber,
                p.Description,
                p.ItemType,
                p.RowNumber
            }),
            ModelStateIsValid = ModelState.IsValid,
            ModelStateErrors = ModelState.Where(ms => ms.Value.Errors.Any())
                .ToDictionary(ms => ms.Key, ms => ms.Value.Errors.Select(e => e.ErrorMessage)),
            Timestamp = DateTime.Now
        });
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
				if (ModelState.IsValid)
				{
					var existingItem = await _inventoryService.GetItemByIdAsync(id);
					if (existingItem == null)
					{
						TempData["ErrorMessage"] = "Item not found.";
						return RedirectToAction("Index");
					}

					// Update the allowed properties (preserve restricted ones)
					existingItem.PartNumber = model.PartNumber;
					existingItem.Description = model.Description;
					existingItem.Comments = model.Comments ?? string.Empty;
					existingItem.ItemType = model.ItemType;
					existingItem.UnitOfMeasure = model.UnitOfMeasure;
					existingItem.MinimumStock = model.MinimumStock;
					existingItem.VendorPartNumber = model.VendorPartNumber;
					existingItem.SalePrice = model.SalePrice;

					// ✅ FIX: Use the existing IsExpense flag directly
					existingItem.IsSellable = model.IsSellable;
					existingItem.IsExpense = model.IsExpense; // Preserve the original flag

					// Handle image upload if provided
					if (newImageFile != null && newImageFile.Length > 0)
					{
						// ... existing image handling code ...
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
	}
}