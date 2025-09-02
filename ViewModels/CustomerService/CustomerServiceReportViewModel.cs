using InventorySystem.Models.CustomerService;
using InventorySystem.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.CustomerService
{
    public class CustomerServiceReportViewModel
    {
        // Report Parameters
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today.AddMonths(-1);

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today;

        [Display(Name = "Agent")]
        public string? AgentFilter { get; set; }

        [Display(Name = "Customer")]
        public int? CustomerFilter { get; set; }

        [Display(Name = "Case Type")]
        public CaseType? CaseTypeFilter { get; set; }

        [Display(Name = "Status")]
        public CaseStatus? StatusFilter { get; set; }

        [Display(Name = "Priority")]
        public CasePriority? PriorityFilter { get; set; }

        // Summary Statistics
        [Display(Name = "Total Cases")]
        public int TotalCases { get; set; }

        [Display(Name = "Cases Created")]
        public int CasesCreated { get; set; }

        [Display(Name = "Cases Resolved")]
        public int CasesResolved { get; set; }

        [Display(Name = "Cases Closed")]
        public int CasesClosed { get; set; }

        [Display(Name = "Open Cases")]
        public int OpenCases { get; set; }

        [Display(Name = "Overdue Cases")]
        public int OverdueCases { get; set; }

        [Display(Name = "Escalated Cases")]
        public int EscalatedCases { get; set; }

        // Performance Metrics
        [Display(Name = "Average Response Time (Hours)")]
        public decimal AverageResponseTimeHours { get; set; }

        [Display(Name = "Average Resolution Time (Hours)")]
        public decimal AverageResolutionTimeHours { get; set; }

        [Display(Name = "First Call Resolution Rate")]
        public decimal FirstCallResolutionRate { get; set; }

        [Display(Name = "Customer Satisfaction Average")]
        public decimal CustomerSatisfactionAverage { get; set; }

        [Display(Name = "SLA Compliance Rate")]
        public decimal SLAComplianceRate { get; set; }

        [Display(Name = "Escalation Rate")]
        public decimal EscalationRate { get; set; }

        // Breakdowns
        [Display(Name = "Cases by Status")]
        public Dictionary<CaseStatus, int> CasesByStatus { get; set; } = new();

        [Display(Name = "Cases by Priority")]
        public Dictionary<CasePriority, int> CasesByPriority { get; set; } = new();

        [Display(Name = "Cases by Type")]
        public Dictionary<CaseType, int> CasesByType { get; set; } = new();

        [Display(Name = "Cases by Channel")]
        public Dictionary<ContactChannel, int> CasesByChannel { get; set; } = new();

        // Agent Performance
        [Display(Name = "Agent Performance")]
        public List<AgentPerformanceReport> AgentPerformance { get; set; } = new();

        // Customer Analysis
        [Display(Name = "Top Customers by Case Volume")]
        public List<CustomerCaseVolumeReport> TopCustomersByVolume { get; set; } = new();

        // Trend Analysis
        [Display(Name = "Daily Case Trends")]
        public List<DailyCaseReport> DailyTrends { get; set; } = new();

        [Display(Name = "Monthly Case Trends")]
        public List<MonthlyCaseReport> MonthlyTrends { get; set; } = new();

        // Resolution Analysis
        [Display(Name = "Resolution Time Analysis")]
        public List<ResolutionTimeAnalysis> ResolutionTimeBreakdown { get; set; } = new();

        // Case Details
        [Display(Name = "Case Details")]
        public List<SupportCase> CaseDetails { get; set; } = new();

        // Computed Properties
        [Display(Name = "Resolution Rate")]
        public decimal ResolutionRate => TotalCases > 0 ? (decimal)CasesResolved / TotalCases * 100 : 0;

        [Display(Name = "Closure Rate")]
        public decimal ClosureRate => TotalCases > 0 ? (decimal)CasesClosed / TotalCases * 100 : 0;

        [Display(Name = "Period Description")]
        public string PeriodDescription => $"{StartDate:MMM dd, yyyy} - {EndDate:MMM dd, yyyy}";

        [Display(Name = "Report Generated")]
        public DateTime ReportGeneratedDate { get; set; } = DateTime.Now;
    }

    public class AgentPerformanceReport
    {
        public string AgentName { get; set; } = string.Empty;
        public int AssignedCases { get; set; }
        public int ResolvedCases { get; set; }
        public int ClosedCases { get; set; }
        public int OverdueCases { get; set; }
        public decimal AverageResponseTimeHours { get; set; }
        public decimal AverageResolutionTimeHours { get; set; }
        public decimal? AverageCustomerSatisfaction { get; set; }
        public decimal ResolutionRate => AssignedCases > 0 ? (decimal)ResolvedCases / AssignedCases * 100 : 0;
        public decimal WorkloadScore => AssignedCases > 0 ? (decimal)OverdueCases / AssignedCases * 100 : 0;
    }

    public class CustomerCaseVolumeReport
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public int TotalCases { get; set; }
        public int OpenCases { get; set; }
        public int ResolvedCases { get; set; }
        public decimal AverageResolutionTimeHours { get; set; }
        public decimal? AverageCustomerSatisfaction { get; set; }
        public string DisplayName => !string.IsNullOrEmpty(CompanyName) ? CompanyName : CustomerName;
        public decimal ResolutionRate => TotalCases > 0 ? (decimal)ResolvedCases / TotalCases * 100 : 0;
    }

    public class DailyCaseReport
    {
        public DateTime Date { get; set; }
        public int CasesCreated { get; set; }
        public int CasesResolved { get; set; }
        public int CasesClosed { get; set; }
        public decimal AverageResponseTimeHours { get; set; }
        public string DateLabel => Date.ToString("MMM dd");
        public string DateLabelShort => Date.ToString("M/d");
    }

    public class MonthlyCaseReport
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public int CasesCreated { get; set; }
        public int CasesResolved { get; set; }
        public int CasesClosed { get; set; }
        public decimal AverageResponseTimeHours { get; set; }
        public decimal AverageResolutionTimeHours { get; set; }
        public decimal? AverageCustomerSatisfaction { get; set; }
        public string MonthLabel => $"{MonthName} {Year}";
    }

    public class ResolutionTimeAnalysis
    {
        public string TimeRange { get; set; } = string.Empty;
        public int CaseCount { get; set; }
        public decimal Percentage { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}