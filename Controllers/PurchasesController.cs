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

    // Controllers/PurchasesController.cs - Enhanced Index method with search functionality

    public async Task<IActionResult> Index(
        string search,
        string vendorFilter,
        string statusFilter,
        DateTime? startDate,
        DateTime? endDate,
        string sortOrder = "date_desc",
        int page = 1)
    {
      try
      {
        Console.WriteLine($"=== PURCHASES INDEX DEBUG ===");
        Console.WriteLine($"Search: {search}");
        Console.WriteLine($"Vendor Filter: {vendorFilter}");
        Console.WriteLine($"Status Filter: {statusFilter}");
        Console.WriteLine($"Date Range: {startDate} to {endDate}");
        Console.WriteLine($"Sort Order: {sortOrder}");

        // Start with base query
        var query = _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.Vendor)
            .Include(p => p.PurchaseDocuments)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
          var searchTerm = search.Trim().ToLower();
          Console.WriteLine($"Applying search filter: {searchTerm}");

          query = query.Where(p =>
            // Search in Item Part Number and Description
            p.Item.PartNumber.ToLower().Contains(searchTerm) ||
            p.Item.Description.ToLower().Contains(searchTerm) ||
            // Search in Vendor Company Name
            p.Vendor.CompanyName.ToLower().Contains(searchTerm) ||
            // Search in Purchase Order Number
            (!string.IsNullOrEmpty(p.PurchaseOrderNumber) && p.PurchaseOrderNumber.ToLower().Contains(searchTerm)) ||
            // Search in Notes
            (!string.IsNullOrEmpty(p.Notes) && p.Notes.ToLower().Contains(searchTerm)) ||
            // Search in Purchase ID (convert to string)
            p.Id.ToString().Contains(searchTerm)
          );
        }

        // Apply vendor filter
        if (!string.IsNullOrWhiteSpace(vendorFilter) && int.TryParse(vendorFilter, out int vendorId))
        {
          Console.WriteLine($"Applying vendor filter: {vendorId}");
          query = query.Where(p => p.VendorId == vendorId);
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<PurchaseStatus>(statusFilter, out var status))
        {
          Console.WriteLine($"Applying status filter: {status}");
          query = query.Where(p => p.Status == status);
        }

        // Apply date range filter
        if (startDate.HasValue)
        {
          Console.WriteLine($"Applying start date filter: {startDate.Value}");
          query = query.Where(p => p.PurchaseDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
          Console.WriteLine($"Applying end date filter: {endDate.Value}");
          var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
          query = query.Where(p => p.PurchaseDate <= endOfDay);
        }

        // Apply sorting
        query = sortOrder switch
        {
          "date_asc" => query.OrderBy(p => p.PurchaseDate),
          "date_desc" => query.OrderByDescending(p => p.PurchaseDate),
          "vendor_asc" => query.OrderBy(p => p.Vendor.CompanyName),
          "vendor_desc" => query.OrderByDescending(p => p.Vendor.CompanyName),
          "item_asc" => query.OrderBy(p => p.Item.PartNumber),
          "item_desc" => query.OrderByDescending(p => p.Item.PartNumber),
          "amount_asc" => query.OrderBy(p => p.QuantityPurchased * p.CostPerUnit),
          "amount_desc" => query.OrderByDescending(p => p.QuantityPurchased * p.CostPerUnit),
          "status_asc" => query.OrderBy(p => p.Status),
          "status_desc" => query.OrderByDescending(p => p.Status),
          _ => query.OrderByDescending(p => p.PurchaseDate)
        };

        // Get results
        var purchases = await query.ToListAsync();
        Console.WriteLine($"Found {purchases.Count} purchases after filtering");

        // Get filter options for dropdowns
        var allVendors = await _vendorService.GetActiveVendorsAsync();
        var purchaseStatuses = Enum.GetValues<PurchaseStatus>().ToList();

        // Prepare ViewBag data
        ViewBag.SearchTerm = search;
        ViewBag.VendorFilter = vendorFilter;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
        ViewBag.SortOrder = sortOrder;
        ViewBag.CurrentPage = page;

        // Dropdown data
        ViewBag.VendorOptions = new SelectList(allVendors, "Id", "CompanyName", vendorFilter);
        ViewBag.StatusOptions = new SelectList(purchaseStatuses.Select(s => new {
          Value = s.ToString(),
          Text = s.ToString().Replace("_", " ")
        }), "Value", "Text", statusFilter);

        // Search statistics
        if (!string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(vendorFilter) ||
            !string.IsNullOrWhiteSpace(statusFilter) || startDate.HasValue || endDate.HasValue)
        {
          var totalPurchases = await _context.Purchases.CountAsync();
          ViewBag.SearchResultsCount = purchases.Count;
          ViewBag.TotalPurchasesCount = totalPurchases;
          ViewBag.IsFiltered = true;
        }
        else
        {
          ViewBag.IsFiltered = false;
        }

        return View(purchases);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Purchases Index: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        ViewBag.ErrorMessage = $"Error loading purchases: {ex.Message}";
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
      try
      {
        Console.WriteLine($"Edit POST called for Purchase ID: {id}");
        Console.WriteLine($"Received Purchase data - VendorId: {purchase.VendorId}, ItemId: {purchase.ItemId}");

        if (id != purchase.Id)
        {
          Console.WriteLine($"ID mismatch: URL ID {id} != Model ID {purchase.Id}");
          return NotFound();
        }

        // Remove validation for navigation properties that aren't bound from the form
        ModelState.Remove("Item");
        ModelState.Remove("Vendor");
        ModelState.Remove("ItemVersionReference");
        ModelState.Remove("PurchaseDocuments");

        // Remove validation for calculated properties
        ModelState.Remove("TotalCost");
        ModelState.Remove("TotalPaid");

        // Ensure required hidden fields are set
        if (purchase.RemainingQuantity <= 0)
        {
          var existingPurchase = await _context.Purchases.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
          if (existingPurchase != null)
          {
            purchase.RemainingQuantity = existingPurchase.RemainingQuantity;
            purchase.CreatedDate = existingPurchase.CreatedDate;
          }
        }

        // Log ModelState errors
        if (!ModelState.IsValid)
        {
          Console.WriteLine("ModelState is invalid:");
          foreach (var modelError in ModelState)
          {
            foreach (var error in modelError.Value.Errors)
            {
              Console.WriteLine($"Field: {modelError.Key}, Error: {error.ErrorMessage}");
            }
          }

          // Reload dropdowns and return view with validation errors
          await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
          return View(purchase);
        }

        Console.WriteLine("ModelState is valid, proceeding with update...");

        // Validate that vendor exists
        var vendor = await _vendorService.GetVendorByIdAsync(purchase.VendorId);
        if (vendor == null)
        {
          Console.WriteLine($"Vendor not found with ID: {purchase.VendorId}");
          ModelState.AddModelError("VendorId", "Selected vendor does not exist.");
          await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
          return View(purchase);
        }

        // Validate that item exists
        var item = await _inventoryService.GetItemByIdAsync(purchase.ItemId);
        if (item == null)
        {
          Console.WriteLine($"Item not found with ID: {purchase.ItemId}");
          ModelState.AddModelError("ItemId", "Selected item does not exist.");
          await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
          return View(purchase);
        }

        Console.WriteLine("Calling UpdatePurchaseAsync...");
        await _purchaseService.UpdatePurchaseAsync(purchase);

        Console.WriteLine("Purchase updated successfully");
        TempData["SuccessMessage"] = "Purchase updated successfully!";
        return RedirectToAction("Details", new { id = purchase.Id });
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error updating purchase: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");

        ModelState.AddModelError("", $"Error updating purchase: {ex.Message}");

        // Reload dropdowns and return view with error
        await ReloadDropdownsAsync(purchase.ItemId, purchase.VendorId);
        return View(purchase);
      }
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