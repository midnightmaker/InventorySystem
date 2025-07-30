// Domain/Entities/Production/WorkflowTransition.cs
using InventorySystem.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Domain.Entities.Production
{
  public class WorkflowTransition
  {
    public int Id { get; set; }

    [Required]
    public int ProductionWorkflowId { get; set; }

    [Required]
    public ProductionStatus FromStatus { get; set; }

    [Required]
    public ProductionStatus ToStatus { get; set; }

    [Required]
    public WorkflowEventType EventType { get; set; }

    [Required]
    public DateTime TransitionDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? TriggeredBy { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public decimal? DurationInMinutes { get; set; }

    [StringLength(200)]
    public string? SystemInfo { get; set; }

    // JSON field for additional metadata
    public string? Metadata { get; set; }

    // Navigation properties
    public virtual ProductionWorkflow ProductionWorkflow { get; set; } = null!;
  }
}