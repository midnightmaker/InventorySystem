using InventorySystem.Models;

namespace InventorySystem.Services
{
  public interface IBomService
  {
    // Existing methods
    Task<IEnumerable<Bom>> GetAllBomsAsync();
    Task<Bom?> GetBomByIdAsync(int id);
    Task<Bom> CreateBomAsync(Bom bom);
    Task<Bom> UpdateBomAsync(Bom bom);
    Task DeleteBomAsync(int id);
    Task<decimal> GetBomTotalCostAsync(int bomId);
    Task<BomItem> AddBomItemAsync(BomItem bomItem);
    Task DeleteBomItemAsync(int bomItemId);

    // Enhanced methods for dashboard functionality
    Task<decimal> GetTotalBomValueAsync();
    Task<int> GetTotalBomItemsCountAsync();
    Task<int> GetCompleteBomCountAsync();
    Task<int> GetIncompleteBomCountAsync();
    Task<IEnumerable<Bom>> GetBomsCreatedInMonthAsync(int year, int month);
  }
}