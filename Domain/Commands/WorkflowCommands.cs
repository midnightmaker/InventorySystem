using InventorySystem.Domain.Enums;

namespace InventorySystem.Domain.Commands
{
  public class StartProductionCommand : ICommand
  {
    public StartProductionCommand(int productionId, string? assignedTo = null, DateTime? estimatedCompletion = null, string? requestedBy = null)
    {
      ProductionId = productionId;
      AssignedTo = assignedTo;
      EstimatedCompletion = estimatedCompletion;
      RequestedBy = requestedBy;
      RequestedAt = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public string? AssignedTo { get; }
    public DateTime? EstimatedCompletion { get; }
    public DateTime RequestedAt { get; }
    public string? RequestedBy { get; }
  }

  public class UpdateProductionStatusCommand : ICommand
  {
    public UpdateProductionStatusCommand(int productionId, ProductionStatus newStatus, string? reason = null, string? notes = null, string? requestedBy = null)
    {
      ProductionId = productionId;
      NewStatus = newStatus;
      Reason = reason;
      Notes = notes;
      RequestedBy = requestedBy;
      RequestedAt = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public ProductionStatus NewStatus { get; }
    public string? Reason { get; }
    public string? Notes { get; }
    public DateTime RequestedAt { get; }
    public string? RequestedBy { get; }
  }

  public class AssignProductionCommand : ICommand
  {
    public AssignProductionCommand(int productionId, string assignedTo, string? requestedBy = null)
    {
      ProductionId = productionId;
      AssignedTo = assignedTo;
      RequestedBy = requestedBy;
      RequestedAt = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public string AssignedTo { get; }
    public DateTime RequestedAt { get; }
    public string? RequestedBy { get; }
  }

  public class CompleteQualityCheckCommand : ICommand
  {
    public CompleteQualityCheckCommand(int productionId, bool passed, string? notes = null, int? qualityCheckerId = null, string? requestedBy = null)
    {
      ProductionId = productionId;
      Passed = passed;
      Notes = notes;
      QualityCheckerId = qualityCheckerId;
      RequestedBy = requestedBy;
      RequestedAt = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public bool Passed { get; }
    public string? Notes { get; }
    public int? QualityCheckerId { get; }
    public DateTime RequestedAt { get; }
    public string? RequestedBy { get; }
  }

  public class PutProductionOnHoldCommand : ICommand
  {
    public PutProductionOnHoldCommand(int productionId, string reason, string? requestedBy = null)
    {
      ProductionId = productionId;
      Reason = reason;
      RequestedBy = requestedBy;
      RequestedAt = DateTime.UtcNow;
    }

    public int ProductionId { get; }
    public string Reason { get; }
    public DateTime RequestedAt { get; }
    public string? RequestedBy { get; }
  }

  public class CommandResult : ICommandResult
  {
    private CommandResult(bool success, string? errorMessage = null, object? data = null)
    {
      Success = success;
      ErrorMessage = errorMessage;
      Data = data;
    }

    public bool Success { get; }
    public string? ErrorMessage { get; }
    public object? Data { get; }

    public static CommandResult SuccessResult(object? data = null) => new(true, data: data);
    public static CommandResult FailureResult(string errorMessage) => new(false, errorMessage);
  }
}