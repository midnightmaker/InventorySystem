namespace InventorySystem.Domain.Queries
{
  public interface IQuery<TResult>
  {
    DateTime RequestedAt { get; }
  }
}
