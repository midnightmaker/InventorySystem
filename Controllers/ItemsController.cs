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

    public async Task<IActionResult> Index()
    {
      var items = await _inventoryService.GetAllItemsAsync();
      return View(items);
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
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessBulkUpload(BulkItemUploadViewModel viewModel)
    {
      // Add null check and logging
      _logger.LogInformation("ProcessBulkUpload called. ViewModel is null: {IsNull}", viewModel == null);
      
      if (viewModel == null)
      {
          _logger.LogWarning("ProcessBulkUpload received null viewModel");
          TempData["ErrorMessage"] = "Invalid form data. Please try uploading the file again.";
          return RedirectToAction("BulkUpload");
      }

      _logger.LogInformation("ProcessBulkUpload - PreviewItems count: {Count}", 
          viewModel.PreviewItems?.Count ?? 0);

      if (viewModel.PreviewItems == null || !viewModel.PreviewItems.Any())
      {
          _logger.LogWarning("ProcessBulkUpload - No preview items found");
          TempData["ErrorMessage"] = "No items to import. Please upload and validate a file first.";
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
            // NEW: Store detailed error information for display
            if (result.DetailedErrors.Any())
            {
              var errorDetails = System.Text.Json.JsonSerializer.Serialize(result.DetailedErrors);
              TempData["ImportErrors"] = errorDetails;
            }
            
            TempData["WarningMessage"] = $"{result.FailedImports} items failed to import. Click 'View Error Details' to see specific issues.";
          }

          // Check if there are vendor assignments that need user review
          if (result is IVendorAssignmentResult vendorAssignmentResult)
          {
            if (vendorAssignmentResult.VendorAssignments?.NewVendorRequests?.Any() == true)
            {
              var vendorAssignmentsJson = System.Text.Json.JsonSerializer.Serialize(vendorAssignmentResult.VendorAssignments);
              TempData["VendorAssignments"] = vendorAssignmentsJson;

              TempData["InfoMessage"] = "Items imported successfully. Please review vendor assignments.";
              return RedirectToAction("ImportVendorAssignments");
            }
          }
        }
        else
        {
          // NEW: Better error handling when no items were imported
          if (result.DetailedErrors.Any())
          {
            var errorDetails = System.Text.Json.JsonSerializer.Serialize(result.DetailedErrors);
            TempData["ImportErrors"] = errorDetails;
            TempData["ErrorMessage"] = $"No items were imported. {result.DetailedErrors.Count} items had errors. Click 'View Error Details' below.";
          }
          else
          {
            TempData["ErrorMessage"] = "No items were imported. " + string.Join("; ", result.Errors);
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during bulk import");
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
        await LoadRawMaterialsForView();
        return View(new CreateItemViewModel());
      }

      try
      {
        // Custom validation for transformed materials
        await ValidateTransformedMaterialAsync(viewModel);

        // Remove validation for optional fields
        ModelState.Remove("ImageFile");
        ModelState.Remove("Manufacturer");
        ModelState.Remove("ManufacturerPartNumber");

        // Conditional validation based on item type
        if (viewModel.ItemType != ItemType.Inventoried)
        {
          ModelState.Remove("MinimumStock");
          ModelState.Remove("InitialQuantity");
          ModelState.Remove("InitialCostPerUnit");
          ModelState.Remove("InitialVendor");
          ModelState.Remove("InitialPurchaseDate");
          ModelState.Remove("InitialPurchaseOrderNumber");
          viewModel.HasInitialPurchase = false;
        }

        // Conditional validation for initial purchase
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
          // Use database transaction to ensure data consistency
          using var transaction = await _context.Database.BeginTransactionAsync();
          
          try
          {
            var item = new Item
            {
              PartNumber = viewModel.PartNumber,
              Description = viewModel.Description,
              Comments = viewModel.Comments ?? string.Empty,
              MinimumStock = viewModel.ItemType == ItemType.Inventoried ? viewModel.MinimumStock : 0,
              CurrentStock = 0,
              UnitOfMeasure = viewModel.UnitOfMeasure,
              VendorPartNumber = viewModel.VendorPartNumber,
              IsSellable = viewModel.IsSellable,
              ItemType = viewModel.ItemType,
              Version = viewModel.Version,
              MaterialType = viewModel.MaterialType,
              ParentRawMaterialId = viewModel.ParentRawMaterialId,
              YieldFactor = viewModel.YieldFactor,
              WastePercentage = viewModel.WastePercentage,
              IsCurrentVersion = true,
              CreatedDate = DateTime.Now
            };

            // Handle image upload with validation
            if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
            {
              var imageValidation = await ValidateAndProcessImageAsync(viewModel.ImageFile);
              if (imageValidation.IsValid)
              {
                item.ImageData = imageValidation.ImageData;
                item.ImageContentType = imageValidation.ContentType;
                item.ImageFileName = imageValidation.FileName;
              }
              else
              {
                ModelState.AddModelError("ImageFile", imageValidation.ErrorMessage);
                ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList(viewModel.UnitOfMeasure);
                await LoadRawMaterialsForView();
                return View(viewModel);
              }
            }

            var createdItem = await _inventoryService.CreateItemAsync(item);
            _logger.LogInformation("Item created successfully: {PartNumber} (ID: {ItemId})", createdItem.PartNumber, createdItem.Id);

            // Create Manufacturing BOM for transformed materials
            string bomMessage = "";
            if (viewModel.IsTransformedMaterial && viewModel.ParentRawMaterialId.HasValue)
            {
              await CreateManufacturingBomAsync(createdItem, viewModel);
              var parentName = await GetParentMaterialName(viewModel.ParentRawMaterialId.Value);
              bomMessage = $" Manufacturing BOM '{createdItem.PartNumber}-MFG' created from {parentName}.";
              _logger.LogInformation("Manufacturing BOM created for transformed item: {PartNumber}", createdItem.PartNumber);
            }

            // Handle vendor relationship creation
            Vendor? vendor = null;
            if (!string.IsNullOrWhiteSpace(viewModel.PreferredVendor))
            {
              vendor = await FindOrCreateVendorAsync(viewModel.PreferredVendor);
              await CreateVendorItemRelationshipAsync(vendor, createdItem, viewModel);
              _logger.LogInformation("Vendor relationship created: {VendorName} for item {PartNumber}", vendor.CompanyName, createdItem.PartNumber);
            }

            // Create initial purchase if specified
            if (viewModel.HasInitialPurchase &&
                viewModel.ItemType == ItemType.Inventoried &&
                viewModel.InitialQuantity > 0 &&
                viewModel.InitialCostPerUnit > 0 &&
                !string.IsNullOrWhiteSpace(viewModel.InitialVendor))
            {
              if (vendor == null || vendor.CompanyName != viewModel.InitialVendor)
              {
                vendor = await FindOrCreateVendorAsync(viewModel.InitialVendor);
              }

              await CreateInitialPurchaseAsync(createdItem, vendor, viewModel);
              
              var manufacturerInfo = !string.IsNullOrWhiteSpace(viewModel.Manufacturer) ? $" (MFG: {viewModel.Manufacturer})" : "";
              TempData["SuccessMessage"] = $"Item created successfully with initial purchase of {viewModel.InitialQuantity} {UnitOfMeasureHelper.GetAbbreviation(viewModel.UnitOfMeasure)} from {vendor.CompanyName}{manufacturerInfo}!{bomMessage}";
              _logger.LogInformation("Initial purchase created for item: {PartNumber}, Qty: {Quantity}, Vendor: {VendorName}", 
                createdItem.PartNumber, viewModel.InitialQuantity, vendor.CompanyName);
            }
            else
            {
              var itemTypeMsg = viewModel.ItemType == ItemType.Inventoried ? "inventoried item" : $"{viewModel.ItemType.ToString().ToLower()} item";
              var manufacturerInfo = !string.IsNullOrWhiteSpace(viewModel.Manufacturer) ? $" (MFG: {viewModel.Manufacturer})" : "";
              TempData["SuccessMessage"] = $"New {itemTypeMsg} created successfully! Unit: {UnitOfMeasureHelper.GetAbbreviation(viewModel.UnitOfMeasure)}{manufacturerInfo}{bomMessage}";
            }

            await transaction.CommitAsync();
            return RedirectToAction("Details", new { id = createdItem.Id });
          }
          catch (Exception ex)
          {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error during item creation transaction for part number: {PartNumber}", viewModel.PartNumber);
            throw;
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating item: {PartNumber}", viewModel.PartNumber);
        ModelState.AddModelError("", $"Error creating item: {ex.Message}");
      }

      // Reload form data if validation fails
      ViewBag.UnitOfMeasureOptions = UnitOfMeasureHelper.GetGroupedUnitOfMeasureSelectList(viewModel.UnitOfMeasure);
      await LoadRawMaterialsForView();
      return View(viewModel);
    }

    #region Validation Methods

    private async Task ValidateTransformedMaterialAsync(CreateItemViewModel viewModel)
    {
      if (viewModel.MaterialType == MaterialType.Transformed)
      {
        // Validate parent raw material is selected
        if (!viewModel.ParentRawMaterialId.HasValue)
        {
          ModelState.AddModelError("ParentRawMaterialId", "Parent raw material is required for transformed materials.");
        }
        else
        {
          // Validate parent material exists and is a raw material
          var parentMaterial = await _context.Items.FindAsync(viewModel.ParentRawMaterialId.Value);
          if (parentMaterial == null)
          {
            ModelState.AddModelError("ParentRawMaterialId", "Selected parent raw material does not exist.");
          }
          else if (parentMaterial.MaterialType != MaterialType.RawMaterial)
          {
            ModelState.AddModelError("ParentRawMaterialId", "Parent material must be of type 'Raw Material'.");
          }
          else if (!parentMaterial.TrackInventory)
          {
            ModelState.AddModelError("ParentRawMaterialId", "Parent raw material must be an inventoried item.");
          }
        }

        // Validate yield factor
        if (!viewModel.YieldFactor.HasValue)
        {
          ModelState.AddModelError("YieldFactor", "Yield factor is required for transformed materials.");
        }
        else if (viewModel.YieldFactor <= 0 || viewModel.YieldFactor > 1)
        {
          ModelState.AddModelError("YieldFactor", "Yield factor must be between 0.01 and 1.0 (1% to 100%).");
        }

        // Validate waste percentage
        if (viewModel.WastePercentage.HasValue && (viewModel.WastePercentage < 0 || viewModel.WastePercentage > 50))
        {
          ModelState.AddModelError("WastePercentage", "Waste percentage must be between 0 and 50%.");
        }

        // Check for circular references
        if (viewModel.ParentRawMaterialId.HasValue)
        {
          var hasCircularReference = await CheckForCircularReferenceAsync(null, viewModel.ParentRawMaterialId.Value);
          if (hasCircularReference)
          {
            ModelState.AddModelError("ParentRawMaterialId", "Circular reference detected. This would create an infinite loop in the material hierarchy.");
          }
        }

        // Check for duplicate part numbers with same yield factor
        var existingTransformedItem = await _context.Items
          .FirstOrDefaultAsync(i => 
            i.PartNumber != viewModel.PartNumber &&
            i.ParentRawMaterialId == viewModel.ParentRawMaterialId &&
            i.YieldFactor == viewModel.YieldFactor);
        
        if (existingTransformedItem != null)
        {
          ModelState.AddModelError("YieldFactor", $"A transformed material with the same parent and yield factor already exists: {existingTransformedItem.PartNumber}");
        }
      }
    }

    private async Task<bool> CheckForCircularReferenceAsync(int? itemId, int parentId)
    {
      // If we're checking the same item, we have a circular reference
      if (itemId.HasValue && itemId.Value == parentId)
        return true;

      // Get the parent item and check its parent recursively
      var parentItem = await _context.Items.FindAsync(parentId);
      if (parentItem?.ParentRawMaterialId.HasValue == true)
      {
        return await CheckForCircularReferenceAsync(itemId, parentItem.ParentRawMaterialId.Value);
      }

      return false;
    }

    private async Task<(bool IsValid, byte[]? ImageData, string? ContentType, string? FileName, string ErrorMessage)> ValidateAndProcessImageAsync(IFormFile imageFile)
    {
      try
      {
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/webp" };
        if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
        {
          return (false, null, null, null, "Please upload a valid image file (JPEG, PNG, GIF, BMP, WebP).");
        }

        if (imageFile.Length > 5 * 1024 * 1024) // 5MB limit
        {
          return (false, null, null, null, "Image file size must be less than 5MB.");
        }

        using var memoryStream = new MemoryStream();
        await imageFile.CopyToAsync(memoryStream);
        return (true, memoryStream.ToArray(), imageFile.ContentType, imageFile.FileName, "");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing image file: {FileName}", imageFile.FileName);
        return (false, null, null, null, "Error processing image file.");
      }
    }

    #endregion

    #region Helper Methods

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

    private async Task<string> GetParentMaterialName(int parentRawMaterialId)
    {
      try
      {
        var parent = await _context.Items.FindAsync(parentRawMaterialId);
        return parent?.PartNumber ?? "unknown material";
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting parent material name for ID: {ParentId}", parentRawMaterialId);
        return "unknown material";
      }
    }

    private async Task<Vendor> FindOrCreateVendorAsync(string vendorName)
    {
      var vendor = await _context.Vendors
        .FirstOrDefaultAsync(v => v.CompanyName.ToLower() == vendorName.ToLower());

      if (vendor == null)
      {
        vendor = new Vendor
        {
          CompanyName = vendorName,
          IsActive = true,
          CreatedDate = DateTime.Now,
          LastUpdated = DateTime.Now,
          QualityRating = 3,
          DeliveryRating = 3,
          ServiceRating = 3
        };
        _context.Vendors.Add(vendor);
        await _context.SaveChangesAsync();
        _logger.LogInformation("New vendor created: {VendorName}", vendorName);
      }

      return vendor;
    }

    private async Task CreateVendorItemRelationshipAsync(Vendor vendor, Item item, CreateItemViewModel viewModel)
    {
      var vendorItem = new VendorItem
      {
        VendorId = vendor.Id,
        ItemId = item.Id,
        VendorPartNumber = viewModel.VendorPartNumber,
        Manufacturer = viewModel.Manufacturer,
        ManufacturerPartNumber = viewModel.ManufacturerPartNumber,
        IsPrimary = true,
        IsActive = true,
        LastUpdated = DateTime.Now
      };

      _context.VendorItems.Add(vendorItem);
      await _context.SaveChangesAsync();

      // Set as preferred vendor
      item.PreferredVendorItemId = vendorItem.Id;
      await _inventoryService.UpdateItemAsync(item);
    }

    private async Task CreateInitialPurchaseAsync(Item item, Vendor vendor, CreateItemViewModel viewModel)
    {
      var initialPurchase = new Purchase
      {
        ItemId = item.Id,
        VendorId = vendor.Id,
        PurchaseDate = viewModel.InitialPurchaseDate ?? DateTime.Today,
        QuantityPurchased = viewModel.InitialQuantity,
        CostPerUnit = viewModel.InitialCostPerUnit,
        PurchaseOrderNumber = viewModel.InitialPurchaseOrderNumber,
        Notes = $"Initial inventory entry - {viewModel.InitialQuantity} {UnitOfMeasureHelper.GetAbbreviation(viewModel.UnitOfMeasure)}",
        Status = PurchaseStatus.Received,
        RemainingQuantity = viewModel.InitialQuantity,
        CreatedDate = DateTime.Now
      };

      await _purchaseService.CreatePurchaseAsync(initialPurchase);
    }

    #endregion

    #region Manufacturing BOM Creation

    private async Task CreateManufacturingBomAsync(Item transformedItem, CreateItemViewModel viewModel)
    {
      if (!viewModel.ParentRawMaterialId.HasValue || !viewModel.YieldFactor.HasValue)
      {
        _logger.LogWarning("CreateManufacturingBomAsync called with missing required data for item: {PartNumber}", transformedItem.PartNumber);
        return;
      }

      try
      {
        // Create the manufacturing BOM using service layer
        var manufacturingBom = new Bom
        {
          BomNumber = $"{transformedItem.PartNumber}-MFG",
          Description = $"Manufacturing BOM for {transformedItem.Description}",
          Version = "A",
          IsCurrentVersion = true,
          CreatedDate = DateTime.Now,
          ModifiedDate = DateTime.Now
        };

        var createdBom = await _bomService.CreateBomAsync(manufacturingBom);
        _logger.LogInformation("Manufacturing BOM created: {BomNumber} for item {PartNumber}", createdBom.BomNumber, transformedItem.PartNumber);

        // Calculate required raw material quantity based on yield factor
        var requiredQuantity = Math.Round(1.0m / (decimal)viewModel.YieldFactor.Value, 4);

        // Add raw material requirement
        var bomItem = new BomItem
        {
          BomId = createdBom.Id,
          ItemId = viewModel.ParentRawMaterialId.Value,
          Quantity = (int)requiredQuantity, // <-- FIX: Explicit cast from decimal to int
          Notes = $"Raw material for {transformedItem.PartNumber}. Yield: {viewModel.YieldFactor:P2}"
        };

        await _bomService.AddBomItemAsync(bomItem);

        // Log the creation details
        var parentMaterial = await _context.Items.FindAsync(viewModel.ParentRawMaterialId.Value);
        _logger.LogInformation("BOM item added: {ParentPartNumber} - Required quantity: {RequiredQuantity} for BOM {BomNumber}", 
          parentMaterial?.PartNumber, requiredQuantity, createdBom.BomNumber);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating manufacturing BOM for item: {PartNumber}", transformedItem.PartNumber);
        throw new InvalidOperationException($"Failed to create manufacturing BOM for {transformedItem.PartNumber}: {ex.Message}", ex);
      }
    }

    #endregion

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
            // item.PreferredVendor = selectedVendor.CompanyName; // <-- REMOVE THIS LINE
            item.PreferredVendorItemId = selectedVendor.Id; // <-- SET THE ID INSTEAD
            Console.WriteLine($"Set preferred vendor to: {selectedVendor.CompanyName}");
          }
          else
          {
            Console.WriteLine($"Vendor with ID {preferredVendorId.Value} not found");
            item.PreferredVendorItemId = null; // <-- CLEAR THE ID
          }
        }
        else
        {
          Console.WriteLine("No preferred vendor selected - clearing field");
          item.PreferredVendorItemId = null; // <-- CLEAR THE ID
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

    // Add these new action methods to ItemsController

    [HttpGet]
    public async Task<IActionResult> ImportVendorAssignments(string? importId)
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

            // NEW: Group items by vendor for consolidated purchase orders
            var vendorGroups = selectedItems.GroupBy(i => i.VendorId.Value).ToList();
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