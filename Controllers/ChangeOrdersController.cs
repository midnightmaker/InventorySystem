using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Services;

namespace InventorySystem.Controllers
{
  public class ChangeOrdersController : Controller
  {
    private readonly InventoryContext _context;
    private readonly VersionControlService _versionService;
    private readonly ILogger<ChangeOrdersController> _logger;

    // Allowed file types for change order documents
    private readonly Dictionary<string, string[]> _allowedFileTypes = new()
        {
            { "PDF", new[] { ".pdf" } },
            { "Images", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" } },
            { "Office", new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" } },
            { "CAD", new[] { ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs" } },
            { "Text", new[] { ".txt", ".rtf" } },
            { "Archive", new[] { ".zip", ".rar", ".7z" } }
        };

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB limit

    public ChangeOrdersController(
        InventoryContext context,
        VersionControlService versionService,
        ILogger<ChangeOrdersController> logger)
    {
      _context = context;
      _versionService = versionService;
      _logger = logger;
    }

    // GET: ChangeOrders
    [HttpGet]
    public async Task<IActionResult> Index()
    {
      try
      {
        var changeOrders = await _versionService.GetAllChangeOrdersAsync();
        var statistics = await _versionService.GetChangeOrderStatisticsAsync();

        ViewBag.Statistics = statistics;

        return View(changeOrders);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading change orders");
        TempData["ErrorMessage"] = "Error loading change orders.";
        return View(new List<ChangeOrder>());
      }
    }

    // GET: ChangeOrders/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
      try
      {
        var changeOrder = await _versionService.GetChangeOrderByIdAsync(id);
        if (changeOrder == null)
        {
          TempData["ErrorMessage"] = "Change order not found.";
          return RedirectToAction("Index");
        }

        return View(changeOrder);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading change order {ChangeOrderId}", id);
        TempData["ErrorMessage"] = "Error loading change order details.";
        return RedirectToAction("Index");
      }
    }

    // Enhanced CreateEntry method to handle documents in the modal
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEntry(ChangeOrder changeOrder, IFormFile[]? documents)
    {
      try
      {
        // Validate the change order data
        if (!ModelState.IsValid)
        {
          var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
          return Json(new { success = false, error = "Validation Error", message = string.Join("; ", errors) });
        }

        // Create the change order
        var createdChangeOrder = await _versionService.CreateChangeOrderAsync(changeOrder);

        // Handle document uploads if any
        if (documents != null && documents.Any())
        {
          await ProcessDocumentUploads(createdChangeOrder.Id, documents);
        }

        _logger.LogInformation("Change order {ChangeOrderNumber} created by {User}",
            createdChangeOrder.ChangeOrderNumber, User.Identity?.Name);

        return Json(new
        {
          success = true,
          message = $"Change order {createdChangeOrder.ChangeOrderNumber} created successfully.",
          changeOrderId = createdChangeOrder.Id,
          changeOrderNumber = createdChangeOrder.ChangeOrderNumber
        });
      }
      catch (InvalidOperationException ex)
      {
        _logger.LogWarning(ex, "Invalid change order creation attempt");
        return Json(new { success = false, error = "Invalid Operation", message = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating change order");
        return Json(new { success = false, error = "System Error", message = "An unexpected error occurred while creating the change order." });
      }
    }

    // Enhanced Implement method to handle documents
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Implement(int id)
    {
      try
      {
        var changeOrder = await _versionService.GetChangeOrderByIdAsync(id);
        if (changeOrder == null)
        {
          TempData["ErrorMessage"] = "Change order not found.";
          return RedirectToAction("Index");
        }

        if (changeOrder.Status != "Pending")
        {
          TempData["WarningMessage"] = $"Change order {changeOrder.ChangeOrderNumber} cannot be implemented (Status: {changeOrder.Status}).";
          return RedirectToAction("Details", new { id });
        }

        var success = await _versionService.ImplementChangeOrderAsync(id, User.Identity?.Name ?? "System");
        if (success)
        {
          TempData["SuccessMessage"] = $"Change order {changeOrder.ChangeOrderNumber} implemented successfully. New version created.";
          _logger.LogInformation("Change order {ChangeOrderNumber} implemented by {User}",
              changeOrder.ChangeOrderNumber, User.Identity?.Name);

          // Log document transfer if any documents exist
          if (changeOrder.HasDocuments)
          {
            _logger.LogInformation("Change order {ChangeOrderNumber} had {DocumentCount} documents that were preserved",
                changeOrder.ChangeOrderNumber, changeOrder.DocumentCount);
          }
        }
        else
        {
          TempData["ErrorMessage"] = "Failed to implement change order.";
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error implementing change order {ChangeOrderId}", id);
        TempData["ErrorMessage"] = $"Error implementing change order: {ex.Message}";
      }

      return RedirectToAction("Details", new { id });
    }

    // POST: ChangeOrders/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
      try
      {
        var changeOrder = await _versionService.GetChangeOrderByIdAsync(id);
        if (changeOrder == null)
        {
          TempData["ErrorMessage"] = "Change order not found.";
          return RedirectToAction("Index");
        }

        if (changeOrder.Status != "Pending")
        {
          TempData["WarningMessage"] = $"Change order {changeOrder.ChangeOrderNumber} cannot be cancelled (Status: {changeOrder.Status}).";
          return RedirectToAction("Details", new { id });
        }

        var success = await _versionService.CancelChangeOrderAsync(id, User.Identity?.Name ?? "System");
        if (success)
        {
          TempData["SuccessMessage"] = $"Change order {changeOrder.ChangeOrderNumber} cancelled successfully.";
          _logger.LogInformation("Change order {ChangeOrderNumber} cancelled by {User}",
              changeOrder.ChangeOrderNumber, User.Identity?.Name);
        }
        else
        {
          TempData["ErrorMessage"] = "Failed to cancel change order.";
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error cancelling change order {ChangeOrderId}", id);
        TempData["ErrorMessage"] = $"Error cancelling change order: {ex.Message}";
      }

      return RedirectToAction("Details", new { id });
    }

    // GET: ChangeOrders/Create - Redirect to modal approach (legacy support)
    [HttpGet]
    public IActionResult Create()
    {
      // Redirect to main change orders page with instruction to use modal
      TempData["InfoMessage"] = "Click the 'New Version' button on any Item or BOM, or use the 'Create New Order' option from the Change Orders menu.";
      return RedirectToAction("Index");
    }

    // GET: ChangeOrders/CreateModal/{entityType}/{entityId} - AJAX endpoint for modal
    [HttpGet]
    [Route("ChangeOrders/CreateModal/{entityType}/{entityId}")]
    public async Task<IActionResult> CreateModal(string entityType, int entityId)
    {
      try
      {
        _logger.LogInformation("CreateModal called with EntityType: {EntityType}, EntityId: {EntityId}", entityType, entityId);

        // Validate entity type
        if (string.IsNullOrEmpty(entityType) || (entityType != "Item" && entityType != "BOM"))
        {
          _logger.LogWarning("Invalid entity type: {EntityType}", entityType);
          return Json(new { success = false, error = "Invalid entity type. Must be 'Item' or 'BOM'." });
        }

        // Validate that the entity exists
        if (entityType == "Item")
        {
          var item = await _context.Items.FindAsync(entityId);
          if (item == null)
          {
            _logger.LogWarning("Item with ID {EntityId} not found", entityId);
            return Json(new { success = false, error = "Item not found." });
          }
        }
        else if (entityType == "BOM")
        {
          var bom = await _context.Boms.FindAsync(entityId);
          if (bom == null)
          {
            _logger.LogWarning("BOM with ID {EntityId} not found", entityId);
            return Json(new { success = false, error = "BOM not found." });
          }
        }

        // Check if there are pending change orders for this entity
        var hasPendingChangeOrders = await _versionService.HasPendingChangeOrdersAsync(entityType, entityId);
        if (hasPendingChangeOrders)
        {
          _logger.LogInformation("Found pending change orders for {EntityType} {EntityId}", entityType, entityId);
          var pendingChangeOrders = await _versionService.GetPendingChangeOrdersForEntityAsync(entityType, entityId);
          var pendingNumbers = string.Join(", ", pendingChangeOrders.Select(co => co.ChangeOrderNumber));

          return Json(new
          {
            success = false,
            error = "Cannot Create Change Order",
            message = $"This {entityType.ToLower()} has pending change orders that must be implemented or cancelled first: {pendingNumbers}",
            pendingChangeOrders = pendingChangeOrders.Select(co => new
            {
              id = co.Id,
              changeOrderNumber = co.ChangeOrderNumber,
              newVersion = co.NewVersion,
              createdDate = co.CreatedDate.ToString("MM/dd/yyyy"),
              createdBy = co.CreatedBy
            })
          });
        }

        var changeOrder = new ChangeOrder
        {
          EntityType = entityType ?? "",
          BaseEntityId = entityId,
          CreatedBy = User.Identity?.Name ?? "System"
        };

        _logger.LogInformation("Returning modal view for {EntityType} {EntityId}", entityType, entityId);
        return PartialView("_CreateChangeOrderModal", changeOrder);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in CreateModal for {EntityType} {EntityId}: {Message}", entityType, entityId, ex.Message);

        // Return a JSON error response for AJAX calls
        if (Request.Headers.ContainsKey("X-Requested-With") && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
          return Json(new { success = false, error = "System Error", message = $"An error occurred: {ex.Message}" });
        }

        // For non-AJAX calls, return a simple error view
        return Json(new { success = false, error = "System Error", message = "An error occurred while loading the change order form." });
      }
    }

    // API endpoint to get change order statistics
    [HttpGet]
    public async Task<IActionResult> GetStatistics()
    {
      try
      {
        var statistics = await _versionService.GetChangeOrderStatisticsAsync();
        return Json(statistics);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting change order statistics");
        return Json(new { error = "Failed to load statistics" });
      }
    }

    // API endpoint to get change orders by status
    [HttpGet]
    public async Task<IActionResult> GetByStatus(string status)
    {
      try
      {
        var changeOrders = await _versionService.GetChangeOrdersByStatusAsync(status);
        return Json(changeOrders.Select(co => new
        {
          id = co.Id,
          changeOrderNumber = co.ChangeOrderNumber,
          entityType = co.EntityType,
          versionChange = $"{co.PreviousVersion} → {co.NewVersion}",
          description = co.Description,
          status = co.Status,
          documentCount = co.DocumentCount,
          hasDocuments = co.HasDocuments,
          createdDate = co.CreatedDate.ToString("MM/dd/yyyy"),
          createdBy = co.CreatedBy
        }));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting change orders by status {Status}", status);
        return Json(new { error = "Failed to load change orders" });
      }
    }

    // API endpoint to get change orders for a specific entity
    [HttpGet]
    public async Task<IActionResult> GetForEntity(string entityType, int entityId)
    {
      try
      {
        var changeOrders = await _versionService.GetChangeOrdersForEntityAsync(entityType, entityId);
        return Json(changeOrders.Select(co => new
        {
          id = co.Id,
          changeOrderNumber = co.ChangeOrderNumber,
          entityType = co.EntityType,
          versionChange = $"{co.PreviousVersion} → {co.NewVersion}",
          description = co.Description,
          status = co.Status,
          documentCount = co.DocumentCount,
          hasDocuments = co.HasDocuments,
          createdDate = co.CreatedDate.ToString("MM/dd/yyyy"),
          createdBy = co.CreatedBy,
          implementedDate = co.ImplementedDate?.ToString("MM/dd/yyyy"),
          implementedBy = co.ImplementedBy
        }));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting change orders for {EntityType} {EntityId}", entityType, entityId);
        return Json(new { error = "Failed to load change orders" });
      }
    }

    // Helper method to process document uploads
    private async Task ProcessDocumentUploads(int changeOrderId, IFormFile[] documents)
    {
      var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff",
                                           ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                                           ".dwg", ".dxf", ".step", ".stp", ".iges", ".igs",
                                           ".txt", ".rtf", ".zip", ".rar", ".7z" };

      foreach (var file in documents)
      {
        if (file == null || file.Length == 0) continue;

        // Validate file size
        if (file.Length > MaxFileSizeBytes)
        {
          _logger.LogWarning("File {FileName} exceeds size limit for change order {ChangeOrderId}",
              file.FileName, changeOrderId);
          continue;
        }

        // Validate file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
          _logger.LogWarning("File {FileName} has invalid extension for change order {ChangeOrderId}",
              file.FileName, changeOrderId);
          continue;
        }

        try
        {
          using var memoryStream = new MemoryStream();
          await file.CopyToAsync(memoryStream);

          var document = new ChangeOrderDocument
          {
            ChangeOrderId = changeOrderId,
            DocumentName = Path.GetFileNameWithoutExtension(file.FileName),
            DocumentType = "Other", // Default type when uploaded via modal
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            DocumentData = memoryStream.ToArray(),
            UploadedDate = DateTime.Now
          };

          _context.ChangeOrderDocuments.Add(document);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error processing document {FileName} for change order {ChangeOrderId}",
              file.FileName, changeOrderId);
        }
      }

      await _context.SaveChangesAsync();
    }

    // Helper method to check if file type is allowed
    private bool IsAllowedFileType(string fileExtension)
    {
      return _allowedFileTypes.Values.Any(extensions => extensions.Contains(fileExtension));
    }

    // Helper method to get allowed file types for display
    private string GetAllowedFileTypesForDisplay()
    {
      var types = new List<string>();
      foreach (var category in _allowedFileTypes)
      {
        types.Add($"{category.Key}: {string.Join(", ", category.Value)}");
      }
      return string.Join(" | ", types);
    }

    // GET: ChangeOrders/Export - Export change orders to CSV
    [HttpGet]
    public async Task<IActionResult> Export(string? status = null, string? entityType = null)
    {
      try
      {
        var changeOrders = await _context.ChangeOrders
            .Include(co => co.BaseItem)
            .Include(co => co.BaseBom)
            .Include(co => co.ChangeOrderDocuments)
            .Where(co => (status == null || co.Status == status) &&
                        (entityType == null || co.EntityType == entityType))
            .OrderByDescending(co => co.CreatedDate)
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Change Order Number,Entity Type,Entity Reference,Previous Version,New Version,Status,Description,Document Count,Created Date,Created By,Implemented Date,Implemented By");

        foreach (var co in changeOrders)
        {
          var entityReference = co.EntityType == "Item"
              ? co.BaseItem?.PartNumber ?? "Unknown"
              : co.BaseBom?.BomNumber ?? "Unknown";

          csv.AppendLine($"\"{co.ChangeOrderNumber}\",\"{co.EntityType}\",\"{entityReference}\",\"{co.PreviousVersion}\",\"{co.NewVersion}\",\"{co.Status}\",\"{co.Description?.Replace("\"", "\"\"")}\",{co.DocumentCount},\"{co.CreatedDate:MM/dd/yyyy}\",\"{co.CreatedBy}\",\"{co.ImplementedDate:MM/dd/yyyy}\",\"{co.ImplementedBy}\"");
        }

        var fileName = $"ChangeOrders_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error exporting change orders");
        TempData["ErrorMessage"] = "Error exporting change orders.";
        return RedirectToAction("Index");
      }
    }

    // GET: ChangeOrders/Dashboard - Summary dashboard view
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
      try
      {
        var statistics = await _versionService.GetChangeOrderStatisticsAsync();
        var recentChangeOrders = await _context.ChangeOrders
            .Include(co => co.BaseItem)
            .Include(co => co.BaseBom)
            .Include(co => co.ChangeOrderDocuments)
            .OrderByDescending(co => co.CreatedDate)
            .Take(10)
            .ToListAsync();

        ViewBag.Statistics = statistics;
        ViewBag.RecentChangeOrders = recentChangeOrders;

        return View();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading change orders dashboard");
        TempData["ErrorMessage"] = "Error loading dashboard.";
        return RedirectToAction("Index");
      }
    }
  }
}