// Models/Project.cs - R&D Project tracking
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InventorySystem.Models.Enums;

namespace InventorySystem.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Project Code")]
        public string ProjectCode { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Display(Name = "Project Name")]
        public string ProjectName { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Project Type")]
        public ProjectType ProjectType { get; set; } = ProjectType.Research;

        [Required]
        [Display(Name = "Status")]
        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "Expected End Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedEndDate { get; set; }

        [Display(Name = "Actual End Date")]
        [DataType(DataType.Date)]
        public DateTime? ActualEndDate { get; set; }

        [Display(Name = "Budget")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue)]
        public decimal Budget { get; set; }

        [Display(Name = "Project Manager")]
        [StringLength(100)]
        public string? ProjectManager { get; set; }

        [Display(Name = "Department")]
        [StringLength(100)]
        public string? Department { get; set; }

        [Display(Name = "Priority")]
        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

        [StringLength(2000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        [StringLength(100)]
        public string? LastModifiedBy { get; set; }

        // Navigation properties
        public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
        
        // Computed properties
        [NotMapped]
        public decimal TotalSpent => Purchases?.Sum(p => p.ExtendedTotal) ?? 0;

        [NotMapped]
        public decimal RemainingBudget => Budget - TotalSpent;

        [NotMapped]
        public decimal BudgetUtilization => Budget > 0 ? (TotalSpent / Budget) * 100 : 0;

        [NotMapped]
        public bool IsOverBudget => TotalSpent > Budget;

        [NotMapped]
        public int PurchaseCount => Purchases?.Count ?? 0;

        [NotMapped]
        public bool IsActive => Status == ProjectStatus.Active || Status == ProjectStatus.Planning;
    }
}