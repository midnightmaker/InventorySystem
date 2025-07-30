// Services/ISalesService.cs
using InventorySystem.Models;
using InventorySystem.Models.Enums;

namespace InventorySystem.Services
{
  public interface ISalesService
  {
    // Sale methods
    Task<IEnumerable<Sale>> GetAllSalesAsync();
    Task<Sale?> GetSaleByIdAsync(int id);
    Task<Sale> CreateSaleAsync(Sale sale);
    Task<Sale> UpdateSaleAsync(Sale sale);
    Task DeleteSaleAsync(int id);
    Task<string> GenerateSaleNumberAsync();

    // Sale item methods
    Task<SaleItem> AddSaleItemAsync(SaleItem saleItem);
    Task<SaleItem> UpdateSaleItemAsync(SaleItem saleItem);
    Task DeleteSaleItemAsync(int saleItemId);

    // Process sales (reduce inventory)
    Task<bool> ProcessSaleAsync(int saleId);
    Task<bool> CanProcessSaleAsync(int saleId);

    // Statistics
    Task<decimal> GetTotalSalesValueAsync();
    Task<decimal> GetTotalSalesValueByMonthAsync(int year, int month);
    Task<decimal> GetTotalProfitAsync();
    Task<decimal> GetTotalProfitByMonthAsync(int year, int month);
    Task<int> GetTotalSalesCountAsync();
    Task<IEnumerable<Sale>> GetSalesByCustomerAsync(string customerName);
    Task<IEnumerable<Sale>> GetSalesByStatusAsync(SaleStatus status);
  }
}