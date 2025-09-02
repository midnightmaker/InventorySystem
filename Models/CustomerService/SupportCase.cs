using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

namespace InventorySystem.Models.CustomerService
{
    public class SupportCase
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Case Number")]
        public string CaseNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Case Status")]
        public CaseStatus Status { get; set; } = CaseStatus.Open;

        [Required]
        [Display(Name = "Priority")]
        public CasePriority Priority { get; set; } = CasePriority.Medium;

        [Required]
        [Display(Name = "Case Type")]
        public CaseType CaseType { get; set; } = CaseType.GeneralInquiry;

        [Required]
        [Display(Name = "Channel")]
        public ContactChannel Channel { get; set; } = ContactChannel.Email;

        // Customer Information
        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; } = null!;

        // Assignment
        [StringLength(100)]
        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [StringLength(100)]
        [Display(Name = "Last Modified By")]
        public string? LastModifiedBy { get; set; }

        // Contact Information
        [Required]
        [StringLength(200)]
        [Display(Name = "Contact Name")]
        public string ContactName { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        [EmailAddress]
        [Display(Name = "Contact Email")]
        public string ContactEmail { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Contact Phone")]
        public string? ContactPhone { get; set; }

        // Related Sales/Orders
        [Display(Name = "Related Sale")]
        public int? RelatedSaleId { get; set; }
        public virtual Sale? RelatedSale { get; set; }

        [Display(Name = "Related Service Order")]
        public int? RelatedServiceOrderId { get; set; }
        public virtual ServiceOrder? RelatedServiceOrder { get; set; }

        // Product Information
        [Display(Name = "Related Product")]
        public int? RelatedProductId { get; set; }
        public virtual Item? RelatedProduct { get; set; }

        [StringLength(100)]
        [Display(Name = "Product Serial Number")]
        public string? ProductSerialNumber { get; set; }

        // Timeline
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Last Modified Date")]
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;

        [Display(Name = "First Response Date")]
        public DateTime? FirstResponseDate { get; set; }

        [Display(Name = "Resolution Date")]
        public DateTime? ResolutionDate { get; set; }

        [Display(Name = "Closed Date")]
        public DateTime? ClosedDate { get; set; }

        // SLA Tracking
        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Is Overdue")]
        public bool IsOverdue => DueDate.HasValue && DueDate < DateTime.Now && !IsResolved;

        // Resolution Information
        [StringLength(2000)]
        [Display(Name = "Resolution Notes")]
        public string? ResolutionNotes { get; set; }

        [Display(Name = "Customer Satisfaction")]
        [Range(1, 5)]
        public int? CustomerSatisfactionRating { get; set; }

        [StringLength(1000)]
        [Display(Name = "Customer Feedback")]
        public string? CustomerFeedback { get; set; }

        // Internal Notes
        [StringLength(2000)]
        [Display(Name = "Internal Notes")]
        public string? InternalNotes { get; set; }

        // Tags for categorization
        [StringLength(500)]
        [Display(Name = "Tags")]
        public string? Tags { get; set; }

        // Navigation Properties
        public virtual ICollection<CaseUpdate> CaseUpdates { get; set; } = new List<CaseUpdate>();
        public virtual ICollection<CaseDocument> CaseDocuments { get; set; } = new List<CaseDocument>();
        public virtual ICollection<CaseEscalation> CaseEscalations { get; set; } = new List<CaseEscalation>();

        // Computed Properties
        [NotMapped]
        [Display(Name = "Is Resolved")]
        public bool IsResolved => Status == CaseStatus.Resolved || Status == CaseStatus.Closed;

        [NotMapped]
        [Display(Name = "Is Open")]
        public bool IsOpen => Status == CaseStatus.Open || Status == CaseStatus.InProgress || Status == CaseStatus.Escalated;

        [NotMapped]
        [Display(Name = "Age (Days)")]
        public int AgeDays => (DateTime.Now - CreatedDate).Days;

        [NotMapped]
        [Display(Name = "Response Time (Hours)")]
        public int? ResponseTimeHours => FirstResponseDate?.Subtract(CreatedDate).Hours;

        [NotMapped]
        [Display(Name = "Resolution Time (Hours)")]
        public int? ResolutionTimeHours => ResolutionDate?.Subtract(CreatedDate).Hours;

        [NotMapped]
        [Display(Name = "Priority Color")]
        public string PriorityColor => Priority switch
        {
            CasePriority.Critical => "danger",
            CasePriority.High => "warning",
            CasePriority.Medium => "info",
            CasePriority.Low => "secondary",
            _ => "secondary"
        };

        [NotMapped]
        [Display(Name = "Status Color")]
        public string StatusColor => Status switch
        {
            CaseStatus.Open => "primary",
            CaseStatus.InProgress => "warning", 
            CaseStatus.Escalated => "danger",
            CaseStatus.Resolved => "success",
            CaseStatus.Closed => "secondary",
            CaseStatus.OnHold => "info",
            _ => "secondary"
        };

        [NotMapped]
        [Display(Name = "Tags List")]
        public List<string> TagsList => string.IsNullOrEmpty(Tags) 
            ? new List<string>() 
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();

        [NotMapped]
        [Display(Name = "Has Updates")]
        public bool HasUpdates => CaseUpdates?.Any() == true;

        [NotMapped]
        [Display(Name = "Latest Update")]
        public DateTime? LatestUpdateDate => CaseUpdates?.Max(u => u.UpdateDate);

        [NotMapped]
        [Display(Name = "Update Count")]
        public int UpdateCount => CaseUpdates?.Count ?? 0;
    }
}