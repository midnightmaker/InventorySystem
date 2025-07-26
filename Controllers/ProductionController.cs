// Controllers/ProductionController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using InventorySystem.Services;
using InventorySystem.Models;
using InventorySystem.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Controllers
{
  public class ProductionController : Controller
  {
    private readonly IProductionService _productionService;
    private readonly IBomService _bomService;
    private readonly IInventoryService _inventoryService;

    public ProductionController(
        IProductionService productionService,
        IBomService bomService,
        IInventoryService inventoryService)
    {
      _productionService = productionService;
      _bomService = bomService;
      _inventoryService = inventoryService;
    }

    // Production Index
    public async Task<IActionResult> Index()
    {
      var productions = await _productionService.GetAllProductionsAsync();
      return View(productions);
    }

    // Production Details
    public async Task<IActionResult> Details(int id)
    {
      var production = await _productionService.GetProductionByIdAsync(id);
      if (production == null) return NotFound();
      return View(production);
    }

    // Build BOM - GET
    public async Task<IActionResult> BuildBom(int? bomId)
    {
      var boms = await _bomService.GetAllBomsAsync();
      ViewBag.BomId = new SelectList(boms, "Id", "Name", bomId);

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
        var bom = await _bomService.GetBomByIdAsync(bomId.Value);
        if (bom != null)
        {
          viewModel.BomName = bom.Name;
          viewModel.BomDescription = bom.Description;
          viewModel.CanBuild = await _productionService.CanBuildBomAsync(bomId.Value, 1);
          viewModel.MaterialCost = await _productionService.CalculateBomMaterialCostAsync(bomId.Value, 1);
        }
      }

      return View(viewModel);
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
          if (!await _productionService.CanBuildBomAsync(viewModel.BomId, viewModel.Quantity))
          {
            TempData["ErrorMessage"] = "Insufficient materials to build the specified quantity.";
          }
          else
          {
            var production = await _productionService.BuildBomAsync(
                viewModel.BomId,
                viewModel.Quantity,
                viewModel.LaborCost,
                viewModel.OverheadCost,
                viewModel.Notes);

            TempData["SuccessMessage"] = $"Successfully built {viewModel.Quantity} units. Production ID: {production.Id}";
            return RedirectToAction("Details", new { id = production.Id });
          }
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Error building BOM: {ex.Message}";
        }
      }

      // Reload data for view
      var boms = await _bomService.GetAllBomsAsync();
      ViewBag.BomId = new SelectList(boms, "Id", "Name", viewModel.BomId);

      if (viewModel.BomId > 0)
      {
        var bom = await _bomService.GetBomByIdAsync(viewModel.BomId);
        if (bom != null)
        {
          viewModel.BomName = bom.Name;
          viewModel.BomDescription = bom.Description;
          viewModel.CanBuild = await _productionService.CanBuildBomAsync(viewModel.BomId, viewModel.Quantity);
          viewModel.MaterialCost = await _productionService.CalculateBomMaterialCostAsync(viewModel.BomId, viewModel.Quantity);
        }
      }

      return View(viewModel);
    }

    // Check BOM Availability (AJAX)
    [HttpGet]
    public async Task<IActionResult> CheckBomAvailability(int bomId, int quantity)
    {
      try
      {
        var canBuild = await _productionService.CanBuildBomAsync(bomId, quantity);
        var materialCost = await _productionService.CalculateBomMaterialCostAsync(bomId, quantity);

        var bom = await _bomService.GetBomByIdAsync(bomId);

        return Json(new
        {
          success = true,
          canBuild = canBuild,
          materialCost = materialCost,
          bomName = bom?.Name ?? "",
          bomDescription = bom?.Description ?? "",
          unitCost = quantity > 0 ? materialCost / quantity : 0
        });
      }
      catch (Exception ex)
      {
        return Json(new { success = false, error = ex.Message });
      }
    }

    // Finished Goods Index
    public async Task<IActionResult> FinishedGoods()
    {
      var finishedGoods = await _productionService.GetAllFinishedGoodsAsync();
      return View(finishedGoods);
    }

    // Finished Good Details
    public async Task<IActionResult> FinishedGoodDetails(int id)
    {
      var finishedGood = await _productionService.GetFinishedGoodByIdAsync(id);
      if (finishedGood == null) return NotFound();
      return View(finishedGood);
    }

    // Create Finished Good - GET
    public async Task<IActionResult> CreateFinishedGood()
    {
      var boms = await _bomService.GetAllBomsAsync();
      ViewBag.BomId = new SelectList(boms, "Id", "Name");
      return View(new FinishedGood());
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

      var boms = await _bomService.GetAllBomsAsync();
      ViewBag.BomId = new SelectList(boms, "Id", "Name", finishedGood.BomId);
      return View(finishedGood);
    }

    // Edit Finished Good - GET
    public async Task<IActionResult> EditFinishedGood(int id)
    {
      var finishedGood = await _productionService.GetFinishedGoodByIdAsync(id);
      if (finishedGood == null) return NotFound();

      var boms = await _bomService.GetAllBomsAsync();
      ViewBag.BomId = new SelectList(boms, "Id", "Name", finishedGood.BomId);
      return View(finishedGood);
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

      var boms = await _bomService.GetAllBomsAsync();
      ViewBag.BomId = new SelectList(boms, "Id", "Name", finishedGood.BomId);
      return View(finishedGood);
    }
  }
}

