// Infrastructure/Services/WorkflowEngine.cs
using Microsoft.EntityFrameworkCore;
using InventorySystem.Data;
using InventorySystem.Domain.Commands;
using InventorySystem.Domain.Entities.Production;
using InventorySystem.Domain.Enums;
using InventorySystem.Domain.Events;
using InventorySystem.Domain.Services;

namespace InventorySystem.Infrastructure.Services
{
  public class WorkflowEngine : IWorkflowEngine
  {
    private readonly InventoryContext _context;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        InventoryContext context,
        IEventPublisher eventPublisher,
        ILogger<WorkflowEngine> logger)
    {
      _context = context;
      _eventPublisher = eventPublisher;
      _logger = logger;
    }

    public async Task<CommandResult> InitializeWorkflowAsync(int productionId, string? createdBy = null)
    {
      try
      {
        var existingWorkflow = await _context.ProductionWorkflows
            .FirstOrDefaultAsync(w => w.ProductionId == productionId);

        if (existingWorkflow != null)
        {
          return CommandResult.FailureResult("Workflow already exists for this production");
        }

        var workflow = new ProductionWorkflow
        {
          ProductionId = productionId,
          Status = ProductionStatus.Planned,
          Priority = Priority.Normal,
          CreatedDate = DateTime.UtcNow,
          LastModifiedDate = DateTime.UtcNow,
          LastModifiedBy = createdBy
        };

        _context.ProductionWorkflows.Add(workflow);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Workflow initialized for Production {ProductionId}", productionId);

        return CommandResult.SuccessResult(workflow);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to initialize workflow for Production {ProductionId}", productionId);
        return CommandResult.FailureResult($"Failed to initialize workflow: {ex.Message}");
      }
    }

    public async Task<CommandResult> TransitionStatusAsync(UpdateProductionStatusCommand command)
    {
      try
      {
        var workflow = await GetWorkflowAsync(command.ProductionId);
        if (workflow == null)
        {
          return CommandResult.FailureResult("Production workflow not found");
        }

        if (!workflow.CanTransitionTo(command.NewStatus))
        {
          return CommandResult.FailureResult($"Invalid transition from {workflow.Status} to {command.NewStatus}");
        }

        var previousStatus = workflow.Status;
        workflow.PreviousStatus = previousStatus;
        workflow.Status = command.NewStatus;
        workflow.LastModifiedDate = DateTime.UtcNow;
        workflow.LastModifiedBy = command.RequestedBy;

        // Handle status-specific logic
        switch (command.NewStatus)
        {
          case ProductionStatus.InProgress:
            if (workflow.StartedAt == null)
            {
              workflow.StartedAt = DateTime.UtcNow;
              workflow.ActualStartDate = DateTime.UtcNow;
            }
            break;

          case ProductionStatus.Completed:
            workflow.CompletedAt = DateTime.UtcNow;
            workflow.ActualEndDate = DateTime.UtcNow;
            break;

          case ProductionStatus.OnHold:
            workflow.OnHoldReason = command.Reason;
            break;
        }

        if (!string.IsNullOrEmpty(command.Notes))
        {
          workflow.Notes = command.Notes;
        }

        // Create transition record
        var transition = new WorkflowTransition
        {
          ProductionWorkflowId = workflow.Id,
          FromStatus = previousStatus,
          ToStatus = command.NewStatus,
          EventType = WorkflowEventType.StatusChanged,
          TransitionDate = DateTime.UtcNow,
          TriggeredBy = command.RequestedBy,
          Reason = command.Reason,
          Notes = command.Notes
        };

        _context.WorkflowTransitions.Add(transition);
        await _context.SaveChangesAsync();

        // Publish domain event
        var statusChangedEvent = new ProductionStatusChangedEvent(
            command.ProductionId,
            previousStatus,
            command.NewStatus,
            command.RequestedBy,
            command.Reason
        );

        await PublishEventAsync(statusChangedEvent);

        _logger.LogInformation(
            "Production {ProductionId} status changed from {FromStatus} to {ToStatus} by {User}",
            command.ProductionId, previousStatus, command.NewStatus, command.RequestedBy);

        return CommandResult.SuccessResult(workflow);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to transition status for Production {ProductionId}", command.ProductionId);
        return CommandResult.FailureResult($"Status transition failed: {ex.Message}");
      }
    }

    public async Task<CommandResult> StartProductionAsync(StartProductionCommand command)
    {
      try
      {
        var workflow = await GetWorkflowAsync(command.ProductionId);
        if (workflow == null)
        {
          return CommandResult.FailureResult("Production workflow not found");
        }

        if (workflow.Status != ProductionStatus.Planned)
        {
          return CommandResult.FailureResult($"Cannot start production from {workflow.Status} status");
        }

        workflow.Status = ProductionStatus.InProgress;
        workflow.StartedAt = DateTime.UtcNow;
        workflow.ActualStartDate = DateTime.UtcNow;
        workflow.EstimatedCompletionDate = command.EstimatedCompletion;
        workflow.AssignedTo = command.AssignedTo;
        workflow.AssignedBy = command.RequestedBy;
        workflow.LastModifiedDate = DateTime.UtcNow;
        workflow.LastModifiedBy = command.RequestedBy;

        // Create transition record
        var transition = new WorkflowTransition
        {
          ProductionWorkflowId = workflow.Id,
          FromStatus = ProductionStatus.Planned,
          ToStatus = ProductionStatus.InProgress,
          EventType = WorkflowEventType.StatusChanged,
          TransitionDate = DateTime.UtcNow,
          TriggeredBy = command.RequestedBy,
          Reason = "Production started"
        };

        _context.WorkflowTransitions.Add(transition);
        await _context.SaveChangesAsync();

        // Publish events
        var statusChangedEvent = new ProductionStatusChangedEvent(
            command.ProductionId,
            ProductionStatus.Planned,
            ProductionStatus.InProgress,
            command.RequestedBy,
            "Production started"
        );

        await PublishEventAsync(statusChangedEvent);

        if (!string.IsNullOrEmpty(command.AssignedTo))
        {
          var assignedEvent = new ProductionAssignedEvent(
              command.ProductionId,
              null,
              command.AssignedTo,
              command.RequestedBy
          );

          await PublishEventAsync(assignedEvent);
        }

        _logger.LogInformation("Production {ProductionId} started by {User}", command.ProductionId, command.RequestedBy);

        return CommandResult.SuccessResult(workflow);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to start Production {ProductionId}", command.ProductionId);
        return CommandResult.FailureResult($"Failed to start production: {ex.Message}");
      }
    }

    public async Task<CommandResult> AssignProductionAsync(AssignProductionCommand command)
    {
      try
      {
        var workflow = await GetWorkflowAsync(command.ProductionId);
        if (workflow == null)
        {
          return CommandResult.FailureResult("Production workflow not found");
        }

        var previousAssignee = workflow.AssignedTo;
        workflow.AssignedTo = command.AssignedTo;
        workflow.AssignedBy = command.RequestedBy;
        workflow.LastModifiedDate = DateTime.UtcNow;
        workflow.LastModifiedBy = command.RequestedBy;

        // Create transition record
        var transition = new WorkflowTransition
        {
          ProductionWorkflowId = workflow.Id,
          FromStatus = workflow.Status,
          ToStatus = workflow.Status, // Status doesn't change
          EventType = WorkflowEventType.AssignmentChanged,
          TransitionDate = DateTime.UtcNow,
          TriggeredBy = command.RequestedBy,
          Reason = $"Assigned to {command.AssignedTo}"
        };

        _context.WorkflowTransitions.Add(transition);
        await _context.SaveChangesAsync();

        // Publish domain event
        var assignedEvent = new ProductionAssignedEvent(
            command.ProductionId,
            previousAssignee,
            command.AssignedTo,
            command.RequestedBy
        );

        await PublishEventAsync(assignedEvent);

        _logger.LogInformation(
            "Production {ProductionId} assigned to {AssignedTo} by {User}",
            command.ProductionId, command.AssignedTo, command.RequestedBy);

        return CommandResult.SuccessResult(workflow);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to assign Production {ProductionId}", command.ProductionId);
        return CommandResult.FailureResult($"Assignment failed: {ex.Message}");
      }
    }

    public async Task<CommandResult> CompleteQualityCheckAsync(CompleteQualityCheckCommand command)
    {
      try
      {
        var workflow = await GetWorkflowAsync(command.ProductionId);
        if (workflow == null)
        {
          return CommandResult.FailureResult("Production workflow not found");
        }

        if (workflow.Status != ProductionStatus.QualityCheck)
        {
          return CommandResult.FailureResult($"Cannot complete quality check from {workflow.Status} status");
        }

        workflow.QualityCheckPassed = command.Passed;
        workflow.QualityCheckNotes = command.Notes;
        workflow.QualityCheckerId = command.QualityCheckerId;
        workflow.QualityCheckDate = DateTime.UtcNow;
        workflow.LastModifiedDate = DateTime.UtcNow;
        workflow.LastModifiedBy = command.RequestedBy;

        ProductionStatus newStatus = command.Passed ? ProductionStatus.Completed : ProductionStatus.InProgress;
        workflow.Status = newStatus;

        if (command.Passed)
        {
          workflow.CompletedAt = DateTime.UtcNow;
          workflow.ActualEndDate = DateTime.UtcNow;
        }

        // Create transition record
        var transition = new WorkflowTransition
        {
          ProductionWorkflowId = workflow.Id,
          FromStatus = ProductionStatus.QualityCheck,
          ToStatus = newStatus,
          EventType = command.Passed ? WorkflowEventType.QualityCheckCompleted : WorkflowEventType.StatusChanged,
          TransitionDate = DateTime.UtcNow,
          TriggeredBy = command.RequestedBy,
          Reason = command.Passed ? "Quality check passed" : "Quality check failed - returned to production",
          Notes = command.Notes
        };

        _context.WorkflowTransitions.Add(transition);
        await _context.SaveChangesAsync();

        // Publish events
        if (!command.Passed)
        {
          var qualityFailedEvent = new QualityCheckFailedEvent(
              command.ProductionId,
              command.Notes ?? "Quality check failed",
              command.QualityCheckerId
          );

          await PublishEventAsync(qualityFailedEvent);
        }

        var statusChangedEvent = new ProductionStatusChangedEvent(
            command.ProductionId,
            ProductionStatus.QualityCheck,
            newStatus,
            command.RequestedBy,
            transition.Reason
        );

        await PublishEventAsync(statusChangedEvent);

        _logger.LogInformation(
            "Quality check completed for Production {ProductionId}. Passed: {Passed}",
            command.ProductionId, command.Passed);

        return CommandResult.SuccessResult(workflow);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to complete quality check for Production {ProductionId}", command.ProductionId);
        return CommandResult.FailureResult($"Quality check completion failed: {ex.Message}");
      }
    }

    public async Task<CommandResult> PutOnHoldAsync(PutProductionOnHoldCommand command)
    {
      try
      {
        var updateCommand = new UpdateProductionStatusCommand(
            command.ProductionId,
            ProductionStatus.OnHold,
            command.Reason,
            null,
            command.RequestedBy
        );

        return await TransitionStatusAsync(updateCommand);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to put Production {ProductionId} on hold", command.ProductionId);
        return CommandResult.FailureResult($"Failed to put on hold: {ex.Message}");
      }
    }

    public async Task<CommandResult> ResumeFromHoldAsync(int productionId, string? resumedBy = null)
    {
      try
      {
        var updateCommand = new UpdateProductionStatusCommand(
            productionId,
            ProductionStatus.InProgress,
            "Resumed from hold",
            null,
            resumedBy
        );

        return await TransitionStatusAsync(updateCommand);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to resume Production {ProductionId} from hold", productionId);
        return CommandResult.FailureResult($"Failed to resume from hold: {ex.Message}");
      }
    }

    public async Task<bool> CanTransitionAsync(int productionId, ProductionStatus newStatus)
    {
      var workflow = await GetWorkflowAsync(productionId);
      return workflow?.CanTransitionTo(newStatus) ?? false;
    }

    public async Task<List<ProductionStatus>> GetValidNextStatusesAsync(int productionId)
    {
      var workflow = await GetWorkflowAsync(productionId);
      return workflow != null ? WorkflowRules.GetValidNextStatuses(workflow.Status) : new List<ProductionStatus>();
    }

    public async Task<ProductionWorkflow?> GetWorkflowAsync(int productionId)
    {
      return await _context.ProductionWorkflows
          .Include(w => w.Production)
          .Include(w => w.WorkflowTransitions)
          .FirstOrDefaultAsync(w => w.ProductionId == productionId);
    }

    public async Task PublishEventAsync(IDomainEvent domainEvent)
    {
      await _eventPublisher.PublishAsync(domainEvent);
    }
  }
}