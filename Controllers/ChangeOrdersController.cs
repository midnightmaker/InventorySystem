// Controllers/ChangeOrdersController.cs - Fixed with Route Parameter Support
using Microsoft.AspNetCore.Mvc;
using InventorySystem.Models;
using InventorySystem.Services;

namespace InventorySystem.Controllers
{
  public class ChangeOrdersController : Controller
  {
    private readonly IVersionControlService _versionService;
    private readonly ILogger<ChangeOrdersController> _logger;

    public ChangeOrdersController(IVersionControlService versionService, ILogger<ChangeOrdersController> logger)
    {
      _versionService = versionService;
      _logger = logger;
    }

    // GET: ChangeOrders
    public async Task<IActionResult> Index()
    {
      try
      {
        var allChangeOrders = await _versionService.GetAllChangeOrdersAsync();
        return View(allChangeOrders);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading change orders");
        TempData["ErrorMessage"] = $"Error loading change orders: {ex.Message}";
        return View(new List<ChangeOrder>());
      }
    }

    // GET: ChangeOrders/Details/5
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
        TempData["ErrorMessage"] = $"Error loading change order: {ex.Message}";
        return RedirectToAction("Index");
      }
    }

    // GET: ChangeOrders/CreateModal?entityType=Item&entityId=5
    public async Task<IActionResult> CreateModal(string entityType, int entityId)
    {
      try
      {
        // Validate entity type
        if (!string.IsNullOrEmpty(entityType) && entityType != "Item" && entityType != "BOM")
        {
          return BadRequest("Invalid entity type. Must be 'Item' or 'BOM'.");
        }

        // Check if there are pending change orders for this entity
        var hasPendingChangeOrders = await _versionService.HasPendingChangeOrdersAsync(entityType, entityId);
        if (hasPendingChangeOrders)
        {
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

        // Get previous version for reference if entity is specified
        if (!string.IsNullOrEmpty(entityType) && entityId > 0)
        {
          if (entityType == "Item")
          {
            var currentVersion = await _versionService.GetCurrentItemVersionAsync(entityId);
            changeOrder.PreviousVersion = currentVersion?.Version;
          }
          else if (entityType == "BOM")
          {
            var currentVersion = await _versionService.GetCurrentBomVersionAsync(entityId);
            changeOrder.PreviousVersion = currentVersion?.Version;
          }
        }

        return PartialView("_CreateChangeOrderModal", changeOrder);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating change order form for {EntityType} {EntityId}", entityType, entityId);
        return StatusCode(500, $"Error creating change order form: {ex.Message}");
      }
    }

    // NEW: GET: ChangeOrders/Create/{entityType}/{entityId} - Route parameter version
    [HttpGet("ChangeOrders/Create/{entityType}/{entityId:int}")]
    public async Task<IActionResult> CreateFromRoute(string entityType, int entityId)
    {
      // Just redirect to the CreateModal method with the same parameters
      return await CreateModal(entityType, entityId);
    }

    // POST: ChangeOrders/Create - Handle the form submission
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEntry(ChangeOrder changeOrder, bool implementImmediately = false)
    {
      // Remove ModelState errors for auto-generated fields
      ModelState.Remove("ChangeOrderNumber");
      ModelState.Remove("Id");

      if (ModelState.IsValid)
      {
        try
        {
          // Create the change order (service will validate version progression and generate number)
          var createdChangeOrder = await _versionService.CreateChangeOrderAsync(changeOrder);

          _logger.LogInformation("Change order {ChangeOrderNumber} created successfully", createdChangeOrder.ChangeOrderNumber);

          // Implement immediately if requested
          if (implementImmediately)
          {
            var success = await _versionService.ImplementChangeOrderAsync(
                createdChangeOrder.Id,
                User.Identity?.Name ?? "System"
            );

            if (success)
            {
              TempData["SuccessMessage"] = $"Change order {createdChangeOrder.ChangeOrderNumber} created and implemented successfully!";
              _logger.LogInformation("Change order {ChangeOrderNumber} implemented successfully", createdChangeOrder.ChangeOrderNumber);
            }
            else
            {
              TempData["WarningMessage"] = $"Change order {createdChangeOrder.ChangeOrderNumber} created but implementation failed.";
              _logger.LogWarning("Change order {ChangeOrderNumber} implementation failed", createdChangeOrder.ChangeOrderNumber);
            }
          }
          else
          {
            TempData["SuccessMessage"] = $"Change order {createdChangeOrder.ChangeOrderNumber} created successfully!";
          }

          // Redirect based on entity type
          if (changeOrder.EntityType == "Item")
          {
            return RedirectToAction("Details", "Items", new { id = changeOrder.BaseEntityId });
          }
          else if (changeOrder.EntityType == "BOM")
          {
            return RedirectToAction("Details", "Boms", new { id = changeOrder.BaseEntityId });
          }

          return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error creating change order");
          TempData["ErrorMessage"] = $"Error creating change order: {ex.Message}";
          ModelState.AddModelError("", ex.Message);
        }
      }
      else
      {
        // Log validation errors for debugging
        var errors = ModelState
            .Where(x => x.Value.Errors.Count > 0)
            .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) });

        foreach (var error in errors)
        {
          _logger.LogWarning("Validation error in {Field}: {Errors}", error.Field, string.Join(", ", error.Errors));
        }
      }

      // If we got this far, something failed, redisplay form
      return PartialView("_CreateChangeOrderModal", changeOrder);
    }

    // POST: ChangeOrders/Implement/5
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
          TempData["WarningMessage"] = $"Change order {changeOrder.ChangeOrderNumber} is not pending and cannot be implemented.";
          return RedirectToAction("Details", new { id });
        }

        var success = await _versionService.ImplementChangeOrderAsync(id, User.Identity?.Name ?? "System");

        if (success)
        {
          TempData["SuccessMessage"] = $"Change order {changeOrder.ChangeOrderNumber} implemented successfully!";
          _logger.LogInformation("Change order {ChangeOrderNumber} implemented by {User}", changeOrder.ChangeOrderNumber, User.Identity?.Name);
        }
        else
        {
          TempData["ErrorMessage"] = "Failed to implement change order. It may have conflicts or errors.";
          _logger.LogWarning("Failed to implement change order {ChangeOrderNumber}", changeOrder.ChangeOrderNumber);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error implementing change order {ChangeOrderId}", id);
        TempData["ErrorMessage"] = $"Error implementing change order: {ex.Message}";
      }

      return RedirectToAction("Details", new { id });
    }

    // GET: ChangeOrders/Pending - View only pending change orders
    public async Task<IActionResult> Pending()
    {
      try
      {
        var pendingChangeOrders = await _versionService.GetPendingChangeOrdersAsync();
        ViewBag.Title = "Pending Change Orders";
        ViewBag.ShowOnlyPending = true;
        return View("Index", pendingChangeOrders);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading pending change orders");
        TempData["ErrorMessage"] = $"Error loading pending change orders: {ex.Message}";
        return View("Index", new List<ChangeOrder>());
      }
    }

    // GET: ChangeOrders/Entity/Item/5 - Get change orders for specific entity
    public async Task<IActionResult> Entity(string entityType, int entityId)
    {
      try
      {
        var changeOrders = await _versionService.GetChangeOrdersByEntityAsync(entityType, entityId);
        ViewBag.EntityType = entityType;
        ViewBag.EntityId = entityId;
        ViewBag.Title = $"{entityType} Change Orders";
        return View("EntityChangeOrders", changeOrders);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading change orders for {EntityType} {EntityId}", entityType, entityId);
        TempData["ErrorMessage"] = $"Error loading change orders: {ex.Message}";
        return View("EntityChangeOrders", new List<ChangeOrder>());
      }
    }

    // API endpoint for AJAX calls to get change order status
    [HttpGet]
    public async Task<IActionResult> GetStatus(int id)
    {
      try
      {
        var changeOrder = await _versionService.GetChangeOrderByIdAsync(id);
        if (changeOrder == null)
        {
          return NotFound();
        }

        return Json(new
        {
          id = changeOrder.Id,
          status = changeOrder.Status,
          implementedDate = changeOrder.ImplementedDate,
          implementedBy = changeOrder.ImplementedBy,
          changeOrderNumber = changeOrder.ChangeOrderNumber
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting status for change order {ChangeOrderId}", id);
        return BadRequest(ex.Message);
      }
    }

    // API endpoint to get entity information (used by modal)
    [HttpGet]
    public async Task<IActionResult> GetEntityInfo(string entityType, int entityId)
    {
      try
      {
        if (entityType == "Item")
        {
          var item = await _versionService.GetCurrentItemVersionAsync(entityId);
          if (item == null)
          {
            return NotFound(new { message = "Item not found" });
          }

          return Json(new
          {
            entityType = "Item",
            partNumber = item.PartNumber,
            description = item.Description,
            currentVersion = item.Version
          });
        }
        else if (entityType == "BOM")
        {
          var bom = await _versionService.GetCurrentBomVersionAsync(entityId);
          if (bom == null)
          {
            return NotFound(new { message = "BOM not found" });
          }

          return Json(new
          {
            entityType = "BOM",
            name = bom.Name,
            description = bom.Description,
            currentVersion = bom.Version
          });
        }

        return BadRequest(new { message = "Invalid entity type" });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting entity info for {EntityType} {EntityId}", entityType, entityId);
        return StatusCode(500, new { message = "Error loading entity information" });
      }
    }

    // POST: ChangeOrders/Cancel/5 - Cancel a pending change order
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
          _logger.LogInformation("Change order {ChangeOrderNumber} cancelled by {User}", changeOrder.ChangeOrderNumber, User.Identity?.Name);
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
  }
}