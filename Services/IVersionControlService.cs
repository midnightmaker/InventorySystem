using InventorySystem.Models;

namespace InventorySystem.Services
{
  public interface IVersionControlService
  {
    #region Item Version Management

    /// <summary>
    /// Creates a new version of an item based on an existing item
    /// </summary>
    /// <param name="baseItemId">The ID of the base item to create a version from</param>
    /// <param name="newVersion">The version string for the new item</param>
    /// <param name="changeOrderId">The change order ID that triggered this version creation</param>
    /// <returns>The newly created item version</returns>
    Task<Item> CreateItemVersionAsync(int baseItemId, string newVersion, int changeOrderId);

    /// <summary>
    /// Gets all versions of an item
    /// </summary>
    /// <param name="baseItemId">The base item ID</param>
    /// <returns>Collection of all item versions</returns>
    Task<IEnumerable<Item>> GetItemVersionsAsync(int baseItemId);

    /// <summary>
    /// Gets a specific version of an item
    /// </summary>
    /// <param name="baseItemId">The base item ID</param>
    /// <param name="version">The version string to retrieve</param>
    /// <returns>The item version or null if not found</returns>
    Task<Item?> GetItemVersionAsync(int baseItemId, string version);

    /// <summary>
    /// Gets the current (active) version of an item
    /// </summary>
    /// <param name="baseItemId">The base item ID</param>
    /// <returns>The current item version or null if not found</returns>
    Task<Item?> GetCurrentItemVersionAsync(int baseItemId);

    #endregion

    #region BOM Version Management

    /// <summary>
    /// Creates a new version of a BOM based on an existing BOM
    /// </summary>
    /// <param name="baseBomId">The ID of the base BOM to create a version from</param>
    /// <param name="newVersion">The version string for the new BOM</param>
    /// <param name="changeOrderId">The change order ID that triggered this version creation</param>
    /// <returns>The newly created BOM version</returns>
    Task<Bom> CreateBomVersionAsync(int baseBomId, string newVersion, int changeOrderId);

    /// <summary>
    /// Gets all versions of a BOM
    /// </summary>
    /// <param name="baseBomId">The base BOM ID</param>
    /// <returns>Collection of all BOM versions</returns>
    Task<IEnumerable<Bom>> GetBomVersionsAsync(int baseBomId);

    /// <summary>
    /// Gets a specific version of a BOM
    /// </summary>
    /// <param name="baseBomId">The base BOM ID</param>
    /// <param name="version">The version string to retrieve</param>
    /// <returns>The BOM version or null if not found</returns>
    Task<Bom?> GetBomVersionAsync(int baseBomId, string version);

    /// <summary>
    /// Gets the current (active) version of a BOM
    /// </summary>
    /// <param name="baseBomId">The base BOM ID</param>
    /// <returns>The current BOM version or null if not found</returns>
    Task<Bom?> GetCurrentBomVersionAsync(int baseBomId);

    #endregion

    #region Change Order Management

    /// <summary>
    /// Creates a new change order with validation
    /// </summary>
    /// <param name="changeOrder">The change order to create</param>
    /// <returns>The created change order with generated number and related entities loaded</returns>
    Task<ChangeOrder> CreateChangeOrderAsync(ChangeOrder changeOrder);

    /// <summary>
    /// Implements a pending change order by creating the new version
    /// </summary>
    /// <param name="changeOrderId">The change order ID to implement</param>
    /// <param name="implementedBy">The user implementing the change order</param>
    /// <returns>True if implementation was successful, false otherwise</returns>
    Task<bool> ImplementChangeOrderAsync(int changeOrderId, string implementedBy);

    /// <summary>
    /// Cancels a pending change order
    /// </summary>
    /// <param name="changeOrderId">The change order ID to cancel</param>
    /// <param name="cancelledBy">The user cancelling the change order</param>
    /// <returns>True if cancellation was successful, false otherwise</returns>
    Task<bool> CancelChangeOrderAsync(int changeOrderId, string cancelledBy);

    /// <summary>
    /// Gets all pending change orders
    /// </summary>
    /// <returns>Collection of pending change orders</returns>
    Task<IEnumerable<ChangeOrder>> GetPendingChangeOrdersAsync();

    /// <summary>
    /// Gets all change orders with related entities and documents loaded
    /// </summary>
    /// <returns>Collection of all change orders</returns>
    Task<List<ChangeOrder>> GetAllChangeOrdersAsync();

    /// <summary>
    /// Gets change orders filtered by status
    /// </summary>
    /// <param name="status">The status to filter by (Pending, Implemented, Cancelled)</param>
    /// <returns>Collection of change orders with the specified status</returns>
    Task<List<ChangeOrder>> GetChangeOrdersByStatusAsync(string status);

    /// <summary>
    /// Gets change orders for a specific entity (Item or BOM)
    /// </summary>
    /// <param name="entityType">The entity type (Item or BOM)</param>
    /// <param name="entityId">The entity ID</param>
    /// <returns>Collection of change orders for the specified entity</returns>
    Task<IEnumerable<ChangeOrder>> GetChangeOrdersByEntityAsync(string entityType, int entityId);

    /// <summary>
    /// Gets change orders for a specific entity (Item or BOM) - List version
    /// </summary>
    /// <param name="entityType">The entity type (Item or BOM)</param>
    /// <param name="entityId">The entity ID</param>
    /// <returns>List of change orders for the specified entity</returns>
    Task<List<ChangeOrder>> GetChangeOrdersForEntityAsync(string entityType, int entityId);

    /// <summary>
    /// Gets a specific change order by ID with all related data loaded
    /// </summary>
    /// <param name="changeOrderId">The change order ID</param>
    /// <returns>The change order or null if not found</returns>
    Task<ChangeOrder?> GetChangeOrderByIdAsync(int changeOrderId);

    #endregion

    #region Change Order Validation

    /// <summary>
    /// Checks if an entity has any pending change orders
    /// </summary>
    /// <param name="entityType">The entity type (Item or BOM)</param>
    /// <param name="entityId">The entity ID</param>
    /// <returns>True if there are pending change orders, false otherwise</returns>
    Task<bool> HasPendingChangeOrdersAsync(string entityType, int entityId);

    /// <summary>
    /// Gets all pending change orders for a specific entity
    /// </summary>
    /// <param name="entityType">The entity type (Item or BOM)</param>
    /// <param name="entityId">The entity ID</param>
    /// <returns>Collection of pending change orders for the specified entity</returns>
    Task<List<ChangeOrder>> GetPendingChangeOrdersForEntityAsync(string entityType, int entityId);

    #endregion

    #region Statistics and Reporting

    /// <summary>
    /// Gets comprehensive statistics about change orders including document counts
    /// </summary>
    /// <returns>Change order statistics object</returns>
    Task<ChangeOrderStatistics> GetChangeOrderStatisticsAsync();

    #endregion
  }
}