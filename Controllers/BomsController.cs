using InventorySystem.Models;
using InventorySystem.Models.ViewModels;
using InventorySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System.Text;

namespace InventorySystem.Controllers
{
  public class BomsController : Controller
  {
    private readonly IBomService _bomService;
    private readonly IInventoryService _inventoryService;
    private readonly IProductionService _productionService;
    private readonly IVersionControlService _versionService;
    private readonly ILogger<BomsController> _logger; 

    public BomsController(
        IBomService bomService,
        IInventoryService inventoryService,
        IProductionService productionService,
        IVersionControlService versionService,
        ILogger<BomsController> logger)
    {
      _bomService = bomService;
      _inventoryService = inventoryService;
      _productionService = productionService;
      _versionService = versionService;
      _logger = logger; // Assign logger
    }

    public async Task<IActionResult> Index()
    {
      var boms = await _bomService.GetAllBomsAsync();
      return View(boms);
    }

    public async Task<IActionResult> Details(int id)
    {
      var bom = await _bomService.GetBomByIdAsync(id); // Use existing method from IBomService

      if (bom == null) return NotFound();

      ViewBag.TotalCost = await _bomService.GetBomTotalCostAsync(id);

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

    public async Task<IActionResult> CostReport(int id)
    {
      var bom = await _bomService.GetBomByIdAsync(id);
      if (bom == null) return NotFound();

      ViewBag.TotalCost = await _bomService.GetBomTotalCostAsync(id);
      return View(bom);
    }

    public async Task<IActionResult> Delete(int id)
    {
      var bom = await _bomService.GetBomByIdAsync(id);
      if (bom == null) return NotFound();
      return View(bom);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
      await _bomService.DeleteBomAsync(id);
      return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetBomDetails(int id)
    {
      try
      {
        var bom = await _bomService.GetBomByIdAsync(id);
        if (bom == null)
        {
          return NotFound(new { error = "BOM not found" });
        }

        var response = new
        {
          id = bom.Id,
          bomNumber = bom.BomNumber,
          description = bom.Description,
          assemblyPartNumber = bom.AssemblyPartNumber,
          version = bom.Version,
          itemCount = bom.BomItems?.Count ?? 0,
          subAssemblyCount = bom.SubAssemblies?.Count ?? 0,
          createdDate = bom.CreatedDate.ToString("MM/dd/yyyy"),
          modifiedDate = bom.ModifiedDate.ToString("MM/dd/yyyy"),
          isCurrentVersion = bom.IsCurrentVersion,
          hasDocuments = bom.HasDocuments,
          documentCount = bom.DocumentCount
        };

        return Json(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting BOM details for ID: {BomId}", id);
        return StatusCode(500, new { error = "Error loading BOM details" });
      }
    }
    // Quick Material Check - GET
    public async Task<IActionResult> QuickMaterialCheck(int id, int quantity = 1)
    {
      try
      {
        var bom = await _bomService.GetBomByIdAsync(id);
        if (bom == null) return NotFound();

        var shortageAnalysis = await _productionService.GetMaterialShortageAnalysisAsync(id, quantity);

        ViewBag.BomId = id;
        ViewBag.Quantity = quantity;
        return View(shortageAnalysis);
      }
      catch (Exception ex)
      {
        TempData["ErrorMessage"] = $"Error checking material availability: {ex.Message}";
        return RedirectToAction("Details", new { id });
      }
    }

    // AJAX endpoint for quick material check
    [HttpGet]
    public async Task<IActionResult> GetQuickMaterialStatus(int bomId, int quantity = 1)
    {
      try
      {
        var canBuild = await _productionService.CanBuildBomAsync(bomId, quantity);
        var materialCost = await _productionService.CalculateBomMaterialCostAsync(bomId, quantity);
        var shortages = await _productionService.GetBomMaterialShortagesAsync(bomId, quantity);

        return Json(new
        {
          success = true,
          canBuild = canBuild,
          materialCost = materialCost,
          shortageCount = shortages.Count(),
          shortageValue = shortages.Sum(s => s.ShortageValue),
          criticalShortages = shortages.Count(s => s.IsCriticalShortage),
          shortages = shortages.Take(5).Select(s => new
          {
            partNumber = s.PartNumber,
            description = s.Description,
            shortageQuantity = s.ShortageQuantity,
            availableQuantity = s.AvailableQuantity,
            requiredQuantity = s.RequiredQuantity,
            isCritical = s.IsCriticalShortage
          })
        });
      }
      catch (Exception ex)
      {
        return Json(new { success = false, error = ex.Message });
      }
    }

    // Add this method to the BomsController to help with BOM document upload
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
  }
}