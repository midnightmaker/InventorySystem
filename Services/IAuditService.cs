// Services/IAuditService.cs
using InventorySystem.Models;

namespace InventorySystem.Services
{
    public interface IAuditService
    {
        /// <summary>
        /// Get all audit logs with optional filtering, paged.
        /// </summary>
        Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsAsync(
            string? entityName = null,
            string? entityId = null,
            string? action = null,
            string? performedBy = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchTerm = null,
            int page = 1,
            int pageSize = 50);

        /// <summary>
        /// Get the full change history for a specific entity instance.
        /// </summary>
        Task<List<AuditLog>> GetEntityHistoryAsync(string entityName, string entityId);

        /// <summary>
        /// Get distinct entity names that appear in the audit log.
        /// </summary>
        Task<List<string>> GetDistinctEntityNamesAsync();

        /// <summary>
        /// Get distinct users that appear in the audit log.
        /// </summary>
        Task<List<string>> GetDistinctUsersAsync();
    }
}
