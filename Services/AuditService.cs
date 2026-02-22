// Services/AuditService.cs
using InventorySystem.Data;
using InventorySystem.Models;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
    public class AuditService : IAuditService
    {
        private readonly InventoryContext _context;

        public AuditService(InventoryContext context)
        {
            _context = context;
        }

        public async Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsAsync(
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
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityName))
                query = query.Where(a => a.EntityName == entityName);

            if (!string.IsNullOrWhiteSpace(entityId))
                query = query.Where(a => a.EntityId == entityId);

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);

            if (!string.IsNullOrWhiteSpace(performedBy))
                query = query.Where(a => a.PerformedBy == performedBy);

            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(a =>
                    (a.Summary != null && a.Summary.ToLower().Contains(term)) ||
                    (a.NewValues != null && a.NewValues.ToLower().Contains(term)) ||
                    (a.OldValues != null && a.OldValues.ToLower().Contains(term)) ||
                    a.EntityName.ToLower().Contains(term) ||
                    a.EntityId.Contains(term));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<List<AuditLog>> GetEntityHistoryAsync(string entityName, string entityId)
        {
            return await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityName == entityName && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<string>> GetDistinctEntityNamesAsync()
        {
            return await _context.AuditLogs
                .AsNoTracking()
                .Select(a => a.EntityName)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();
        }

        public async Task<List<string>> GetDistinctUsersAsync()
        {
            return await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.PerformedBy != null)
                .Select(a => a.PerformedBy!)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();
        }
    }
}
