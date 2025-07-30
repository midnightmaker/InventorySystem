// Domain/Entities/Production/ProductionWorkflow.cs
using InventorySystem.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Domain.Entities.Production
{
  public class ProductionWorkflow
  {
    public int Id { get; set; }

    [Required]
    public int ProductionId { get; set; }

    [Required]
    public ProductionStatus Status { get; set; }

    public ProductionStatus? PreviousStatus { get; set; }

    public Priority Priority { get; set; } = Priority.Normal;

    [StringLength(100)]
    public string? AssignedTo { get; set; }

    [StringLength(100)]
    public string? AssignedBy { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? EstimatedCompletionDate { get; set; }

    public DateTime? ActualStartDate { get; set; }

    public DateTime? ActualEndDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [StringLength(500)]
    public string? QualityCheckNotes { get; set; }

    public bool QualityCheckPassed { get; set; } = true;

    public int? QualityCheckerId { get; set; }

    public DateTime? QualityCheckDate { get; set; }

    [StringLength(200)]
    public string? OnHoldReason { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? LastModifiedBy { get; set; }

    // Navigation properties
    public virtual Models.Production Production { get; set; } = null!;
    public virtual ICollection<WorkflowTransition> WorkflowTransitions { get; set; } = new List<WorkflowTransition>();

    // Calculated properties
    public TimeSpan? Duration => ActualEndDate.HasValue && ActualStartDate.HasValue
        ? ActualEndDate.Value - ActualStartDate.Value
        : null;

    public bool IsOverdue => EstimatedCompletionDate.HasValue
        && DateTime.UtcNow > EstimatedCompletionDate.Value
        && Status != ProductionStatus.Completed
        && Status != ProductionStatus.Cancelled;

    public bool IsInProgress => Status == ProductionStatus.InProgress;

    public bool IsCompleted => Status == ProductionStatus.Completed;

    public bool CanTransitionTo(ProductionStatus newStatus)
    {
      return WorkflowRules.IsValidTransition(Status, newStatus);
    }
  }

  public static class WorkflowRules
  {
    private static readonly Dictionary<ProductionStatus, List<ProductionStatus>> ValidTransitions = new()
        {
            {
                ProductionStatus.Planned,
                new List<ProductionStatus> { ProductionStatus.InProgress, ProductionStatus.OnHold, ProductionStatus.Cancelled }
            },
            {
                ProductionStatus.InProgress,
                new List<ProductionStatus> { ProductionStatus.QualityCheck, ProductionStatus.OnHold, ProductionStatus.Cancelled }
            },
            {
                ProductionStatus.QualityCheck,
                new List<ProductionStatus> { ProductionStatus.Completed, ProductionStatus.InProgress, ProductionStatus.OnHold }
            },
            {
                ProductionStatus.OnHold,
                new List<ProductionStatus> { ProductionStatus.InProgress, ProductionStatus.Cancelled }
            },
            {
                ProductionStatus.Completed,
                new List<ProductionStatus>() // Terminal state
            },
            {
                ProductionStatus.Cancelled,
                new List<ProductionStatus>() // Terminal state
            }
        };

    public static bool IsValidTransition(ProductionStatus from, ProductionStatus to)
    {
      return ValidTransitions.ContainsKey(from) && ValidTransitions[from].Contains(to);
    }

    public static List<ProductionStatus> GetValidNextStatuses(ProductionStatus currentStatus)
    {
      return ValidTransitions.ContainsKey(currentStatus) ? ValidTransitions[currentStatus] : new List<ProductionStatus>();
    }
  }
}