// Controllers/ProductionController.cs
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
          viewModel.BomName = bom.BomNumber;
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
          viewModel.BomName = bom.BomNumber;
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
          bomName = bom?.BomNumber ?? "",
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

    // Bulk Purchase Request - GET
    public async Task<IActionResult> CreateBulkPurchaseRequest(int bomId, int quantity)
    {
      try
      {
        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(bomId, quantity);

        var bulkRequest = new BulkPurchaseRequest
        {
          BomId = bomId,
          Quantity = quantity,
          ExpectedDeliveryDate = DateTime.Now.AddDays(14), // Default 2 weeks
          ItemsToPurchase = shortageAnalysis.MaterialShortages.Select(s => new ShortageItemPurchase
          {
            ItemId = s.ItemId,
            Selected = true,
            QuantityToPurchase = s.SuggestedPurchaseQuantity,
            EstimatedUnitCost = s.EstimatedUnitCost,
            PreferredVendor = s.PreferredVendor
          }).ToList()
        };

        ViewBag.ShortageAnalysis = shortageAnalysis;
        return View(bulkRequest);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error creating bulk purchase request: {ex.Message}";
        return RedirectToAction("MaterialShortageReport", new { bomId, quantity });
      }
    }

    // Bulk Purchase Request - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBulkPurchaseRequest(BulkPurchaseRequest request)
    {
      if (ModelState.IsValid)
      {
        try
        {
          var createdPurchases = new List<int>();
          var errors = new List<string>();

          foreach (var itemPurchase in request.ItemsToPurchase.Where(ip => ip.Selected))
          {
            try
            {
              var purchase = new Purchase
              {
                ItemId = itemPurchase.ItemId,
                Vendor = itemPurchase.PreferredVendor ?? "TBD",
                PurchaseDate = DateTime.Now,
                QuantityPurchased = itemPurchase.QuantityToPurchase,
                CostPerUnit = itemPurchase.EstimatedUnitCost,
                PurchaseOrderNumber = request.PurchaseOrderNumber,
                Notes = $"Bulk purchase for BOM production. {request.Notes}",
                RemainingQuantity = itemPurchase.QuantityToPurchase,
                CreatedDate = DateTime.Now
              };

              var createdPurchase = await _purchaseService.CreatePurchaseAsync(purchase);
              createdPurchases.Add(createdPurchase.Id);
            }
            catch (Exception ex)
            {
              var item = await _inventoryService.GetItemByIdAsync(itemPurchase.ItemId);
              errors.Add($"Error creating purchase for {item?.PartNumber}: {ex.Message}");
            }
          }

          if (createdPurchases.Any())
          {
            TempData["SuccessMessage"] = $"Successfully created {createdPurchases.Count} purchase orders for material shortages.";

            if (errors.Any())
            {
              TempData["WarningMessage"] = $"Some purchases failed: {string.Join(", ", errors)}";
            }

            return RedirectToAction("MaterialShortageReport", new { bomId = request.BomId, quantity = request.Quantity });
          }
          else
          {
            TempData["ErrorMessage"] = $"Failed to create any purchases. Errors: {string.Join(", ", errors)}";
          }
        }
        catch (Exception ex)
        {
          TempData["ErrorMessage"] = $"Error processing bulk purchase request: {ex.Message}";
        }
      }

      // Reload view with errors
      var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(request.BomId, request.Quantity);
      ViewBag.ShortageAnalysis = shortageAnalysis;
      return View(request);
    }

    // Quick Purchase for Single Item
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickPurchaseShortageItem(int itemId, int quantity, decimal estimatedCost, string? vendor, int bomId, int bomQuantity)
    {
      try
      {
        var item = await _inventoryService.GetItemByIdAsync(itemId);
        if (item == null)
        {
          TempData["ErrorMessage"] = "Item not found.";
          return RedirectToAction("MaterialShortageReport", new { bomId, quantity = bomQuantity });
        }

        var purchase = new Purchase
        {
          ItemId = itemId,
          Vendor = vendor ?? "Quick Purchase",
          PurchaseDate = DateTime.Now,
          QuantityPurchased = quantity,
          CostPerUnit = estimatedCost,
          PurchaseOrderNumber = $"QP-{DateTime.Now:yyyyMMddHHmm}",
          Notes = $"Quick purchase to resolve shortage for BOM production",
          RemainingQuantity = quantity,
          CreatedDate = DateTime.Now
        };

        await _purchaseService.CreatePurchaseAsync(purchase);
        TempData["SuccessMessage"] = $"Quick purchase created for {item.PartNumber} - {quantity} units at {estimatedCost:C} each.";
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error creating quick purchase: {ex.Message}";
      }

      return RedirectToAction("MaterialShortageReport", new { bomId, quantity = bomQuantity });
    }

    // Export Shortage Report to CSV
    public async Task<IActionResult> ExportShortageReport(int bomId, int quantity)
    {
      try
      {
        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(bomId, quantity);

        var csv = new StringBuilder();
        csv.AppendLine("Part Number,Description,Required Qty,Available Qty,Shortage Qty,Shortage Value,Suggested Purchase Qty,Last Purchase Price,Preferred Vendor,BOM Context");

        foreach (var shortage in shortageAnalysis.MaterialShortages)
        {
          csv.AppendLine($"\"{shortage.PartNumber}\",\"{shortage.Description}\",{shortage.RequiredQuantity},{shortage.AvailableQuantity},{shortage.ShortageQuantity},{shortage.ShortageValue:F2},{shortage.SuggestedPurchaseQuantity},{shortage.LastPurchasePrice:F2},\"{shortage.PreferredVendor}\",\"{shortage.BomContext}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"MaterialShortageReport_{shortageAnalysis.BomName}_{DateTime.Now:yyyyMMdd}.csv";

        return File(bytes, "text/csv", fileName);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error exporting report: {ex.Message}";
        return RedirectToAction("MaterialShortageReport", new { bomId, quantity });
      }
    }
  }
}

