// Controllers/PurchasesController.cs - Enhanced with pagination, performance optimizations, multi-line purchase support, and PO report features
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
        ViewBag.StatusOptions = new SelectList(purchaseStatuses.Select(s => new
        {
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

        // Set essential ViewBag properties that the view expects
        ViewBag.ErrorMessage = $"Error loading purchases: {ex.Message}";
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
        ViewBag.VendorFilter = vendorFilter;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
        ViewBag.SortOrder = sortOrder;
        ViewBag.IsFiltered = false;

        // Set empty dropdown options
        ViewBag.VendorOptions = new SelectList(new List<object>(), "Id", "CompanyName");
        ViewBag.StatusOptions = new SelectList(new List<object>(), "Value", "Text");

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

        var viewModel = new CreatePurchaseViewModel
        {
          PurchaseDate = DateTime.Today,
          QuantityPurchased = 1,
          Status = PurchaseStatus.Pending
        };

        // If itemId is provided, set it and get the recommended vendor using priority logic
        if (itemId.HasValue)
        {
          viewModel.ItemId = itemId.Value;

          // NEW: Use the comprehensive vendor selection logic instead of just last vendor
          var recommendedVendor = await _vendorService.GetPreferredVendorForItemAsync(itemId.Value);
          if (recommendedVendor != null)
          {
            viewModel.VendorId = recommendedVendor.Id;

            // Get the vendor item relationship to get the last known cost
            var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(itemId.Value);
            if (vendorInfo.RecommendedCost.HasValue && vendorInfo.RecommendedCost.Value > 0)
            {
              viewModel.CostPerUnit = vendorInfo.RecommendedCost.Value;
            }
            else
            {
              // Fallback to average cost if no vendor cost is available
              var averageCost = await _inventoryService.GetAverageCostAsync(itemId.Value);
              if (averageCost > 0)
              {
                viewModel.CostPerUnit = averageCost;
              }
            }

            // Store selection reason for user feedback (optional)
            ViewBag.VendorSelectionReason = vendorInfo.SelectionReason;
          }
          else
          {
            // No vendor found, but still set a default cost
            var averageCost = await _inventoryService.GetAverageCostAsync(itemId.Value);
            if (averageCost > 0)
            {
              viewModel.CostPerUnit = averageCost;
            }
            ViewBag.VendorSelectionReason = "No preferred vendor found";
          }

          // Get the item details for display
          var item = await _inventoryService.GetItemByIdAsync(itemId.Value);
          if (item != null)
          {
            ViewBag.ItemDetails = new
            {
              PartNumber = item.PartNumber,
              Description = item.Description,
              CurrentStock = item.CurrentStock,
              MinimumStock = item.MinimumStock
            };
          }
        }

        // Format items dropdown with part number and description
        var formattedItems = items.Select(item => new
        {
          Value = item.Id,
          Text = $"{item.PartNumber} - {item.Description}"
        }).ToList();

        // Enhanced vendor dropdown with priority indicators
        var formattedVendors = vendors.Select(vendor => new
        {
          Value = vendor.Id,
          Text = vendor.CompanyName,
          Selected = vendor.Id == viewModel.VendorId
        }).ToList();

        ViewBag.ItemId = new SelectList(formattedItems, "Value", "Text", viewModel.ItemId);
        ViewBag.VendorId = new SelectList(formattedVendors, "Value", "Text", viewModel.VendorId);

        return View(viewModel);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Create GET: {ex.Message}");
        ViewBag.ErrorMessage = ex.Message;

        // Ensure dropdowns are available even on error
        ViewBag.ItemId = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.VendorId = new SelectList(new List<object>(), "Value", "Text");

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
    // AJAX endpoint to get recommended vendor for an item with comprehensive logic
    [HttpGet]
    public async Task<IActionResult> GetRecommendedVendorForItem(int itemId)
    {
      try
      {
        var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(itemId);
        var averageCost = await _inventoryService.GetAverageCostAsync(itemId);

        return Json(new
        {
          success = true,
          vendorId = vendorInfo.RecommendedVendor?.Id,
          vendorName = vendorInfo.RecommendedVendor?.CompanyName,
          recommendedCost = vendorInfo.RecommendedCost ?? averageCost,
          selectionReason = vendorInfo.SelectionReason,
          hasPrimaryVendor = vendorInfo.PrimaryVendor != null,
          hasItemPreferredVendor = !string.IsNullOrEmpty(vendorInfo.ItemPreferredVendorName),
          hasLastPurchaseVendor = vendorInfo.LastPurchaseVendor != null,
          lastPurchaseDate = vendorInfo.LastPurchaseDate?.ToString("yyyy-MM-dd"),
          primaryVendorName = vendorInfo.PrimaryVendor?.CompanyName,
          itemPreferredVendorName = vendorInfo.ItemPreferredVendorName,
          lastPurchaseVendorName = vendorInfo.LastPurchaseVendor?.CompanyName
        });
      }
      catch (Exception ex)
      {
        return Json(new { success = false, error = ex.Message });
      }
    }

    // Keep the existing method for backward compatibility, but mark it as obsolete
    [Obsolete("Use GetRecommendedVendorForItem instead")]
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

    // Action methods for multi-line purchase creation

    [HttpGet]
    public async Task<IActionResult> CreateMultiLine()
    {
      try
      {
        var vendors = await _vendorService.GetActiveVendorsAsync();
        var items = await _inventoryService.GetAllItemsAsync();

        var viewModel = new MultiLinePurchaseViewModel
        {
          PurchaseDate = DateTime.Today,
          ExpectedDeliveryDate = DateTime.Today.AddDays(7),
          Status = PurchaseStatus.Pending,
          LineItems = new List<PurchaseLineItemViewModel>()
        };

        ViewBag.AllVendors = new SelectList(vendors, "Id", "CompanyName");
        ViewBag.AllItems = items.Select(i => new
        {
          Value = i.Id,
          Text = $"{i.PartNumber} - {i.Description}",
          CurrentStock = i.CurrentStock,
          MinStock = i.MinimumStock
        }).ToList();

        return View(viewModel);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading multi-line purchase form: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMultiLine(MultiLinePurchaseViewModel viewModel)
    {
      try
      {
        if (!ModelState.IsValid)
        {
          await ReloadMultiLineViewData(viewModel);
          return View(viewModel);
        }

        var selectedItems = viewModel.LineItems.Where(l => l.Selected && l.Quantity > 0).ToList();

        if (!selectedItems.Any())
        {
          TempData["ErrorMessage"] = "Please add at least one line item.";
          await ReloadMultiLineViewData(viewModel);
          return View(viewModel);
        }

        // Group by vendor for consolidated purchase orders
        var vendorGroups = selectedItems.GroupBy(l => l.VendorId).ToList();
        var createdPurchases = new List<string>();

        foreach (var vendorGroup in vendorGroups)
        {
          var vendorId = vendorGroup.Key;
          var vendor = await _vendorService.GetVendorByIdAsync(vendorId);

          if (vendor == null) continue;

          // Generate PO number for this vendor group
          var poNumber = !string.IsNullOrEmpty(viewModel.PurchaseOrderNumber)
              ? $"{viewModel.PurchaseOrderNumber}-{vendor.CompanyName.Substring(0, Math.Min(3, vendor.CompanyName.Length)).ToUpper()}"
              : await _purchaseService.GeneratePurchaseOrderNumberAsync();

          var vendorItems = vendorGroup.ToList();

          // Create individual purchases for each line item
          foreach (var lineItem in vendorItems)
          {
            var purchase = new Purchase
            {
              ItemId = lineItem.ItemId,
              VendorId = vendorId,
              PurchaseDate = viewModel.PurchaseDate,
              QuantityPurchased = lineItem.Quantity,
              CostPerUnit = lineItem.UnitCost,
              PurchaseOrderNumber = poNumber,
              Notes = $"Multi-line Purchase | {viewModel.Notes} | {lineItem.Notes}".Trim(' ', '|'),
              Status = viewModel.Status,
              ExpectedDeliveryDate = viewModel.ExpectedDeliveryDate,
              RemainingQuantity = lineItem.Quantity,
              CreatedDate = DateTime.Now
            };

            await _purchaseService.CreatePurchaseAsync(purchase);
          }

          createdPurchases.Add($"{vendor.CompanyName}: {poNumber} ({vendorItems.Count} items)");
        }

        TempData["SuccessMessage"] = $"Successfully created multi-line purchase orders: {string.Join(", ", createdPurchases)}";
        return RedirectToAction("Index");
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error creating multi-line purchase: {ex.Message}";
        await ReloadMultiLineViewData(viewModel);
        return View(viewModel);
      }
    }

    // AJAX endpoint to add item to multi-line purchase
    [HttpGet]
    public async Task<IActionResult> GetItemForMultiLine(int itemId)
    {
      try
      {
        var item = await _inventoryService.GetItemByIdAsync(itemId);
        if (item == null)
          return Json(new { success = false, error = "Item not found" });

        var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(itemId);
        var averageCost = await _inventoryService.GetAverageCostAsync(itemId);

        return Json(new
        {
          success = true,
          itemId = item.Id,
          partNumber = item.PartNumber,
          description = item.Description,
          currentStock = item.CurrentStock,
          minimumStock = item.MinimumStock,
          recommendedVendorId = vendorInfo.RecommendedVendor?.Id,
          recommendedVendorName = vendorInfo.RecommendedVendor?.CompanyName,
          recommendedCost = vendorInfo.RecommendedCost ?? averageCost,
          selectionReason = vendorInfo.SelectionReason,
          isLowStock = item.CurrentStock <= item.MinimumStock
        });
      }
      catch (Exception ex)
      {
        return Json(new { success = false, error = ex.Message });
      }
    }

    private async Task ReloadMultiLineViewData(MultiLinePurchaseViewModel viewModel)
    {
      try
      {
        var vendors = await _vendorService.GetActiveVendorsAsync();
        var items = await _inventoryService.GetAllItemsAsync();

        ViewBag.AllVendors = new SelectList(vendors, "Id", "CompanyName");
        ViewBag.AllItems = items.Select(i => new
        {
          Value = i.Id,
          Text = $"{i.PartNumber} - {i.Description}",
          CurrentStock = i.CurrentStock,
          MinStock = i.MinimumStock
        }).ToList();
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error reloading multi-line view data: {ex.Message}");
        ViewBag.AllVendors = new SelectList(new List<object>(), "Id", "CompanyName");
        ViewBag.AllItems = new List<object>();
      }
    }

    // Purchase Order Report - View all line items for a PO
    [HttpGet]
    public async Task<IActionResult> PurchaseOrderReport(string poNumber)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
          TempData["ErrorMessage"] = "Purchase Order Number is required.";
          return RedirectToAction("Index");
        }

        // Get all purchases for this PO number
        var purchases = await _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.Vendor)
            .Where(p => p.PurchaseOrderNumber == poNumber)
            .OrderBy(p => p.Item.PartNumber)
            .ToListAsync();

        if (!purchases.Any())
        {
          TempData["ErrorMessage"] = $"No purchases found for PO Number: {poNumber}";
          return RedirectToAction("Index");
        }

        // Group by vendor (in case PO has multiple vendors - shouldn't happen but just in case)
        var primaryVendor = purchases.First().Vendor;

        var viewModel = new PurchaseOrderReportViewModel
        {
          PurchaseOrderNumber = poNumber,
          PurchaseDate = purchases.Min(p => p.PurchaseDate),
          ExpectedDeliveryDate = purchases.FirstOrDefault()?.ExpectedDeliveryDate,
          Status = purchases.First().Status,
          Notes = string.Join("; ", purchases.Where(p => !string.IsNullOrEmpty(p.Notes)).Select(p => p.Notes).Distinct()),
          Vendor = primaryVendor,
          LineItems = purchases.Select(p => new PurchaseOrderLineItem
          {
            ItemId = p.ItemId,
            PartNumber = p.Item.PartNumber,
            Description = p.Item.Description,
            Quantity = p.QuantityPurchased,
            UnitCost = p.CostPerUnit,
            ShippingCost = p.ShippingCost,
            TaxAmount = p.TaxAmount,
            Notes = p.Notes ?? string.Empty,
            PurchaseDate = p.PurchaseDate,
            ExpectedDeliveryDate = p.ExpectedDeliveryDate,
            Status = p.Status
          }).ToList(),
          CompanyInfo = await GetCompanyInfo(), // Helper method to get your company info
          VendorEmail = primaryVendor.ContactEmail ?? string.Empty,
          EmailSubject = $"Purchase Order {poNumber}",
          EmailMessage = $"Please find attached Purchase Order {poNumber} for your review and processing."
        };

        return View(viewModel);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error generating PO report: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // Print-friendly version of the PO report
    [HttpGet]
    public async Task<IActionResult> PurchaseOrderReportPrint(string poNumber)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
          return BadRequest("Purchase Order Number is required.");
        }

        // Get all purchases for this PO number
        var purchases = await _context.Purchases
            .Include(p => p.Item)
            .Include(p => p.Vendor)
            .Where(p => p.PurchaseOrderNumber == poNumber)
            .OrderBy(p => p.Item.PartNumber)
            .ToListAsync();

        if (!purchases.Any())
        {
          return NotFound($"No purchases found for PO Number: {poNumber}");
        }

        var primaryVendor = purchases.First().Vendor;

        var viewModel = new PurchaseOrderReportViewModel
        {
          PurchaseOrderNumber = poNumber,
          PurchaseDate = purchases.Min(p => p.PurchaseDate),
          ExpectedDeliveryDate = purchases.FirstOrDefault()?.ExpectedDeliveryDate,
          Status = purchases.First().Status,
          Notes = string.Join("; ", purchases.Where(p => !string.IsNullOrEmpty(p.Notes)).Select(p => p.Notes).Distinct()),
          Vendor = primaryVendor,
          LineItems = purchases.Select(p => new PurchaseOrderLineItem
          {
            ItemId = p.ItemId,
            PartNumber = p.Item.PartNumber,
            Description = p.Item.Description,
            Quantity = p.QuantityPurchased,
            UnitCost = p.CostPerUnit,
            ShippingCost = p.ShippingCost,
            TaxAmount = p.TaxAmount,
            Notes = p.Notes ?? string.Empty,
            PurchaseDate = p.PurchaseDate,
            ExpectedDeliveryDate = p.ExpectedDeliveryDate,
            Status = p.Status
          }).ToList(),
          CompanyInfo = await GetCompanyInfo()
        };

        return View("PurchaseOrderReportPrint", viewModel);
      }
      catch (Exception ex)
      {
        return BadRequest($"Error generating PO report: {ex.Message}");
      }
    }

    // Email PO Report to Vendor
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailPurchaseOrderReport(PurchaseOrderReportViewModel model)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(model.VendorEmail))
        {
          TempData["ErrorMessage"] = "Vendor email address is required.";
          return RedirectToAction("PurchaseOrderReport", new { poNumber = model.PurchaseOrderNumber });
        }

        // Generate the HTML report
        var reportHtml = await RenderViewToStringAsync("PurchaseOrderReportEmail", model);

        // Send email (you'll need to implement IEmailService)
        //var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
        //var emailSuccess = await emailService.SendEmailAsync(
        //    model.VendorEmail,
        //    model.EmailSubject,
        //    reportHtml,
        //    isHtml: true
        //);

        //if (emailSuccess)
        //{
        //  TempData["SuccessMessage"] = $"Purchase Order {model.PurchaseOrderNumber} emailed successfully to {model.VendorEmail}";
        //}
        //else
        //{
        //  TempData["ErrorMessage"] = "Failed to send email. Please try again or contact the vendor directly.";
        //}

        return RedirectToAction("PurchaseOrderReport", new { poNumber = model.PurchaseOrderNumber });
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error sending email: {ex.Message}";
        return RedirectToAction("PurchaseOrderReport", new { poNumber = model.PurchaseOrderNumber });
      }
    }

    // Helper method to get company information
    private async Task<Models.CompanyInfo> GetCompanyInfo()
    {
      try
      {
        // Try to get from the database first
        var companyInfoService = HttpContext.RequestServices.GetRequiredService<ICompanyInfoService>();
        var dbCompanyInfo = await companyInfoService.GetCompanyInfoAsync();

        // Convert to the ViewModel CompanyInfo with logo support
        return new Models.CompanyInfo
        {
            CompanyName = dbCompanyInfo.CompanyName,
            Address = dbCompanyInfo.Address,
            City = dbCompanyInfo.City,
            State = dbCompanyInfo.State,
            ZipCode = dbCompanyInfo.ZipCode,
            Phone = dbCompanyInfo.Phone,
            Email = dbCompanyInfo.Email,
            Website = dbCompanyInfo.Website,
            // Add logo properties
            LogoData = dbCompanyInfo.LogoData,
            LogoContentType = dbCompanyInfo.LogoContentType,
            LogoFileName = dbCompanyInfo.LogoFileName
        };
      }
      catch
      {
        // Fallback to hardcoded values if database access fails
        return new Models.CompanyInfo
        {
            CompanyName = "Your Inventory Management Company",
            Address = "123 Business Drive",
            City = "Business City",
            State = "NC",
            ZipCode = "27101",
            Phone = "(336) 555-0123",
            Email = "purchasing@yourcompany.com",
            Website = "www.yourcompany.com",
        };
      }
    }

    // Add a new action to serve the company logo for PO reports
    [HttpGet]
    public async Task<IActionResult> CompanyLogo()
    {
      try
      {
        var companyInfoService = HttpContext.RequestServices.GetRequiredService<ICompanyInfoService>();
        var companyInfo = await companyInfoService.GetCompanyInfoAsync();
        
        if (companyInfo?.LogoData != null && companyInfo.LogoData.Length > 0)
        {
            return File(companyInfo.LogoData, companyInfo.LogoContentType ?? "image/png", companyInfo.LogoFileName);
        }
        
        // Return a default placeholder or 404
        return NotFound();
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error retrieving company logo: {ex.Message}");
        return NotFound();
      }
    }

    // Helper method to render view to string (for email HTML)
    private Task<string> RenderViewToStringAsync(string viewName, object model)
    {
      // This is a simplified implementation - you might want to use a proper view rendering service
      // For now, we'll return a basic HTML template
      var viewModel = model as PurchaseOrderReportViewModel;
      if (viewModel == null) return Task.FromResult(string.Empty);

      var html = $@"
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; margin: 20px; }}
                .header {{ text-align: center; border-bottom: 2px solid #333; padding-bottom: 10px; }}
                .company-info {{ margin: 20px 0; }}
                .vendor-info {{ margin: 20px 0; }}
                table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
                th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                th {{ background-color: #f2f2f2; }}
                .total-row {{ font-weight: bold; background-color: #f9f9f9; }}
            </style>
        </head>
        <body>
            <div class='header'>
                <h1>PURCHASE ORDER</h1>
                <h2>PO# {viewModel.PurchaseOrderNumber}</h2>
            </div>
            
            <div class='company-info'>
                <h3>From:</h3>
                <p><strong>{viewModel.CompanyInfo.CompanyName}</strong><br/>
                {viewModel.CompanyInfo.Address}<br/>
                {viewModel.CompanyInfo.City}, {viewModel.CompanyInfo.State} {viewModel.CompanyInfo.ZipCode}<br/>
                Phone: {viewModel.CompanyInfo.Phone}<br/>
                Email: {viewModel.CompanyInfo.Email}</p>
            </div>
            
            <div class='vendor-info'>
                <h3>To:</h3>
                <p><strong>{viewModel.Vendor.CompanyName}</strong><br/>
                {GetVendorAddressForEmail(viewModel.Vendor)}<br/>
                Phone: {viewModel.Vendor.ContactPhone}<br/>
                Email: {viewModel.Vendor.ContactEmail}</p>
            </div>
            
            <p><strong>PO Date:</strong> {viewModel.PurchaseDate:MM/dd/yyyy}<br/>
            <strong>Expected Delivery:</strong> {viewModel.ExpectedDeliveryDate?.ToString("MM/dd/yyyy") ?? "TBD"}<br/>
            <strong>Status:</strong> {viewModel.Status}</p>
            
            <table>
                <thead>
                    <tr>
                        <th>Item #</th>
                        <th>Description</th>
                        <th>Qty</th>
                        <th>Unit Price</th>
                        <th>Total</th>
                    </tr>
                </thead>
                <tbody>";

      foreach (var item in viewModel.LineItems)
      {
        html += $@"
                    <tr>
                        <td>{item.PartNumber}</td>
                        <td>{item.Description}</td>
                        <td>{item.Quantity}</td>
                        <td>${item.UnitCost:F2}</td>
                        <td>${item.LineTotal:F2}</td>
                    </tr>";
      }

      html += $@"
                    <tr class='total-row'>
                        <td colspan='4'><strong>TOTAL</strong></td>
                        <td><strong>${viewModel.SubtotalAmount:F2}</strong></td>
                    </tr>
                </tbody>
            </table>
            
            {(string.IsNullOrEmpty(viewModel.Notes) ? "" : $"<p><strong>Notes:</strong> {viewModel.Notes}</p>")}
            
            <p><em>Please include the purchase order number with all shipments</em></p>
        </body>
        </html>";

      return Task.FromResult(html);
    }

    // Helper method to format vendor address for email
    private string GetVendorAddressForEmail(Vendor vendor)
    {
      var addressParts = new List<string>();
      
      if (!string.IsNullOrWhiteSpace(vendor.AddressLine1))
        addressParts.Add(vendor.AddressLine1);
      
      if (!string.IsNullOrWhiteSpace(vendor.AddressLine2))
        addressParts.Add(vendor.AddressLine2);
      
      var cityStateZip = new List<string>();
      if (!string.IsNullOrWhiteSpace(vendor.City))
        cityStateZip.Add(vendor.City);
      if (!string.IsNullOrWhiteSpace(vendor.State))
        cityStateZip.Add(vendor.State);
      if (!string.IsNullOrWhiteSpace(vendor.PostalCode))
        cityStateZip.Add(vendor.PostalCode);
      
      if (cityStateZip.Any())
      {
        var cityStateLine = string.Join(", ", cityStateZip.Take(2));
        if (cityStateZip.Count > 2)
          cityStateLine += " " + cityStateZip.Last();
        addressParts.Add(cityStateLine);
      }
      
      if (!string.IsNullOrWhiteSpace(vendor.Country) && 
          !vendor.Country.Equals("United States", StringComparison.OrdinalIgnoreCase))
      {
          addressParts.Add(vendor.Country);
      }
      
      return addressParts.Any() ? string.Join("<br/>", addressParts) : "Address not available";
    }
  }
}