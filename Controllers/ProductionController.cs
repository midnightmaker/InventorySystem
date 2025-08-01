// Controllers/ProductionController.cs - Enhanced Version
using InventorySystem.Domain.Commands;
using InventorySystem.Domain.Enums;
using InventorySystem.Domain.Queries;
using InventorySystem.Domain.Services;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
using InventorySystem.ViewModels;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // Create the bulk purchase request model
        var bulkRequest = new BulkPurchaseRequest
        {
          BomId = bomId,
          Quantity = quantity,
          ExpectedDeliveryDate = DateTime.Today.AddDays(7), // Default to 1 week
          IncludeSafetyStock = true,
          SafetyStockMultiplier = 1.2m
        };

        // Convert material shortages to purchase items
        foreach (var shortage in shortageAnalysis.MaterialShortages)
        {
          var purchaseItem = new ShortageItemPurchase
          {
            ItemId = shortage.ItemId,
            Selected = true, // Pre-select all items
            QuantityToPurchase = shortage.SuggestedPurchaseQuantity,
            EstimatedUnitCost = shortage.EstimatedUnitCost,
            PreferredVendor = shortage.PreferredVendor,
            Notes = $"For BOM: {shortageAnalysis.BomName}"
          };

          bulkRequest.ItemsToPurchase.Add(purchaseItem);
        }

        // Pass the shortage analysis to the view via ViewBag
        ViewBag.ShortageAnalysis = shortageAnalysis;

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
    [HttpPost]
    public async Task<IActionResult> CreateBulkPurchaseRequest(BulkPurchaseRequest model)
    {
      try
      {
        if (!ModelState.IsValid)
        {
          var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(model.BomId, model.Quantity);
          ViewBag.ShortageAnalysis = shortageAnalysis;
          return View(model);
        }

        var selectedItems = model.ItemsToPurchase.Where(i => i.Selected).ToList();

        if (!selectedItems.Any())
        {
          TempData["ErrorMessage"] = "Please select at least one item to purchase.";
          var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(model.BomId, model.Quantity);
          ViewBag.ShortageAnalysis = shortageAnalysis;
          return View(model);
        }

        var createdPurchases = new List<int>();

        foreach (var item in selectedItems)
        {
          // Find or create vendor - FIXED to work with VendorId
          Vendor vendor = null;

          if (!string.IsNullOrWhiteSpace(item.PreferredVendor))
          {
            // Try to find existing vendor by name
            vendor = await _vendorService.GetVendorByNameAsync(item.PreferredVendor);

            if (vendor == null)
            {
              // Create new vendor if it doesn't exist
              vendor = new Vendor
              {
                CompanyName = item.PreferredVendor,
                IsActive = true,
                CreatedDate = DateTime.Now,
                LastUpdated = DateTime.Now,
                QualityRating = 3,
                DeliveryRating = 3,
                ServiceRating = 3,
                PaymentTerms = "Net 30"
              };

              vendor = await _vendorService.CreateVendorAsync(vendor);
            }
          }

          // If still no vendor, create a "TBD" vendor
          if (vendor == null)
          {
            vendor = await _vendorService.GetVendorByNameAsync("TBD");

            if (vendor == null)
            {
              vendor = new Vendor
              {
                CompanyName = "TBD",
                IsActive = true,
                CreatedDate = DateTime.Now,
                LastUpdated = DateTime.Now,
                QualityRating = 3,
                DeliveryRating = 3,
                ServiceRating = 3,
                PaymentTerms = "Net 30"
              };

              vendor = await _vendorService.CreateVendorAsync(vendor);
            }
          }

          var purchase = new Purchase
          {
            ItemId = item.ItemId,
            QuantityPurchased = item.QuantityToPurchase,
            CostPerUnit = item.EstimatedUnitCost,
            VendorId = vendor.Id, // Use VendorId instead of Vendor string
            PurchaseOrderNumber = model.PurchaseOrderNumber ?? $"PO-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
            Notes = $"{model.Notes} | {item.Notes}".Trim(' ', '|'),
            PurchaseDate = DateTime.Today,
            RemainingQuantity = item.QuantityToPurchase,
            CreatedDate = DateTime.Now,
            ShippingCost = 0,
            TaxAmount = 0,
            Status = PurchaseStatus.Pending,
            ExpectedDeliveryDate = model.ExpectedDeliveryDate
          };

          var createdPurchase = await _purchaseService.CreatePurchaseAsync(purchase);
          createdPurchases.Add(createdPurchase.Id);
        }

        TempData["SuccessMessage"] = $"Successfully created {createdPurchases.Count} purchase orders.";
        return RedirectToAction("Index", "Purchases");
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error creating purchase orders: {ex.Message}";
        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(model.BomId, model.Quantity);
        ViewBag.ShortageAnalysis = shortageAnalysis;
        return View(model);
      }
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
          MinimumStock = finishedGood.MinimumStock
        };

        return View("CreateFinishedGood", viewModel);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading finished good for editing: {ex.Message}";
        return RedirectToAction("FinishedGoods");
      }
    }

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

          await _productionService.UpdateFinishedGoodAsync(finishedGood);
          TempData["SuccessMessage"] = $"Finished good '{finishedGood.PartNumber}' updated successfully!";
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
  }
}