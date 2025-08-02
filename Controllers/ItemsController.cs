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

    public ItemsController(IInventoryService inventoryService, IPurchaseService purchaseService, 
      IVendorService vendorService, InventoryContext context, IVersionControlService versionService)
    {
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
      _versionService = versionService;
      _vendorService = vendorService;
      _context = context;

    }


    public async Task<IActionResult> Index()
    {
      var items = await _inventoryService.GetAllItemsAsync();
      return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null) return NotFound();

      // ? ADD: Get all versions for this item (similar to BOM implementation)
      var itemVersions = await _versionService.GetItemVersionsAsync(item.BaseItemId ?? item.Id);
      ViewBag.ItemVersions = itemVersions;

      // ? ADD: Get purchases filtered by version (similar to BOM implementation)
      var allPurchases = await _purchaseService.GetPurchasesByItemIdAsync(id);

      // Group purchases by version for the filter dropdown
      var purchasesByVersion = allPurchases
          .GroupBy(p => p.ItemVersion ?? "N/A")
          .ToDictionary(g => g.Key, g => g.AsEnumerable());
      ViewBag.PurchasesByVersion = purchasesByVersion;

      ViewBag.AverageCost = await _inventoryService.GetAverageCostAsync(id);
      ViewBag.FifoValue = await _inventoryService.GetFifoValueAsync(id);
      ViewBag.Purchases = allPurchases; // ? CHANGED: Use allPurchases instead of specific call

      // Check for pending change orders
      var pendingChangeOrders = await _versionService.GetPendingChangeOrdersForEntityAsync("Item", item.BaseItemId ?? item.Id);
      ViewBag.PendingChangeOrders = pendingChangeOrders;
      ViewBag.EntityType = "Item";

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
          UnitOfMeasure = UnitOfMeasure.Each, // Set default to "Each"
          InitialPurchaseDate = DateTime.Today
        };

        // Pass UOM options to the view
        ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList();

        return View(viewModel);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading create form: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpload(BulkItemUploadViewModel viewModel)
    {
      if (viewModel.CsvFile == null)
      {
        ModelState.AddModelError("CsvFile", "Please select a CSV file to upload.");
        return View(viewModel);
      }

      // Validate file type
      var allowedExtensions = new[] { ".csv" };
      var fileExtension = Path.GetExtension(viewModel.CsvFile.FileName).ToLower();

      if (!allowedExtensions.Contains(fileExtension))
      {
        ModelState.AddModelError("CsvFile", "Please upload a valid CSV file (.csv).");
        return View(viewModel);
      }

      // Validate file size (10MB limit)
      if (viewModel.CsvFile.Length > 10 * 1024 * 1024)
      {
        ModelState.AddModelError("CsvFile", "File size must be less than 10MB.");
        return View(viewModel);
      }

      try
      {
        var bulkUploadService = HttpContext.RequestServices.GetRequiredService<IBulkUploadService>();
        viewModel.ValidationResults = await bulkUploadService.ValidateCsvFileAsync(viewModel.CsvFile, viewModel.SkipHeaderRow);

        if (viewModel.ValidationResults.Any())
        {
          viewModel.PreviewItems = viewModel.ValidationResults
              .Where(vr => vr.IsValid)
              .Select(vr => vr.ItemData!)
              .ToList();
        }

        if (viewModel.ValidItemsCount == 0)
        {
          viewModel.ErrorMessage = "No valid items found in the CSV file. Please check the format and data.";
        }
        else if (viewModel.InvalidItemsCount > 0)
        {
          viewModel.ErrorMessage = $"Found {viewModel.InvalidItemsCount} invalid items. Please review and correct the errors.";
        }
      }
      catch (Exception ex)
      {
        ModelState.AddModelError("", $"Error processing file: {ex.Message}");
      }

      return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessBulkUpload(BulkItemUploadViewModel viewModel)
    {
      if (viewModel.PreviewItems == null || !viewModel.PreviewItems.Any())
      {
        TempData["ErrorMessage"] = "No items to import.";
        return RedirectToAction("BulkUpload");
      }

      try
      {
        var bulkUploadService = HttpContext.RequestServices.GetRequiredService<IBulkUploadService>();
        var result = await bulkUploadService.ImportValidItemsAsync(viewModel.PreviewItems);

        if (result.SuccessfulImports > 0)
        {
          TempData["SuccessMessage"] = $"Successfully imported {result.SuccessfulImports} items.";

          if (result.FailedImports > 0)
          {
            TempData["WarningMessage"] = $"{result.FailedImports} items failed to import.";
          }
        }
        else
        {
          TempData["ErrorMessage"] = "No items were imported. " + string.Join("; ", result.Errors);
        }
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error during import: {ex.Message}";
      }

      return RedirectToAction("Index");
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateItemViewModel viewModel)
    {
      if (viewModel == null)
      {
        ModelState.AddModelError("", "ViewModel is null");
        ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList();
        return View(new CreateItemViewModel());
      }

      // Remove validation for optional fields
      ModelState.Remove("ImageFile");

      // Remove stock-related validation for non-inventoried items
      if (viewModel.ItemType != ItemType.Inventoried)
      {
        ModelState.Remove("MinimumStock");
        ModelState.Remove("InitialQuantity");
        ModelState.Remove("InitialCostPerUnit");
        ModelState.Remove("InitialVendor");
        ModelState.Remove("InitialPurchaseDate");
        ModelState.Remove("InitialPurchaseOrderNumber");

        // Force HasInitialPurchase to false for non-inventoried items
        viewModel.HasInitialPurchase = false;
      }

      // Remove validation for initial purchase fields when not selected
      if (!viewModel.HasInitialPurchase || viewModel.ItemType != ItemType.Inventoried)
      {
        ModelState.Remove("InitialQuantity");
        ModelState.Remove("InitialCostPerUnit");
        ModelState.Remove("InitialVendor");
        ModelState.Remove("InitialPurchaseDate");
        ModelState.Remove("InitialPurchaseOrderNumber");
      }
      else
      {
        // Only validate initial purchase fields if HasInitialPurchase is true and item is inventoried
        if (viewModel.InitialQuantity <= 0)
        {
          ModelState.AddModelError("InitialQuantity", "Initial quantity must be greater than 0 when adding initial purchase.");
        }

        if (viewModel.InitialCostPerUnit <= 0)
        {
          ModelState.AddModelError("InitialCostPerUnit", "Initial cost per unit must be greater than 0 when adding initial purchase.");
        }

        if (string.IsNullOrWhiteSpace(viewModel.InitialVendor))
        {
          ModelState.AddModelError("InitialVendor", "Initial vendor is required when adding initial purchase.");
        }
      }

      if (ModelState.IsValid)
      {
        try
        {
          var item = new Item
          {
            PartNumber = viewModel.PartNumber,
            Description = viewModel.Description,
            Comments = viewModel.Comments ?? string.Empty,
            MinimumStock = viewModel.ItemType == ItemType.Inventoried ? viewModel.MinimumStock : 0,
            CurrentStock = 0, // Will be updated if initial purchase is added
            UnitOfMeasure = viewModel.UnitOfMeasure, // NEW: Set the Unit of Measure

            // NEW PHASE 1 PROPERTIES
            VendorPartNumber = viewModel.VendorPartNumber,
            PreferredVendor = viewModel.PreferredVendor,
            IsSellable = viewModel.IsSellable,
            ItemType = viewModel.ItemType,
            Version = viewModel.Version
          };

          // Handle image upload (existing code)
          if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
          {
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
            if (!allowedTypes.Contains(viewModel.ImageFile.ContentType.ToLower()))
            {
              ModelState.AddModelError("ImageFile", "Please upload a valid image file (JPG, PNG, GIF, BMP).");
              ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList(viewModel.UnitOfMeasure);
              return View(viewModel);
            }

            if (viewModel.ImageFile.Length > 5 * 1024 * 1024) // 5MB limit
            {
              ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
              ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList(viewModel.UnitOfMeasure);
              return View(viewModel);
            }

            using (var memoryStream = new MemoryStream())
            {
              await viewModel.ImageFile.CopyToAsync(memoryStream);
              item.ImageData = memoryStream.ToArray();
              item.ImageContentType = viewModel.ImageFile.ContentType;
              item.ImageFileName = viewModel.ImageFile.FileName;
            }
          }

          var createdItem = await _inventoryService.CreateItemAsync(item);

          // Create initial purchase ONLY if HasInitialPurchase is true AND item is inventoried
          if (viewModel.HasInitialPurchase &&
              viewModel.ItemType == ItemType.Inventoried &&
              viewModel.InitialQuantity > 0 &&
              viewModel.InitialCostPerUnit > 0 &&
              !string.IsNullOrWhiteSpace(viewModel.InitialVendor))
          {
            // Find the vendor by name to get the VendorId
            var vendor = await _context.Vendors
              .FirstOrDefaultAsync(v => v.CompanyName.ToLower() == viewModel.InitialVendor.ToLower());

            if (vendor == null)
            {
              // Create a new vendor if it doesn't exist
              vendor = new Vendor
              {
                CompanyName = viewModel.InitialVendor,
                IsActive = true,
                CreatedDate = DateTime.Now,
                LastUpdated = DateTime.Now,
                QualityRating = 3,
                DeliveryRating = 3,
                ServiceRating = 3
              };

              _context.Vendors.Add(vendor);
              await _context.SaveChangesAsync();
            }

            var initialPurchase = new Purchase
            {
              ItemId = createdItem.Id,
              VendorId = vendor.Id, // Use VendorId instead of Vendor string
              PurchaseDate = viewModel.InitialPurchaseDate ?? DateTime.Today,
              QuantityPurchased = viewModel.InitialQuantity,
              CostPerUnit = viewModel.InitialCostPerUnit,
              PurchaseOrderNumber = viewModel.InitialPurchaseOrderNumber,
              Notes = $"Initial inventory entry - {viewModel.InitialQuantity} {UnitOfMeasureHelper.GetAbbreviation(viewModel.UnitOfMeasure)}",
              Status = PurchaseStatus.Received, // Mark as received since it's initial inventory
              RemainingQuantity = viewModel.InitialQuantity,
              CreatedDate = DateTime.Now
            };

            await _purchaseService.CreatePurchaseAsync(initialPurchase);

            TempData["SuccessMessage"] = $"Item created successfully with initial purchase of {viewModel.InitialQuantity} {UnitOfMeasureHelper.GetAbbreviation(viewModel.UnitOfMeasure)} from {vendor.CompanyName}!";
          }
          else
          {
            var itemTypeMsg = viewModel.ItemType == ItemType.Inventoried ? "inventoried item" : $"{viewModel.ItemType.ToString().ToLower()} item";
            TempData["SuccessMessage"] = $"New {itemTypeMsg} created successfully! Unit: {UnitOfMeasureHelper.GetAbbreviation(viewModel.UnitOfMeasure)}";
          }

          return RedirectToAction("Details", new { id = createdItem.Id });
        }
        catch (Exception ex)
        {
          ModelState.AddModelError("", $"Error creating item: {ex.Message}");
        }
      }


      // Reload UOM options if validation fails
      ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList(viewModel.UnitOfMeasure);
      return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
      try
      {
        Console.WriteLine($"=== ITEM EDIT GET DEBUG ===");
        Console.WriteLine($"Loading item with ID: {id}");

        var item = await _inventoryService.GetItemByIdAsync(id);
        if (item == null)
        {
          Console.WriteLine("Item not found");
          TempData["ErrorMessage"] = "Item not found.";
          return RedirectToAction("Index");
        }

        Console.WriteLine($"Item loaded: {item.PartNumber}");
        Console.WriteLine($"Current preferred vendor: {item.PreferredVendor}");

        // Load vendors for the preferred vendor dropdown
        var vendors = await _vendorService.GetActiveVendorsAsync();
        Console.WriteLine($"Loaded {vendors.Count()} active vendors");

        // Find the current preferred vendor ID if a preferred vendor name exists
        int? currentPreferredVendorId = null;
        if (!string.IsNullOrEmpty(item.PreferredVendor))
        {
          var currentVendor = vendors.FirstOrDefault(v =>
            v.CompanyName.Equals(item.PreferredVendor, StringComparison.OrdinalIgnoreCase));
          currentPreferredVendorId = currentVendor?.Id;
          Console.WriteLine($"Current preferred vendor ID: {currentPreferredVendorId}");
        }

        // Prepare dropdown data
        var vendorSelectList = new List<SelectListItem>
    {
      new SelectListItem { Value = "", Text = "-- No Preferred Vendor --", Selected = !currentPreferredVendorId.HasValue }
    };

        vendorSelectList.AddRange(vendors.Select(v => new SelectListItem
        {
          Value = v.Id.ToString(),
          Text = v.CompanyName,
          Selected = v.Id == currentPreferredVendorId
        }));

        ViewBag.PreferredVendorId = vendorSelectList;
        ViewBag.CurrentPreferredVendorId = currentPreferredVendorId;

        // Pass UOM options to the view with current selection
        ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList(item.UnitOfMeasure);

        Console.WriteLine("View data prepared successfully");
        return View(item);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Edit GET: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        TempData["ErrorMessage"] = $"Error loading item for editing: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // Controllers/ItemsController.cs - Service layer approach (RECOMMENDED)

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Item item, IFormFile? newImageFile, int? preferredVendorId)
    {
      Console.WriteLine($"=== ITEM EDIT POST DEBUG ===");
      Console.WriteLine($"Item ID: {id}");
      Console.WriteLine($"Received Item ID: {item.Id}");
      Console.WriteLine($"Preferred Vendor ID: {preferredVendorId}");
      Console.WriteLine($"Part Number: {item.PartNumber}");

      if (id != item.Id)
      {
        Console.WriteLine("ID mismatch - returning NotFound");
        return NotFound();
      }

      // Handle preferred vendor selection
      try
      {
        if (preferredVendorId.HasValue && preferredVendorId.Value > 0)
        {
          var selectedVendor = await _vendorService.GetVendorByIdAsync(preferredVendorId.Value);
          if (selectedVendor != null)
          {
            item.PreferredVendor = selectedVendor.CompanyName;
            Console.WriteLine($"Set preferred vendor to: {item.PreferredVendor}");
          }
          else
          {
            Console.WriteLine($"Vendor with ID {preferredVendorId.Value} not found");
            item.PreferredVendor = null;
          }
        }
        else
        {
          Console.WriteLine("No preferred vendor selected - clearing field");
          item.PreferredVendor = null;
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error handling preferred vendor: {ex.Message}");
      }

      // Remove validation for fields we don't want to validate
      ModelState.Remove("ImageFile");
      ModelState.Remove("newImageFile");
      ModelState.Remove("preferredVendorId");
      ModelState.Remove("UnitOfMeasureDisplayName");
      ModelState.Remove("TotalValue");
      ModelState.Remove("IsLowStock");

      if (ModelState.IsValid)
      {
        try
        {
          // Handle image upload if provided
          if (newImageFile != null && newImageFile.Length > 0)
          {
            Console.WriteLine($"Processing image upload: {newImageFile.FileName}");

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(newImageFile.ContentType.ToLower()))
            {
              Console.WriteLine($"Invalid file type: {newImageFile.ContentType}");
              ModelState.AddModelError("newImageFile", "Please upload a valid image file (JPEG, PNG, GIF, WebP).");
            }
            else if (newImageFile.Length > 5 * 1024 * 1024) // 5MB limit
            {
              Console.WriteLine($"File too large: {newImageFile.Length} bytes");
              ModelState.AddModelError("newImageFile", "Image file size cannot exceed 5MB.");
            }
            else
            {
              using var memoryStream = new MemoryStream();
              await newImageFile.CopyToAsync(memoryStream);
              item.ImageData = memoryStream.ToArray();
              item.ImageContentType = newImageFile.ContentType;
              item.ImageFileName = newImageFile.FileName;
              Console.WriteLine("Image uploaded successfully");
            }
          }

          // Only proceed if no image validation errors
          if (ModelState.IsValid)
          {
            Console.WriteLine("Calling UpdateItemAsync service method...");

            // **USE SERVICE LAYER TO HANDLE EF TRACKING**
            // The service layer should handle the tracking properly
            await _inventoryService.UpdateItemAsync(item);

            Console.WriteLine("Item updated successfully!");
            TempData["SuccessMessage"] = $"Item '{item.PartNumber}' updated successfully!";
            return RedirectToAction("Details", new { id = item.Id });
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error updating item: {ex.Message}");
          Console.WriteLine($"Stack trace: {ex.StackTrace}");
          ModelState.AddModelError("", $"Error updating item: {ex.Message}");
        }
      }

      // Log validation errors
      if (!ModelState.IsValid)
      {
        Console.WriteLine("=== VALIDATION ERRORS ===");
        foreach (var error in ModelState)
        {
          if (error.Value.Errors.Any())
          {
            Console.WriteLine($"{error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
          }
        }
      }

      // Reload dropdown data for return to view
      await ReloadEditViewData(preferredVendorId, item.UnitOfMeasure);
      return View(item);
    }

    // Helper method to reload view data
    private async Task ReloadEditViewData(int? preferredVendorId, UnitOfMeasure unitOfMeasure)
    {
      try
      {
        var vendors = await _vendorService.GetActiveVendorsAsync();
        var vendorSelectList = new List<SelectListItem>
    {
      new SelectListItem { Value = "", Text = "-- No Preferred Vendor --", Selected = !preferredVendorId.HasValue }
    };

        vendorSelectList.AddRange(vendors.Select(v => new SelectListItem
        {
          Value = v.Id.ToString(),
          Text = v.CompanyName,
          Selected = v.Id == preferredVendorId
        }));

        ViewBag.PreferredVendorId = vendorSelectList;
        ViewBag.CurrentPreferredVendorId = preferredVendorId;
        ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList(unitOfMeasure);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error reloading view data: {ex.Message}");
        ViewBag.PreferredVendorId = new List<SelectListItem>();
        ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList(unitOfMeasure);
      }
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



  }
}