using InventorySystem.Models;

namespace InventorySystem.Services
{
    public interface IBomService
    {
        Task<IEnumerable<Bom>> GetAllBomsAsync();
        Task<Bom?> GetBomByIdAsync(int id);
        Task<Bom> CreateBomAsync(Bom bom);
        Task<Bom> UpdateBomAsync(Bom bom);
        Task DeleteBomAsync(int id);
        Task<decimal> GetBomTotalCostAsync(int bomId);
        Task<BomItem> AddBomItemAsync(BomItem bomItem);
        Task DeleteBomItemAsync(int bomItemId);
    }
}