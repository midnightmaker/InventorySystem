using InventorySystem.Domain.Events;

namespace InventorySystem.Infrastructure.Services
{
  public interface IEventHandler<in T> where T : IDomainEvent
  {
    Task HandleAsync(T domainEvent);
  }
}