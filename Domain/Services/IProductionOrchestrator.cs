// Domain/Services/IProductionOrchestrator.cs
using InventorySystem.Domain.Commands;
using InventorySystem.Domain.Queries;
using InventorySystem.ViewModels;

namespace InventorySystem.Domain.Services
{
  /// <summary>
  /// High-level orchestrator that coordinates production workflow with business logic
  /// </summary>
  public interface IProductionOrchestrator
  {
    /// <summary>
    /// Creates a new production with initial workflow setup
    /// </summary>
    Task<CommandResult> CreateProductionWithWorkflowAsync(int bomId, int quantity, decimal laborCost = 0, decimal overheadCost = 0, string? notes = null, string? createdBy = null);

    /// <summary>
    /// Handles production status changes with business rule validation
    /// </summary>
    Task<CommandResult> UpdateProductionStatusAsync(UpdateProductionStatusCommand command);

    /// <summary>
    /// Starts production with prerequisite checks
    /// </summary>
    Task<CommandResult> StartProductionAsync(StartProductionCommand command);

    /// <summary>
    /// Completes production with inventory updates
    /// </summary>
    Task<CommandResult> CompleteProductionAsync(int productionId, string? completedBy = null);

    /// <summary>
    /// Handles quality check completion with conditional logic
    /// </summary>
    Task<CommandResult> ProcessQualityCheckAsync(CompleteQualityCheckCommand command);

    /// <summary>
    /// Manages production assignment with workload considerations
    /// </summary>
    Task<CommandResult> AssignProductionAsync(AssignProductionCommand command);

    /// <summary>
    /// Handles material shortage scenarios
    /// </summary>
    Task<CommandResult> HandleMaterialShortageAsync(int productionId, string reason, string? handledBy = null);

    /// <summary>
    /// Processes equipment issues
    /// </summary>
    Task<CommandResult> HandleEquipmentIssueAsync(int productionId, string reason, string? handledBy = null);

    /// <summary>
    /// Cancels production with cleanup
    /// </summary>
    Task<CommandResult> CancelProductionAsync(int productionId, string reason, string? cancelledBy = null);

    /// <summary>
    /// Gets production workflow details
    /// </summary>
    Task<ProductionWorkflowResult?> GetProductionWorkflowAsync(GetProductionWorkflowQuery query);

    /// <summary>
    /// Gets WIP dashboard data
    /// </summary>
    Task<WipDashboardResult> GetWipDashboardAsync(GetWipDashboardQuery query);

    /// <summary>
    /// Gets production timeline
    /// </summary>
    Task<ProductionTimelineResult> GetProductionTimelineAsync(GetProductionTimelineQuery query);

    /// <summary>
    /// Gets active productions
    /// </summary>
    Task<List<ProductionSummary>> GetActiveProductionsAsync(GetActiveProductionsQuery query);

    /// <summary>
    /// Gets overdue productions
    /// </summary>
    Task<List<ProductionSummary>> GetOverdueProductionsAsync(GetOverdueProductionsQuery query);

    /// <summary>
    /// Validates production can be started
    /// </summary>
    Task<bool> CanStartProductionAsync(int productionId);

    /// <summary>
    /// Estimates completion time based on historical data
    /// </summary>
    Task<DateTime?> EstimateCompletionTimeAsync(int productionId);

    /// <summary>
    /// Gets employee workload for assignment optimization
    /// </summary>
    Task<Dictionary<string, int>> GetEmployeeWorkloadAsync();
  }
}
