using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Domain.Enums
{
  public enum ProductionStatus
  {
    [Display(Name = "Planned", Description = "Production order created but not started")]
    Planned = 0,

    [Display(Name = "In Progress", Description = "Work has begun on the production floor")]
    InProgress = 1,

    [Display(Name = "Quality Check", Description = "Production complete, awaiting quality control")]
    QualityCheck = 2,

    [Display(Name = "Completed", Description = "Ready for inventory/shipping")]
    Completed = 3,

    [Display(Name = "On Hold", Description = "Temporary stop due to material shortage or equipment issue")]
    OnHold = 4,

    [Display(Name = "Cancelled", Description = "Production order cancelled")]
    Cancelled = 5
  }

  public enum WorkflowEventType
  {
    StatusChanged = 0,
    AssignmentChanged = 1,
    NoteAdded = 2,
    QualityCheckStarted = 3,
    QualityCheckCompleted = 4,
    MaterialShortage = 5,
    EquipmentIssue = 6
  }

  public enum Priority
  {
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
  }
}