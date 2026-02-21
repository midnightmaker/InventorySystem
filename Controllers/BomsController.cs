using InventorySystem.Models;
using InventorySystem.Models.ViewModels;
using InventorySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using ClosedXML.Excel;

namespace InventorySystem.Controllers
{
  public class BomsController : Controller
  {
    private readonly IBomService _bomService;
    private readonly IInventoryService _inventoryService;
    private readonly IProductionService _productionService;
    private readonly IVersionControlService _versionService;
    private readonly ILogger<BomsController> _logger;
    private readonly InventoryContext _context;

    public BomsController(
        IBomService bomService,
        IInventoryService inventoryService,
        IProductionService productionService,
        IVersionControlService versionService,
        ILogger<BomsController> logger,
        InventoryContext context)
    {
      _bomService = bomService;
      _inventoryService = inventoryService;
      _productionService = productionService;
      _versionService = versionService;
      _logger = logger;
      _context = context;
    }

    // Enhanced BOMs Index with filtering, pagination, and search
    public async Task<IActionResult> Index(
        string search,
        string bomTypeFilter,
        string versionFilter,
        string assemblyFilter,
        string sortOrder = "bomNumber_asc",
        int page = 1,
        int pageSize = 25)
    {
      try
      {
        // Pagination constants
        const int DefaultPageSize = 25;
        const int MaxPageSize = 100;
        var AllowedPageSizes = new[] { 10, 25, 50, 100 };

        // Validate and constrain pagination parameters
        page = Math.Max(1, page);
        pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

        _logger.LogInformation("=== BOMS INDEX DEBUG ===");
        _logger.LogInformation("Search: {Search}", search);
        _logger.LogInformation("BOM Type Filter: {BomTypeFilter}", bomTypeFilter);
        _logger.LogInformation("Version Filter: {VersionFilter}", versionFilter);
        _logger.LogInformation("Assembly Filter: {AssemblyFilter}", assemblyFilter);
        _logger.LogInformation("Sort Order: {SortOrder}", sortOrder);
        _logger.LogInformation("Page: {Page}, PageSize: {PageSize}", page, pageSize);

        // Start with base query including necessary navigation properties
        var query = _context.Boms
            .Include(b => b.BomItems)
                .ThenInclude(bi => bi.Item)
            .Include(b => b.SubAssemblies)
            .Include(b => b.Documents)
            .AsQueryable();

        // Apply search filter with wildcard support
        if (!string.IsNullOrWhiteSpace(search))
        {
          var searchTerm = search.Trim();
          _logger.LogInformation("Applying search filter: {SearchTerm}", searchTerm);

          if (searchTerm.Contains('*') || searchTerm.Contains('?'))
          {
            var likePattern = ConvertWildcardToLike(searchTerm);
            _logger.LogInformation("Using LIKE pattern: {LikePattern}", likePattern);

            query = query.Where(b =>
              EF.Functions.Like(b.BomNumber, likePattern) ||
              EF.Functions.Like(b.Description, likePattern) ||
              (b.AssemblyPartNumber != null && EF.Functions.Like(b.AssemblyPartNumber, likePattern)) ||
              EF.Functions.Like(b.Version, likePattern) ||
              EF.Functions.Like(b.Id.ToString(), likePattern)
            );
          }
          else
          {
            query = query.Where(b =>
              b.BomNumber.Contains(searchTerm) ||
              b.Description.Contains(searchTerm) ||
              (b.AssemblyPartNumber != null && b.AssemblyPartNumber.Contains(searchTerm)) ||
              b.Version.Contains(searchTerm) ||
              b.Id.ToString().Contains(searchTerm)
            );
          }
        }

        // Apply BOM type filter
        if (!string.IsNullOrWhiteSpace(bomTypeFilter))
        {
          _logger.LogInformation("Applying BOM type filter: {BomTypeFilter}", bomTypeFilter);
          query = bomTypeFilter switch
          {
            "main" => query.Where(b => b.ParentBomId == null), // Main assemblies
            "subassembly" => query.Where(b => b.ParentBomId != null), // Sub-assemblies
            "withitems" => query.Where(b => b.BomItems.Any()), // BOMs with items
            "withsubs" => query.Where(b => b.SubAssemblies.Any()), // BOMs with sub-assemblies
            "withdocs" => query.Where(b => b.Documents.Any()), // BOMs with documents
            "empty" => query.Where(b => !b.BomItems.Any() && !b.SubAssemblies.Any()), // Empty BOMs
            _ => query
          };
        }

        // Apply version filter
        if (!string.IsNullOrWhiteSpace(versionFilter))
        {
          _logger.LogInformation("Applying version filter: {VersionFilter}", versionFilter);
          query = versionFilter switch
          {
            "current" => query.Where(b => b.IsCurrentVersion),
            "archived" => query.Where(b => !b.IsCurrentVersion),
            "latest" => query.Where(b => b.Version.Contains("latest") || b.Version.Contains("current")),
            "draft" => query.Where(b => b.Version.Contains("draft") || b.Version.Contains("beta")),
            _ => query
          };
        }

        // Apply assembly filter
        if (!string.IsNullOrWhiteSpace(assemblyFilter))
        {
          _logger.LogInformation("Applying assembly filter: {AssemblyFilter}", assemblyFilter);
          if (assemblyFilter.Contains('*') || assemblyFilter.Contains('?'))
          {
            var likePattern = ConvertWildcardToLike(assemblyFilter);
            query = query.Where(b => b.AssemblyPartNumber != null && 
                                   EF.Functions.Like(b.AssemblyPartNumber, likePattern));
          }
          else
          {
            query = query.Where(b => b.AssemblyPartNumber != null && 
                                   b.AssemblyPartNumber.Contains(assemblyFilter));
          }
        }

        // Apply sorting
        query = sortOrder switch
        {
          "bomNumber_asc" => query.OrderBy(b => b.BomNumber),
          "bomNumber_desc" => query.OrderByDescending(b => b.BomNumber),
          "description_asc" => query.OrderBy(b => b.Description),
          "description_desc" => query.OrderByDescending(b => b.Description),
          "assembly_asc" => query.OrderBy(b => b.AssemblyPartNumber ?? ""),
          "assembly_desc" => query.OrderByDescending(b => b.AssemblyPartNumber ?? ""),
          "version_asc" => query.OrderBy(b => b.Version),
          "version_desc" => query.OrderByDescending(b => b.Version),
          "created_asc" => query.OrderBy(b => b.CreatedDate),
          "created_desc" => query.OrderByDescending(b => b.CreatedDate),
          "modified_desc" => query.OrderByDescending(b => b.ModifiedDate),
          "modified_asc" => query.OrderBy(b => b.ModifiedDate),
          "itemcount_desc" => query.OrderByDescending(b => b.BomItems.Count()),
          "itemcount_asc" => query.OrderBy(b => b.BomItems.Count()),
          _ => query.OrderBy(b => b.BomNumber)
        };

        // Get total count for pagination (before Skip/Take)
        var totalCount = await query.CountAsync();
        _logger.LogInformation("Total filtered records: {TotalCount}", totalCount);

        // Calculate pagination values
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var skip = (page - 1) * pageSize;

        // Get paginated results
        var boms = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation("Retrieved {BomCount} BOMs for page {Page}", boms.Count, page);

        // Prepare ViewBag data
        ViewBag.SearchTerm = search;
        ViewBag.BomTypeFilter = bomTypeFilter;
        ViewBag.VersionFilter = versionFilter;
        ViewBag.AssemblyFilter = assemblyFilter;
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
        ViewBag.BomTypeOptions = new SelectList(new[]
        {
          new { Value = "", Text = "All BOM Types" },
          new { Value = "main", Text = "Main Assemblies" },
          new { Value = "subassembly", Text = "Sub-Assemblies" },
          new { Value = "withitems", Text = "With Components" },
          new { Value = "withsubs", Text = "With Sub-Assemblies" },
          new { Value = "withdocs", Text = "With Documents" },
          new { Value = "empty", Text = "Empty BOMs" }
        }, "Value", "Text", bomTypeFilter);

        ViewBag.VersionOptions = new SelectList(new[]
        {
          new { Value = "", Text = "All Versions" },
          new { Value = "current", Text = "Current Versions Only" },
          new { Value = "archived", Text = "Archived Versions" },
          new { Value = "latest", Text = "Latest/Current" },
          new { Value = "draft", Text = "Draft/Beta" }
        }, "Value", "Text", versionFilter);

        // Search statistics
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
                           !string.IsNullOrWhiteSpace(bomTypeFilter) ||
                           !string.IsNullOrWhiteSpace(versionFilter) ||
                           !string.IsNullOrWhiteSpace(assemblyFilter);

        if (ViewBag.IsFiltered)
        {
          var totalBoms = await _bomService.GetAllBomsAsync();
          ViewBag.SearchResultsCount = totalCount;
          ViewBag.TotalBomsCount = totalBoms.Count();
        }

        return View(boms);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in BOMs Index");

        // Set essential ViewBag properties that the view expects
        ViewBag.ErrorMessage = $"Error loading BOMs: {ex.Message}";
        ViewBag.AllowedPageSizes = new[] { 10, 25, 50, 100 };

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
        ViewBag.BomTypeFilter = bomTypeFilter;
        ViewBag.VersionFilter = versionFilter;
        ViewBag.AssemblyFilter = assemblyFilter;
        ViewBag.SortOrder = sortOrder;
        ViewBag.IsFiltered = false;

        // Set empty dropdown options
        ViewBag.BomTypeOptions = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.VersionOptions = new SelectList(new List<object>(), "Value", "Text");

        return View(new List<Bom>());
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

    public async Task<IActionResult> Details(int id)
    {
      var bom = await _bomService.GetBomByIdAsync(id); // Use existing method from IBomService

      if (bom == null) return NotFound();

      ViewBag.TotalCost = await _bomService.GetBomTotalCostAsync(id);

      // Calculate individual sub-assembly costs for the Details view
      if (bom.SubAssemblies?.Any() == true)
      {
        var subAssemblyCosts = new Dictionary<int, decimal>();
        foreach (var subAssembly in bom.SubAssemblies)
        {
          var subAssemblyCost = await _bomService.GetBomTotalCostAsync(subAssembly.Id);
          subAssemblyCosts[subAssembly.Id] = subAssemblyCost;
        }
        ViewBag.SubAssemblyCosts = subAssemblyCosts;
      }

      // Check for pending change orders  
      var pendingChangeOrders = await _versionService.GetPendingChangeOrdersForEntityAsync("BOM", bom.BaseBomId ?? bom.Id);
      ViewBag.PendingChangeOrders = pendingChangeOrders;
      ViewBag.EntityType = "BOM";

      // Add BOM versions for the version dropdown
      var bomVersions = await _versionService.GetBomVersionsAsync(bom.BaseBomId ?? bom.Id);
      ViewBag.BomVersions = bomVersions.Select(v => new
      {
        Id = v.Id,
        Version = v.Version,
        IsCurrentVersion = v.IsCurrentVersion,
        CreatedDate = v.CreatedDate
      });

      return View(bom);
    }

    public IActionResult Create(int? parentBomId)
    {
      var bom = new Bom();
      if (parentBomId.HasValue)
      {
        bom.ParentBomId = parentBomId.Value;
      }
      return View(bom);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Bom bom)
    {
      if (ModelState.IsValid)
      {
        await _bomService.CreateBomAsync(bom);
        return RedirectToAction(nameof(Index));
      }
      return View(bom);
    }

    public async Task<IActionResult> Edit(int id)
    {
      var bom = await _bomService.GetBomByIdAsync(id);
      if (bom == null) return NotFound();

      ViewBag.TotalCost = await _bomService.GetBomTotalCostAsync(id);
      return View(bom);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Bom bom)
    {
      if (id != bom.Id) return NotFound();

      Console.WriteLine("=== BOM EDIT DEBUG ===");
      Console.WriteLine($"BomId: {bom.Id}");
      Console.WriteLine($"Name: {bom.BomNumber}");
      Console.WriteLine($"Version: {bom.Version}");
      Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");

      // Remove validation for navigation properties and calculated fields
      ModelState.Remove("BomItems");
      ModelState.Remove("SubAssemblies");
      ModelState.Remove("ParentBom");
      ModelState.Remove("ModifiedDate");

      // Update the ModifiedDate automatically
      bom.ModifiedDate = DateTime.Now;

      if (ModelState.IsValid)
      {
        try
        {
          await _bomService.UpdateBomAsync(bom);
          TempData["SuccessMessage"] = "BOM updated successfully!";
          return RedirectToAction("Details", new { id = bom.Id });
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error updating BOM: {ex.Message}");
          TempData["ErrorMessage"] = $"Error updating BOM: {ex.Message}";
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

      // Reload the BOM with all related data for the view
      var fullBom = await _bomService.GetBomByIdAsync(id);
      if (fullBom != null)
      {
        // Copy the form values to the full BOM object
        fullBom.BomNumber = bom.BomNumber;
        fullBom.Description = bom.Description;
        fullBom.Version = bom.Version;
        fullBom.AssemblyPartNumber = bom.AssemblyPartNumber;

        ViewBag.TotalCost = await _bomService.GetBomTotalCostAsync(id);
        return View(fullBom);
      }

      return View(bom);
    }

    // REMOVE THE EditBomItem method completely since GetBomItemByIdAsync doesn't exist
    // Instead, users can edit BOM items by removing and re-adding them

    // Optional: Add a method to update BOM item quantities inline (if you want this feature later)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateBomItemQuantity(int bomItemId, int newQuantity, int bomId)
    {
      try
      {
        // You could implement this by getting the BOM, finding the item, and updating it
        // For now, we'll just redirect with a message
        TempData["InfoMessage"] = "To edit component quantities, please remove and re-add the component.";
        return RedirectToAction("Details", new { id = bomId });
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error updating component: {ex.Message}";
        return RedirectToAction("Details", new { id = bomId });
      }
    }

    public async Task<IActionResult> AddItem(int bomId)
    {
      var bom = await _bomService.GetBomByIdAsync(bomId);
      if (bom == null) return NotFound();

      var items = await _inventoryService.GetAllItemsAsync();

      // Format dropdown to show both part number and description
      var formattedItems = items.Select(item => new
      {
        Value = item.Id,
        Text = $"{item.PartNumber} - {item.Description}"
      }).ToList();

      ViewBag.BomId = bomId;
      ViewBag.ItemId = new SelectList(formattedItems, "Value", "Text");

      var bomItem = new BomItem
      {
        BomId = bomId,
        Quantity = 1
      };

      return View(bomItem);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(BomItem bomItem)
    {
      Console.WriteLine("=== BOM ADD ITEM DEBUG ===");
      Console.WriteLine($"BomId: {bomItem.BomId}");
      Console.WriteLine($"ItemId: {bomItem.ItemId}");
      Console.WriteLine($"Quantity: {bomItem.Quantity}");
      Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");

      // Remove validation for navigation properties that aren't needed for creation
      ModelState.Remove("Bom");
      ModelState.Remove("Item");
      ModelState.Remove("Id");
      ModelState.Remove("UnitCost");
      ModelState.Remove("ExtendedCost");

      // Manual validation for required fields
      if (bomItem.BomId <= 0)
      {
        ModelState.AddModelError("BomId", "BOM ID is required.");
      }

      if (bomItem.ItemId <= 0)
      {
        ModelState.AddModelError("ItemId", "Please select an item.");
      }

      if (bomItem.Quantity <= 0)
      {
        ModelState.AddModelError("Quantity", "Quantity must be greater than 0.");
      }

      // Check if the item is already in this BOM
      if (bomItem.BomId > 0 && bomItem.ItemId > 0)
      {
        var existingBomItems = await _bomService.GetBomByIdAsync(bomItem.BomId);
        if (existingBomItems?.BomItems?.Any(bi => bi.ItemId == bomItem.ItemId) == true)
        {
          ModelState.AddModelError("ItemId", "This item is already in the BOM. Edit the existing entry instead.");
        }
      }

      if (ModelState.IsValid)
      {
        try
        {
          await _bomService.AddBomItemAsync(bomItem);
          TempData["SuccessMessage"] = "Item added to BOM successfully!";
          return RedirectToAction("Details", new { id = bomItem.BomId });
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error adding BOM item: {ex.Message}");
          TempData["ErrorMessage"] = $"Error adding item to BOM: {ex.Message}";
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

      // Reload the view with data
      var items = await _inventoryService.GetAllItemsAsync();
      ViewBag.BomId = bomItem.BomId;
      ViewBag.ItemId = new SelectList(items, "Id", "PartNumber", bomItem.ItemId);

      return View(bomItem);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveItem(int bomItemId, int bomId)
    {
      await _bomService.DeleteBomItemAsync(bomItemId);
      return RedirectToAction("Details", new { id = bomId });
    }

    public async Task<IActionResult> CostReport(int id, bool explodeSubAssemblies = false)
    {
      var bom = await _bomService.GetBomByIdAsync(id);
      if (bom == null) return NotFound();

      ViewBag.TotalCost = await _bomService.GetBomTotalCostAsync(id);
      ViewBag.ExplodeSubAssemblies = explodeSubAssemblies;
      
      // Calculate individual sub-assembly costs for the Cost Report view
      if (bom.SubAssemblies?.Any() == true)
      {
        var subAssemblyCosts = new Dictionary<int, decimal>();
        foreach (var subAssembly in bom.SubAssemblies)
        {
          var subAssemblyCost = await _bomService.GetBomTotalCostAsync(subAssembly.Id);
          subAssemblyCosts[subAssembly.Id] = subAssemblyCost;
        }
        ViewBag.SubAssemblyCosts = subAssemblyCosts;
      }
      
      if (explodeSubAssemblies)
      {
        ViewBag.ExplodedCostData = await _bomService.GetExplodedBomCostDataAsync(id);
      }

      return View(bom);
    }

    // ?? DELETE BOM ????????????????????????????????????????????????????????

    public async Task<IActionResult> Delete(int id)
    {
      var bom = await _bomService.GetBomByIdAsync(id);
      if (bom == null) return NotFound();

      // Run impact check so the view can show what will be affected / blocked
      var deletability = await _bomService.CheckBomDeletabilityAsync(id);
      ViewBag.Deletability = deletability;

      return View(bom);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
      try
      {
        var bom = await _bomService.GetBomByIdAsync(id);
        if (bom == null) return NotFound();

        var parentBomId = bom.ParentBomId; // remember for redirect

        await _bomService.DeleteBomAsync(id);

        TempData["SuccessMessage"] = $"BOM \"{bom.BomNumber}\" was permanently deleted.";

        // If it was a sub-assembly, return to the parent; otherwise go to index.
        if (parentBomId.HasValue)
          return RedirectToAction("Details", new { id = parentBomId.Value });

        return RedirectToAction(nameof(Index));
      }
      catch (InvalidOperationException ex)
      {
        TempData["ErrorMessage"] = ex.Message;
        return RedirectToAction("Delete", new { id });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unhandled error deleting BOM {BomId}", id);
        TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
        return RedirectToAction("Delete", new { id });
      }
    }

    // ?? DETACH SUB-ASSEMBLY (remove the link, keep the BOM) ???????????????

    /// <summary>
    /// Shows a confirmation page before detaching a sub-assembly from its parent.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DetachSubAssembly(int subAssemblyId, int parentBomId)
    {
      var subBom = await _bomService.GetBomByIdAsync(subAssemblyId);
      var parentBom = await _bomService.GetBomByIdAsync(parentBomId);

      if (subBom == null || parentBom == null) return NotFound();

      ViewBag.ParentBom = parentBom;
      return View(subBom);
    }

    [HttpPost, ActionName("DetachSubAssembly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DetachSubAssemblyConfirmed(int subAssemblyId, int parentBomId)
    {
      try
      {
        var subBom = await _bomService.GetBomByIdAsync(subAssemblyId);
        var parentBom = await _bomService.GetBomByIdAsync(parentBomId);

        if (subBom == null || parentBom == null) return NotFound();

        await _bomService.DetachSubAssemblyAsync(subAssemblyId, parentBomId);

        TempData["SuccessMessage"] = $"Sub-assembly \"{subBom.BomNumber}\" was removed from \"{parentBom.BomNumber}\". The sub-assembly BOM still exists independently.";
        return RedirectToAction("Details", new { id = parentBomId });
      }
      catch (InvalidOperationException ex)
      {
        TempData["ErrorMessage"] = ex.Message;
        return RedirectToAction("Details", new { id = parentBomId });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error detaching sub-assembly {SubId} from parent {ParentId}", subAssemblyId, parentBomId);
        TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
        return RedirectToAction("Details", new { id = parentBomId });
      }
    }

    [HttpGet]
    public async Task<IActionResult> UploadDocument(int id)
    {
      var bom = await _bomService.GetBomByIdAsync(id);
      if (bom == null) return NotFound();

      // Redirect to Documents controller with proper BOM ID
      return RedirectToAction("UploadBom", "Documents", new { bomId = id });
    }
    
    [HttpGet]
    public IActionResult Import()
    {
      return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file)
    {
      if (file == null || file.Length == 0)
      {
        TempData["ErrorMessage"] = "Please select a valid CSV file.";
        return View();
      }

      // Validate file extension
      var allowedExtensions = new[] { ".csv", ".txt" };
      var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

      if (!allowedExtensions.Contains(fileExtension))
      {
        TempData["ErrorMessage"] = "Only CSV files (.csv, .txt) are supported.";
        return View();
      }

      try
      {
        using var stream = file.OpenReadStream();
        var bomImportService = HttpContext.RequestServices.GetRequiredService<BomImportService>();
        var result = await bomImportService.ImportBomFromCsvAsync(stream, file.FileName);

        if (result.IsSuccess)
        {
          TempData["SuccessMessage"] = result.GetSummary();

          // Store detailed results in TempData for display
          TempData["ImportDetails"] = System.Text.Json.JsonSerializer.Serialize(new
          {
            result.BomsCreated,
            result.ItemsCreated,
            result.BomItemsCreated,
            result.CreatedBoms,
            result.CreatedItems,
            result.Warnings
          });

          return RedirectToAction("ImportResults");
        }
        else
        {
          TempData["ErrorMessage"] = $"Import failed: {result.ErrorMessage}";
          return View();
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during BOM import");
        TempData["ErrorMessage"] = $"An error occurred during import: {ex.Message}";
        return View();
      }
    }


    [HttpGet]
    public IActionResult DownloadSample()
    {
      try
      {
        var csv = new StringBuilder();

        // Add CSV headers
        csv.AppendLine("Level,Part Number,Description,Revision,Quantity");

        // Add sample data demonstrating hierarchy
        var sampleData = new[]
        {
            "1,ASSY-001,Main Assembly,A,1",
            "1.1,PART-001,Component 1,B,2",
            "1.2,PART-002,Component 2,A,1",
            "1.3,SCREW-001,M4x10 Screw,,4",
            "1.4,SUB-ASSY-001,Sub Assembly 1,A,1",
            "1.4.1,PART-003,Sub Component 1,A,3",
            "1.4.2,PART-004,Sub Component 2,B,2",
            "1.4.3,GASKET-001,Rubber Gasket,,1",
            "1.5,PART-005,Component 3,A,1",
            "1.6,SUB-ASSY-002,Sub Assembly 2,B,2",
            "1.6.1,PART-006,Sub Component 3,A,1",
            "1.6.2,PART-007,Sub Component 4,A,2",
            "1.6.2.1,PART-008,Nested Component,A,1",
            "1.7,LABEL-001,Product Label,,1"
        };

        // Add sample data to CSV
        foreach (var line in sampleData)
        {
          csv.AppendLine(line);
        }

        // Add notes as comments (lines starting with #)
        csv.AppendLine();
        csv.AppendLine("# NOTES:");
        csv.AppendLine("# Level 1 = Main Assembly");
        csv.AppendLine("# Level 1.x = Direct Components");
        csv.AppendLine("# Level 1.x.x = Sub-Assembly Components");
        csv.AppendLine("# Level 1.x.x.x = Nested Components");
        csv.AppendLine("# Sub-assemblies will be created as separate BOMs");
        csv.AppendLine("# Missing items will be created automatically");
        csv.AppendLine("# Use quotes around fields that contain commas");
        csv.AppendLine("# Example: \"1.5,PART-005,\"\"Component with, comma\"\",A,1\"");

        // Convert to byte array
        var fileBytes = Encoding.UTF8.GetBytes(csv.ToString());

        // Return CSV file
        return File(fileBytes,
            "text/csv",
            "BOM_Import_Sample.csv");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error generating sample BOM CSV file");
        TempData["ErrorMessage"] = "Error generating sample file. Please try again.";
        return RedirectToAction("Import");
      }
    }
    
    [HttpGet]
    public IActionResult ImportResults()
    {
      var importDetailsJson = TempData["ImportDetails"] as string;
      if (string.IsNullOrEmpty(importDetailsJson))
      {
        return RedirectToAction("Index");
      }

      var importDetails = System.Text.Json.JsonSerializer.Deserialize<ImportResultsViewModel>(importDetailsJson);
      return View(importDetails);
    }

    // BOM Excel Export - GET
    [HttpGet]
    public async Task<IActionResult> ExportExcel(int id)
    {
      try
      {
        var bom = await _bomService.GetBomByIdAsync(id);
        if (bom == null)
        {
          TempData["ErrorMessage"] = "BOM not found.";
          return RedirectToAction("Index");
        }

        var explodedData = await _bomService.GetExplodedBomCostDataAsync(id);

        using var workbook = new XLWorkbook();

        // ?? Sheet 1: Summary ?????????????????????????????????????????????
        var summary = workbook.Worksheets.Add("Summary");

        // Title
        summary.Cell("A1").Value = $"BOM: {bom.BomNumber}";
        summary.Cell("A1").Style.Font.Bold = true;
        summary.Cell("A1").Style.Font.FontSize = 16;
        summary.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        summary.Range("A1:D1").Merge();

        summary.Cell("A2").Value = "Generated:";
        summary.Cell("B2").Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        // BOM metadata
        int r = 4;
        void AddMeta(string label, string value)
        {
          summary.Cell(r, 1).Value = label;
          summary.Cell(r, 1).Style.Font.Bold = true;
          summary.Cell(r, 2).Value = value;
          r++;
        }

        AddMeta("BOM Number:", bom.BomNumber);
        AddMeta("Description:", bom.Description ?? "");
        AddMeta("Assembly P/N:", bom.AssemblyPartNumber ?? "");
        AddMeta("Version:", bom.Version ?? "");
        AddMeta("Created:", bom.CreatedDate.ToString("yyyy-MM-dd"));
        AddMeta("Modified:", bom.ModifiedDate.ToString("yyyy-MM-dd"));

        r++;
        // Cost summary section
        summary.Cell(r, 1).Value = "Cost Summary";
        summary.Cell(r, 1).Style.Font.Bold = true;
        summary.Cell(r, 1).Style.Font.FontSize = 12;
        r++;

        void AddCost(string label, decimal amount)
        {
          summary.Cell(r, 1).Value = label;
          summary.Cell(r, 1).Style.Font.Bold = true;
          summary.Cell(r, 2).Value = amount;
          summary.Cell(r, 2).Style.NumberFormat.Format = "$#,##0.00";
          r++;
        }

        AddCost("Direct Components:", explodedData.Summary.DirectComponentsCost);
        AddCost("Sub-Assemblies:", explodedData.Summary.SubAssembliesCost);

        // Total row — highlighted
        summary.Cell(r, 1).Value = "Total BOM Cost:";
        summary.Cell(r, 1).Style.Font.Bold = true;
        summary.Cell(r, 2).Value = explodedData.TotalCost;
        summary.Cell(r, 2).Style.NumberFormat.Format = "$#,##0.00";
        summary.Cell(r, 2).Style.Font.Bold = true;
        summary.Row(r).Style.Fill.BackgroundColor = XLColor.LightGreen;
        r++;

        r++;
        summary.Cell(r, 1).Value = "Component Count:";
        summary.Cell(r, 1).Style.Font.Bold = true;
        summary.Cell(r, 2).Value = explodedData.Summary.TotalComponentCount;
        r++;
        summary.Cell(r, 1).Value = "Sub-Assembly Count:";
        summary.Cell(r, 1).Style.Font.Bold = true;
        summary.Cell(r, 2).Value = explodedData.Summary.TotalSubAssemblyCount;

        summary.Column(1).Width = 22;
        summary.Column(2).Width = 28;
        summary.SheetView.FreezeRows(0);

        // ?? Sheet 2: Components ???????????????????????????????????????????
        var sheet = workbook.Worksheets.Add("Components");

        // Header row
        var headers = new[]
        {
          "Level", "Source BOM", "Part Number", "Description",
          "Ref. Designator", "Qty", "Unit Cost", "Extended Cost", "Notes"
        };

        for (int c = 0; c < headers.Length; c++)
        {
          var cell = sheet.Cell(1, c + 1);
          cell.Value = headers[c];
          cell.Style.Font.Bold = true;
          cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F5597");
          cell.Style.Font.FontColor = XLColor.White;
          cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Freeze header
        sheet.SheetView.FreezeRows(1);

        int row = 2;
        decimal grandTotal = 0;

        foreach (var comp in explodedData.AllComponents)
        {
          // Indent the description column to visually show hierarchy depth
          var indent = new string(' ', comp.Level * 3);

          sheet.Cell(row, 1).Value = comp.Level;
          sheet.Cell(row, 2).Value = comp.SourceBom;
          sheet.Cell(row, 3).Value = comp.PartNumber;
          sheet.Cell(row, 4).Value = indent + comp.Description;
          sheet.Cell(row, 5).Value = comp.ReferenceDesignator;
          sheet.Cell(row, 6).Value = comp.Quantity;
          sheet.Cell(row, 7).Value = comp.UnitCost;
          sheet.Cell(row, 7).Style.NumberFormat.Format = "$#,##0.00";
          sheet.Cell(row, 8).Value = comp.ExtendedCost;
          sheet.Cell(row, 8).Style.NumberFormat.Format = "$#,##0.00";
          sheet.Cell(row, 9).Value = comp.Notes;

          // Alternate row shading per level for readability
          if (comp.Level == 0)
            sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEEFF");
          else if (row % 2 == 0)
            sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F7F7");

          grandTotal += comp.ExtendedCost;
          row++;
        }

        // Grand-total footer row
        sheet.Cell(row, 7).Value = "TOTAL:";
        sheet.Cell(row, 7).Style.Font.Bold = true;
        sheet.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(row, 8).Value = grandTotal;
        sheet.Cell(row, 8).Style.NumberFormat.Format = "$#,##0.00";
        sheet.Cell(row, 8).Style.Font.Bold = true;
        sheet.Row(row).Style.Fill.BackgroundColor = XLColor.LightGreen;

        // Auto-fit columns
        sheet.Column(1).Width = 8;
        sheet.Column(2).Width = 18;
        sheet.Column(3).Width = 18;
        sheet.Column(4).Width = 40;
        sheet.Column(5).Width = 16;
        sheet.Column(6).Width = 8;
        sheet.Column(7).Width = 14;
        sheet.Column(8).Width = 14;
        sheet.Column(9).Width = 30;

        // Add auto-filter to header row
        sheet.RangeUsed()?.SetAutoFilter();

        // ?? Stream to client ??????????????????????????????????????????????
        var safeNumber = (bom.BomNumber ?? "BOM").Replace(' ', '_')
                                                  .Replace('/', '-')
                                                  .Replace('\\', '-');
        var fileName = $"BOM_{safeNumber}_{DateTime.Now:yyyy-MM-dd}.xlsx";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        _logger.LogInformation("Excel export generated for BOM {BomId} ({BomNumber})", id, bom.BomNumber);

        return File(
          stream.ToArray(),
          "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
          fileName);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error generating Excel export for BOM {BomId}", id);
        TempData["ErrorMessage"] = $"Error generating Excel export: {ex.Message}";
        return RedirectToAction("Visualize", new { id });
      }
    }

    // BOM Visualization - GET
    public async Task<IActionResult> Visualize(int id)
    {
      try
      {
        var bom = await _bomService.GetBomByIdAsync(id);
        if (bom == null)
        {
          TempData["ErrorMessage"] = "BOM not found.";
          return RedirectToAction("Index");
        }

        // Load full hierarchy
        var bomHierarchy = await _bomService.GetBomHierarchyAsync(id);
        
        ViewBag.BomHierarchy = bomHierarchy;
        
        // Ensure we always have valid JSON data
        var jsonOptions = new JsonSerializerOptions
        {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          WriteIndented = false
        };
        
        ViewBag.JsonData = System.Text.Json.JsonSerializer.Serialize(bomHierarchy ?? new { }, jsonOptions);
        
        _logger.LogInformation("BOM visualization loaded for BOM {BomId} ({BomNumber})", id, bom.BomNumber);
        
        return View(bom);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading BOM visualization for BOM {BomId}", id);
        TempData["ErrorMessage"] = $"Error loading BOM visualization: {ex.Message}";
        return RedirectToAction("Index");
      }
    }
  }
}