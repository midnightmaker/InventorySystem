// Controllers/PurchasesController.cs - Enhanced with pagination and performance optimizations
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

    // Pagination constants
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };

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

    public async Task<IActionResult> Index(
        string search,
        string vendorFilter,
        string statusFilter,
        DateTime? startDate,
        DateTime? endDate,
        string sortOrder = "date_desc",
        int page = 1,
        int pageSize = DefaultPageSize)
    {
      try
      {
        // Validate and constrain pagination parameters
        page = Math.Max(1, page);
        pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

        Console.WriteLine($"=== PURCHASES INDEX DEBUG ===");
        Console.WriteLine($"Search: {search}");
        Console.WriteLine($"Vendor Filter: {vendorFilter}");
        Console.WriteLine($"Status Filter: {statusFilter}");
        Console.WriteLine($"Date Range: {startDate} to {endDate}");
        Console.WriteLine($"Sort Order: {sortOrder}");
        Console.WriteLine($"Page: {page}, PageSize: {pageSize}");

        // Start with base query - only select necessary fields for listing
        var query = _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.Vendor)
            .Select(p => new
            {
              p.Id,
              p.PurchaseDate,
              p.QuantityPurchased,
              p.CostPerUnit,
              p.ShippingCost,
              p.TaxAmount,
              p.PurchaseOrderNumber,
              p.Status,
              p.RemainingQuantity,
              p.CreatedDate,
              ItemPartNumber = p.Item.PartNumber,
              ItemDescription = p.Item.Description,
              VendorCompanyName = p.Vendor.CompanyName,
              p.Notes
            })
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
          var searchTerm = search.Trim();
          Console.WriteLine($"Applying search filter: {searchTerm}");

          if (searchTerm.Contains('*') || searchTerm.Contains('?'))
          {
            var likePattern = ConvertWildcardToLike(searchTerm);
            Console.WriteLine($"Using LIKE pattern: {likePattern}");

            query = query.Where(p =>
              EF.Functions.Like(p.ItemPartNumber, likePattern) ||
              EF.Functions.Like(p.ItemDescription, likePattern) ||
              EF.Functions.Like(p.VendorCompanyName, likePattern) ||
              (p.PurchaseOrderNumber != null && EF.Functions.Like(p.PurchaseOrderNumber, likePattern)) ||
              (p.Notes != null && EF.Functions.Like(p.Notes, likePattern)) ||
              EF.Functions.Like(p.Id.ToString(), likePattern)
            );
          }
          else
          {
            query = query.Where(p =>
              p.ItemPartNumber.Contains(searchTerm) ||
              p.ItemDescription.Contains(searchTerm) ||
              p.VendorCompanyName.Contains(searchTerm) ||
              (p.PurchaseOrderNumber != null && p.PurchaseOrderNumber.Contains(searchTerm)) ||
              (p.Notes != null && p.Notes.Contains(searchTerm)) ||
              p.Id.ToString().Contains(searchTerm)
            );
          }
        }

        // Apply vendor filter
        if (!string.IsNullOrWhiteSpace(vendorFilter) && int.TryParse(vendorFilter, out int vendorId))
        {
          Console.WriteLine($"Applying vendor filter: {vendorId}");
          // Need to go back to original query for this filter since we're using projection
          var baseQuery = _context.Purchases
              .Include(p => p.Item)
              .Include(p => p.Vendor)
              .Where(p => p.VendorId == vendorId);

          // Reapply search if it exists
          if (!string.IsNullOrWhiteSpace(search))
          {
            var searchTerm = search.Trim();
            if (searchTerm.Contains('*') || searchTerm.Contains('?'))
            {
              var likePattern = ConvertWildcardToLike(searchTerm);
              baseQuery = baseQuery.Where(p =>
                EF.Functions.Like(p.Item.PartNumber, likePattern) ||
                EF.Functions.Like(p.Item.Description, likePattern) ||
                EF.Functions.Like(p.Vendor.CompanyName, likePattern) ||
                (p.PurchaseOrderNumber != null && EF.Functions.Like(p.PurchaseOrderNumber, likePattern)) ||
                (p.Notes != null && EF.Functions.Like(p.Notes, likePattern)) ||
                EF.Functions.Like(p.Id.ToString(), likePattern)
              );
            }
            else
            {
              baseQuery = baseQuery.Where(p =>
                p.Item.PartNumber.Contains(searchTerm) ||
                p.Item.Description.Contains(searchTerm) ||
                p.Vendor.CompanyName.Contains(searchTerm) ||
                (p.PurchaseOrderNumber != null && p.PurchaseOrderNumber.Contains(searchTerm)) ||
                (p.Notes != null && p.Notes.Contains(searchTerm)) ||
                p.Id.ToString().Contains(searchTerm)
              );
            }
          }

          query = baseQuery.Select(p => new
          {
            p.Id,
            p.PurchaseDate,
            p.QuantityPurchased,
            p.CostPerUnit,
            p.ShippingCost,
            p.TaxAmount,
            p.PurchaseOrderNumber,
            p.Status,
            p.RemainingQuantity,
            p.CreatedDate,
            ItemPartNumber = p.Item.PartNumber,
            ItemDescription = p.Item.Description,
            VendorCompanyName = p.Vendor.CompanyName,
            p.Notes
          });
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
          "vendor_asc" => query.OrderBy(p => p.VendorCompanyName),
          "vendor_desc" => query.OrderByDescending(p => p.VendorCompanyName),
          "item_asc" => query.OrderBy(p => p.ItemPartNumber),
          "item_desc" => query.OrderByDescending(p => p.ItemPartNumber),
          "amount_asc" => query.OrderBy(p => p.QuantityPurchased * p.CostPerUnit),
          "amount_desc" => query.OrderByDescending(p => p.QuantityPurchased * p.CostPerUnit),
          "status_asc" => query.OrderBy(p => p.Status),
          "status_desc" => query.OrderByDescending(p => p.Status),
          _ => query.OrderByDescending(p => p.PurchaseDate)
        };

        // Get total count for pagination (before Skip/Take)
        var totalCount = await query.CountAsync();
        Console.WriteLine($"Total filtered records: {totalCount}");

        // Calculate pagination values
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var skip = (page - 1) * pageSize;

        // Get paginated results
        var paginatedResults = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        Console.WriteLine($"Retrieved {paginatedResults.Count} purchases for page {page}");

        // Convert to Purchase objects for the view
        var purchases = paginatedResults.Select(p => new Purchase
        {
          Id = p.Id,
          PurchaseDate = p.PurchaseDate,
          QuantityPurchased = p.QuantityPurchased,
          CostPerUnit = p.CostPerUnit,
          ShippingCost = p.ShippingCost,
          TaxAmount = p.TaxAmount,
          PurchaseOrderNumber = p.PurchaseOrderNumber,
          Status = p.Status,
          RemainingQuantity = p.RemainingQuantity,
          CreatedDate = p.CreatedDate,
          Notes = p.Notes,
          Item = new Item
          {
            PartNumber = p.ItemPartNumber,
            Description = p.ItemDescription
          },
          Vendor = new Vendor
          {
            CompanyName = p.VendorCompanyName
          }
        }).ToList();

        // Get filter options for dropdowns (cached or optimized)
        var allVendors = await _vendorService.GetActiveVendorsAsync();
        var purchaseStatuses = Enum.GetValues<PurchaseStatus>().ToList();

        // Prepare ViewBag data
        ViewBag.SearchTerm = search;
        ViewBag.VendorFilter = vendorFilter;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
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
        ViewBag.VendorOptions = new SelectList(allVendors, "Id", "CompanyName", vendorFilter);
        ViewBag.StatusOptions = new SelectList(purchaseStatuses.Select(s => new {
          Value = s.ToString(),
          Text = s.ToString().Replace("_", " ")
        }), "Value", "Text", statusFilter);

        // Search statistics
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
                           !string.IsNullOrWhiteSpace(vendorFilter) ||
                           !string.IsNullOrWhiteSpace(statusFilter) ||
                           startDate.HasValue ||
                           endDate.HasValue;

        if (ViewBag.IsFiltered)
        {
          var totalPurchases = await _context.Purchases.CountAsync();
          ViewBag.SearchResultsCount = totalCount;
          ViewBag.TotalPurchasesCount = totalPurchases;
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
            .Include(p => p.PurchaseDocuments) // This was missing!
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
    // Add this action method to PurchasesController
    public async Task<IActionResult> Vendors()
    {
      try
      {
        var vendors = await _vendorService.GetActiveVendorsAsync();

        // You might want to include purchase-related information for each vendor
        var vendorsWithPurchaseInfo = new List<dynamic>();

        foreach (var vendor in vendors)
        {
          var purchaseHistory = await _vendorService.GetVendorPurchaseHistoryAsync(vendor.Id);
          var totalPurchases = await _vendorService.GetVendorTotalPurchasesAsync(vendor.Id);

          vendorsWithPurchaseInfo.Add(new
          {
            Vendor = vendor,
            PurchaseCount = purchaseHistory.Count(),
            TotalPurchaseValue = totalPurchases,
            LastPurchaseDate = purchaseHistory.FirstOrDefault()?.PurchaseDate
          });
        }

        return View(vendorsWithPurchaseInfo);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error loading vendors: {ex.Message}");
        TempData["ErrorMessage"] = $"Error loading vendors: {ex.Message}";
        return View(new List<object>());
      }
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

    // AJAX endpoint to generate purchase order number
    [HttpGet]
    public async Task<IActionResult> GeneratePurchaseOrderNumber()
    {
        try
        {
            var purchaseOrderNumber = await _purchaseService.GeneratePurchaseOrderNumberAsync();
            return Json(new { success = true, purchaseOrderNumber = purchaseOrderNumber });
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

    /// <summary>
    /// Converts wildcard patterns (* and ?) to regex patterns
    /// * matches any sequence of characters
    /// ? matches any single character
    /// </summary>
    /// <param name="wildcardPattern">The wildcard pattern to convert</param>
    /// <returns>A regex pattern string</returns>
    private string ConvertWildcardToRegex(string wildcardPattern)
    {
      // Escape special regex characters except * and ?
      var escaped = System.Text.RegularExpressions.Regex.Escape(wildcardPattern);

      // Replace escaped wildcards with regex equivalents
      escaped = escaped.Replace(@"\*", ".*");  // * becomes .*
      escaped = escaped.Replace(@"\?", ".");   // ? becomes .

      // Anchor the pattern to match the entire string
      return $"^{escaped}$";
    }
  }
}