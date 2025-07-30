// Domain/Services/IWorkflowEngine.cs
using InventorySystem.Domain.Commands;
using InventorySystem.Domain.Entities.Production;
using InventorySystem.Domain.Enums;
using InventorySystem.Domain.Events;

namespace InventorySystem.Domain.Services
{
  /// <summary>
  /// Core workflow engine responsible for managing production workflow state transitions
  /// </summary>
  public interface IWorkflowEngine
  {
    /// <summary>
    /// Initializes workflow for a new production
    /// </summary>
    Task<CommandResult> InitializeWorkflowAsync(int productionId, string? createdBy = null);

    /// <summary>
    /// Transitions production to a new status with validation
    /// </summary>
    Task<CommandResult> TransitionStatusAsync(UpdateProductionStatusCommand command);

    /// <summary>
    /// Starts production workflow
    /// </summary>
    Task<CommandResult> StartProductionAsync(StartProductionCommand command);

    /// <summary>
    /// Assigns production to an employee
    /// </summary>
    Task<CommandResult> AssignProductionAsync(AssignProductionCommand command);

    /// <summary>
    /// Completes quality check step
    /// </summary>
    Task<CommandResult> CompleteQualityCheckAsync(CompleteQualityCheckCommand command);

    /// <summary>
    /// Puts production on hold
    /// </summary>
    Task<CommandResult> PutOnHoldAsync(PutProductionOnHoldCommand command);

    /// <summary>
    /// Resumes production from hold
    /// </summary>
    Task<CommandResult> ResumeFromHoldAsync(int productionId, string? resumedBy = null);

    /// <summary>
    /// Validates if transition is allowed
    /// </summary>
    Task<bool> CanTransitionAsync(int productionId, ProductionStatus newStatus);

    /// <summary>
    /// Gets valid next statuses for a production
    /// </summary>
    Task<List<ProductionStatus>> GetValidNextStatusesAsync(int productionId);

    /// <summary>
    /// Gets current workflow state
    /// </summary>
    Task<ProductionWorkflow?> GetWorkflowAsync(int productionId);

    /// <summary>
    /// Publishes domain events
    /// </summary>
    Task PublishEventAsync(IDomainEvent domainEvent);
  }
}