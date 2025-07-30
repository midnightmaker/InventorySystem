// Controllers/ProductionController.cs - Enhanced Version
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Services;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using InventorySystem.Domain.Services;
using InventorySystem.Domain.Queries;
using InventorySystem.Domain.Commands;
using InventorySystem.Domain.Enums;

namespace InventorySystem.Controllers
{
  public class ProductionController : Controller
  {
    private readonly IProductionService _productionService;
    private readonly IBomService _bomService;
    private readonly IInventoryService _inventoryService;
    private readonly IPurchaseService _purchaseService;
    private readonly IProductionOrchestrator _orchestrator;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ILogger<ProductionController> _logger;

    public ProductionController(
        IProductionService productionService,
        IBomService bomService,
        IInventoryService inventoryService,
        IPurchaseService purchaseService,
        IProductionOrchestrator orchestrator,
        IWorkflowEngine workflowEngine,
        ILogger<ProductionController> logger)
    {
      _productionService = productionService;
      _bomService = bomService;
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
      _orchestrator = orchestrator;
      _workflowEngine = workflowEngine;
      _logger = logger;
    }

    // Production Index with Workflow Status
    public async Task<IActionResult> Index()
    {
      try
      {
        var query = new GetActiveProductionsQuery();
        var activeProductions = await _orchestrator.GetActiveProductionsAsync(query);

        // Get traditional production list for comparison
        var allProductions = await _productionService.GetAllProductionsAsync();

        var viewModel = new ProductionIndexViewModel
        {
          ActiveProductions = activeProductions,
          AllProductions = allProductions.ToList(),
          ShowWorkflowView = true
        };

        return View(viewModel);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading productions");
        TempData["ErrorMessage"] = $"Error loading productions: {ex.Message}";
        return View(new ProductionIndexViewModel());
      }
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
    public async Task<IActionResult> StartProduction(int productionId, string? assignedTo = null, DateTime? estimatedCompletion = null)
    {
      try
      {
        var command = new StartProductionCommand(productionId, assignedTo, estimatedCompletion, User.Identity?.Name);
        var result = await _orchestrator.StartProductionAsync(command);

        if (result.Success)
        {
          TempData["SuccessMessage"] = "Production started successfully";
        }
        else
        {
          TempData["ErrorMessage"] = result.ErrorMessage;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error starting production {ProductionId}", productionId);
        TempData["ErrorMessage"] = "Failed to start production";
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

    
  }

  
}