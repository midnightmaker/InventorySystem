// Controllers/PurchasesController.cs - Clean implementation with vendor selection
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Controllers
{
  public class PurchasesController : Controller
  {
    private readonly IPurchaseService _purchaseService;
    private readonly IInventoryService _inventoryService;
    private readonly IVendorService _vendorService;
    private readonly InventoryContext _context;

    public PurchasesController(
        IPurchaseService purchaseService,
        IInventoryService inventoryService,
        IVendorService vendorService,
        InventoryContext context)
    {
      _purchaseService = purchaseService;
      _inventoryService = inventoryService;
      _vendorService = vendorService;
      _context = context;
    }

    public async Task<IActionResult> Index()
    {
      try
      {
        var purchases = await _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.Vendor)
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

    [HttpGet]
    public async Task<IActionResult> Create(int? itemId)
    {
      try
      {
        var items = await _inventoryService.GetAllItemsAsync();
        var vendors = await _vendorService.GetActiveVendorsAsync();

        var viewModel = new CreatePurchaseViewModel();

        // If itemId is provided, set it and get the last vendor used for this item
        if (itemId.HasValue)
        {
          viewModel.ItemId = itemId.Value;

          // Get the last vendor used for this item
          var lastVendorId = await _purchaseService.GetLastVendorIdForItemAsync(itemId.Value);
          if (lastVendorId.HasValue)
          {
            viewModel.VendorId = lastVendorId.Value;
          }
        }

        // Format items dropdown with part number and description
        var formattedItems = items.Select(item => new
        {
          Value = item.Id,
          Text = $"{item.PartNumber} - {item.Description}"
        }).ToList();

        ViewBag.ItemId = new SelectList(formattedItems, "Value", "Text", viewModel.ItemId);
        ViewBag.VendorId = new SelectList(vendors, "Id", "CompanyName", viewModel.VendorId);

        return View(viewModel);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Create GET: {ex.Message}");
        ViewBag.ErrorMessage = ex.Message;
        return View(new CreatePurchaseViewModel());
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePurchaseViewModel viewModel)
    {
      if (!ModelState.IsValid)
      {
        // Reload dropdowns
        await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId);
        return View(viewModel);
      }

      try
      {
        // Convert ViewModel to Purchase entity
        var purchase = new Purchase
        {
          ItemId = viewModel.ItemId,
          VendorId = viewModel.VendorId,
          PurchaseDate = viewModel.PurchaseDate,
          QuantityPurchased = viewModel.QuantityPurchased,
          CostPerUnit = viewModel.CostPerUnit,
          ShippingCost = viewModel.ShippingCost,
          TaxAmount = viewModel.TaxAmount,
          PurchaseOrderNumber = viewModel.PurchaseOrderNumber,
          Notes = viewModel.Notes,
          Status = viewModel.Status,
          ExpectedDeliveryDate = viewModel.ExpectedDeliveryDate,
          ActualDeliveryDate = viewModel.ActualDeliveryDate,
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

        // Reload dropdowns
        await ReloadDropdownsAsync(viewModel.ItemId, viewModel.VendorId);
        return View(viewModel);
      }
    }

    // GET: Purchases/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
      try
      {
        var purchase = await _context.Purchases
            .Include(p => p.Vendor)
            .Include(p => p.Item)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (purchase == null)
        {
          TempData["ErrorMessage"] = "Purchase not found.";
          return RedirectToAction("Index");
        }

        await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
        return View(purchase);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Edit GET: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading purchase for editing: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Purchase purchase)
    {
      if (id != purchase.Id)
      {
        return NotFound();
      }

      if (ModelState.IsValid)
      {
        try
        {
          await _purchaseService.UpdatePurchaseAsync(purchase);
          TempData["SuccessMessage"] = "Purchase updated successfully!";
          return RedirectToAction("Details", new { id = purchase.Id });
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error updating purchase: {ex.Message}");
          ModelState.AddModelError("", $"Error updating purchase: {ex.Message}");
        }
      }

      // Reload dropdowns
      await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
      return View(purchase);
    }

    public async Task<IActionResult> Details(int id)
    {
      try
      {
        var purchase = await _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.Vendor)
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

    // Helper method to reload dropdowns
    private async Task ReloadDropdownsAsync(int selectedItemId = 0, int? selectedVendorId = null)
    {
      var items = await _inventoryService.GetAllItemsAsync();
      var vendors = await _vendorService.GetActiveVendorsAsync();

      var formattedItems = items.Select(item => new
      {
        Value = item.Id,
        Text = $"{item.PartNumber} - {item.Description}"
      }).ToList();

      ViewBag.ItemId = new SelectList(formattedItems, "Value", "Text", selectedItemId);
      ViewBag.VendorId = new SelectList(vendors, "Id", "CompanyName", selectedVendorId);
    }

    // AJAX endpoint to get last vendor for an item
    [HttpGet]
    public async Task<IActionResult> GetLastVendorForItem(int itemId)
    {
      try
      {
        var lastVendorId = await _purchaseService.GetLastVendorIdForItemAsync(itemId);
        return Json(new { success = true, vendorId = lastVendorId });
      }
      catch (Exception ex)
      {
        return Json(new { success = false, error = ex.Message });
      }
    }

    // GET: Purchases/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
      try
      {
        var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
        if (purchase == null)
        {
          TempData["ErrorMessage"] = "Purchase not found.";
          return RedirectToAction("Index");
        }

        return View(purchase);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Delete GET: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading purchase for deletion: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
      try
      {
        await _purchaseService.DeletePurchaseAsync(id);
        TempData["SuccessMessage"] = "Purchase deleted successfully!";
        return RedirectToAction("Index");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error deleting purchase: {ex.Message}");
        TempData["ErrorMessage"] = $"Error deleting purchase: {ex.Message}";
        return RedirectToAction("Index");
      }
    }
  }
}