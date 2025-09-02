using InventorySystem.Models.CustomerService;
using InventorySystem.Models.Enums;

namespace InventorySystem.ViewModels.CustomerService
{
    public class CustomerServiceDashboardViewModel
    {
        // Summary Statistics
        public int TotalOpenCases { get; set; }
        public int TotalCasesToday { get; set; }
        public int TotalOverdueCases { get; set; }
        public int TotalUnassignedCases { get; set; }
        public int TotalEscalatedCases { get; set; }
        
        // Performance Metrics
        public decimal AverageResponseTimeHours { get; set; }
        public decimal AverageResolutionTimeHours { get; set; }
        public decimal CustomerSatisfactionAverage { get; set; }
        public decimal FirstCallResolutionRate { get; set; }
        
        // Case Distribution
        public Dictionary<CaseStatus, int> CasesByStatus { get; set; } = new();
        public Dictionary<CasePriority, int> CasesByPriority { get; set; } = new();
        public Dictionary<CaseType, int> CasesByType { get; set; } = new();
        public Dictionary<ContactChannel, int> CasesByChannel { get; set; } = new();
        
        // Recent Activity
        public List<SupportCase> RecentCases { get; set; } = new();
        public List<SupportCase> MyCases { get; set; } = new();
        public List<SupportCase> HighPriorityCases { get; set; } = new();
        public List<SupportCase> OverdueCases { get; set; } = new();
        
        // Trends (last 30 days)
        public List<DailyCaseStats> DailyStats { get; set; } = new();
        
        // Top Customers by Case Volume
        public List<CustomerCaseStats> TopCustomersByVolume { get; set; } = new();
        
        // Team Performance
        public List<AgentPerformanceStats> AgentStats { get; set; } = new();
    }

    public class DailyCaseStats
    {
        public DateTime Date { get; set; }
        public int CasesCreated { get; set; }
        public int CasesResolved { get; set; }
        public string DateLabel => Date.ToString("MMM dd");
    }

    public class CustomerCaseStats
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public int TotalCases { get; set; }
        public int OpenCases { get; set; }
        public decimal AverageResolutionTime { get; set; }
        public decimal? AverageSatisfaction { get; set; }
        public string DisplayName => !string.IsNullOrEmpty(CompanyName) ? CompanyName : CustomerName;
    }

    public class AgentPerformanceStats
    {
        public string AgentName { get; set; } = string.Empty;
        public int AssignedCases { get; set; }
        public int ResolvedCases { get; set; }
        public decimal AverageResolutionTime { get; set; }
        public decimal? AverageSatisfaction { get; set; }
        public int OverdueCases { get; set; }
        public decimal ResolutionRate => AssignedCases > 0 ? (decimal)ResolvedCases / AssignedCases * 100 : 0;
    }
}