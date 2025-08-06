using InventorySystem.Models;

namespace InventorySystem.Services
{
    public interface ICompanyInfoService
    {
        Task<CompanyInfo> GetCompanyInfoAsync();
        Task<CompanyInfo> UpdateCompanyInfoAsync(CompanyInfo companyInfo);
        Task<CompanyInfo> UpdateCompanyLogoAsync(int id, byte[] logoData, string contentType, string fileName);
        Task<CompanyInfo> RemoveCompanyLogoAsync(int id);
        Task<bool> CompanyInfoExistsAsync();
        Task<CompanyInfo> CreateDefaultCompanyInfoAsync();
    }
}