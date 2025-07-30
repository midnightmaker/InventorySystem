// Controllers/ProductionController.cs - FIXED VERSION
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Services;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace InventorySystem.Controllers
{
  public class ProductionController : Controller
  {
    private readonly IProductionService _productionService;
    private readonly IBomService _bomService;
    private readonly IInventoryService _inventoryService;
    private readonly IPurchaseService _purchaseService;

    public ProductionController(
        IProductionService productionService,
        IBomService bomService,
        IInventoryService inventoryService,
        IPurchaseService purchaseService)
    {
      _productionService = productionService;
      _bomService = bomService;
      _inventoryService = inventoryService;
      _purchaseService = purchaseService;
    }

    // Production Index
    public async Task<IActionResult> Index()
    {
      try
      {
        var productions = await _productionService.GetAllProductionsAsync();
        return View(productions);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading productions: {ex.Message}";
        return View(new List<Production>());
      }
    }

    // Production Details
    public async Task<IActionResult> Details(int id)
    {
      try
      {
        var production = await _productionService.GetProductionByIdAsync(id);
        if (production == null) return NotFound();
        return View(production);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading production details: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // Build BOM - GET
    public async Task<IActionResult> BuildBom(int? bomId)
    {
      try
      {
        // FIXED: Use current version BOMs and correct property name
        var boms = await _bomService.GetCurrentVersionBomsAsync();

        // FIXED: Use BomNumber instead of Name (which doesn't exist on Bom model)
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", bomId);

        var viewModel = new BuildBomViewModel
        {
          BomId = bomId ?? 0,
          Quantity = 1,
          ProductionDate = DateTime.Now,
          LaborCost = 0,
          OverheadCost = 0
        };

        if (bomId.HasValue)
        {
          // FIXED: Use current version method
          var bom = await _bomService.GetCurrentVersionBomByIdAsync(bomId.Value);
          if (bom != null)
          {
            viewModel.BomName = bom.BomNumber;
            viewModel.BomDescription = bom.Description;
            viewModel.CanBuild = await _productionService.CanBuildBomAsync(bomId.Value, 1);
            viewModel.MaterialCost = await _productionService.CalculateBomMaterialCostAsync(bomId.Value, 1);
          }
          else
          {
            TempData["ErrorMessage"] = "Selected BOM is not available for production. It may not be the current version.";
            viewModel.BomId = 0; // Reset selection
          }
        }

        return View(viewModel);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading Build BOM page: {ex.Message}";
        return View(new BuildBomViewModel
        {
          BomId = 0,
          Quantity = 1,
          ProductionDate = DateTime.Now,
          LaborCost = 0,
          OverheadCost = 0
        });
      }
    }

    // Build BOM - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuildBom(BuildBomViewModel viewModel)
    {
      if (ModelState.IsValid)
      {
        try
        {
          // FIXED: Validate BOM exists and is current version
          var currentBom = await _bomService.GetCurrentVersionBomByIdAsync(viewModel.BomId);
          if (currentBom == null)
          {
            TempData["ErrorMessage"] = "Selected BOM is not available for production. Please select a current version BOM.";
            return await RefreshBuildBomView(viewModel);
          }

          // Check material availability
          if (!await _productionService.CanBuildBomAsync(viewModel.BomId, viewModel.Quantity))
          {
            TempData["ErrorMessage"] = "Insufficient materials to build the specified quantity.";
            return await RefreshBuildBomView(viewModel);
          }

          // Build the BOM
          var production = await _productionService.BuildBomAsync(
              viewModel.BomId,
              viewModel.Quantity,
              viewModel.LaborCost,
              viewModel.OverheadCost,
              viewModel.Notes);

          TempData["SuccessMessage"] = $"Successfully built {viewModel.Quantity} units. Production ID: {production.Id}";
          return RedirectToAction("Details", new { id = production.Id });
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

    // Check BOM Availability (AJAX)
    [HttpGet]
    public async Task<IActionResult> CheckBomAvailability(int bomId, int quantity)
    {
      try
      {
        // FIXED: Check if BOM is current version first
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

    // FIXED: Helper method to refresh view data safely
    private async Task<IActionResult> RefreshBuildBomView(BuildBomViewModel viewModel)
    {
      try
      {
        // FIXED: Use current version BOMs and correct property name
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
        // Set empty dropdown if there's an error
        ViewBag.BomId = new SelectList(new List<Bom>(), "Id", "BomNumber");
      }

      return View(viewModel);
    }

    // Finished Goods Index
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

    // Finished Good Details
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

    // Create Finished Good - GET
    public async Task<IActionResult> CreateFinishedGood()
    {
      try
      {
        // FIXED: Use current version BOMs and correct property name
        var boms = await _bomService.GetCurrentVersionBomsAsync();
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber");
        return View(new FinishedGood());
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error loading create finished good page: {ex.Message}";
        ViewBag.BomId = new SelectList(new List<Bom>(), "Id", "BomNumber");
        return View(new FinishedGood());
      }
    }

    // Create Finished Good - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFinishedGood(FinishedGood finishedGood)
    {
      if (ModelState.IsValid)
      {
        try
        {
          await _productionService.CreateFinishedGoodAsync(finishedGood);
          TempData["SuccessMessage"] = "Finished good created successfully!";
          return RedirectToAction("FinishedGoods");
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Error creating finished good: {ex.Message}";
        }
      }

      // FIXED: Reload dropdown with correct property name
      try
      {
        var boms = await _bomService.GetCurrentVersionBomsAsync();
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", finishedGood.BomId);
      }
      catch (Exception)
      {
        ViewBag.BomId = new SelectList(new List<Bom>(), "Id", "BomNumber");
      }

      return View(finishedGood);
    }

    // Edit Finished Good - GET
    public async Task<IActionResult> EditFinishedGood(int id)
    {
      try
      {
        var finishedGood = await _productionService.GetFinishedGoodByIdAsync(id);
        if (finishedGood == null) return NotFound();

        // FIXED: Use current version BOMs and correct property name
        var boms = await _bomService.GetCurrentVersionBomsAsync();
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", finishedGood.BomId);
        return View(finishedGood);
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
    public async Task<IActionResult> EditFinishedGood(FinishedGood finishedGood)
    {
      if (ModelState.IsValid)
      {
        try
        {
          await _productionService.UpdateFinishedGoodAsync(finishedGood);
          TempData["SuccessMessage"] = "Finished good updated successfully!";
          return RedirectToAction("FinishedGoodDetails", new { id = finishedGood.Id });
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Error updating finished good: {ex.Message}";
        }
      }

      // FIXED: Reload dropdown with correct property name
      try
      {
        var boms = await _bomService.GetCurrentVersionBomsAsync();
        ViewBag.BomId = new SelectList(boms, "Id", "BomNumber", finishedGood.BomId);
      }
      catch (Exception)
      {
        ViewBag.BomId = new SelectList(new List<Bom>(), "Id", "BomNumber");
      }

      return View(finishedGood);
    }

    // Material Shortage Report - GET
    public async Task<IActionResult> MaterialShortageReport(int bomId, int quantity = 1)
    {
      try
      {
        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(bomId, quantity);
        return View(shortageAnalysis);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error generating shortage report: {ex.Message}";
        return RedirectToAction("BuildBom", new { bomId });
      }
    }

    // AJAX endpoint to get shortage data
    [HttpGet]
    public async Task<IActionResult> GetMaterialShortageData(int bomId, int quantity)
    {
      try
      {
        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(bomId, quantity);

        return Json(new
        {
          success = true,
          canBuild = shortageAnalysis.CanBuild,
          hasShortages = shortageAnalysis.HasShortages,
          totalShortageItems = shortageAnalysis.TotalShortageItems,
          shortageValue = shortageAnalysis.ShortageValue,
          shortages = shortageAnalysis.MaterialShortages.Select(s => new
          {
            itemId = s.ItemId,
            partNumber = s.PartNumber,
            description = s.Description,
            requiredQuantity = s.RequiredQuantity,
            availableQuantity = s.AvailableQuantity,
            shortageQuantity = s.ShortageQuantity,
            shortageValue = s.ShortageValue,
            suggestedPurchaseQuantity = s.SuggestedPurchaseQuantity,
            bomContext = s.BomContext,
            isCritical = s.IsCriticalShortage
          })
        });
      }
      catch (Exception ex)
      {
        return Json(new { success = false, error = ex.Message });
      }
    }
  }
}