using System.ComponentModel.DataAnnotations;
using InventorySystem.Models.Enums;

namespace InventorySystem.Models.CustomerService
{
    public class CaseEscalation
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Support Case")]
        public int SupportCaseId { get; set; }
        public virtual SupportCase SupportCase { get; set; } = null!;

        [Required]
        [Display(Name = "Escalation Level")]
        public EscalationLevel EscalationLevel { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Escalated By")]
        public string EscalatedBy { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Escalated To")]
        public string EscalatedTo { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        [Display(Name = "Escalation Reason")]
        public string EscalationReason { get; set; } = string.Empty;

        [Display(Name = "Escalation Date")]
        public DateTime EscalationDate { get; set; } = DateTime.Now;

        [Display(Name = "Resolution Date")]
        public DateTime? ResolutionDate { get; set; }

        [StringLength(1000)]
        [Display(Name = "Resolution Notes")]
        public string? ResolutionNotes { get; set; }

        [Display(Name = "Is Resolved")]
        public bool IsResolved => ResolutionDate.HasValue;

        [Display(Name = "Escalation Age (Hours)")]
        public int EscalationAgeHours => (DateTime.Now - EscalationDate).Hours;
    }
}