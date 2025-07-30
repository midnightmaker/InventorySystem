// Infrastructure/Events/EventPublisher.cs
using InventorySystem.Data;
using InventorySystem.Domain.Events;

namespace InventorySystem.Infrastructure.Services
{
  public class EventPublisher : IEventPublisher
  {
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IServiceProvider serviceProvider, ILogger<EventPublisher> logger)
    {
      _serviceProvider = serviceProvider;
      _logger = logger;
    }

    public async Task PublishAsync(IDomainEvent domainEvent)
    {
      // Get the actual type of the domain event
      var eventType = domainEvent.GetType();

      // Create the generic method to call PublishAsync<T>
      var method = typeof(EventPublisher).GetMethod(nameof(PublishAsync), new[] { eventType });
      if (method != null)
      {
        var genericMethod = method.MakeGenericMethod(eventType);
        var task = (Task)genericMethod.Invoke(this, new object[] { domainEvent })!;
        await task;
      }
      else
      {
        // Fallback to direct type checking
        await PublishByTypeAsync(domainEvent);
      }
    }

    public async Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
    {
      try
      {
        // Get handlers for the specific event type
        var handlers = _serviceProvider.GetServices<IEventHandler<T>>();

        if (!handlers.Any())
        {
          _logger.LogDebug("No handlers found for event type {EventType}", typeof(T).Name);
          return;
        }

        // Execute all handlers
        var tasks = handlers.Select(handler => handler.HandleAsync(domainEvent));
        await Task.WhenAll(tasks);

        _logger.LogDebug("Published event {EventType} with {HandlerCount} handlers",
            domainEvent.EventType, handlers.Count());
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to publish event {EventType}", domainEvent.EventType);
        throw;
      }
    }

    // Fallback method for type-specific handling
    private async Task PublishByTypeAsync(IDomainEvent domainEvent)
    {
      try
      {
        switch (domainEvent)
        {
          case ProductionStatusChangedEvent statusChangedEvent:
            await PublishAsync(statusChangedEvent);
            break;
          case ProductionAssignedEvent assignedEvent:
            await PublishAsync(assignedEvent);
            break;
          case QualityCheckFailedEvent qualityFailedEvent:
            await PublishAsync(qualityFailedEvent);
            break;
          case WorkflowStepCompletedEvent stepCompletedEvent:
            await PublishAsync(stepCompletedEvent);
            break;
          case ProductionOverdueEvent overdueEvent:
            await PublishAsync(overdueEvent);
            break;
          default:
            _logger.LogWarning("No specific handler found for event type {EventType}", domainEvent.GetType().Name);
            break;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to publish event {EventType} via fallback method", domainEvent.EventType);
        throw;
      }
    }
  }
}