using Microsoft.AspNetCore.Mvc;
using InventorySystem.Models;
using InventorySystem.Services;
using InventorySystem.ViewModels;

namespace InventorySystem.Controllers
{
    public class ItemsController : Controller
    {
        private readonly IInventoryService _inventoryService;
        private readonly IPurchaseService _purchaseService;
        
        public ItemsController(IInventoryService inventoryService, IPurchaseService purchaseService)
        {
            _inventoryService = inventoryService;
            _purchaseService = purchaseService;
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

    // Replace the Create actions in your ItemsController.cs with these:

    public IActionResult Create()
    {
      var viewModel = new CreateItemViewModel
      {
        // Initialize with default values
       
        InitialPurchaseDate = DateTime.Today
      };
      return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateItemViewModel viewModel)
    {
      // Debug: Check if we're receiving the viewModel
      if (viewModel == null)
      {
        ModelState.AddModelError("", "ViewModel is null");
        return View(new CreateItemViewModel());
      }

      // Remove validation for optional fields
      ModelState.Remove("ImageFile");
      if (!viewModel.HasInitialPurchase)
      {
        ModelState.Remove("InitialQuantity");
        ModelState.Remove("InitialCostPerUnit");
        ModelState.Remove("InitialVendor");
        ModelState.Remove("InitialPurchaseDate");
        ModelState.Remove("InitialPurchaseOrderNumber");
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
            CurrentStock = 0 // Will be set by initial purchase if provided
          };

          // Handle image upload
          if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
          {
            // Validate image file
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

          // Create the item first
          var createdItem = await _inventoryService.CreateItemAsync(item);

          // Create initial purchase if provided
          if (viewModel.HasInitialPurchase &&
              viewModel.InitialQuantity > 0 &&
              viewModel.InitialCostPerUnit > 0 &&
              !string.IsNullOrEmpty(viewModel.InitialVendor))
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
          }

          TempData["SuccessMessage"] = "Item created successfully!";
          return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
          ModelState.AddModelError("", $"Error creating item: {ex.Message}");
        }
      }

      // If we got this far, something failed, redisplay form
      // Add debug info to help troubleshoot
      ViewBag.ModelStateErrors = ModelState
          .Where(x => x.Value.Errors.Count > 0)
          .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) });

      return View(viewModel);
    }
  }
}