// Services/IBomService.cs
using InventorySystem.Models;

namespace InventorySystem.Services
{
  public interface IBomService
  {
    #region Basic CRUD Operations
    Task<IEnumerable<Bom>> GetAllBomsAsync();
    Task<Bom?> GetBomByIdAsync(int id);
    Task<Bom> CreateBomAsync(Bom bom);
    Task<Bom> UpdateBomAsync(Bom bom);
    Task DeleteBomAsync(int id);

    /// <summary>
    /// Returns a detailed impact report describing whether the BOM can be safely deleted.
    /// </summary>
    Task<BomDeletabilityResult> CheckBomDeletabilityAsync(int id);

    /// <summary>
    /// Removes the sub-assembly link between a child BOM and its parent without deleting either BOM.
    /// </summary>
    Task DetachSubAssemblyAsync(int subAssemblyBomId, int parentBomId);
    #endregion

    #region Production-Specific Methods (CRITICAL FOR PRODUCTION FIXES)
    /// <summary>
    /// Gets only current version BOMs for production dropdown - CRITICAL for production consistency
    /// </summary>
    Task<IEnumerable<Bom>> GetCurrentVersionBomsAsync();

    /// <summary>
    /// Gets current version BOM for production - CRITICAL for version consistency
    /// </summary>
    Task<Bom?> GetCurrentVersionBomByIdAsync(int bomId);

    /// <summary>
    /// Validates if BOM is ready for production
    /// </summary>
    Task<ProductionValidationResult> ValidateBomForProductionAsync(int bomId);
    #endregion

    #region BOM Item Management
    Task<BomItem> AddBomItemAsync(BomItem bomItem);
    Task<BomItem> UpdateBomItemAsync(BomItem bomItem);
    Task DeleteBomItemAsync(int bomItemId);
    #endregion

    #region Cost Calculations
    Task<decimal> GetBomTotalCostAsync(int bomId);
    Task<decimal> GetBomMaterialCostAsync(int bomId, int quantity = 1);

    /// <summary>
    /// Gets exploded BOM cost data including all sub-assembly details
    /// </summary>
    Task<ExplodedBomCostData> GetExplodedBomCostDataAsync(int bomId);
    #endregion

    #region Search and Filtering
    Task<IEnumerable<Bom>> SearchBomsAsync(string searchTerm);
    Task<IEnumerable<Bom>> GetBomsByAssemblyPartNumberAsync(string assemblyPartNumber);
    Task<IEnumerable<Bom>> GetBomsByItemIdAsync(int itemId);
    #endregion

    #region Helper Methods
    Task<bool> BomExistsAsync(int bomId);
    Task<bool> BomNumberExistsAsync(string bomNumber, int? excludeId = null);
    Task<string> GenerateNextBomNumberAsync(string prefix = "BOM");
    Task<object> GetBomHierarchyAsync(int bomId);
    #endregion

    #region Statistics and Reporting
    Task<BomStatistics> GetBomStatisticsAsync();
    #endregion
  }
}