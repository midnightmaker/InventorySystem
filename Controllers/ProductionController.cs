// Controllers/ProductionController.cs - Enhanced Version
using InventorySystem.Domain.Commands;
using InventorySystem.Domain.Enums;
using InventorySystem.Domain.Queries;
using InventorySystem.Domain.Services;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;
using InventorySystem.Data;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace InventorySystem.Controllers
{
  public class ProductionController : Controller
  {
    private readonly IProductionService _productionService;
    private readonly IBomService _bomService;
    private readonly IInventoryService _inventoryService;
    private readonly IPurchaseService _purchaseService;
    private readonly IVendorService _vendorService; // ADD THIS
    private readonly IProductionOrchestrator _orchestrator;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ILogger<ProductionController> _logger;

    public ProductionController(
        IProductionService productionService,
        IBomService bomService,
        IInventoryService inventoryService,
        IPurchaseService purchaseService,
        IVendorService vendorService, // ADD THIS PARAMETER
        IProductionOrchestrator orchestrator,
        IWorkflowEngine workflowEngine,
        ILogger<ProductionController> logger)
    {
      _productionService = productionService;
      _bomService = bomService;
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
      _vendorService = vendorService; // ADD THIS
      _orchestrator = orchestrator;
      _workflowEngine = workflowEngine;
      _logger = logger;
    }

    // Enhanced Production Index with filtering, pagination, and search
    public async Task<IActionResult> Index(
        string search,
        string statusFilter,
        string dateFilter,
        string bomFilter,
        string costFilter,
        string sortOrder = "productionDate_desc",
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

        _logger.LogInformation("=== PRODUCTION INDEX DEBUG ===");
        _logger.LogInformation("Search: {Search}", search);
        _logger.LogInformation("Status Filter: {StatusFilter}", statusFilter);
        _logger.LogInformation("Date Filter: {DateFilter}", dateFilter);
        _logger.LogInformation("BOM Filter: {BomFilter}", bomFilter);
        _logger.LogInformation("Cost Filter: {CostFilter}", costFilter);
        _logger.LogInformation("Sort Order: {SortOrder}", sortOrder);
        _logger.LogInformation("Page: {Page}, PageSize: {PageSize}", page, pageSize);

        // Get active productions for workflow section
        var query = new GetActiveProductionsQuery();
        var activeProductions = await _orchestrator.GetActiveProductionsAsync(query);

        // Get all productions with database context for filtering
        var productionsQuery = HttpContext.RequestServices.GetRequiredService<InventoryContext>()
            .Productions
            .Include(p => p.FinishedGood)
            .Include(p => p.Bom)
            .Include(p => p.MaterialConsumptions)
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

            productionsQuery = productionsQuery.Where(p =>
              (p.FinishedGood != null && EF.Functions.Like(p.FinishedGood.PartNumber, likePattern)) ||
              (p.Bom != null && EF.Functions.Like(p.Bom.BomNumber, likePattern)) ||
              (p.Notes != null && EF.Functions.Like(p.Notes, likePattern)) ||
              EF.Functions.Like(p.Id.ToString(), likePattern)
            );
          }
          else
          {
            productionsQuery = productionsQuery.Where(p =>
              (p.FinishedGood != null && p.FinishedGood.PartNumber.Contains(searchTerm)) ||
              (p.Bom != null && p.Bom.BomNumber.Contains(searchTerm)) ||
              (p.Notes != null && p.Notes.Contains(searchTerm)) ||
              p.Id.ToString().Contains(searchTerm)
            );
          }
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
          _logger.LogInformation("Applying status filter: {StatusFilter}", statusFilter);
          productionsQuery = statusFilter switch
          {
            "recent" => productionsQuery.Where(p => p.ProductionDate >= DateTime.Today.AddDays(-30)),
            "thismonth" => productionsQuery.Where(p => p.ProductionDate.Month == DateTime.Now.Month && p.ProductionDate.Year == DateTime.Now.Year),
            "lastmonth" => productionsQuery.Where(p => p.ProductionDate.Month == DateTime.Now.AddMonths(-1).Month && p.ProductionDate.Year == DateTime.Now.AddMonths(-1).Year),
            "thisyear" => productionsQuery.Where(p => p.ProductionDate.Year == DateTime.Now.Year),
            "highvolume" => productionsQuery.Where(p => p.QuantityProduced >= 100),
            "lowvolume" => productionsQuery.Where(p => p.QuantityProduced < 10),
            _ => productionsQuery
          };
        }

        // Apply date filter
        if (!string.IsNullOrWhiteSpace(dateFilter))
        {
          _logger.LogInformation("Applying date filter: {DateFilter}", dateFilter);
          var today = DateTime.Today;
          productionsQuery = dateFilter switch
          {
            "today" => productionsQuery.Where(p => p.ProductionDate.Date == today),
            "yesterday" => productionsQuery.Where(p => p.ProductionDate.Date == today.AddDays(-1)),
            "thisweek" => productionsQuery.Where(p => p.ProductionDate >= today.AddDays(-(int)today.DayOfWeek)),
            "lastweek" => productionsQuery.Where(p => p.ProductionDate >= today.AddDays(-7 - (int)today.DayOfWeek) && p.ProductionDate < today.AddDays(-(int)today.DayOfWeek)),
            "last30days" => productionsQuery.Where(p => p.ProductionDate >= today.AddDays(-30)),
            "last90days" => productionsQuery.Where(p => p.ProductionDate >= today.AddDays(-90)),
            _ => productionsQuery
          };
        }

        // Apply BOM filter
        if (!string.IsNullOrWhiteSpace(bomFilter))
        {
          _logger.LogInformation("Applying BOM filter: {BomFilter}", bomFilter);
          if (bomFilter.Contains('*') || bomFilter.Contains('?'))
          {
            var likePattern = ConvertWildcardToLike(bomFilter);
            productionsQuery = productionsQuery.Where(p => p.Bom != null && 
                                                     EF.Functions.Like(p.Bom.BomNumber, likePattern));
          }
          else
          {
            productionsQuery = productionsQuery.Where(p => p.Bom != null && 
                                                     p.Bom.BomNumber.Contains(bomFilter));
          }
        }

        // Apply cost filter
        if (!string.IsNullOrWhiteSpace(costFilter))
        {
          _logger.LogInformation("Applying cost filter: {CostFilter}", costFilter);
          productionsQuery = costFilter switch
          {
            "highcost" => productionsQuery.Where(p => (p.MaterialCost + p.LaborCost + p.OverheadCost) > 1000),
            "mediumcost" => productionsQuery.Where(p => (p.MaterialCost + p.LaborCost + p.OverheadCost) > 100 && (p.MaterialCost + p.LaborCost + p.OverheadCost) <= 1000),
            "lowcost" => productionsQuery.Where(p => (p.MaterialCost + p.LaborCost + p.OverheadCost) <= 100),
            "efficient" => productionsQuery.Where(p => p.QuantityProduced > 0 && (p.MaterialCost + p.LaborCost + p.OverheadCost) / p.QuantityProduced < 10),
            "expensive" => productionsQuery.Where(p => p.QuantityProduced > 0 && (p.MaterialCost + p.LaborCost + p.OverheadCost) / p.QuantityProduced > 100),
            "nocost" => productionsQuery.Where(p => p.MaterialCost == 0 && p.LaborCost == 0 && p.OverheadCost == 0),
            _ => productionsQuery
          };
        }

        // Apply sorting
        productionsQuery = sortOrder switch
        {
          "productionDate_asc" => productionsQuery.OrderBy(p => p.ProductionDate),
          "productionDate_desc" => productionsQuery.OrderByDescending(p => p.ProductionDate),
          "finishedGood_asc" => productionsQuery.OrderBy(p => p.FinishedGood != null ? p.FinishedGood.PartNumber : ""),
          "finishedGood_desc" => productionsQuery.OrderByDescending(p => p.FinishedGood != null ? p.FinishedGood.PartNumber : ""),
          "bom_asc" => productionsQuery.OrderBy(p => p.Bom != null ? p.Bom.BomNumber : ""),
          "bom_desc" => productionsQuery.OrderByDescending(p => p.Bom != null ? p.Bom.BomNumber : ""),
          "quantity_desc" => productionsQuery.OrderByDescending(p => p.QuantityProduced),
          "quantity_asc" => productionsQuery.OrderBy(p => p.QuantityProduced),
          "totalCost_desc" => productionsQuery.OrderByDescending(p => p.MaterialCost + p.LaborCost + p.OverheadCost),
          "totalCost_asc" => productionsQuery.OrderBy(p => p.MaterialCost + p.LaborCost + p.OverheadCost),
          "unitCost_desc" => productionsQuery.OrderByDescending(p => p.QuantityProduced > 0 ? (p.MaterialCost + p.LaborCost + p.OverheadCost) / p.QuantityProduced : 0),
          "unitCost_asc" => productionsQuery.OrderBy(p => p.QuantityProduced > 0 ? (p.MaterialCost + p.LaborCost + p.OverheadCost) / p.QuantityProduced : 0),
          _ => productionsQuery.OrderByDescending(p => p.ProductionDate)
        };

        // Get total count for pagination (before Skip/Take)
        var totalCount = await productionsQuery.CountAsync();
        _logger.LogInformation("Total filtered records: {TotalCount}", totalCount);

        // Calculate pagination values
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var skip = (page - 1) * pageSize;

        // Get paginated results
        var productions = await productionsQuery
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation("Retrieved {ProductionCount} productions for page {Page}", productions.Count, page);

        // Calculate summary statistics
        var totalProductions = totalCount;
        var totalUnitsProduced = productions.Sum(p => p.QuantityProduced);
        var totalValue = productions.Sum(p => p.TotalCost);
        var averageUnitCost = totalUnitsProduced > 0 ? totalValue / totalUnitsProduced : 0;

        // Create enhanced view model
        var viewModel = new ProductionIndexViewModel
        {
          ActiveProductions = activeProductions,
          AllProductions = productions.ToList(),
          ShowWorkflowView = true,
          TotalProductions = totalProductions,
          TotalUnitsProduced = totalUnitsProduced,
          TotalValue = totalValue,
          AverageUnitCost = averageUnitCost
        };

        // Prepare ViewBag data
        ViewBag.SearchTerm = search;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.DateFilter = dateFilter;
        ViewBag.BomFilter = bomFilter;
        ViewBag.CostFilter = costFilter;
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
        ViewBag.StatusOptions = new SelectList(new[]
        {
          new { Value = "", Text = "All Periods" },
          new { Value = "recent", Text = "Recent (30 days)" },
          new { Value = "thismonth", Text = "This Month" },
          new { Value = "lastmonth", Text = "Last Month" },
          new { Value = "thisyear", Text = "This Year" },
          new { Value = "highvolume", Text = "High Volume (100+)" },
          new { Value = "lowvolume", Text = "Low Volume (<10)" }
        }, "Value", "Text", statusFilter);

        ViewBag.DateOptions = new SelectList(new[]
        {
          new { Value = "", Text = "All Dates" },
          new { Value = "today", Text = "Today" },
          new { Value = "yesterday", Text = "Yesterday" },
          new { Value = "thisweek", Text = "This Week" },
          new { Value = "lastweek", Text = "Last Week" },
          new { Value = "last30days", Text = "Last 30 Days" },
          new { Value = "last90days", Text = "Last 90 Days" }
        }, "Value", "Text", dateFilter);

        ViewBag.CostOptions = new SelectList(new[]
        {
          new { Value = "", Text = "All Cost Levels" },
          new { Value = "highcost", Text = "High Cost ($1000+)" },
          new { Value = "mediumcost", Text = "Medium Cost ($100-$1000)" },
          new { Value = "lowcost", Text = "Low Cost (<$100)" },
          new { Value = "efficient", Text = "Efficient (<$10/unit)" },
          new { Value = "expensive", Text = "Expensive (>$100/unit)" },
          new { Value = "nocost", Text = "No Cost Data" }
        }, "Value", "Text", costFilter);

        // Search statistics
        ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(search) ||
                           !string.IsNullOrWhiteSpace(statusFilter) ||
                           !string.IsNullOrWhiteSpace(dateFilter) ||
                           !string.IsNullOrWhiteSpace(bomFilter) ||
                           !string.IsNullOrWhiteSpace(costFilter);

        if (ViewBag.IsFiltered)
        {
          var allProductionsCount = await HttpContext.RequestServices.GetRequiredService<InventoryContext>()
              .Productions.CountAsync();
          ViewBag.SearchResultsCount = totalCount;
          ViewBag.TotalProductionsCount = allProductionsCount;
        }

        return View(viewModel);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in Production Index");

        // Set essential ViewBag properties that the view expects
        ViewBag.ErrorMessage = $"Error loading productions: {ex.Message}";
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
        ViewBag.StatusFilter = statusFilter;
        ViewBag.DateFilter = dateFilter;
        ViewBag.BomFilter = bomFilter;
        ViewBag.CostFilter = costFilter;
        ViewBag.SortOrder = sortOrder;
        ViewBag.IsFiltered = false;

        // Set empty dropdown options
        ViewBag.StatusOptions = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.DateOptions = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.CostOptions = new SelectList(new List<object>(), "Value", "Text");

        return View(new ProductionIndexViewModel());
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

    // Enhanced Production Details with Workflow
    public async Task<IActionResult> Details(int id)
    {
      try
      {
        var production = await _productionService.GetProductionByIdAsync(id);
        if (production == null) return NotFound();

        // Get workflow information
        var workflowQuery = new GetProductionWorkflowQuery(id);
        var workflow = await _orchestrator.GetProductionWorkflowAsync(workflowQuery);

        // Get timeline
        var timelineQuery = new GetProductionTimelineQuery(id);
        var timeline = await _orchestrator.GetProductionTimelineAsync(timelineQuery);

        var viewModel = new ProductionDetailsViewModel
        {
          Production = production,
          Workflow = workflow,
          Timeline = timeline,
          ValidNextStatuses = workflow?.ValidNextStatuses ?? new List<ProductionStatus>()
        };

        return View(viewModel);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading production details for {ProductionId}", id);
        TempData["ErrorMessage"] = $"Error loading production details: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // Enhanced Build BOM with Workflow Integration
    public async Task<IActionResult> BuildBom(int? bomId)
    {
      try
      {
        var boms = await _bomService.GetCurrentVersionBomsAsync();
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", bomId);

        var viewModel = new BuildBomViewModel
        {
          BomId = bomId ?? 0,
          Quantity = 1,
          ProductionDate = DateTime.Today,
          CreateWithWorkflow = true // New option
        };

        if (bomId.HasValue && bomId.Value > 0)
        {
          var bom = await _bomService.GetCurrentVersionBomByIdAsync(bomId.Value);
          if (bom != null)
          {
            viewModel.BomName = bom.BomNumber;
            viewModel.BomDescription = bom.Description;
            viewModel.CanBuild = await _productionService.CanBuildBomAsync(bomId.Value, viewModel.Quantity);
            viewModel.MaterialCost = await _productionService.CalculateBomMaterialCostAsync(bomId.Value, viewModel.Quantity);
          }
        }

        return View(viewModel);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading build BOM page");
        TempData["ErrorMessage"] = $"Error loading page: {ex.Message}";
        ViewBag.BomId = new SelectList(new List<Bom>(), "Id", "BomNumber");
        return View(new BuildBomViewModel());
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuildBom(BuildBomViewModel viewModel)
    {
      if (ModelState.IsValid)
      {
        try
        {
          if (viewModel.BomId <= 0)
          {
            TempData["ErrorMessage"] = "Please select a BOM to build.";
            return await RefreshBuildBomView(viewModel);
          }

          CommandResult result;

          if (viewModel.CreateWithWorkflow)
          {
            // Use the new orchestrated workflow
            result = await _orchestrator.CreateProductionWithWorkflowAsync(
                viewModel.BomId,
                viewModel.Quantity,
                viewModel.LaborCost,
                viewModel.OverheadCost,
                viewModel.Notes,
                User.Identity?.Name);
          }
          else
          {
            // Use traditional production creation
            var production = await _productionService.BuildBomAsync(
                viewModel.BomId,
                viewModel.Quantity,
                viewModel.LaborCost,
                viewModel.OverheadCost,
                viewModel.Notes);

            result = CommandResult.SuccessResult(production);
          }

          if (result.Success)
          {
            var productionData = result.Data;
            int productionId;

            if (viewModel.CreateWithWorkflow && productionData != null)
            {
              var data = (dynamic)productionData;
              productionId = data.Production.Id;
            }
            else
            {
              productionId = ((Production)productionData!).Id;
            }

            TempData["SuccessMessage"] = $"Successfully built {viewModel.Quantity} units. Production ID: {productionId}";
            return RedirectToAction("Details", new { id = productionId });
          }
          else
          {
            TempData["ErrorMessage"] = result.ErrorMessage;
          }
        }
        catch (ArgumentException ex)
        {
          TempData["ErrorMessage"] = $"BOM Error: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
          TempData["ErrorMessage"] = $"Production Error: {ex.Message}";
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Unexpected error building BOM: {ex.Message}";
        }
      }

      return await RefreshBuildBomView(viewModel);
    }

    // Workflow Action Methods
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartProduction(int productionId, string? assignedTo = null, DateTime? estimatedCompletion = null)
    {
      try
      {
        _logger.LogInformation("User {User} attempting to start production {ProductionId}", 
            User.Identity?.Name, productionId);

        // Validate input
        if (productionId <= 0)
        {
          TempData["ErrorMessage"] = "Invalid production ID provided.";
          return RedirectToAction("Details", new { id = productionId });
        }

        var command = new StartProductionCommand(productionId, assignedTo, estimatedCompletion, User.Identity?.Name);
        var result = await _orchestrator.StartProductionAsync(command);

        if (result.Success)
        {
          TempData["SuccessMessage"] = "Production started successfully and status updated to 'In Progress'.";
          _logger.LogInformation("Production {ProductionId} started successfully by {User}", 
              productionId, User.Identity?.Name);
        }
        else
        {
          // Display the specific error message from the orchestrator
          TempData["ErrorMessage"] = result.ErrorMessage;
          _logger.LogWarning("Failed to start production {ProductionId} for user {User}: {Error}", 
              productionId, User.Identity?.Name, result.ErrorMessage);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Exception while starting production {ProductionId} for user {User}", 
            productionId, User.Identity?.Name);
        TempData["ErrorMessage"] = "An unexpected error occurred while starting production. Please try again or contact support if the problem persists.";
      }

      return RedirectToAction("Details", new { id = productionId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int productionId, ProductionStatus newStatus, string? reason = null, string? notes = null)
    {
      try
      {
        var command = new UpdateProductionStatusCommand(productionId, newStatus, reason, notes, User.Identity?.Name);
        var result = await _orchestrator.UpdateProductionStatusAsync(command);

        if (result.Success)
        {
          TempData["SuccessMessage"] = $"Status updated to {newStatus}";
        }
        else
        {
          TempData["ErrorMessage"] = result.ErrorMessage;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating status for production {ProductionId}", productionId);
        TempData["ErrorMessage"] = "Failed to update status";
      }

      return RedirectToAction("Details", new { id = productionId });
    }

    [HttpPost]
    public async Task<IActionResult> AssignProduction(int productionId, string assignedTo)
    {
      try
      {
        var command = new AssignProductionCommand(productionId, assignedTo, User.Identity?.Name);
        var result = await _orchestrator.AssignProductionAsync(command);

        if (result.Success)
        {
          TempData["SuccessMessage"] = $"Production assigned to {assignedTo}";
        }
        else
        {
          TempData["ErrorMessage"] = result.ErrorMessage;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error assigning production {ProductionId}", productionId);
        TempData["ErrorMessage"] = "Failed to assign production";
      }

      return RedirectToAction("Details", new { id = productionId });
    }

    [HttpPost]
    public async Task<IActionResult> CompleteQualityCheck(int productionId, bool passed, string? notes = null, int? qualityCheckerId = null)
    {
      try
      {
        var command = new CompleteQualityCheckCommand(productionId, passed, notes, qualityCheckerId, User.Identity?.Name);
        var result = await _orchestrator.ProcessQualityCheckAsync(command);

        if (result.Success)
        {
          TempData["SuccessMessage"] = passed ? "Quality check passed - production completed" : "Quality check failed - returned to production";
        }
        else
        {
          TempData["ErrorMessage"] = result.ErrorMessage;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error completing quality check for production {ProductionId}", productionId);
        TempData["ErrorMessage"] = "Failed to complete quality check";
      }

      return RedirectToAction("Details", new { id = productionId });
    }

    // AJAX endpoints for dynamic updates
    [HttpGet]
    public async Task<IActionResult> GetValidStatuses(int productionId)
    {
      try
      {
        var validStatuses = await _workflowEngine.GetValidNextStatusesAsync(productionId);
        return Json(new { success = true, data = validStatuses });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting valid statuses for production {ProductionId}", productionId);
        return Json(new { success = false, error = "Failed to get valid statuses" });
      }
    }

    [HttpGet]
    public async Task<IActionResult> CheckBomAvailability(int bomId, int quantity)
    {
      try
      {
        var bom = await _bomService.GetCurrentVersionBomByIdAsync(bomId);
        if (bom == null)
        {
          return Json(new
          {
            success = false,
            error = "Selected BOM is not the current version and cannot be used for production."
          });
        }

        var canBuild = await _productionService.CanBuildBomAsync(bomId, quantity);
        var materialCost = await _productionService.CalculateBomMaterialCostAsync(bomId, quantity);

        return Json(new
        {
          success = true,
          canBuild = canBuild,
          materialCost = materialCost,
          bomName = bom.BomNumber,
          bomDescription = bom.Description,
          unitCost = quantity > 0 ? materialCost / quantity : 0
        });
      }
      catch (Exception ex)
      {
        return Json(new
        {
          success = false,
          error = ex.Message
        });
      }
    }

    // Helper methods
    private async Task<IActionResult> RefreshBuildBomView(BuildBomViewModel viewModel)
    {
      try
      {
        var boms = await _bomService.GetCurrentVersionBomsAsync();
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);

        if (viewModel.BomId > 0)
        {
          var bom = await _bomService.GetCurrentVersionBomByIdAsync(viewModel.BomId);
          if (bom != null)
          {
            viewModel.BomName = bom.BomNumber;
            viewModel.BomDescription = bom.Description;
            viewModel.CanBuild = await _productionService.CanBuildBomAsync(viewModel.BomId, viewModel.Quantity);
            viewModel.MaterialCost = await _productionService.CalculateBomMaterialCostAsync(viewModel.BomId, viewModel.Quantity);
          }
        }
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error refreshing BOM data: {ex.Message}";
        ViewBag.BomId = new SelectList(new List<Bom>(), "Id", "BomNumber");
      }

      return View(viewModel);
    }

    // Existing finished goods methods remain the same...
    public async Task<IActionResult> FinishedGoods()
    {
      try
      {
        var finishedGoods = await _productionService.GetAllFinishedGoodsAsync();
        return View(finishedGoods);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading finished goods: {ex.Message}";
        return View(new List<FinishedGood>());
      }
    }

    public async Task<IActionResult> FinishedGoodDetails(int id)
    {
      try
      {
        var finishedGood = await _productionService.GetFinishedGoodByIdAsync(id);
        if (finishedGood == null) return NotFound();
        return View(finishedGood);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading finished good details: {ex.Message}";
        return RedirectToAction("FinishedGoods");
      }
    }

    [HttpGet]
    public async Task<IActionResult> MaterialShortageReport(int bomId, int quantity = 1)
    {
      try
      {
        // Return loading view immediately
        ViewBag.BomId = bomId;
        ViewBag.Quantity = quantity;
        ViewBag.IsLoading = true;

        // Get basic BOM info for loading screen
        var bom = await _bomService.GetBomByIdAsync(bomId);
        ViewBag.BomName = bom?.BomNumber ?? "Unknown BOM";

        return View("MaterialShortageReportLoading");
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading material shortage report: {ex.Message}";
        return RedirectToAction("BuildBom");
      }
    }
    [HttpGet]
    public async Task<IActionResult> GetMaterialShortageReportData(int bomId, int quantity = 1)
    {
      try
      {
        // This is the actual heavy computation
        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(bomId, quantity);

        return Json(new
        {
          success = true,
          data = new
          {
            bomId = shortageAnalysis.BomId,
            bomName = shortageAnalysis.BomName,
            bomDescription = shortageAnalysis.BomDescription,
            requestedQuantity = shortageAnalysis.RequestedQuantity,
            canBuild = shortageAnalysis.CanBuild,
            hasShortages = shortageAnalysis.HasShortages,
            totalRequiredItems = shortageAnalysis.TotalRequiredItems,
            totalShortageItems = shortageAnalysis.TotalShortageItems,
            totalShortageValue = shortageAnalysis.TotalShortageValue,
            materialShortages = shortageAnalysis.MaterialShortages.Select(s => new
            {
              itemId = s.ItemId,
              partNumber = s.PartNumber,
              description = s.Description,
              requiredQuantity = s.RequiredQuantity,
              availableQuantity = s.AvailableQuantity,
              shortageQuantity = s.ShortageQuantity,
              shortageValue = s.ShortageValue,
              suggestedPurchaseQuantity = s.SuggestedPurchaseQuantity,
              estimatedUnitCost = s.EstimatedUnitCost,
              isCriticalShortage = s.IsCriticalShortage,
              preferredVendor = s.PreferredVendor,
              bomContext = s.BomContext,
              lastPurchaseDate = s.LastPurchaseDate?.ToString("MM/dd/yyyy"),
              lastPurchaseCost = s.LastPurchaseCost
            }).ToList(),
            materialRequirements = shortageAnalysis.MaterialRequirements.Select(r => new
            {
              itemId = r.ItemId,
              partNumber = r.PartNumber,
              description = r.Description,
              requiredQuantity = r.RequiredQuantity,
              availableQuantity = r.AvailableQuantity,
              hasSufficientStock = r.HasSufficientStock,
              estimatedUnitCost = r.EstimatedUnitCost,
              totalCost = r.TotalCost,
              bomContext = r.BomContext
            }).ToList()
          }
        });
      }
      catch (Exception ex)
      {
        return Json(new
        {
          success = false,
          error = ex.Message,
          details = "Failed to generate material shortage analysis. Please try again or contact support if the problem persists."
        });
      }
    }

    [HttpGet]
    public async Task<IActionResult> MaterialShortageReportComplete(int bomId, int quantity = 1)
    {
      try
      {
        // This action returns the complete report view after data is loaded
        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(bomId, quantity);
        return View("MaterialShortageReport", shortageAnalysis);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error generating material shortage report: {ex.Message}";
        return RedirectToAction("BuildBom");
      }
    }

    // Enhanced method for real-time progress updates (optional)
    [HttpGet]
    public async Task<IActionResult> GetMaterialShortageProgress(int bomId, int quantity = 1)
    {
      try
      {
        // You can implement this to return progress updates during long operations
        // For now, we'll return a simple status
        var bom = await _bomService.GetBomByIdAsync(bomId);
        if (bom == null)
        {
          return Json(new { success = false, error = "BOM not found" });
        }

        var itemCount = bom.BomItems?.Count ?? 0;
        var subAssemblyCount = bom.SubAssemblies?.Count ?? 0;

        return Json(new
        {
          success = true,
          progress = new
          {
            totalItems = itemCount + subAssemblyCount,
            currentStep = "Analyzing BOM structure...",
            estimatedTimeRemaining = itemCount > 50 ? "30-60 seconds" : "10-30 seconds"
          }
        });
      }
      catch (Exception ex)
      {
        return Json(new { success = false, error = ex.Message });
      }
    }

    // Export shortage report to CSV
    public async Task<IActionResult> ExportShortageReport(int bomId, int quantity = 1)
    {
      try
      {
        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(bomId, quantity);

        // Create CSV content
        var csv = new StringBuilder();
        csv.AppendLine("Part Number,Description,Required,Available,Shortage,Value,Suggested Purchase");

        foreach (var shortage in shortageAnalysis.MaterialShortages)
        {
          csv.AppendLine($"{shortage.PartNumber},{shortage.Description},{shortage.RequiredQuantity},{shortage.AvailableQuantity},{shortage.ShortageQuantity},{shortage.ShortageValue:C},{shortage.SuggestedPurchaseQuantity}");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"MaterialShortageReport_BOM{bomId}_{DateTime.Now:yyyyMMdd}.csv");
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error exporting shortage report: {ex.Message}";
        return RedirectToAction("MaterialShortageReport", new { bomId, quantity });
      }
    }

    // Bulk purchase request creation
    public async Task<IActionResult> CreateBulkPurchaseRequest(int bomId, int quantity = 1)
    {
      try
      {
        // Get the shortage analysis
        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(bomId, quantity);

        // Get all active vendors for dropdowns
        var vendors = await _vendorService.GetActiveVendorsAsync();

        // Create the bulk purchase request model
        var bulkRequest = new BulkPurchaseRequest
        {
          BomId = bomId,
          Quantity = quantity,
          ExpectedDeliveryDate = DateTime.Today.AddDays(7), // Default to 1 week
          IncludeSafetyStock = true,
          SafetyStockMultiplier = 1.2m
        };

        // Convert material shortages to purchase items with enhanced vendor selection
        foreach (var shortage in shortageAnalysis.MaterialShortages)
        {
          // Get comprehensive vendor selection info for this item
          var vendorInfo = await _vendorService.GetVendorSelectionInfoForItemAsync(shortage.ItemId);

          var purchaseItem = new ShortageItemPurchase
          {
            ItemId = shortage.ItemId,
            Selected = true, // Pre-select all items
            QuantityToPurchase = shortage.SuggestedPurchaseQuantity,
            EstimatedUnitCost = shortage.EstimatedUnitCost,
            Notes = $"For BOM: {shortageAnalysis.BomName}",

            // Enhanced vendor selection with priority:
            // 1. Primary vendor (VendorItem.IsPrimary)
            // 2. Item's preferred vendor 
            // 3. Last purchase vendor
            VendorId = vendorInfo.RecommendedVendor?.Id,
            PreferredVendor = vendorInfo.RecommendedVendor?.CompanyName ?? shortage.PreferredVendor,
            LastVendorId = vendorInfo.LastPurchaseVendor?.Id,
            LastVendorName = vendorInfo.LastPurchaseVendor?.CompanyName,

            // Additional vendor context for UI display
            PrimaryVendorId = vendorInfo.PrimaryVendor?.Id,
            PrimaryVendorName = vendorInfo.PrimaryVendor?.CompanyName,
            ItemPreferredVendorName = vendorInfo.ItemPreferredVendorName,
            SelectionReason = vendorInfo.SelectionReason
          };

          // Use the recommended cost if available and valid
          if (vendorInfo.RecommendedCost.HasValue && vendorInfo.RecommendedCost.Value > 0)
          {
            purchaseItem.EstimatedUnitCost = vendorInfo.RecommendedCost.Value;
          }

          bulkRequest.ItemsToPurchase.Add(purchaseItem);
        }

        // Pass data to the view
        ViewBag.ShortageAnalysis = shortageAnalysis;
        ViewBag.Vendors = vendors; // All active vendors for dropdowns

        return View(bulkRequest);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error creating bulk purchase request: {ex.Message}";
        return RedirectToAction("MaterialShortageReport", new { bomId, quantity });
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Controllers/ProductionController.cs - Updated POST action
    public async Task<IActionResult> CreateBulkPurchaseRequest(BulkPurchaseRequest model)
    {
      // Replace the existing foreach loop with a call to the new vendor grouping method
      return await CreateVendorGroupedBulkPurchases(model);
    }


    // Create Finished Good - GET
    public async Task<IActionResult> CreateFinishedGood()
    {
      try
      {
        // Get all BOMs to allow linking finished goods to BOMs
        var boms = await _bomService.GetAllBomsAsync();
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", null);

        var viewModel = new CreateFinishedGoodViewModel
        {
          UnitCost = 0,
          SellingPrice = 0,
          CurrentStock = 0,
          MinimumStock = 1
        };

        return View(viewModel);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading create finished good page: {ex.Message}";
        return RedirectToAction("FinishedGoods");
      }
    }

    // Create Finished Good - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFinishedGood(CreateFinishedGoodViewModel viewModel)
    {
      if (ModelState.IsValid)
      {
        try
        {
          var finishedGood = new FinishedGood
          {
            PartNumber = viewModel.PartNumber,
            Description = viewModel.Description,
            BomId = viewModel.BomId,
            UnitCost = viewModel.UnitCost,
            SellingPrice = viewModel.SellingPrice,
            CurrentStock = viewModel.CurrentStock,
            MinimumStock = viewModel.MinimumStock
          };

          // ✅ NEW: Handle image upload
          if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
          {
            // Validate file size (5MB limit)
            if (viewModel.ImageFile.Length > 5 * 1024 * 1024)
            {
              ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
              var boms = await _bomService.GetAllBomsAsync();
              ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
              return View(viewModel);
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
            if (!allowedTypes.Contains(viewModel.ImageFile.ContentType.ToLower()))
            {
              ModelState.AddModelError("ImageFile", "Please upload a valid image file (JPG, PNG, GIF, BMP).");
              var boms = await _bomService.GetAllBomsAsync();
              ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
              return View(viewModel);
            }

            using var memoryStream = new MemoryStream();
            await viewModel.ImageFile.CopyToAsync(memoryStream);
            finishedGood.ImageData = memoryStream.ToArray();
            finishedGood.ImageContentType = viewModel.ImageFile.ContentType;
            finishedGood.ImageFileName = viewModel.ImageFile.FileName;
          }

          // ✅ NEW: Handle image upload
          if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
          {
            // Validate file size (5MB limit)
            if (viewModel.ImageFile.Length > 5 * 1024 * 1024)
            {
              ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
              var boms = await _bomService.GetAllBomsAsync();
              ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
              return View(viewModel);
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
            if (!allowedTypes.Contains(viewModel.ImageFile.ContentType.ToLower()))
            {
              ModelState.AddModelError("ImageFile", "Please upload a valid image file (JPG, PNG, GIF, BMP).");
              var boms = await _bomService.GetAllBomsAsync();
              ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
              return View(viewModel);
            }

            using var memoryStream = new MemoryStream();
            await viewModel.ImageFile.CopyToAsync(memoryStream);
            finishedGood.ImageData = memoryStream.ToArray();
            finishedGood.ImageContentType = viewModel.ImageFile.ContentType;
            finishedGood.ImageFileName = viewModel.ImageFile.FileName;
          }

          // ✅ NEW: Handle image upload
          if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
          {
            // Validate file size (5MB limit)
            if (viewModel.ImageFile.Length > 5 * 1024 * 1024)
            {
              ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
              var boms = await _bomService.GetAllBomsAsync();
              ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
              return View(viewModel);
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
            if (!allowedTypes.Contains(viewModel.ImageFile.ContentType.ToLower()))
            {
              ModelState.AddModelError("ImageFile", "Please upload a valid image file (JPG, PNG, GIF, BMP).");
              var boms = await _bomService.GetAllBomsAsync();
              ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
              return View(viewModel);
            }

            using var memoryStream = new MemoryStream();
            await viewModel.ImageFile.CopyToAsync(memoryStream);
            finishedGood.ImageData = memoryStream.ToArray();
            finishedGood.ImageContentType = viewModel.ImageFile.ContentType;
            finishedGood.ImageFileName = viewModel.ImageFile.FileName;
          }

          await _productionService.CreateFinishedGoodAsync(finishedGood);
          TempData["SuccessMessage"] = $"Finished good '{finishedGood.PartNumber}' created successfully!";
          return RedirectToAction("FinishedGoods");
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Error creating finished good: {ex.Message}";
        }
      }

      // Reload dropdown data on validation error
      try
      {
        var boms = await _bomService.GetAllBomsAsync();
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error reloading BOM dropdown");
        ViewBag.BomId = new SelectList(new List<Bom>(), "Id", "BomNumber");
      }

      return View(viewModel);
    }

    // Edit Finished Good - GET
    public async Task<IActionResult> EditFinishedGood(int id)
    {
      try
      {
        var finishedGood = await _productionService.GetFinishedGoodByIdAsync(id);
        if (finishedGood == null) return NotFound();

        var boms = await _bomService.GetAllBomsAsync();
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", finishedGood.BomId);

        var viewModel = new CreateFinishedGoodViewModel
        {
          Id = finishedGood.Id,
          PartNumber = finishedGood.PartNumber,
          Description = finishedGood.Description,
          BomId = finishedGood.BomId,
          UnitCost = finishedGood.UnitCost,
          SellingPrice = finishedGood.SellingPrice,
          CurrentStock = finishedGood.CurrentStock,
          MinimumStock = finishedGood.MinimumStock,
          // ✅ CRITICAL FIX: Map the requirements properties
          RequiresSerialNumber = finishedGood.RequiresSerialNumber,
          RequiresModelNumber = finishedGood.RequiresModelNumber,
          // ✅ CRITICAL FIX: Map the image properties
          HasImage = finishedGood.HasImage,
          ImageFileName = finishedGood.ImageFileName
        };

        return View("CreateFinishedGood", viewModel);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading finished good for edit: {FinishedGoodId}", id);
        TempData["ErrorMessage"] = $"Error loading finished good: {ex.Message}";
        return RedirectToAction("FinishedGoods");
      }
    }

		// Edit Finished Good - POST
		// Edit Finished Good - POST
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditFinishedGood(CreateFinishedGoodViewModel viewModel)
		{
			if (ModelState.IsValid)
			{
				try
				{
					var finishedGood = await _productionService.GetFinishedGoodByIdAsync(viewModel.Id);
					if (finishedGood == null) return NotFound();

					finishedGood.PartNumber = viewModel.PartNumber;
					finishedGood.Description = viewModel.Description;
					finishedGood.BomId = viewModel.BomId;
					finishedGood.UnitCost = viewModel.UnitCost;
					finishedGood.SellingPrice = viewModel.SellingPrice;
					finishedGood.CurrentStock = viewModel.CurrentStock;
					finishedGood.MinimumStock = viewModel.MinimumStock;
					finishedGood.RequiresSerialNumber = viewModel.RequiresSerialNumber;
					finishedGood.RequiresModelNumber = viewModel.RequiresModelNumber;
					finishedGood.LastModified = DateTime.Now;
					finishedGood.ModifiedBy = User.Identity?.Name ?? "System";

					// ✅ Handle image upload (new image replaces existing)
					if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
					{
						// Validate file size (5MB limit)
						if (viewModel.ImageFile.Length > 5 * 1024 * 1024)
						{
							ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
							var boms = await _bomService.GetAllBomsAsync();
							ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
							return View("CreateFinishedGood", viewModel);
						}

						// Validate file type
						var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp" };
						if (!allowedTypes.Contains(viewModel.ImageFile.ContentType.ToLower()))
						{
							ModelState.AddModelError("ImageFile", "Please upload a valid image file (JPG, PNG, GIF, BMP).");
							var boms = await _bomService.GetAllBomsAsync();
							ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
							return View("CreateFinishedGood", viewModel);
						}

						using var memoryStream = new MemoryStream();
						await viewModel.ImageFile.CopyToAsync(memoryStream);
						finishedGood.ImageData = memoryStream.ToArray();
						finishedGood.ImageContentType = viewModel.ImageFile.ContentType;
						finishedGood.ImageFileName = viewModel.ImageFile.FileName;
					}

					await _productionService.UpdateFinishedGoodAsync(finishedGood);

					TempData["SuccessMessage"] = $"Finished good '{finishedGood.PartNumber}' updated successfully! " +
							$"Requirements: {(finishedGood.RequiresSerialNumber ? "Serial Number " : "")}" +
							$"{(finishedGood.RequiresModelNumber ? "Model Number" : "")}";

					return RedirectToAction("FinishedGoods");
				}
				catch (Exception ex)
				{
					TempData["ErrorMessage"] = $"Error updating finished good: {ex.Message}";
				}
			}

			// Reload dropdown data on validation error
			try
			{
				var boms = await _bomService.GetAllBomsAsync();
				ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", viewModel.BomId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reloading BOM dropdown");
				ViewBag.BomId = new SelectList(new List<Bom>(), "Id", "BomNumber");
			}

			return View("CreateFinishedGood", viewModel);
		}

		// Delete Finished Good
		[HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFinishedGood(int id)
    {
      try
      {
        var finishedGood = await _productionService.GetFinishedGoodByIdAsync(id);
        if (finishedGood == null) return NotFound();

        await _productionService.DeleteFinishedGoodAsync(id);
        TempData["SuccessMessage"] = $"Finished good '{finishedGood.PartNumber}' deleted successfully!";
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error deleting finished good: {ex.Message}";
      }

      return RedirectToAction("FinishedGoods");
    }

    // AJAX method to get BOM details when selected
    [HttpGet]
    public async Task<IActionResult> GetBomDetails(int bomId)
    {
      try
      {
        var bom = await _bomService.GetBomByIdAsync(bomId);
        if (bom == null)
        {
          return Json(new { success = false, error = "BOM not found" });
        }

        var bomCost = await _productionService.CalculateBomMaterialCostAsync(bomId, 1);

        return Json(new
        {
          success = true,
          bomNumber = bom.BomNumber,
          description = bom.Description,
          suggestedUnitCost = bomCost,
          suggestedSellingPrice = bomCost * 6m, // markup suggestion
          partNumber = $"FG-{bom.BomNumber}" // Suggested part number
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting BOM details for {BomId}", bomId);
        return Json(new { success = false, error = "Error loading BOM details" });
      }
    }

    // AJAX endpoint to check if production can be started (for UI feedback)
    [HttpGet]
    public async Task<IActionResult> CheckProductionReadiness(int productionId)
    {
      try
      {
        var readinessCheck = await _orchestrator.CanStartProductionWithDetailsAsync(productionId);
        
        return Json(new { 
          success = true, 
          canStart = readinessCheck.CanStart,
          reason = readinessCheck.Reason,
          message = readinessCheck.CanStart ? "Production is ready to start" : readinessCheck.Reason
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error checking production readiness for {ProductionId}", productionId);
        return Json(new { 
          success = false, 
          error = "Unable to check production readiness", 
          details = ex.Message 
        });
      }
    }

    // Add this method to your ProductionController.cs class
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVendorGroupedBulkPurchases(BulkPurchaseRequest model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
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

            // Group items by vendor for consolidated purchase orders
            var vendorGroups = selectedItems.GroupBy(i => i.VendorId.Value).ToList();
            var createdPurchaseOrders = new List<string>();
            var totalPurchasesCreated = 0;

            foreach (var vendorGroup in vendorGroups)
            {
                var vendorId = vendorGroup.Key;
                var vendor = await _vendorService.GetVendorByIdAsync(vendorId);
                
                if (vendor == null)
                {
                    _logger.LogWarning("Vendor not found for ID {VendorId}", vendorId);
                    continue;
                }

                // Calculate totals for this vendor group
                var vendorItems = vendorGroup.ToList();
                var totalItemValue = vendorItems.Sum(i => i.QuantityToPurchase * i.EstimatedUnitCost);
                
                // Generate unique PO number for this vendor
                var purchaseOrderNumber = !string.IsNullOrEmpty(model.PurchaseOrderNumber) 
                    ? $"{model.PurchaseOrderNumber}-{vendor.CompanyName.Replace(" ", "").Substring(0, Math.Min(3, vendor.CompanyName.Length)).ToUpper()}"
                    : await _purchaseService.GeneratePurchaseOrderNumberAsync();

                // Calculate vendor-specific shipping and tax
                var shippingCost = CalculateShippingCost(totalItemValue, vendor);
                var taxAmount = CalculateTaxAmount(totalItemValue, vendor);

                _logger.LogInformation("Processing vendor group - Vendor: {VendorName}, Items: {ItemCount}, Total Value: {TotalValue:C}, Shipping: {Shipping:C}, Tax: {Tax:C}",
                    vendor.CompanyName, vendorItems.Count, totalItemValue, shippingCost, taxAmount);

                // Create individual Purchase records for each item, with proportional costs
                foreach (var item in vendorItems)
                {
                    var itemValue = item.QuantityToPurchase * item.EstimatedUnitCost;
                    var proportionOfTotal = totalItemValue > 0 ? itemValue / totalItemValue : 0;
                    
                    // Calculate proportional shipping and tax for this item
                    var itemShippingCost = Math.Round(shippingCost * proportionOfTotal, 2);
                    var itemTaxAmount = Math.Round(taxAmount * proportionOfTotal, 2);

                    var purchase = new Purchase
                    {
                        ItemId = item.ItemId,
                        QuantityPurchased = item.QuantityToPurchase,
                        CostPerUnit = item.EstimatedUnitCost,
                        VendorId = vendorId,
                        PurchaseOrderNumber = purchaseOrderNumber,
                        Notes = BuildPurchaseNotes(model.Notes, item.Notes, vendorItems.Count, vendor.CompanyName),
                        PurchaseDate = DateTime.Now,
                        RemainingQuantity = item.QuantityToPurchase,
                        CreatedDate = DateTime.Now,
                        
                        // Proportional shipping and tax allocation
                        ShippingCost = itemShippingCost,
                        TaxAmount = itemTaxAmount,
                        
                        Status = PurchaseStatus.Pending,
                        ExpectedDeliveryDate = model.ExpectedDeliveryDate
                    };

                    await _purchaseService.CreatePurchaseAsync(purchase);
                    totalPurchasesCreated++;

                    _logger.LogDebug("Created purchase for item {ItemId} - Qty: {Quantity}, Unit Cost: {UnitCost:C}, Shipping: {Shipping:C}, Tax: {Tax:C}",
                        item.ItemId, item.QuantityToPurchase, item.EstimatedUnitCost, itemShippingCost, itemTaxAmount);
                }

                createdPurchaseOrders.Add($"{vendor.CompanyName}: {purchaseOrderNumber} ({vendorItems.Count} items, {totalItemValue:C})");
            }

            var successMessage = $"Successfully created {vendorGroups.Count} consolidated purchase orders with {totalPurchasesCreated} line items:\n" +
                           string.Join("\n", createdPurchaseOrders);
            
            TempData["SuccessMessage"] = successMessage;
            _logger.LogInformation("Bulk purchase completed - {VendorCount} vendors, {PurchaseCount} purchases created", 
                vendorGroups.Count, totalPurchasesCreated);

            return RedirectToAction("Index", "Purchases");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vendor-grouped bulk purchases");
            TempData["ErrorMessage"] = $"Error creating vendor-grouped bulk purchases: {ex.Message}";
            await ReloadBulkPurchaseViewData(model);
            return View("CreateBulkPurchaseRequest", model);
        }
    }

    // Helper method to calculate shipping costs based on order value and vendor
    private decimal CalculateShippingCost(decimal orderValue, Vendor vendor)
    {
        // Business rules for shipping calculation - customize as needed
        
        // Free shipping threshold
        if (orderValue >= 500m) return 0m;
        
        // Flat rate for small orders
        if (orderValue < 100m) return 25m;
        
        // Percentage-based shipping for medium orders
        if (orderValue < 300m) return Math.Round(orderValue * 0.05m, 2); // 5%
        
        // Reduced rate for larger orders
        return Math.Round(orderValue * 0.03m, 2); // 3%
    }

    // Helper method to calculate tax based on order value and vendor location
    private decimal CalculateTaxAmount(decimal orderValue, Vendor vendor)
    {
        // Get tax rate for vendor - customize based on your tax rules
        decimal taxRate = GetTaxRateForVendor(vendor);
        return Math.Round(orderValue * taxRate, 2);
    }

    // Helper method to get tax rate for vendor
    private decimal GetTaxRateForVendor(Vendor vendor)
    {
        // Example tax rate logic - customize based on your business needs
        // You might want to:
        // 1. Store tax rate in Vendor entity
        // 2. Use a tax calculation service
        // 3. Look up rates by state/province
        // 4. Use different rates for different vendor types
        
        // For now, return a default rate (8.75%)
        return 0.0875m;
    }

    // Helper method to build purchase notes
    private string BuildPurchaseNotes(string? modelNotes, string? itemNotes, int vendorItemCount, string vendorName)
    {
        var notes = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(modelNotes))
            notes.Add(modelNotes);
        
        notes.Add($"Vendor Group PO ({vendorItemCount} items from {vendorName})");
        
        if (!string.IsNullOrWhiteSpace(itemNotes))
            notes.Add(itemNotes);
        
        return string.Join(" | ", notes);
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

    // Image handling actions for Finished Goods
public async Task<IActionResult> GetFinishedGoodImage(int id)
    {
        var finishedGood = await _productionService.GetFinishedGoodByIdAsync(id);
        if (finishedGood == null || !finishedGood.HasImage) return NotFound();

        return File(finishedGood.ImageData!, finishedGood.ImageContentType!, finishedGood.ImageFileName);
    }

    public async Task<IActionResult> GetFinishedGoodImageThumbnail(int id, int size = 150)
    {
        var finishedGood = await _productionService.GetFinishedGoodByIdAsync(id);
        if (finishedGood == null || !finishedGood.HasImage) return NotFound();

        // For simplicity, return the original image
        return File(finishedGood.ImageData!, finishedGood.ImageContentType!, finishedGood.ImageFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFinishedGoodImage(int id)
    {
        var finishedGood = await _productionService.GetFinishedGoodByIdAsync(id);
        if (finishedGood == null) return NotFound();

        finishedGood.ImageData = null;
        finishedGood.ImageContentType = null;
        finishedGood.ImageFileName = null;

        await _productionService.UpdateFinishedGoodAsync(finishedGood);
        TempData["SuccessMessage"] = "Image removed successfully!";

        return RedirectToAction("FinishedGoodDetails", new { id });
    }
  }
}