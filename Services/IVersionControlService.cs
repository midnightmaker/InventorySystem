using InventorySystem.Models;

namespace InventorySystem.Services
{
  public interface IVersionControlService
  {
    // Item Version Management
    Task<Item> CreateItemVersionAsync(int baseItemId, string newVersion, int changeOrderId);
    Task<IEnumerable<Item>> GetItemVersionsAsync(int baseItemId);
    Task<Item?> GetItemVersionAsync(int baseItemId, string version);
    Task<Item?> GetCurrentItemVersionAsync(int baseItemId);

    // BOM Version Management
    Task<Bom> CreateBomVersionAsync(int baseBomId, string newVersion, int changeOrderId);
    Task<IEnumerable<Bom>> GetBomVersionsAsync(int baseBomId);
    Task<Bom?> GetBomVersionAsync(int baseBomId, string version);
    Task<Bom?> GetCurrentBomVersionAsync(int baseBomId);

    // Change Order Management
    Task<ChangeOrder> CreateChangeOrderAsync(ChangeOrder changeOrder);
    Task<bool> ImplementChangeOrderAsync(int changeOrderId, string implementedBy);
    Task<IEnumerable<ChangeOrder>> GetPendingChangeOrdersAsync();
    Task<IEnumerable<ChangeOrder>> GetAllChangeOrdersAsync();
    Task<IEnumerable<ChangeOrder>> GetChangeOrdersByEntityAsync(string entityType, int entityId);
    Task<ChangeOrder?> GetChangeOrderByIdAsync(int changeOrderId);
    Task<bool> CancelChangeOrderAsync(int changeOrderId, string cancelledBy);
  }
}
