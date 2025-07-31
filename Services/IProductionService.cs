// Services/IProductionService.cs
using InventorySystem.Models;
using InventorySystem.ViewModels;

namespace InventorySystem.Services
{
  public interface IProductionService
  {
    // Production methods
    Task<IEnumerable<Production>> GetAllProductionsAsync();
    Task<Production?> GetProductionByIdAsync(int id);
    Task<Production> CreateProductionAsync(Production production);
    Task<Production> UpdateProductionAsync(Production production);
    Task DeleteProductionAsync(int id);

    // Build BOM into finished goods
    Task<Production> BuildBomAsync(int bomId, int quantity, decimal laborCost = 0, decimal overheadCost = 0, string? notes = null);
    Task<bool> CanBuildBomAsync(int bomId, int quantity);
    Task<decimal> CalculateBomMaterialCostAsync(int bomId, int quantity);

    // Finished goods methods
    Task<IEnumerable<FinishedGood>> GetAllFinishedGoodsAsync();
    Task<FinishedGood?> GetFinishedGoodByIdAsync(int id);
    Task<FinishedGood> CreateFinishedGoodAsync(FinishedGood finishedGood);
    Task<FinishedGood> UpdateFinishedGoodAsync(FinishedGood finishedGood);
    Task DeleteFinishedGoodAsync(int id);
    Task<IEnumerable<FinishedGood>> GetLowStockFinishedGoodsAsync();

    // Statistics
    Task<decimal> GetTotalFinishedGoodsValueAsync();
    Task<int> GetTotalFinishedGoodsCountAsync();
    Task<decimal> GetTotalProductionCostAsync();

    Task<MaterialShortageViewModel> GetMaterialShortageAnalysisAsync(int bomId, int quantity);
    Task<IEnumerable<MaterialShortageItem>> GetBomMaterialShortagesAsync(int bomId, int quantity);
    Task<IEnumerable<MaterialRequirement>> GetBomMaterialRequirementsAsync(int bomId, int quantity);

   
  }
}
