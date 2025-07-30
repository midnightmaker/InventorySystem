namespace InventorySystem.Domain.Commands
{
  public interface ICommand
  {
    DateTime RequestedAt { get; }
    string? RequestedBy { get; }
  }

  public interface ICommandResult
  {
    bool Success { get; }
    string? ErrorMessage { get; }
    object? Data { get; }
  }
}