// Infrastructure/Events/IEventPublisher.cs
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
      await PublishAsync<IDomainEvent>(domainEvent);
    }

    public async Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
    {
      try
      {
        var handlerType = typeof(IEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = _serviceProvider.GetServices(handlerType);

        var tasks = handlers.Cast<IEventHandler<T>>()
            .Select(handler => handler.HandleAsync(domainEvent));

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
  }
}

