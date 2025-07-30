namespace InventorySystem.Domain.Events
{
  public interface IDomainEvent
  {
    DateTime OccurredOn { get; }
    string EventType { get; }
  }
}