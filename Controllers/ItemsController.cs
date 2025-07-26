using Microsoft.AspNetCore.Mvc;
using InventorySystem.Models;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;

namespace InventorySystem.Controllers
{
  public class ItemsController : Controller
  {
    private readonly IInventoryService _inventoryService;
    private readonly IPurchaseService _purchaseService;
    private readonly InventoryContext _context;

    public ItemsController(IInventoryService inventoryService, IPurchaseService purchaseService, InventoryContext context)
    {
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
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

      ViewBag.AverageCost = await _inventoryService.GetAverageCostAsync(id);
      ViewBag.FifoValue = await _inventoryService.GetFifoValueAsync(id);
      ViewBag.Purchases = await _purchaseService.GetPurchasesByItemIdAsync(id);

      return View(item);
    }

    public IActionResult Create()
    {
      var viewModel = new CreateItemViewModel
      {
        InitialPurchaseDate = DateTime.Today
      };
      return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateItemViewModel viewModel)
    {
      if (viewModel == null)
      {
        ModelState.AddModelError("", "ViewModel is null");
        return View(new CreateItemViewModel());
      }

      // Remove validation for optional fields
      ModelState.Remove("ImageFile");

      // CRITICAL FIX: Remove validation for initial purchase fields when not selected
      if (!viewModel.HasInitialPurchase)
      {
        ModelState.Remove("InitialQuantity");
        ModelState.Remove("InitialCostPerUnit");
        ModelState.Remove("InitialVendor");
        ModelState.Remove("InitialPurchaseDate");
        ModelState.Remove("InitialPurchaseOrderNumber");
      }
      else
      {
        // Only validate initial purchase fields if HasInitialPurchase is true
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
            MinimumStock = viewModel.MinimumStock,
            CurrentStock = 0 // Will be updated if initial purchase is added
          };

          // Handle image upload
          if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
          {
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
            if (!allowedTypes.Contains(viewModel.ImageFile.ContentType.ToLower()))
            {
              ModelState.AddModelError("ImageFile", "Please upload a valid image file (JPG, PNG, GIF, BMP).");
              return View(viewModel);
            }

            if (viewModel.ImageFile.Length > 5 * 1024 * 1024) // 5MB limit
            {
              ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
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

          // Create initial purchase ONLY if HasInitialPurchase is true AND all required fields are provided
          if (viewModel.HasInitialPurchase &&
              viewModel.InitialQuantity > 0 &&
              viewModel.InitialCostPerUnit > 0 &&
              !string.IsNullOrWhiteSpace(viewModel.InitialVendor))
          {
            var initialPurchase = new Purchase
            {
              ItemId = createdItem.Id,
              Vendor = viewModel.InitialVendor,
              PurchaseDate = viewModel.InitialPurchaseDate ?? DateTime.Today,
              QuantityPurchased = viewModel.InitialQuantity,
              CostPerUnit = viewModel.InitialCostPerUnit,
              PurchaseOrderNumber = viewModel.InitialPurchaseOrderNumber,
              Notes = "Initial inventory entry"
            };

            await _purchaseService.CreatePurchaseAsync(initialPurchase);

            TempData["SuccessMessage"] = $"Item created successfully with initial purchase of {viewModel.InitialQuantity} units!";
          }
          else
          {
            TempData["SuccessMessage"] = "Item created successfully!";
          }

          return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
          ModelState.AddModelError("", $"Error creating item: {ex.Message}");
        }
      }

      // Debug: Log validation errors
      Console.WriteLine("=== VALIDATION ERRORS ===");
      foreach (var error in ModelState)
      {
        if (error.Value.Errors.Any())
        {
          Console.WriteLine($"{error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
        }
      }

      ViewBag.ModelStateErrors = ModelState
          .Where(x => x.Value.Errors.Count > 0)
          .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) });

      return View(viewModel);
    }

    public async Task<IActionResult> Edit(int id)
    {
      var item = await _inventoryService.GetItemByIdAsync(id);
      if (item == null) return NotFound();
      return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Item item, IFormFile? newImageFile)
    {
      if (id != item.Id) return NotFound();

      if (ModelState.IsValid)
      {
        try
        {
          // Handle new image upload
          if (newImageFile != null && newImageFile.Length > 0)
          {
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
            if (!allowedTypes.Contains(newImageFile.ContentType.ToLower()))
            {
              ModelState.AddModelError("newImageFile", "Please upload a valid image file (JPG, PNG, GIF, BMP).");
              return View(item);
            }

            if (newImageFile.Length > 5 * 1024 * 1024) // 5MB limit
            {
              ModelState.AddModelError("newImageFile", "Image file size must be less than 5MB.");
              return View(item);
            }

            using (var memoryStream = new MemoryStream())
            {
              await newImageFile.CopyToAsync(memoryStream);
              item.ImageData = memoryStream.ToArray();
              item.ImageContentType = newImageFile.ContentType;
              item.ImageFileName = newImageFile.FileName;
            }
          }

          await _inventoryService.UpdateItemAsync(item);
          TempData["SuccessMessage"] = "Item updated successfully!";
          return RedirectToAction("Details", new { id = item.Id });
        }
        catch (Exception ex)
        {
          ModelState.AddModelError("", $"Error updating item: {ex.Message}");
        }
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

    // Test this by navigating to: /Items/TestDocuments/1 (replace 1 with actual item ID)
  }
}