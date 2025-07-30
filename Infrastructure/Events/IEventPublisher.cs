using InventorySystem.Domain.Events;

namespace InventorySystem.Infrastructure.Services
{
  public interface IEventPublisher
  {
    Task PublishAsync(IDomainEvent domainEvent);
    Task PublishAsync<T>(T domainEvent) where T : IDomainEvent;
  }
}
