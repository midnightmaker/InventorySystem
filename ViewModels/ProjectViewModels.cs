// ViewModels/ProjectViewModels.cs - View models for R&D project management
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class ProjectIndexViewModel
    {
        public List<Project> Projects { get; set; } = new();
        public ProjectFilterOptions FilterOptions { get; set; } = new();
        public ProjectSummaryStatistics SummaryStats { get; set; } = new();
    }

    public class ProjectFilterOptions
    {
        public string? SearchTerm { get; set; }
        public ProjectStatus? Status { get; set; }
        public ProjectType? ProjectType { get; set; }
        public string? Department { get; set; }
        public ProjectPriority? Priority { get; set; }
        public bool? IsOverBudget { get; set; }
    }

    public class ProjectSummaryStatistics
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal OverallBudgetUtilization { get; set; }
        public int ProjectsOverBudget { get; set; }
    }

    public class ProjectDetailsViewModel
    {
        public Project Project { get; set; } = new();
        public List<Purchase> RecentPurchases { get; set; } = new();
        public List<Purchase> PendingPurchases { get; set; } = new();
        public ProjectFinancialSummary FinancialSummary { get; set; } = new();
        public List<MonthlySpending> MonthlySpending { get; set; } = new();
    }

    public class ProjectFinancialSummary
    {
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal RemainingBudget { get; set; }
        public decimal BudgetUtilization { get; set; }
        public bool IsOverBudget { get; set; }
        public int TotalPurchases { get; set; }
        public decimal AverageTransactionSize { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
    }

    public class MonthlySpending
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }

    public class CreateProjectViewModel
    {
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
        public ProjectType ProjectType { get; set; }

        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "Expected End Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedEndDate { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Budget must be greater than or equal to 0")]
        [Display(Name = "Budget")]
        public decimal Budget { get; set; }

        [StringLength(100)]
        [Display(Name = "Project Manager")]
        public string? ProjectManager { get; set; }

        [StringLength(100)]
        [Display(Name = "Department")]
        public string? Department { get; set; }

        [Display(Name = "Priority")]
        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

        [StringLength(2000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }
    }

    public class ProjectReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Department { get; set; }
        public ProjectType? ProjectType { get; set; }
        public List<Project> Projects { get; set; } = new();
        public ProjectReportSummary Summary { get; set; } = new();
        public List<ProjectStatusBreakdown> StatusBreakdown { get; set; } = new();
        public List<ProjectTypeBreakdown> TypeBreakdown { get; set; } = new();
        public List<DepartmentBreakdown> DepartmentBreakdown { get; set; } = new();
    }

    public class ProjectReportSummary
    {
        public int TotalProjects { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal AverageBudget { get; set; }
        public decimal AverageSpent { get; set; }
        public int OverBudgetCount { get; set; }
        public double OverBudgetPercentage { get; set; }
    }

    public class ProjectStatusBreakdown
    {
        public ProjectStatus Status { get; set; }
        public int Count { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class ProjectTypeBreakdown
    {
        public ProjectType Type { get; set; }
        public string TypeDisplayName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class DepartmentBreakdown
    {
        public string Department { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class ProjectDashboardViewModel
    {
        public List<Project> RecentProjects { get; set; } = new();
        public ProjectSummaryStatistics SummaryStats { get; set; } = new();
        public List<MonthlySpending> MonthlyTrend { get; set; } = new();
        public List<ProjectTypeBreakdown> TypeDistribution { get; set; } = new();
        public List<ProjectBudgetUtilization> BudgetUtilization { get; set; } = new();
        public List<Project> OverBudgetProjects { get; set; } = new();
    }

    public class ProjectBudgetUtilization
    {
        public string ProjectCode { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public decimal Budget { get; set; }
        public decimal Spent { get; set; }
        public decimal Utilization { get; set; }
        public bool IsOverBudget { get; set; }
    }
}