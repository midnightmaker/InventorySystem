// Controllers/WipDashboardController.cs
using Microsoft.AspNetCore.Mvc;
using InventorySystem.Domain.Queries;
using InventorySystem.Domain.Services;
using InventorySystem.Domain.Enums;
using InventorySystem.ViewModels;

namespace InventorySystem.Controllers
{
  public class WipDashboardController : Controller
  {
    private readonly IProductionOrchestrator _orchestrator;
    private readonly ILogger<WipDashboardController> _logger;

    public WipDashboardController(
        IProductionOrchestrator orchestrator,
        ILogger<WipDashboardController> logger)
    {
      _orchestrator = orchestrator;
      _logger = logger;
    }

    public async Task<IActionResult> Index(DateTime? fromDate = null, DateTime? toDate = null, string? assignedTo = null)
    {
      try
      {
        var query = new GetWipDashboardQuery(fromDate, toDate, assignedTo);
        var dashboardData = await _orchestrator.GetWipDashboardAsync(query);

        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.AssignedTo = assignedTo;

        return View(dashboardData);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading WIP dashboard");
        TempData["ErrorMessage"] = "Failed to load dashboard data";
        return View(new WipDashboardResult());
      }
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboardData(DateTime? fromDate = null, DateTime? toDate = null, string? assignedTo = null)
    {
      try
      {
        var query = new GetWipDashboardQuery(fromDate, toDate, assignedTo);
        var dashboardData = await _orchestrator.GetWipDashboardAsync(query);

        return Json(new { success = true, data = dashboardData });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting dashboard data");
        return Json(new { success = false, error = "Failed to load dashboard data" });
      }
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveProductions(string? assignedTo = null, ProductionStatus? status = null)
    {
      try
      {
        var query = new GetActiveProductionsQuery(assignedTo, status);
        var productions = await _orchestrator.GetActiveProductionsAsync(query);

        return Json(new { success = true, data = productions });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting active productions");
        return Json(new { success = false, error = "Failed to load active productions" });
      }
    }

    [HttpGet]
    public async Task<IActionResult> GetOverdueProductions()
    {
      try
      {
        var query = new GetOverdueProductionsQuery();
        var productions = await _orchestrator.GetOverdueProductionsAsync(query);

        return Json(new { success = true, data = productions });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting overdue productions");
        return Json(new { success = false, error = "Failed to load overdue productions" });
      }
    }

    public async Task<IActionResult> Kanban(string? assignedTo = null)
    {
      try
      {
        var query = new GetWipDashboardQuery(assignedTo: assignedTo);
        var dashboardData = await _orchestrator.GetWipDashboardAsync(query);

        ViewBag.AssignedTo = assignedTo;
        return View(dashboardData);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading Kanban board");
        TempData["ErrorMessage"] = "Failed to load Kanban board";
        return View(new WipDashboardResult());
      }
    }

    public async Task<IActionResult> Timeline(int productionId)
    {
      try
      {
        var query = new GetProductionTimelineQuery(productionId);
        var timeline = await _orchestrator.GetProductionTimelineAsync(query);

        return View(timeline);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading production timeline for {ProductionId}", productionId);
        TempData["ErrorMessage"] = "Failed to load production timeline";
        return RedirectToAction("Index");
      }
    }
  }
}