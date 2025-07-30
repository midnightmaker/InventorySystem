using InventorySystem.Domain.Enums;

namespace InventorySystem.Domain.Events
{
  public class ProductionStatusChangedEvent : IDomainEvent
  {
    public ProductionStatusChangedEvent(int productionId, ProductionStatus fromStatus, ProductionStatus toStatus, string? triggeredBy = null, string? reason = null)
    {
      ProductionId = productionId;
      FromStatus = fromStatus;
      ToStatus = toStatus;
      TriggeredBy = triggeredBy;
      Reason = reason;
      OccurredOn = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public ProductionStatus FromStatus { get; }
    public ProductionStatus ToStatus { get; }
    public string? TriggeredBy { get; }
    public string? Reason { get; }
    public DateTime OccurredOn { get; }
    public string EventType => nameof(ProductionStatusChangedEvent);
  }

  public class ProductionAssignedEvent : IDomainEvent
  {
    public ProductionAssignedEvent(int productionId, string? previousAssignee, string newAssignee, string? assignedBy = null)
    {
      ProductionId = productionId;
      PreviousAssignee = previousAssignee;
      NewAssignee = newAssignee;
      AssignedBy = assignedBy;
      OccurredOn = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public string? PreviousAssignee { get; }
    public string NewAssignee { get; }
    public string? AssignedBy { get; }
    public DateTime OccurredOn { get; }
    public string EventType => nameof(ProductionAssignedEvent);
  }

  public class WorkflowStepCompletedEvent : IDomainEvent
  {
    public WorkflowStepCompletedEvent(int productionId, ProductionStatus completedStep, TimeSpan duration, bool onTime)
    {
      ProductionId = productionId;
      CompletedStep = completedStep;
      Duration = duration;
      OnTime = onTime;
      OccurredOn = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public ProductionStatus CompletedStep { get; }
    public TimeSpan Duration { get; }
    public bool OnTime { get; }
    public DateTime OccurredOn { get; }
    public string EventType => nameof(WorkflowStepCompletedEvent);
  }

  public class QualityCheckFailedEvent : IDomainEvent
  {
    public QualityCheckFailedEvent(int productionId, string reason, int? qualityCheckerId = null)
    {
      ProductionId = productionId;
      Reason = reason;
      QualityCheckerId = qualityCheckerId;
      OccurredOn = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public string Reason { get; }
    public int? QualityCheckerId { get; }
    public DateTime OccurredOn { get; }
    public string EventType => nameof(QualityCheckFailedEvent);
  }

  public class ProductionOverdueEvent : IDomainEvent
  {
    public ProductionOverdueEvent(int productionId, DateTime originalDueDate, TimeSpan overdueBy)
    {
      ProductionId = productionId;
      OriginalDueDate = originalDueDate;
      OverdueBy = overdueBy;
      OccurredOn = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public DateTime OriginalDueDate { get; }
    public TimeSpan OverdueBy { get; }
    public DateTime OccurredOn { get; }
    public string EventType => nameof(ProductionOverdueEvent);
  }
}