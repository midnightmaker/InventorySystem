// Models/AuditLog.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        /// <summary>
        /// The entity/table name, e.g. "Sale", "Purchase", "Item".
        /// </summary>
        [Required, StringLength(100)]
        public string EntityName { get; set; } = string.Empty;

        /// <summary>
        /// Primary key of the affected entity.
        /// </summary>
        [StringLength(50)]
        public string EntityId { get; set; } = string.Empty;

        /// <summary>
        /// Create, Update, or Delete.
        /// </summary>
        [Required, StringLength(20)]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// JSON snapshot of the property values before the change (null for Create).
        /// </summary>
        public string? OldValues { get; set; }

        /// <summary>
        /// JSON snapshot of the property values after the change (null for Delete).
        /// </summary>
        public string? NewValues { get; set; }

        /// <summary>
        /// Comma-separated list of property names that changed (for Update).
        /// </summary>
        [StringLength(2000)]
        public string? AffectedColumns { get; set; }

        /// <summary>
        /// Username or identifier of who made the change.
        /// </summary>
        [StringLength(100)]
        public string? PerformedBy { get; set; }

        /// <summary>
        /// UTC timestamp of the change.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional human-readable summary, e.g. "Changed SaleStatus from Draft to Shipped".
        /// </summary>
        [StringLength(500)]
        public string? Summary { get; set; }

        // ---------- Computed helpers ----------

        [NotMapped]
        public bool IsCreate => Action == "Create";

        [NotMapped]
        public bool IsUpdate => Action == "Update";

        [NotMapped]
        public bool IsDelete => Action == "Delete";

        /// <summary>
        /// Bootstrap badge colour for the action.
        /// </summary>
        [NotMapped]
        public string ActionBadgeClass => Action switch
        {
            "Create" => "bg-success",
            "Update" => "bg-warning text-dark",
            "Delete" => "bg-danger",
            _ => "bg-secondary"
        };

        /// <summary>
        /// Font Awesome icon for the action.
        /// </summary>
        [NotMapped]
        public string ActionIcon => Action switch
        {
            "Create" => "fas fa-plus-circle",
            "Update" => "fas fa-edit",
            "Delete" => "fas fa-trash-alt",
            _ => "fas fa-question-circle"
        };
    }
}
