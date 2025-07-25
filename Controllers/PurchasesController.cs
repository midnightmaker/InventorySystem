using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Models;
using InventorySystem.Services;
using InventorySystem.Data;
using InventorySystem.ViewModels; 

namespace InventorySystem.Controllers
{
  public class PurchasesController : Controller
  {
    private readonly IPurchaseService _purchaseService;
    private readonly IInventoryService _inventoryService;
    private readonly InventoryContext _context;

    public PurchasesController(IPurchaseService purchaseService, IInventoryService inventoryService, InventoryContext context)
    {
      _purchaseService = purchaseService;
      _inventoryService = inventoryService;
      _context = context;
    }

    // TEST ACTION - Add this to verify controller is working
    public IActionResult Test()
    {
      return Json(new
      {
        Success = true,
        Message = "PurchasesController is working!",
        Timestamp = DateTime.Now,
        ControllerName = "Purchases"
      });
    }

    public async Task<IActionResult> Index()
    {
      try
      {
        var purchases = await _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.PurchaseDocuments)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();
        return View(purchases);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Index: {ex.Message}");
        ViewBag.ErrorMessage = ex.Message;
        return View(new List<Purchase>());
      }
    }

    // Update your PurchasesController Create actions:

    [HttpGet]
    public async Task<IActionResult> Create(int? itemId)
    {
      try
      {
        var items = await _inventoryService.GetAllItemsAsync();
        ViewBag.ItemId = new SelectList(items, "Id", "PartNumber", itemId);

        var viewModel = new CreatePurchaseViewModel
        {
          PurchaseDate = DateTime.Today
        };

        if (itemId.HasValue)
        {
          viewModel.ItemId = itemId.Value;
        }

        return View(viewModel);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Create GET: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading create form: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePurchaseViewModel viewModel)
    {
      Console.WriteLine("=== CREATE POST WITH VIEWMODEL ===");
      Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
      Console.WriteLine($"ItemId: {viewModel.ItemId}");
      Console.WriteLine($"Vendor: '{viewModel.Vendor}'");
      Console.WriteLine($"Quantity: {viewModel.QuantityPurchased}");
      Console.WriteLine($"Cost: {viewModel.CostPerUnit}");

      if (!ModelState.IsValid)
      {
        Console.WriteLine("ModelState Errors:");
        foreach (var error in ModelState)
        {
          if (error.Value.Errors.Any())
          {
            Console.WriteLine($"  {error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
          }
        }

        // Reload dropdown
        var items = await _inventoryService.GetAllItemsAsync();
        ViewBag.ItemId = new SelectList(items, "Id", "PartNumber", viewModel.ItemId);
        return View(viewModel);
      }

      try
      {
        // Convert ViewModel to Purchase entity
        var purchase = new Purchase
        {
          ItemId = viewModel.ItemId,
          Vendor = viewModel.Vendor,
          PurchaseDate = viewModel.PurchaseDate,
          QuantityPurchased = viewModel.QuantityPurchased,
          CostPerUnit = viewModel.CostPerUnit,
          ShippingCost = viewModel.ShippingCost,
          TaxAmount = viewModel.TaxAmount,
          PurchaseOrderNumber = viewModel.PurchaseOrderNumber,
          Notes = viewModel.Notes,
          RemainingQuantity = viewModel.QuantityPurchased,
          CreatedDate = DateTime.Now
        };

        Console.WriteLine("Creating purchase from ViewModel...");
        await _purchaseService.CreatePurchaseAsync(purchase);

        Console.WriteLine($"Purchase created successfully with ID: {purchase.Id}");
        TempData["SuccessMessage"] = $"Purchase recorded successfully! ID: {purchase.Id}";
        return RedirectToAction("Index");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error creating purchase: {ex.Message}");
        ModelState.AddModelError("", $"Error creating purchase: {ex.Message}");

        var items = await _inventoryService.GetAllItemsAsync();
        ViewBag.ItemId = new SelectList(items, "Id", "PartNumber", viewModel.ItemId);
        return View(viewModel);
      }
    }

    // GET: Purchases/Details/5
    public async Task<IActionResult> Details(int id)
    {
      try
      {
        var purchase = await _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.PurchaseDocuments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (purchase == null)
        {
          TempData["ErrorMessage"] = "Purchase not found.";
          return RedirectToAction("Index");
        }

        return View(purchase);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Details: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading purchase details: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // Additional test method to verify routing
    [HttpGet]
    public IActionResult CreateTest()
    {
      return Json(new
      {
        Success = true,
        Message = "Create GET route is working",
        Timestamp = DateTime.Now
      });
    }
  }
}