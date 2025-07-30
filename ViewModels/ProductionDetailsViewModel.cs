// ViewModels/ProductionDetailsViewModel.cs
using InventorySystem.Models;
using InventorySystem.Domain.Enums;

namespace InventorySystem.ViewModels
{
  /// <summary>
  /// ViewModel for the enhanced Production Details view with workflow integration
  /// </summary>
  public class ProductionDetailsViewModel
  {
    public Production Production { get; set; } = null!;
    public ProductionWorkflowResult? Workflow { get; set; }
    public ProductionTimelineResult? Timeline { get; set; }
    public List<ProductionStatus> ValidNextStatuses { get; set; } = new();
    public List<string> AvailableEmployees { get; set; } = new();
    public bool CanEditWorkflow { get; set; } = true;
    public string? CurrentUser { get; set; }

    // Quick actions flags
    public bool CanStart => Workflow?.CanBeStarted == true;
    public bool CanComplete => Workflow?.CanBeCompleted == true;
    public bool IsInQualityCheck => Workflow?.IsInQualityCheck == true;
    public bool IsOverdue => Workflow?.IsOverdue == true;

    // Display helpers
    public string StatusDisplayName => Workflow?.StatusDisplayName ?? "Unknown";
    public string ProgressPercentageDisplay => Workflow?.ProgressPercentage.ToString("F0") + "%" ?? "0%";
  }

  /// <summary>
  /// ViewModel for the enhanced Production Index view
  /// </summary>
  public class ProductionIndexViewModel
  {
    public List<ProductionSummary> ActiveProductions { get; set; } = new();
    public List<Production> AllProductions { get; set; } = new();
    public bool ShowWorkflowView { get; set; } = true;
    public WipStatistics? Statistics { get; set; }
    public ProductionIndexFilters Filters { get; set; } = new();

    // Quick stats
    public int TotalProductions => AllProductions.Count;
    public int TotalUnitsProduced => AllProductions.Sum(p => p.QuantityProduced);
    public decimal TotalValue => AllProductions.Sum(p => p.TotalCost);
    public decimal AverageUnitCost => TotalUnitsProduced > 0 ? TotalValue / TotalUnitsProduced : 0;
  }

  
  /// <summary>
  /// Filters for Production Index view
  /// </summary>
  public class ProductionIndexFilters
  {
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public ProductionStatus? Status { get; set; }
    public string? AssignedTo { get; set; }
    public string? BomNumber { get; set; }
    public bool? IsOverdue { get; set; }
    public Priority? Priority { get; set; }

    public bool HasActiveFilters => FromDate.HasValue || ToDate.HasValue || Status.HasValue ||
                                   !string.IsNullOrEmpty(AssignedTo) || !string.IsNullOrEmpty(BomNumber) ||
                                   IsOverdue.HasValue || Priority.HasValue;
  }

  /// <summary>
  /// ViewModel for workflow action modals
  /// </summary>
  public class WorkflowActionViewModel
  {
    public int ProductionId { get; set; }
    public ProductionStatus CurrentStatus { get; set; }
    public ProductionStatus TargetStatus { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string? AssignedTo { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string ActionTitle { get; set; } = null!;
    public string ActionDescription { get; set; } = null!;
  }

  /// <summary>
  /// ViewModel for quality check actions
  /// </summary>
  public class QualityCheckViewModel
  {
    public int ProductionId { get; set; }
    public bool Passed { get; set; }
    public string? Notes { get; set; }
    public int? QualityCheckerId { get; set; }
    public List<QualityCheckItem> CheckItems { get; set; } = new();
    public DateTime CheckDate { get; set; } = DateTime.Now;
  }

  /// <summary>
  /// Individual quality check item
  /// </summary>
  public class QualityCheckItem
  {
    public string Description { get; set; } = null!;
    public bool Passed { get; set; } = true;
    public string? Notes { get; set; }
    public bool IsRequired { get; set; } = true;
  }

  /// <summary>
  /// ViewModel for production assignment
  /// </summary>
  public class ProductionAssignmentViewModel
  {
    public int ProductionId { get; set; }
    public string? CurrentAssignee { get; set; }
    public string NewAssignee { get; set; } = null!;
    public List<EmployeeOption> AvailableEmployees { get; set; } = new();
    public string? Notes { get; set; }
    public bool NotifyEmployee { get; set; } = true;
  }

  /// <summary>
  /// Employee option for assignment dropdowns
  /// </summary>
  public class EmployeeOption
  {
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public int CurrentWorkload { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string? Department { get; set; }

    public string WorkloadDisplay => $"{DisplayName} ({CurrentWorkload} active)";
    public bool IsOverloaded => CurrentWorkload >= 5;
  }

  /// <summary>
  /// ViewModel for production metrics and analytics
  /// </summary>
  public class ProductionMetricsViewModel
  {
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // Throughput metrics
    public int TotalCompleted { get; set; }
    public int TotalStarted { get; set; }
    public decimal CompletionRate { get; set; }

    // Time metrics
    public decimal AverageLeadTime { get; set; }
    public decimal AverageCycleTime { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }

    // Quality metrics
    public decimal QualityPassRate { get; set; }
    public int QualityIssues { get; set; }

    // Cost metrics
    public decimal TotalProductionCost { get; set; }
    public decimal AverageCostPerUnit { get; set; }

    // Charts data
    public List<ChartDataPoint> CompletionTrend { get; set; } = new();
    public List<ChartDataPoint> StatusDistribution { get; set; } = new();
    public List<ChartDataPoint> EmployeePerformance { get; set; } = new();
  }

  /// <summary>
  /// Data point for charts
  /// </summary>
  public class ChartDataPoint
  {
    public string Label { get; set; } = null!;
    public decimal Value { get; set; }
    public string? Color { get; set; }
    public DateTime? Date { get; set; }
  }
}