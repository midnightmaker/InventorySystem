// Controllers/AuditController.cs
using InventorySystem.Services;
using InventorySystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Controllers
{
    public class AuditController : Controller
    {
        private readonly IAuditService _auditService;
        private readonly ILogger<AuditController> _logger;

        public AuditController(IAuditService auditService, ILogger<AuditController> logger)
        {
            _auditService = auditService;
            _logger = logger;
        }

        // GET: /Audit
        public async Task<IActionResult> Index(
            string? entityName = null,
            string? entityId = null,
            string? action = null,
            string? performedBy = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchTerm = null,
            int page = 1,
            int pageSize = 50)
        {
            try
            {
                var (items, totalCount) = await _auditService.GetAuditLogsAsync(
                    entityName, entityId, action, performedBy,
                    fromDate, toDate, searchTerm, page, pageSize);

                var entityNames = await _auditService.GetDistinctEntityNamesAsync();
                var users = await _auditService.GetDistinctUsersAsync();

                var viewModel = new AuditLogIndexViewModel
                {
                    AuditLogs = items,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    EntityName = entityName,
                    EntityId = entityId,
                    Action = action,
                    PerformedBy = performedBy,
                    FromDate = fromDate,
                    ToDate = toDate,
                    SearchTerm = searchTerm,
                    AvailableEntityNames = entityNames,
                    AvailableUsers = users
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading audit log");
                TempData["ErrorMessage"] = "Error loading audit log.";
                return View(new AuditLogIndexViewModel());
            }
        }

        // GET: /Audit/EntityHistory?entityName=Sale&entityId=42
        public async Task<IActionResult> EntityHistory(string entityName, string entityId)
        {
            try
            {
                var history = await _auditService.GetEntityHistoryAsync(entityName, entityId);

                var viewModel = new EntityHistoryViewModel
                {
                    EntityName = entityName,
                    EntityId = entityId,
                    History = history
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading entity history for {EntityName} #{EntityId}", entityName, entityId);
                TempData["ErrorMessage"] = "Error loading entity history.";
                return RedirectToAction("Index");
            }
        }
    }
}
