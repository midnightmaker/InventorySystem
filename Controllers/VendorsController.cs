using InventorySystem.Models;
using InventorySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventorySystem.Controllers
{
  public class VendorsController : Controller
  {
    private readonly IVendorService _vendorService;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<VendorsController> _logger;

    public VendorsController(
      IVendorService vendorService,
      IInventoryService inventoryService,
      ILogger<VendorsController> logger)
    {
      _vendorService = vendorService;
      _inventoryService = inventoryService;
      _logger = logger;
    }

    // GET: Vendors
    public async Task<IActionResult> Index(string search, bool activeOnly = true)
    {
      try
      {
        IEnumerable<Vendor> vendors;

        if (!string.IsNullOrWhiteSpace(search))
        {
          vendors = await _vendorService.SearchVendorsAsync(search);
          if (activeOnly)
          {
            vendors = vendors.Where(v => v.IsActive);
          }
        }
        else
        {
          vendors = activeOnly
            ? await _vendorService.GetActiveVendorsAsync()
            : await _vendorService.GetAllVendorsAsync();
        }

        ViewBag.SearchTerm = search;
        ViewBag.ActiveOnly = activeOnly;
        return View(vendors);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading vendors index");
        TempData["ErrorMessage"] = "Error loading vendors: " + ex.Message;
        return View(new List<Vendor>());
      }
    }

    [HttpGet]
    public async Task<IActionResult> GetVendorItemInfo(int vendorId, int itemId)
    {
      try
      {
        var vendorItem = await _vendorService.GetVendorItemAsync(vendorId, itemId);

        if (vendorItem != null)
        {
          return Json(new
          {
            success = true,
            vendorItem = new
            {
              unitCost = vendorItem.UnitCost.ToString("F2"),
              leadTimeDays = vendorItem.LeadTimeDays,
              minimumOrderQuantity = vendorItem.MinimumOrderQuantity,
              isPrimary = vendorItem.IsPrimary,
              vendorPartNumber = vendorItem.VendorPartNumber,
              lastPurchaseDate = vendorItem.LastPurchaseDate?.ToString("MM/dd/yyyy"),
              lastPurchaseCost = vendorItem.LastPurchaseCost?.ToString("F2"),
              notes = vendorItem.Notes
            }
          });
        }
        else
        {
          return Json(new { success = false, message = "No vendor-item relationship found" });
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting vendor item info for VendorId: {VendorId}, ItemId: {ItemId}", vendorId, itemId);
        return Json(new { success = false, error = ex.Message });
      }
    }

    // GET: Vendors/Details/5
    public async Task<IActionResult> Details(int id)
    {
      try
      {
        var vendor = await _vendorService.GetVendorByIdAsync(id);
        if (vendor == null)
        {
          TempData["ErrorMessage"] = "Vendor not found.";
          return RedirectToAction("Index");
        }

        // Get vendor items and purchase history
        var vendorItems = await _vendorService.GetVendorItemsAsync(id);
        var purchaseHistory = await _vendorService.GetVendorPurchaseHistoryAsync(id);

        ViewBag.VendorItems = vendorItems;
        ViewBag.PurchaseHistory = purchaseHistory.Take(10); // Last 10 purchases
        ViewBag.TotalPurchases = await _vendorService.GetVendorTotalPurchasesAsync(id);

        return View(vendor);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading vendor details for ID: {VendorId}", id);
        TempData["ErrorMessage"] = "Error loading vendor details: " + ex.Message;
        return RedirectToAction("Index");
      }
    }

    // GET: Vendors/Create
    public IActionResult Create()
    {
      var vendor = new Vendor
      {
        IsActive = true,
        PaymentTerms = "Net 30",
        Country = "United States",
        QualityRating = 3,
        DeliveryRating = 3,
        ServiceRating = 3
      };

      return View(vendor);
    }

    // POST: Vendors/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vendor vendor)
    {
      if (ModelState.IsValid)
      {
        try
        {
          // Check for duplicate vendor name
          var existingVendor = await _vendorService.GetVendorByNameAsync(vendor.CompanyName);
          if (existingVendor != null)
          {
            ModelState.AddModelError("CompanyName", "A vendor with this company name already exists.");
            return View(vendor);
          }

          var createdVendor = await _vendorService.CreateVendorAsync(vendor);
          TempData["SuccessMessage"] = $"Vendor '{createdVendor.CompanyName}' created successfully!";
          return RedirectToAction("Details", new { id = createdVendor.Id });
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error creating vendor: {VendorName}", vendor.CompanyName);
          ModelState.AddModelError("", "Error creating vendor: " + ex.Message);
        }
      }

      return View(vendor);
    }

    // GET: Vendors/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
      try
      {
        var vendor = await _vendorService.GetVendorByIdAsync(id);
        if (vendor == null)
        {
          TempData["ErrorMessage"] = "Vendor not found.";
          return RedirectToAction("Index");
        }

        return View(vendor);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading vendor for edit: {VendorId}", id);
        TempData["ErrorMessage"] = "Error loading vendor: " + ex.Message;
        return RedirectToAction("Index");
      }
    }

    // POST: Vendors/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Vendor vendor)
    {
      if (id != vendor.Id)
      {
        return NotFound();
      }

      if (ModelState.IsValid)
      {
        try
        {
          // Check for duplicate vendor name (excluding current vendor)
          var existingVendor = await _vendorService.GetVendorByNameAsync(vendor.CompanyName);
          if (existingVendor != null && existingVendor.Id != vendor.Id)
          {
            ModelState.AddModelError("CompanyName", "A vendor with this company name already exists.");
            return View(vendor);
          }

          await _vendorService.UpdateVendorAsync(vendor);
          TempData["SuccessMessage"] = $"Vendor '{vendor.CompanyName}' updated successfully!";
          return RedirectToAction("Details", new { id = vendor.Id });
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error updating vendor: {VendorId}", id);
          ModelState.AddModelError("", "Error updating vendor: " + ex.Message);
        }
      }

      return View(vendor);
    }

    // POST: Vendors/Deactivate/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
      try
      {
        var success = await _vendorService.DeactivateVendorAsync(id);
        if (success)
        {
          TempData["SuccessMessage"] = "Vendor deactivated successfully.";
        }
        else
        {
          TempData["ErrorMessage"] = "Vendor not found.";
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deactivating vendor: {VendorId}", id);
        TempData["ErrorMessage"] = "Error deactivating vendor: " + ex.Message;
      }

      return RedirectToAction("Index");
    }

    // POST: Vendors/Activate/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
      try
      {
        var success = await _vendorService.ActivateVendorAsync(id);
        if (success)
        {
          TempData["SuccessMessage"] = "Vendor activated successfully.";
        }
        else
        {
          TempData["ErrorMessage"] = "Vendor not found.";
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error activating vendor: {VendorId}", id);
        TempData["ErrorMessage"] = "Error activating vendor: " + ex.Message;
      }

      return RedirectToAction("Index");
    }

    // GET: Vendors/ManageItems/5
    public async Task<IActionResult> ManageItems(int id)
    {
      try
      {
        var vendor = await _vendorService.GetVendorByIdAsync(id);
        if (vendor == null)
        {
          TempData["ErrorMessage"] = "Vendor not found.";
          return RedirectToAction("Index");
        }

        var vendorItems = await _vendorService.GetVendorItemsAsync(id);
        var allItems = await _inventoryService.GetAllItemsAsync();

        ViewBag.Vendor = vendor;
        ViewBag.AllItems = allItems;
        return View(vendorItems);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading vendor items management: {VendorId}", id);
        TempData["ErrorMessage"] = "Error loading vendor items: " + ex.Message;
        return RedirectToAction("Details", new { id });
      }
    }

    // POST: Vendors/AddItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(VendorItem vendorItem)
    {
      // Remove validation for navigation properties that aren't populated by form binding
      ModelState.Remove("Vendor");
      ModelState.Remove("Item");

      if (ModelState.IsValid)
      {
        try
        {
          // Check if vendor-item relationship already exists
          var existingVendorItem = await _vendorService.GetVendorItemAsync(vendorItem.VendorId, vendorItem.ItemId);
          if (existingVendorItem != null)
          {
            TempData["ErrorMessage"] = "This item is already associated with this vendor.";
            return RedirectToAction("ManageItems", new { id = vendorItem.VendorId });
          }

          await _vendorService.CreateVendorItemAsync(vendorItem);
          TempData["SuccessMessage"] = "Item added to vendor successfully!";
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error adding item to vendor: {VendorId}, {ItemId}", vendorItem.VendorId, vendorItem.ItemId);
          TempData["ErrorMessage"] = "Error adding item to vendor: " + ex.Message;
        }
      }
      else
      {
        TempData["ErrorMessage"] = "Invalid data provided.";
      }

      return RedirectToAction("ManageItems", new { id = vendorItem.VendorId });
    }

    // POST: Vendors/UpdateItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(VendorItem vendorItem)
    {
      // Remove validation for navigation properties that aren't populated by form binding
      ModelState.Remove("Vendor");
      ModelState.Remove("Item");

      if (ModelState.IsValid)
      {
        try
        {
          await _vendorService.UpdateVendorItemAsync(vendorItem);
          TempData["SuccessMessage"] = "Vendor item updated successfully!";
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error updating vendor item: {VendorId}, {ItemId}", vendorItem.VendorId, vendorItem.ItemId);
          TempData["ErrorMessage"] = "Error updating vendor item: " + ex.Message;
        }
      }
      else
      {
        TempData["ErrorMessage"] = "Invalid data provided.";
      }

      return RedirectToAction("ManageItems", new { id = vendorItem.VendorId });
    }

    // POST: Vendors/RemoveItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(int vendorId, int itemId)
    {
      try
      {
        var success = await _vendorService.DeleteVendorItemAsync(vendorId, itemId);
        if (success)
        {
          TempData["SuccessMessage"] = "Item removed from vendor successfully.";
        }
        else
        {
          TempData["ErrorMessage"] = "Vendor item relationship not found.";
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error removing item from vendor: {VendorId}, {ItemId}", vendorId, itemId);
        TempData["ErrorMessage"] = "Error removing item from vendor: " + ex.Message;
      }

      return RedirectToAction("ManageItems", new { id = vendorId });
    }

    // GET: API endpoint for vendor search (for autocomplete)
    [HttpGet]
    public async Task<IActionResult> SearchApi(string term)
    {
      try
      {
        var vendors = await _vendorService.SearchVendorsAsync(term ?? "");
        var result = vendors.Where(v => v.IsActive).Select(v => new
        {
          id = v.Id,
          label = v.CompanyName,
          value = v.CompanyName,
          vendorCode = v.VendorCode
        }).Take(10);

        return Json(result);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in vendor search API");
        return Json(new List<object>());
      }
    }

    // GET: Vendors/ItemVendors/5 (for item details page)
    public async Task<IActionResult> ItemVendors(int itemId)
    {
      try
      {
        var itemVendors = await _vendorService.GetItemVendorsAsync(itemId);
        var item = await _inventoryService.GetItemByIdAsync(itemId);

        if (item == null)
        {
          TempData["ErrorMessage"] = "Item not found.";
          return RedirectToAction("Index", "Items");
        }

        ViewBag.Item = item;
        return View(itemVendors);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading item vendors: {ItemId}", itemId);
        TempData["ErrorMessage"] = "Error loading item vendors: " + ex.Message;
        return RedirectToAction("Index", "Items");
      }
    }

    // GET: Vendors/CheapestVendors/5 (API endpoint)
    [HttpGet]
    public async Task<IActionResult> CheapestVendorsApi(int itemId)
    {
      try
      {
        var vendors = await _vendorService.GetCheapestVendorsForItemAsync(itemId);
        var result = vendors.Select(vi => new
        {
          vendorId = vi.VendorId,
          vendorName = vi.Vendor.CompanyName,
          unitCost = vi.UnitCost,
          leadTimeDays = vi.LeadTimeDays,
          minimumOrderQty = vi.MinimumOrderQuantity,
          isPrimary = vi.IsPrimary
        });

        return Json(result);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting cheapest vendors for item: {ItemId}", itemId);
        return Json(new List<object>());
      }
    }

    // GET: Vendors/FastestVendors/5 (API endpoint)
    [HttpGet]
    public async Task<IActionResult> FastestVendorsApi(int itemId)
    {
      try
      {
        var vendors = await _vendorService.GetFastestVendorsForItemAsync(itemId);
        var result = vendors.Select(vi => new
        {
          vendorId = vi.VendorId,
          vendorName = vi.Vendor.CompanyName,
          unitCost = vi.UnitCost,
          leadTimeDays = vi.LeadTimeDays,
          leadTimeDescription = vi.LeadTimeDescription,
          minimumOrderQty = vi.MinimumOrderQuantity,
          isPrimary = vi.IsPrimary
        });

        return Json(result);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting fastest vendors for item: {ItemId}", itemId);
        return Json(new List<object>());
      }
    }

    // GET: Vendors/Reports
    public async Task<IActionResult> Reports()
    {
      try
      {
        var allVendors = await _vendorService.GetAllVendorsAsync();
        var activeVendors = allVendors.Where(v => v.IsActive);
        var preferredVendors = allVendors.Where(v => v.IsPreferred);

        var reportData = new
        {
          TotalVendors = allVendors.Count(),
          ActiveVendors = activeVendors.Count(),
          PreferredVendors = preferredVendors.Count(),
          InactiveVendors = allVendors.Count(v => !v.IsActive),
          TopVendorsByPurchases = allVendors.OrderByDescending(v => v.TotalPurchases).Take(10),
          TopVendorsByItemCount = allVendors.OrderByDescending(v => v.ItemsSuppliedCount).Take(10),
          HighestRatedVendors = activeVendors.OrderByDescending(v => v.OverallRating).Take(10),
          RecentlyCreatedVendors = allVendors.OrderByDescending(v => v.CreatedDate).Take(5)
        };

        return View(reportData);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading vendor reports");
        TempData["ErrorMessage"] = "Error loading vendor reports: " + ex.Message;
        return View();
      }
    }
  }
}