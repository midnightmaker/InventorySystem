using InventorySystem.Data;
using InventorySystem.Domain.Commands;
using InventorySystem.Domain.Entities.Production;
using InventorySystem.Domain.Enums;
using InventorySystem.Domain.Queries;
using InventorySystem.Domain.Services;
using InventorySystem.Services;
using Microsoft.EntityFrameworkCore;
using InventorySystem.ViewModels;

namespace InventorySystem.Infrastructure.Services
{
  public class ProductionOrchestrator : IProductionOrchestrator
  {
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IProductionService _productionService;
    private readonly IBomService _bomService;
    private readonly IInventoryService _inventoryService;
    private readonly InventoryContext _context;
    private readonly ILogger<ProductionOrchestrator> _logger;

    public ProductionOrchestrator(
        IWorkflowEngine workflowEngine,
        IProductionService productionService,
        IBomService bomService,
        IInventoryService inventoryService,
        InventoryContext context,
        ILogger<ProductionOrchestrator> logger)
    {
      _workflowEngine = workflowEngine;
      _productionService = productionService;
      _bomService = bomService;
      _inventoryService = inventoryService;
      _context = context;
      _logger = logger;
    }

    public async Task<CommandResult> CreateProductionWithWorkflowAsync(
        int bomId,
        int quantity,
        decimal laborCost = 0,
        decimal overheadCost = 0,
        string? notes = null,
        string? createdBy = null)
    {
      using var transaction = await _context.Database.BeginTransactionAsync();
      try
      {
        // Validate BOM can be built
        var canBuild = await _productionService.CanBuildBomAsync(bomId, quantity);
        if (!canBuild)
        {
          return CommandResult.FailureResult("Insufficient materials to build BOM");
        }

        // Create the production record
        var production = await _productionService.BuildBomAsync(bomId, quantity, laborCost, overheadCost, notes);

        // Initialize the workflow
        var workflowResult = await _workflowEngine.InitializeWorkflowAsync(production.Id, createdBy);
        if (!workflowResult.Success)
        {
          await transaction.RollbackAsync();
          return workflowResult;
        }

        await transaction.CommitAsync();

        _logger.LogInformation(
            "Production {ProductionId} created with workflow by {User}",
            production.Id, createdBy);

        return CommandResult.SuccessResult(new { Production = production, Workflow = workflowResult.Data });
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Failed to create production with workflow for BOM {BomId}", bomId);
        return CommandResult.FailureResult($"Failed to create production: {ex.Message}");
      }
    }

    public async Task<CommandResult> UpdateProductionStatusAsync(UpdateProductionStatusCommand command)
    {
      try
      {
        // Additional business rule validation can go here
        var workflow = await _workflowEngine.GetWorkflowAsync(command.ProductionId);
        if (workflow == null)
        {
          return CommandResult.FailureResult("Production workflow not found");
        }

        // Check for business-specific rules
        if (command.NewStatus == ProductionStatus.QualityCheck)
        {
          // Ensure production is actually complete before QC
          if (workflow.Status != ProductionStatus.InProgress)
          {
            return CommandResult.FailureResult("Production must be in progress before quality check");
          }
        }

        return await _workflowEngine.TransitionStatusAsync(command);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to update production status for Production {ProductionId}", command.ProductionId);
        return CommandResult.FailureResult($"Status update failed: {ex.Message}");
      }
    }

    public async Task<CommandResult> StartProductionAsync(StartProductionCommand command)
    {
      try
      {
        // Pre-flight checks
        var canStart = await CanStartProductionAsync(command.ProductionId);
        if (!canStart)
        {
          return CommandResult.FailureResult("Production cannot be started - prerequisite checks failed");
        }

        // Estimate completion time if not provided
        DateTime? estimatedCompletion = command.EstimatedCompletion;
        if (estimatedCompletion == null)
        {
          estimatedCompletion = await EstimateCompletionTimeAsync(command.ProductionId);
        }

        var updatedCommand = new StartProductionCommand(
            command.ProductionId,
            command.AssignedTo,
            estimatedCompletion,
            command.RequestedBy);

        return await _workflowEngine.StartProductionAsync(updatedCommand);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to start Production {ProductionId}", command.ProductionId);
        return CommandResult.FailureResult($"Failed to start production: {ex.Message}");
      }
    }

    public async Task<CommandResult> CompleteProductionAsync(int productionId, string? completedBy = null)
    {
      using var transaction = await _context.Database.BeginTransactionAsync();
      try
      {
        var workflow = await _workflowEngine.GetWorkflowAsync(productionId);
        if (workflow == null)
        {
          return CommandResult.FailureResult("Production workflow not found");
        }

        if (workflow.Status != ProductionStatus.QualityCheck)
        {
          return CommandResult.FailureResult("Production must be in quality check before completion");
        }

        // Complete the workflow
        var command = new UpdateProductionStatusCommand(
            productionId,
            ProductionStatus.Completed,
            "Production completed",
            null,
            completedBy);

        var result = await _workflowEngine.TransitionStatusAsync(command);
        if (!result.Success)
        {
          await transaction.RollbackAsync();
          return result;
        }

        // Update inventory with finished goods
        // This would integrate with your existing inventory system

        await transaction.CommitAsync();

        _logger.LogInformation("Production {ProductionId} completed by {User}", productionId, completedBy);

        return result;
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Failed to complete Production {ProductionId}", productionId);
        return CommandResult.FailureResult($"Failed to complete production: {ex.Message}");
      }
    }

    public async Task<CommandResult> ProcessQualityCheckAsync(CompleteQualityCheckCommand command)
    {
      try
      {
        var result = await _workflowEngine.CompleteQualityCheckAsync(command);
        if (!result.Success)
        {
          return result;
        }

        // If quality check passed, we can complete the production
        if (command.Passed)
        {
          return await CompleteProductionAsync(command.ProductionId, command.RequestedBy);
        }

        return result;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to process quality check for Production {ProductionId}", command.ProductionId);
        return CommandResult.FailureResult($"Quality check processing failed: {ex.Message}");
      }
    }

    public async Task<CommandResult> AssignProductionAsync(AssignProductionCommand command)
    {
      try
      {
        // Check employee workload before assignment
        var workload = await GetEmployeeWorkloadAsync();
        var currentWorkload = workload.GetValueOrDefault(command.AssignedTo, 0);

        // Business rule: Don't assign more than 5 active productions to one person
        if (currentWorkload >= 5)
        {
          _logger.LogWarning(
              "Assignment of Production {ProductionId} to {Employee} may exceed recommended workload ({CurrentWorkload})",
              command.ProductionId, command.AssignedTo, currentWorkload);
        }

        return await _workflowEngine.AssignProductionAsync(command);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to assign Production {ProductionId}", command.ProductionId);
        return CommandResult.FailureResult($"Assignment failed: {ex.Message}");
      }
    }

    public async Task<CommandResult> HandleMaterialShortageAsync(int productionId, string reason, string? handledBy = null)
    {
      try
      {
        var command = new PutProductionOnHoldCommand(productionId, $"Material shortage: {reason}", handledBy);
        return await _workflowEngine.PutOnHoldAsync(command);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to handle material shortage for Production {ProductionId}", productionId);
        return CommandResult.FailureResult($"Failed to handle material shortage: {ex.Message}");
      }
    }

    public async Task<CommandResult> HandleEquipmentIssueAsync(int productionId, string reason, string? handledBy = null)
    {
      try
      {
        var command = new PutProductionOnHoldCommand(productionId, $"Equipment issue: {reason}", handledBy);
        return await _workflowEngine.PutOnHoldAsync(command);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to handle equipment issue for Production {ProductionId}", productionId);
        return CommandResult.FailureResult($"Failed to handle equipment issue: {ex.Message}");
      }
    }

    public async Task<CommandResult> CancelProductionAsync(int productionId, string reason, string? cancelledBy = null)
    {
      using var transaction = await _context.Database.BeginTransactionAsync();
      try
      {
        var command = new UpdateProductionStatusCommand(
            productionId,
            ProductionStatus.Cancelled,
            reason,
            null,
            cancelledBy);

        var result = await _workflowEngine.TransitionStatusAsync(command);
        if (!result.Success)
        {
          await transaction.RollbackAsync();
          return result;
        }

        // Handle any cleanup (return materials to inventory, etc.)
        // This would integrate with your existing inventory system

        await transaction.CommitAsync();

        _logger.LogInformation("Production {ProductionId} cancelled by {User}. Reason: {Reason}",
            productionId, cancelledBy, reason);

        return result;
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Failed to cancel Production {ProductionId}", productionId);
        return CommandResult.FailureResult($"Failed to cancel production: {ex.Message}");
      }
    }

    public async Task<ProductionWorkflowResult?> GetProductionWorkflowAsync(GetProductionWorkflowQuery query)
    {
      try
      {
        var workflow = await _context.ProductionWorkflows
            .Include(w => w.Production)
                .ThenInclude(p => p.Bom)
            .Include(w => w.Production)
                .ThenInclude(p => p.FinishedGood)
            .Include(w => w.WorkflowTransitions)
            .FirstOrDefaultAsync(w => w.ProductionId == query.ProductionId);

        if (workflow == null)
        {
          return null;
        }

        var result = new ProductionWorkflowResult
        {
          ProductionId = workflow.ProductionId,
          BomNumber = workflow.Production.Bom?.BomNumber ?? "Unknown",
          FinishedGoodName = workflow.Production.FinishedGood?.Description,
          Quantity = workflow.Production.QuantityProduced,
          Status = workflow.Status,
          Priority = workflow.Priority,
          AssignedTo = workflow.AssignedTo,
          StartedAt = workflow.StartedAt,
          EstimatedCompletionDate = workflow.EstimatedCompletionDate,
          ActualCompletionDate = workflow.ActualEndDate,
          IsOverdue = workflow.IsOverdue,
          Duration = workflow.Duration,
          Transitions = workflow.WorkflowTransitions.Select(t => new WorkflowTransitionSummary
          {
            FromStatus = t.FromStatus,
            ToStatus = t.ToStatus,
            TransitionDate = t.TransitionDate,
            TriggeredBy = t.TriggeredBy,
            Reason = t.Reason,
            DurationInMinutes = t.DurationInMinutes
          }).OrderBy(t => t.TransitionDate).ToList(),
          ValidNextStatuses = WorkflowRules.GetValidNextStatuses(workflow.Status)
        };

        return result;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get production workflow for Production {ProductionId}", query.ProductionId);
        throw;
      }
    }

    public async Task<WipDashboardResult> GetWipDashboardAsync(GetWipDashboardQuery query)
    {
      try
      {
        var workflowsQuery = _context.ProductionWorkflows
            .Include(w => w.Production)
                .ThenInclude(p => p.Bom)
            .Include(w => w.Production)
                .ThenInclude(p => p.FinishedGood)
            .Where(w => w.Status != ProductionStatus.Completed && w.Status != ProductionStatus.Cancelled);

        if (query.FromDate.HasValue)
        {
          workflowsQuery = workflowsQuery.Where(w => w.CreatedDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
          workflowsQuery = workflowsQuery.Where(w => w.CreatedDate <= query.ToDate.Value);
        }

        if (!string.IsNullOrEmpty(query.AssignedTo))
        {
          workflowsQuery = workflowsQuery.Where(w => w.AssignedTo == query.AssignedTo);
        }

        var workflows = await workflowsQuery.ToListAsync();

        var productionsByStatus = workflows
            .GroupBy(w => w.Status)
            .ToDictionary(
                g => g.Key,
                g => g.Select(w => new ProductionSummary
                {
                  ProductionId = w.ProductionId,
                  BomNumber = w.Production.Bom?.BomNumber ?? "Unknown",
                  FinishedGoodName = w.Production.FinishedGood?.Description,
                  Quantity = w.Production.QuantityProduced,
                  Status = w.Status,
                  Priority = w.Priority,
                  AssignedTo = w.AssignedTo,
                  CreatedDate = w.CreatedDate,
                  StartedAt = w.StartedAt,
                  EstimatedCompletionDate = w.EstimatedCompletionDate,
                  IsOverdue = w.IsOverdue,
                  ProgressPercentage = CalculateProgressPercentage(w.Status)
                }).ToList()
            );

        var statistics = new WipStatistics
        {
          TotalActiveProductions = workflows.Count,
          PlannedCount = workflows.Count(w => w.Status == ProductionStatus.Planned),
          InProgressCount = workflows.Count(w => w.Status == ProductionStatus.InProgress),
          QualityCheckCount = workflows.Count(w => w.Status == ProductionStatus.QualityCheck),
          OnHoldCount = workflows.Count(w => w.Status == ProductionStatus.OnHold),
          OverdueCount = workflows.Count(w => w.IsOverdue),
          CompletedTodayCount = await GetCompletedTodayCountAsync(),
          AverageCompletionTime = await GetAverageCompletionTimeAsync(),
          OnTimeCompletionRate = await GetOnTimeCompletionRateAsync()
        };

        var overdueProductions = workflows
            .Where(w => w.IsOverdue)
            .Select(w => new ProductionSummary
            {
              ProductionId = w.ProductionId,
              BomNumber = w.Production.Bom?.BomNumber ?? "Unknown",
              FinishedGoodName = w.Production.FinishedGood?.Description,
              Quantity = w.Production.QuantityProduced,
              Status = w.Status,
              Priority = w.Priority,
              AssignedTo = w.AssignedTo,
              CreatedDate = w.CreatedDate,
              StartedAt = w.StartedAt,
              EstimatedCompletionDate = w.EstimatedCompletionDate,
              IsOverdue = w.IsOverdue
            }).ToList();

        var completedToday = await GetCompletedTodayAsync();

        return new WipDashboardResult
        {
          ProductionsByStatus = productionsByStatus,
          Statistics = statistics,
          OverdueProductions = overdueProductions,
          CompletedToday = completedToday
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get WIP dashboard data");
        throw;
      }
    }

    public async Task<ProductionTimelineResult> GetProductionTimelineAsync(GetProductionTimelineQuery query)
    {
      try
      {
        var workflow = await _context.ProductionWorkflows
            .Include(w => w.WorkflowTransitions)
            .FirstOrDefaultAsync(w => w.ProductionId == query.ProductionId);

        if (workflow == null)
        {
          return new ProductionTimelineResult { ProductionId = query.ProductionId };
        }

        var events = workflow.WorkflowTransitions
            .Select(t => new TimelineEvent
            {
              Timestamp = t.TransitionDate,
              EventType = t.EventType.ToString(),
              Description = $"Status changed from {t.FromStatus} to {t.ToStatus}",
              TriggeredBy = t.TriggeredBy,
              AdditionalData = new
              {
                Reason = t.Reason,
                Notes = t.Notes,
                DurationInMinutes = t.DurationInMinutes
              }
            })
            .OrderBy(e => e.Timestamp)
            .ToList();

        var metrics = new Dictionary<string, object>
        {
          ["TotalDuration"] = workflow.Duration?.TotalHours ?? 0,
          ["IsOverdue"] = workflow.IsOverdue,
          ["StatusChangeCount"] = workflow.WorkflowTransitions.Count,
          ["CurrentStatus"] = workflow.Status.ToString(),
          ["Priority"] = workflow.Priority.ToString()
        };

        return new ProductionTimelineResult
        {
          ProductionId = query.ProductionId,
          Events = events,
          Metrics = metrics
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get production timeline for Production {ProductionId}", query.ProductionId);
        throw;
      }
    }

    public async Task<List<ProductionSummary>> GetActiveProductionsAsync(GetActiveProductionsQuery query)
    {
      try
      {
        var workflowsQuery = _context.ProductionWorkflows
            .Include(w => w.Production)
                .ThenInclude(p => p.Bom)
            .Include(w => w.Production)
                .ThenInclude(p => p.FinishedGood)
            .Where(w => w.Status != ProductionStatus.Completed && w.Status != ProductionStatus.Cancelled);

        if (!string.IsNullOrEmpty(query.AssignedTo))
        {
          workflowsQuery = workflowsQuery.Where(w => w.AssignedTo == query.AssignedTo);
        }

        if (query.Status.HasValue)
        {
          workflowsQuery = workflowsQuery.Where(w => w.Status == query.Status.Value);
        }

        var workflows = await workflowsQuery.ToListAsync();

        return workflows.Select(w => new ProductionSummary
        {
          ProductionId = w.ProductionId,
          BomNumber = w.Production.Bom?.BomNumber ?? "Unknown",
          FinishedGoodName = w.Production.FinishedGood?.Description,
          Quantity = w.Production.QuantityProduced,
          Status = w.Status,
          Priority = w.Priority,
          AssignedTo = w.AssignedTo,
          CreatedDate = w.CreatedDate,
          StartedAt = w.StartedAt,
          EstimatedCompletionDate = w.EstimatedCompletionDate,
          IsOverdue = w.IsOverdue,
          ProgressPercentage = CalculateProgressPercentage(w.Status)
        }).ToList();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get active productions");
        throw;
      }
    }

    public async Task<List<ProductionSummary>> GetOverdueProductionsAsync(GetOverdueProductionsQuery query)
    {
      try
      {
        var workflows = await _context.ProductionWorkflows
            .Include(w => w.Production)
                .ThenInclude(p => p.Bom)
            .Include(w => w.Production)
                .ThenInclude(p => p.FinishedGood)
            .Where(w => w.EstimatedCompletionDate.HasValue
                       && w.EstimatedCompletionDate.Value < DateTime.UtcNow
                       && w.Status != ProductionStatus.Completed
                       && w.Status != ProductionStatus.Cancelled)
            .ToListAsync();

        return workflows.Select(w => new ProductionSummary
        {
          ProductionId = w.ProductionId,
          BomNumber = w.Production.Bom?.BomNumber ?? "Unknown",
          FinishedGoodName = w.Production.FinishedGood?.Description,
          Quantity = w.Production.QuantityProduced,
          Status = w.Status,
          Priority = w.Priority,
          AssignedTo = w.AssignedTo,
          CreatedDate = w.CreatedDate,
          StartedAt = w.StartedAt,
          EstimatedCompletionDate = w.EstimatedCompletionDate,
          IsOverdue = w.IsOverdue
        }).ToList();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get overdue productions");
        throw;
      }
    }

    public async Task<bool> CanStartProductionAsync(int productionId)
    {
      try
      {
        var workflow = await _workflowEngine.GetWorkflowAsync(productionId);
        if (workflow == null || workflow.Status != ProductionStatus.Planned)
        {
          return false;
        }

        // Check if materials are still available
        var production = workflow.Production;
        if (production?.BomId != null)
        {
          return await _productionService.CanBuildBomAsync(production.BomId, production.QuantityProduced);
        }

        return true;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to check if Production {ProductionId} can start", productionId);
        return false;
      }
    }

    public async Task<DateTime?> EstimateCompletionTimeAsync(int productionId)
    {
      try
      {
        // Simple estimation based on quantity and historical data
        // In a real implementation, this would use machine learning or statistical analysis

        var workflow = await _workflowEngine.GetWorkflowAsync(productionId);
        if (workflow?.Production == null)
        {
          return null;
        }

        var quantity = workflow.Production.QuantityProduced;
        var baseHoursPerUnit = 2.0; // Default estimate

        // Get historical data for similar productions
        var historicalAverage = await GetHistoricalAverageCompletionTimeAsync();
        if (historicalAverage.HasValue)
        {
          baseHoursPerUnit = historicalAverage.Value;
        }

        var estimatedHours = quantity * baseHoursPerUnit;
        return DateTime.UtcNow.AddHours(estimatedHours);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to estimate completion time for Production {ProductionId}", productionId);
        return null;
      }
    }

    public async Task<Dictionary<string, int>> GetEmployeeWorkloadAsync()
    {
      try
      {
        var workloads = await _context.ProductionWorkflows
            .Where(w => !string.IsNullOrEmpty(w.AssignedTo)
                       && w.Status != ProductionStatus.Completed
                       && w.Status != ProductionStatus.Cancelled)
            .GroupBy(w => w.AssignedTo)
            .Select(g => new { Employee = g.Key!, Count = g.Count() })
            .ToDictionaryAsync(x => x.Employee, x => x.Count);

        return workloads;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get employee workload data");
        return new Dictionary<string, int>();
      }
    }

    // Helper methods
    private static decimal? CalculateProgressPercentage(ProductionStatus status)
    {
      return status switch
      {
        ProductionStatus.Planned => 0,
        ProductionStatus.InProgress => 50,
        ProductionStatus.QualityCheck => 85,
        ProductionStatus.Completed => 100,
        ProductionStatus.OnHold => null,
        ProductionStatus.Cancelled => null,
        _ => null
      };
    }

    private async Task<int> GetCompletedTodayCountAsync()
    {
      var today = DateTime.Today;
      return await _context.ProductionWorkflows
          .CountAsync(w => w.Status == ProductionStatus.Completed
                         && w.CompletedAt.HasValue
                         && w.CompletedAt.Value.Date == today);
    }

    private async Task<List<ProductionSummary>> GetCompletedTodayAsync()
    {
      var today = DateTime.Today;
      var workflows = await _context.ProductionWorkflows
          .Include(w => w.Production)
              .ThenInclude(p => p.Bom)
          .Include(w => w.Production)
              .ThenInclude(p => p.FinishedGood)
          .Where(w => w.Status == ProductionStatus.Completed
                     && w.CompletedAt.HasValue
                     && w.CompletedAt.Value.Date == today)
          .ToListAsync();

      return workflows.Select(w => new ProductionSummary
      {
        ProductionId = w.ProductionId,
        BomNumber = w.Production.Bom?.BomNumber ?? "Unknown",
        FinishedGoodName = w.Production.FinishedGood?.Description,
        Quantity = w.Production.QuantityProduced,
        Status = w.Status,
        Priority = w.Priority,
        AssignedTo = w.AssignedTo,
        CreatedDate = w.CreatedDate,
        StartedAt = w.StartedAt,
        EstimatedCompletionDate = w.EstimatedCompletionDate,
        IsOverdue = false
      }).ToList();
    }

    private async Task<decimal> GetAverageCompletionTimeAsync()
    {
      var completedProductions = await _context.ProductionWorkflows
          .Where(w => w.Status == ProductionStatus.Completed
                     && w.ActualStartDate.HasValue
                     && w.ActualEndDate.HasValue)
          .Select(w => new
          {
            Duration = w.ActualEndDate!.Value - w.ActualStartDate!.Value
          })
          .ToListAsync();

      if (!completedProductions.Any())
        return 0;

      return (decimal)completedProductions.Average(p => p.Duration.TotalHours);
    }

    private async Task<decimal> GetOnTimeCompletionRateAsync()
    {
      var completedWithEstimates = await _context.ProductionWorkflows
          .Where(w => w.Status == ProductionStatus.Completed
                     && w.ActualEndDate.HasValue
                     && w.EstimatedCompletionDate.HasValue)
          .Select(w => new
          {
            OnTime = w.ActualEndDate!.Value <= w.EstimatedCompletionDate!.Value
          })
          .ToListAsync();

      if (!completedWithEstimates.Any())
        return 100;

      return (decimal)completedWithEstimates.Count(p => p.OnTime) / completedWithEstimates.Count * 100;
    }

    private async Task<double?> GetHistoricalAverageCompletionTimeAsync()
    {
      var completedProductions = await _context.ProductionWorkflows
          .Where(w => w.Status == ProductionStatus.Completed
                     && w.ActualStartDate.HasValue
                     && w.ActualEndDate.HasValue)
          .Select(w => new
          {
            Duration = w.ActualEndDate!.Value - w.ActualStartDate!.Value,
            Quantity = w.Production.QuantityProduced
          })
          .ToListAsync();

      if (!completedProductions.Any())
        return null;

      return completedProductions.Average(p => p.Duration.TotalHours / p.Quantity);
    }
  }
}