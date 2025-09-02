using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

namespace InventorySystem.Models.CustomerService
{
    public class CaseUpdate
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Support Case")]
        public int SupportCaseId { get; set; }
        public virtual SupportCase SupportCase { get; set; } = null!;

        [Required]
        [StringLength(2000)]
        [Display(Name = "Update Text")]
        public string UpdateText { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Update Type")]
        public CaseUpdateType UpdateType { get; set; } = CaseUpdateType.Comment;

        [Required]
        [StringLength(100)]
        [Display(Name = "Updated By")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Display(Name = "Update Date")]
        public DateTime UpdateDate { get; set; } = DateTime.Now;

        [Display(Name = "Is Internal")]
        public bool IsInternal { get; set; } = false;

        [Display(Name = "Customer Visible")]
        public bool IsCustomerVisible => !IsInternal;

        [Display(Name = "Previous Status")]
        public CaseStatus? PreviousStatus { get; set; }

        [Display(Name = "New Status")]
        public CaseStatus? NewStatus { get; set; }

        [Display(Name = "Previous Priority")]
        public CasePriority? PreviousPriority { get; set; }

        [Display(Name = "New Priority")]
        public CasePriority? NewPriority { get; set; }

        [StringLength(100)]
        [Display(Name = "Previous Assignee")]
        public string? PreviousAssignee { get; set; }

        [StringLength(100)]
        [Display(Name = "New Assignee")]
        public string? NewAssignee { get; set; }

        // Time tracking
        [Display(Name = "Time Spent (Hours)")]
        [Column(TypeName = "decimal(4,2)")]
        public decimal? TimeSpentHours { get; set; }

        [StringLength(500)]
        [Display(Name = "Work Category")]
        public string? WorkCategory { get; set; }

        // Navigation Properties
        public virtual ICollection<CaseUpdateDocument> Documents { get; set; } = new List<CaseUpdateDocument>();

        // Computed Properties
        [NotMapped]
        [Display(Name = "Update Type Icon")]
        public string UpdateTypeIcon => UpdateType switch
        {
            CaseUpdateType.Comment => "fas fa-comment",
            CaseUpdateType.StatusChange => "fas fa-exchange-alt",
            CaseUpdateType.Assignment => "fas fa-user",
            CaseUpdateType.PriorityChange => "fas fa-flag",
            CaseUpdateType.Resolution => "fas fa-check-circle",
            CaseUpdateType.Escalation => "fas fa-arrow-up",
            CaseUpdateType.CustomerResponse => "fas fa-reply",
            _ => "fas fa-info-circle"
        };

        [NotMapped]
        [Display(Name = "Update Type Color")]
        public string UpdateTypeColor => UpdateType switch
        {
            CaseUpdateType.StatusChange => "primary",
            CaseUpdateType.Assignment => "info",
            CaseUpdateType.PriorityChange => "warning",
            CaseUpdateType.Resolution => "success",
            CaseUpdateType.Escalation => "danger",
            CaseUpdateType.CustomerResponse => "info",
            _ => "secondary"
        };

        [NotMapped]
        [Display(Name = "Is Status Change")]
        public bool IsStatusChange => PreviousStatus.HasValue && NewStatus.HasValue;

        [NotMapped]
        [Display(Name = "Is Assignment Change")]
        public bool IsAssignmentChange => !string.IsNullOrEmpty(PreviousAssignee) || !string.IsNullOrEmpty(NewAssignee);

        [NotMapped]
        [Display(Name = "Has Time Logged")]
        public bool HasTimeLogged => TimeSpentHours.HasValue && TimeSpentHours > 0;
    }
}