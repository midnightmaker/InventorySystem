// ViewModels/ServiceViewModels.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    public class CreateServiceOrderViewModel
    {
        [Required(ErrorMessage = "Customer is required")]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Service type is required")]
        [Display(Name = "Service Type")]
        public int ServiceTypeId { get; set; }

        [Required(ErrorMessage = "Request date is required")]
        [Display(Name = "Request Date")]
        [DataType(DataType.Date)]
        public DateTime RequestDate { get; set; } = DateTime.Today;

        [Display(Name = "Promised Date")]
        [DataType(DataType.Date)]
        public DateTime? PromisedDate { get; set; }

        [Display(Name = "Priority")]
        public ServicePriority Priority { get; set; } = ServicePriority.Normal;

        [Display(Name = "Customer Request")]
        [StringLength(1000, ErrorMessage = "Customer request cannot exceed 1000 characters")]
        public string? CustomerRequest { get; set; }

        [Display(Name = "Equipment/Asset Details")]
        [StringLength(200)]
        public string? EquipmentDetails { get; set; }

        [Display(Name = "Serial Number")]
        [StringLength(100)]
        public string? SerialNumber { get; set; }

        [Display(Name = "Model Number")]
        [StringLength(100)]
        public string? ModelNumber { get; set; }

        [Display(Name = "Is Prepaid")]
        public bool IsPrepaid { get; set; }

        [Display(Name = "Payment Method")]
        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        [Display(Name = "Related Sale ID")]
        public int? SaleId { get; set; }

        [Display(Name = "Initial Notes")]
        [StringLength(2000)]
        public string? ServiceNotes { get; set; }

        // Dropdowns
        public IEnumerable<SelectListItem> CustomerOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> ServiceTypeOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> SaleOptions { get; set; } = new List<SelectListItem>();

        // Selected items for display
        public string? SelectedCustomerName { get; set; }
        public string? SelectedServiceTypeName { get; set; }
        public decimal? EstimatedCost { get; set; }
        public decimal? EstimatedHours { get; set; }
    }

    public class ServiceOrderListViewModel
    {
        public IEnumerable<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();
        public IEnumerable<ServiceType> ServiceTypes { get; set; } = new List<ServiceType>();
        public IEnumerable<Customer> Customers { get; set; } = new List<Customer>();

        // Filters
        public string? SearchTerm { get; set; }
        public ServiceOrderStatus? StatusFilter { get; set; }
        public ServicePriority? PriorityFilter { get; set; }
        public int? CustomerFilter { get; set; }
        public int? ServiceTypeFilter { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool ShowOverdueOnly { get; set; }
        public string? AssignedTechnicianFilter { get; set; }

        // Statistics
        public int TotalServiceOrders { get; set; }
        public int CompletedThisMonth { get; set; }
        public int InProgressCount { get; set; }
        public int OverdueCount { get; set; }
        public decimal TotalRevenueThisMonth { get; set; }
        public decimal AverageHoursPerService { get; set; }
    }

    public class ServiceOrderDetailsViewModel
    {
        public ServiceOrder ServiceOrder { get; set; } = null!;
        public IEnumerable<ServiceTimeLog> TimeLogs { get; set; } = new List<ServiceTimeLog>();
        public IEnumerable<ServiceMaterial> Materials { get; set; } = new List<ServiceMaterial>();
        public IEnumerable<ServiceDocument> Documents { get; set; } = new List<ServiceDocument>();

        // Related data
        public Customer Customer { get; set; } = null!;
        public ServiceType ServiceType { get; set; } = null!;
        public Sale? RelatedSale { get; set; }

        // Status change options
        public IEnumerable<ServiceOrderStatus> AvailableStatusChanges { get; set; } = new List<ServiceOrderStatus>();
        public string? StatusChangeReason { get; set; }

        // Quick actions
        public bool CanEdit { get; set; } = true;
        public bool CanDelete { get; set; }
        public bool CanStart { get; set; }
        public bool CanComplete { get; set; }
        public bool CanSchedule { get; set; }
        public bool CanCancel { get; set; }

        // Calculated fields
        public decimal TotalLaborCost { get; set; }
        public decimal TotalMaterialCost { get; set; }
        public decimal TotalServiceCost { get; set; }
        public decimal EstimatedProfit { get; set; }
        public TimeSpan? TimeToComplete { get; set; }
    }

    public class ServiceSchedulingViewModel
    {
        public IEnumerable<ServiceOrder> ScheduledServices { get; set; } = new List<ServiceOrder>();
        public IEnumerable<ServiceOrder> UnscheduledServices { get; set; } = new List<ServiceOrder>();
        public IEnumerable<string> AvailableTechnicians { get; set; } = new List<string>();

        public DateTime ScheduleDate { get; set; } = DateTime.Today;
        public string? SelectedTechnician { get; set; }

        // Calendar/scheduling data
        public Dictionary<DateTime, List<ServiceOrder>> WeeklySchedule { get; set; } = new();
        public Dictionary<string, List<ServiceOrder>> TechnicianWorkload { get; set; } = new();

        // Statistics
        public int TotalScheduledToday { get; set; }
        public int TotalUnscheduled { get; set; }
        public decimal AverageHoursPerDay { get; set; }
        public Dictionary<string, decimal> TechnicianUtilization { get; set; } = new();
    }

    public class AddTimeLogViewModel
    {
        [Required]
        public int ServiceOrderId { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Technician is required")]
        [Display(Name = "Technician")]
        [StringLength(100)]
        public string Technician { get; set; } = string.Empty;

        [Required(ErrorMessage = "Hours are required")]
        [Display(Name = "Hours Worked")]
        [Range(0.1, 24, ErrorMessage = "Hours must be between 0.1 and 24")]
        public decimal Hours { get; set; }

        [Display(Name = "Hourly Rate")]
        [Range(0, 500, ErrorMessage = "Rate must be between 0 and 500")]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Work Description")]
        [StringLength(1000)]
        public string? WorkDescription { get; set; }

        [Display(Name = "Is Billable")]
        public bool IsBillable { get; set; } = true;

        // For display
        public ServiceOrder? ServiceOrder { get; set; }
        public IEnumerable<SelectListItem> TechnicianOptions { get; set; } = new List<SelectListItem>();
    }

    public class AddMaterialViewModel
    {
        [Required]
        public int ServiceOrderId { get; set; }

        [Required(ErrorMessage = "Item is required")]
        [Display(Name = "Material/Part")]
        public int ItemId { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Display(Name = "Quantity Used")]
        [Range(0.01, 10000, ErrorMessage = "Quantity must be greater than 0")]
        public decimal QuantityUsed { get; set; }

        [Display(Name = "Unit Cost")]
        [Range(0, 10000, ErrorMessage = "Cost cannot be negative")]
        public decimal UnitCost { get; set; }

        [Display(Name = "Is Billable")]
        public bool IsBillable { get; set; } = true;

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }

        // For display
        public ServiceOrder? ServiceOrder { get; set; }
        public IEnumerable<SelectListItem> ItemOptions { get; set; } = new List<SelectListItem>();
        public Item? SelectedItem { get; set; }
    }

    public class ServiceReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Summary Statistics
        public int TotalServiceOrders { get; set; }
        public int CompletedServiceOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalLaborHours { get; set; }
        public decimal AverageServiceTime { get; set; }
        public decimal CustomerSatisfactionRate { get; set; }

        // Breakdowns
        public Dictionary<string, int> ServicesByType { get; set; } = new();
        public Dictionary<string, decimal> RevenueByType { get; set; } = new();
        public Dictionary<string, int> ServicesByTechnician { get; set; } = new();
        public Dictionary<string, decimal> TechnicianUtilization { get; set; } = new();

        // Trend Data
        public List<ServiceTrendData> MonthlyTrends { get; set; } = new();
        public List<ServicePerformanceData> Performance { get; set; } = new();

        // Top Items
        public List<ServiceOrder> TopRevenueServices { get; set; } = new();
        public List<Item> MostUsedMaterials { get; set; } = new();
        public List<Customer> TopServiceCustomers { get; set; } = new();
    }

    public class ServiceTrendData
    {
        public DateTime Month { get; set; }
        public int ServiceCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal AverageHours { get; set; }
        public decimal CompletionRate { get; set; }
    }

    public class ServicePerformanceData
    {
        public string Technician { get; set; } = string.Empty;
        public int ServicesCompleted { get; set; }
        public decimal TotalHours { get; set; }
        public decimal Revenue { get; set; }
        public decimal AverageRating { get; set; }
        public decimal UtilizationRate { get; set; }
    }

    public class ServiceDashboardViewModel
    {
        // Quick Stats
        public int TotalActiveServices { get; set; }
        public int ServicesScheduledToday { get; set; }
        public int OverdueServices { get; set; }
        public int EmergencyServices { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal AvgServiceTime { get; set; }

        // Recent Activity
        public List<ServiceOrder> RecentServiceOrders { get; set; } = new();
        public List<ServiceOrder> TodaysSchedule { get; set; } = new();
        public List<ServiceOrder> OverdueList { get; set; } = new();

        // Charts Data
        public Dictionary<string, int> ServicesByStatus { get; set; } = new();
        public Dictionary<string, int> ServicesByPriority { get; set; } = new();
        public List<ServiceTrendData> RecentTrends { get; set; } = new();

        // Performance Metrics
        public decimal AvgCompletionTime { get; set; }
        public decimal OnTimeCompletionRate { get; set; }
        public decimal CustomerSatisfactionRate { get; set; }
        public Dictionary<string, decimal> TechnicianUtilization { get; set; } = new();

        // Alerts and Notifications
        public List<string> SystemAlerts { get; set; } = new();
        public int PendingApprovals { get; set; }
        public int ServicesAwaitingParts { get; set; }
    }

   
    public class UpdateServiceStatusViewModel
    {
        [Required]
        public int ServiceOrderId { get; set; }

        [Required]
        [Display(Name = "New Status")]
        public ServiceOrderStatus NewStatus { get; set; }

        [Display(Name = "Reason for Change")]
        [StringLength(500)]
        public string? Reason { get; set; }

        [Display(Name = "Completion Notes")]
        [StringLength(1000)]
        public string? CompletionNotes { get; set; }

        [Display(Name = "Schedule Date/Time")]
        public DateTime? ScheduledDateTime { get; set; }

        [Display(Name = "Assigned Technician")]
        [StringLength(100)]
        public string? AssignedTechnician { get; set; }

        // For display
        public ServiceOrder? ServiceOrder { get; set; }
        public IEnumerable<SelectListItem> TechnicianOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<ServiceOrderStatus> ValidStatuses { get; set; } = new List<ServiceOrderStatus>();
    }
}