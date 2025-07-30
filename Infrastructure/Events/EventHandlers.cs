using InventorySystem.Domain.Events;
using InventorySystem.Data;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Services
{
  public class ProductionStatusChangedEventHandler : IEventHandler<ProductionStatusChangedEvent>
  {
    private readonly ILogger<ProductionStatusChangedEventHandler> _logger;
    private readonly InventoryContext _context;

    public ProductionStatusChangedEventHandler(
        ILogger<ProductionStatusChangedEventHandler> logger,
        InventoryContext context)
    {
      _logger = logger;
      _context = context;
    }

    public async Task HandleAsync(ProductionStatusChangedEvent domainEvent)
    {
      try
      {
        // Log the status change
        _logger.LogInformation(
            "Production {ProductionId} status changed from {FromStatus} to {ToStatus} by {User}",
            domainEvent.ProductionId,
            domainEvent.FromStatus,
            domainEvent.ToStatus,
            domainEvent.TriggeredBy);

        // Additional business logic can be added here
        // For example: Send notifications, update dashboards, trigger other workflows

        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to handle ProductionStatusChangedEvent for Production {ProductionId}",
            domainEvent.ProductionId);
        throw;
      }
    }
  }

  public class ProductionAssignedEventHandler : IEventHandler<ProductionAssignedEvent>
  {
    private readonly ILogger<ProductionAssignedEventHandler> _logger;

    public ProductionAssignedEventHandler(ILogger<ProductionAssignedEventHandler> logger)
    {
      _logger = logger;
    }

    public async Task HandleAsync(ProductionAssignedEvent domainEvent)
    {
      try
      {
        _logger.LogInformation(
            "Production {ProductionId} assigned from {PreviousAssignee} to {NewAssignee} by {AssignedBy}",
            domainEvent.ProductionId,
            domainEvent.PreviousAssignee ?? "Unassigned",
            domainEvent.NewAssignee,
            domainEvent.AssignedBy);

        // Here you could:
        // - Send email notifications
        // - Update workload tracking
        // - Trigger calendar updates

        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to handle ProductionAssignedEvent for Production {ProductionId}",
            domainEvent.ProductionId);
        throw;
      }
    }
  }

  public class QualityCheckFailedEventHandler : IEventHandler<QualityCheckFailedEvent>
  {
    private readonly ILogger<QualityCheckFailedEventHandler> _logger;

    public QualityCheckFailedEventHandler(ILogger<QualityCheckFailedEventHandler> logger)
    {
      _logger = logger;
    }

    public async Task HandleAsync(QualityCheckFailedEvent domainEvent)
    {
      try
      {
        _logger.LogWarning(
            "Quality check failed for Production {ProductionId}. Reason: {Reason}. Checker: {QualityCheckerId}",
            domainEvent.ProductionId,
            domainEvent.Reason,
            domainEvent.QualityCheckerId);

        // Here you could:
        // - Send alert notifications
        // - Create quality reports
        // - Trigger corrective action workflows

        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to handle QualityCheckFailedEvent for Production {ProductionId}",
            domainEvent.ProductionId);
        throw;
      }
    }
  }
}