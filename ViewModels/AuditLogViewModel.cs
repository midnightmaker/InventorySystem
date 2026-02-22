// ViewModels/AuditLogViewModel.cs
using InventorySystem.Models;

namespace InventorySystem.ViewModels
{
    public class AuditLogIndexViewModel
    {
        public List<AuditLog> AuditLogs { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        // Filters
        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
        public string? Action { get; set; }
        public string? PerformedBy { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SearchTerm { get; set; }

        // Dropdowns
        public List<string> AvailableEntityNames { get; set; } = new();
        public List<string> AvailableUsers { get; set; } = new();
    }

    public class EntityHistoryViewModel
    {
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public List<AuditLog> History { get; set; } = new();
    }
}
