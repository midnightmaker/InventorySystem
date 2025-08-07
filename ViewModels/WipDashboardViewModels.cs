// ViewModels/WipDashboardViewModels.cs
using InventorySystem.Domain.Enums;

namespace InventorySystem.ViewModels
{
  /// <summary>
  /// Main result object for WIP Dashboard data
  /// </summary>
  public class WipDashboardResult
  {
    public Dictionary<ProductionStatus, List<ProductionSummary>> ProductionsByStatus { get; set; } = new();
    public WipStatistics Statistics { get; set; } = new();
    public List<ProductionSummary> OverdueProductions { get; set; } = new();
    public List<ProductionSummary> CompletedToday { get; set; } = new();
    public List<string> AvailableEmployees { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
  }

  /// <summary>
  /// Summary information for a production in the WIP system
  /// </summary>
  public class ProductionSummary
  {
    public int ProductionId { get; set; }
    public string BomNumber { get; set; } = null!;
    public string? FinishedGoodName { get; set; }
    public int Quantity { get; set; }
    public ProductionStatus Status { get; set; }
    public Priority Priority { get; set; } = Priority.Normal;
    public string? AssignedTo { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public bool IsOverdue { get; set; }
    public decimal? ProgressPercentage { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }

    // Calculated properties
    public TimeSpan? TimeInCurrentStatus => StartedAt.HasValue ? DateTime.UtcNow - StartedAt.Value : null;
    public string StatusDisplayName => Status.ToString().Replace("InProgress", "In Progress");
    public string PriorityDisplayName => Priority.ToString();
    public bool IsHighPriority => Priority == Priority.High || Priority == Priority.Critical;
  }

  /// <summary>
  /// Statistical data for the WIP dashboard
  /// </summary>
  public class WipStatistics
  {
    // Current Status Counts
    public int TotalActiveProductions { get; set; }
    public int PlannedCount { get; set; }
    public int InProgressCount { get; set; }
    public int QualityCheckCount { get; set; }
    public int OnHoldCount { get; set; }
    public int CompletedTodayCount { get; set; }
    public int OverdueCount { get; set; }

    // Performance Metrics
    public decimal AverageCompletionTime { get; set; } // in hours
    public decimal OnTimeCompletionRate { get; set; } // percentage
    public decimal AverageTimeInStatus { get; set; } // in hours
    public decimal ProductionEfficiency { get; set; } // percentage

    // Workload Distribution
    public Dictionary<string, int> WorkloadByEmployee { get; set; } = new();
    public int UnassignedProductionsCount { get; set; }

    // Quality Metrics
    public decimal QualityPassRate { get; set; } // percentage
    public int QualityIssuesThisWeek { get; set; }

    // Cost Metrics
    public decimal TotalValueInProgress { get; set; }
    public decimal AverageProductionValue { get; set; }

    // Calculated Properties
    public int TotalCompletedThisWeek { get; set; }
    public int TotalStartedThisWeek { get; set; }
    public decimal WeeklyCompletionRate => TotalStartedThisWeek > 0 ?
        (decimal)TotalCompletedThisWeek / TotalStartedThisWeek * 100 : 0;
  }

  /// <summary>
  /// Detailed workflow information for a specific production
  /// </summary>
  public class ProductionWorkflowResult
  {
    public int ProductionId { get; set; }
    public string BomNumber { get; set; } = null!;
    public string? FinishedGoodName { get; set; }
    public int Quantity { get; set; }
    public ProductionStatus Status { get; set; }
    public Priority Priority { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedBy { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public bool IsOverdue { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? Notes { get; set; }
    public string? QualityCheckNotes { get; set; }
    public bool QualityCheckPassed { get; set; } = true;
    public DateTime? QualityCheckDate { get; set; }
    public string? OnHoldReason { get; set; }

    // Collections
    public List<WorkflowTransitionSummary> Transitions { get; set; } = new();
    public List<ProductionStatus> ValidNextStatuses { get; set; } = new();

    // Calculated Properties
    public decimal ProgressPercentage => CalculateProgressPercentage();
    public string StatusDisplayName => Status.ToString().Replace("InProgress", "In Progress");
    public bool CanBeStarted => Status == ProductionStatus.Planned;
    public bool CanBeCompleted => Status == ProductionStatus.QualityCheck && QualityCheckPassed;
    public bool IsInQualityCheck => Status == ProductionStatus.QualityCheck;

    private decimal CalculateProgressPercentage()
    {
      return Status switch
      {
        ProductionStatus.Planned => 0,
        ProductionStatus.InProgress => 50,
        ProductionStatus.QualityCheck => 85,
        ProductionStatus.Completed => 100,
        ProductionStatus.OnHold => 25, // Partial progress
        ProductionStatus.Cancelled => 0,
        _ => 0
      };
    }
  }

  /// <summary>
  /// Summary of a workflow transition for timeline display
  /// </summary>
  public class WorkflowTransitionSummary
  {
    public int Id { get; set; }
    public ProductionStatus FromStatus { get; set; }
    public ProductionStatus ToStatus { get; set; }
    public DateTime TransitionDate { get; set; }
    public string? TriggeredBy { get; set; }
    public string? Reason { get; set; }
    public decimal? DurationInMinutes { get; set; }
    public string? Notes { get; set; }
    public WorkflowEventType EventType { get; set; }

    // Display Properties
    public string FromStatusDisplay => FromStatus.ToString().Replace("InProgress", "In Progress");
    public string ToStatusDisplay => ToStatus.ToString().Replace("InProgress", "In Progress");
    public string DurationDisplay => DurationInMinutes.HasValue ?
        TimeSpan.FromMinutes((double)DurationInMinutes.Value).ToString(@"hh\:mm") : "N/A";
    public string TransitionDescription => $"Changed from {FromStatusDisplay} to {ToStatusDisplay}";
  }

  /// <summary>
  /// Timeline event for production history
  /// </summary>
  public class ProductionTimelineResult
  {
    public int ProductionId { get; set; }
    public List<TimelineEvent> Events { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
    public ProductionWorkflowResult? CurrentWorkflow { get; set; }
  }

  /// <summary>
  /// Individual timeline event
  /// </summary>
  public class TimelineEvent
  {
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? TriggeredBy { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public object? AdditionalData { get; set; }

    // Display Properties
    public string TimeDisplay => Timestamp.ToString("yyyy-MM-dd HH:mm");
    public string RelativeTimeDisplay => GetRelativeTime();

    private string GetRelativeTime()
    {
      var timeSpan = DateTime.UtcNow - Timestamp;

      if (timeSpan.TotalDays >= 1)
        return $"{(int)timeSpan.TotalDays} days ago";
      if (timeSpan.TotalHours >= 1)
        return $"{(int)timeSpan.TotalHours} hours ago";
      if (timeSpan.TotalMinutes >= 1)
        return $"{(int)timeSpan.TotalMinutes} minutes ago";

      return "Just now";
    }
  }

  /// <summary>
  /// Employee workload information
  /// </summary>
  public class EmployeeWorkload
  {
    public string EmployeeName { get; set; } = null!;
    public int ActiveProductionsCount { get; set; }
    public int CompletedThisWeek { get; set; }
    public decimal AverageCompletionTime { get; set; }
    public decimal EfficiencyRating { get; set; }
    public List<ProductionSummary> CurrentAssignments { get; set; } = new();
    public bool IsOverloaded => ActiveProductionsCount > 5; // Business rule
  }

  /// <summary>
  /// Performance analytics for management reporting
  /// </summary>
  public class WipPerformanceAnalytics
  {
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }

    // Throughput Metrics
    public int TotalProductionsCompleted { get; set; }
    public int TotalProductionsStarted { get; set; }
    public decimal ThroughputRate { get; set; }

    // Time Metrics
    public decimal AverageLeadTime { get; set; } // Start to finish
    public decimal AverageCycleTime { get; set; } // Active work time
    public decimal AverageWaitTime { get; set; } // Time between stages

    // Quality Metrics
    public int QualityChecksPassed { get; set; }
    public int QualityChecksFailed { get; set; }
    public decimal QualityPassRate { get; set; }
    public decimal ReworkRate { get; set; }

    // Efficiency Metrics
    public decimal OverallEquipmentEffectiveness { get; set; }
    public decimal ScheduleAdherence { get; set; }
    public int OnTimeDeliveries { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }

    // Cost Metrics
    public decimal TotalProductionCost { get; set; }
    public decimal AverageCostPerUnit { get; set; }
    public decimal LaborEfficiency { get; set; }

    // Bottleneck Analysis
    public Dictionary<ProductionStatus, decimal> AverageTimeInStatus { get; set; } = new();
    public ProductionStatus? BottleneckStatus { get; set; }
    public List<string> ImprovementRecommendations { get; set; } = new();
  }

  /// <summary>
  /// Filters for WIP dashboard queries
  /// </summary>
  public class WipDashboardFilters
  {
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? AssignedTo { get; set; }
    public ProductionStatus? Status { get; set; }
    public Priority? Priority { get; set; }
    public bool? IsOverdue { get; set; }
    public List<string>? BomNumbers { get; set; }
    public List<string>? FinishedGoodNames { get; set; }

    // Pagination
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    // Sorting
    public string SortBy { get; set; } = "CreatedDate";
    public bool SortDescending { get; set; } = true;
  }

  // Add the WorkflowEventType enum if it doesn't exist
  public enum WorkflowEventType
  {
    StatusChange,
    Assignment,
    QualityCheck,
    Note,
    Hold,
    Resume,
    Cancel,
    Complete
  }
}