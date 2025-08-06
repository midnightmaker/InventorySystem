using InventorySystem.Data;
using InventorySystem.Models;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
    public class CompanyInfoService : ICompanyInfoService
    {
        private readonly InventoryContext _context;

        public CompanyInfoService(InventoryContext context)
        {
            _context = context;
        }

        public async Task<CompanyInfo> GetCompanyInfoAsync()
        {
            var companyInfo = await _context.CompanyInfo.FirstOrDefaultAsync();
            
            if (companyInfo == null)
            {
                // Create default company info if none exists
                companyInfo = await CreateDefaultCompanyInfoAsync();
            }

            return companyInfo;
        }

        public async Task<CompanyInfo> UpdateCompanyInfoAsync(CompanyInfo companyInfo)
        {
            var existingCompanyInfo = await _context.CompanyInfo.FirstOrDefaultAsync();

            if (existingCompanyInfo == null)
            {
                // Create new
                companyInfo.CreatedDate = DateTime.Now;
                companyInfo.LastUpdated = DateTime.Now;
                _context.CompanyInfo.Add(companyInfo);
            }
            else
            {
                // Update existing
                existingCompanyInfo.CompanyName = companyInfo.CompanyName;
                existingCompanyInfo.Address = companyInfo.Address;
                existingCompanyInfo.AddressLine2 = companyInfo.AddressLine2;
                existingCompanyInfo.City = companyInfo.City;
                existingCompanyInfo.State = companyInfo.State;
                existingCompanyInfo.ZipCode = companyInfo.ZipCode;
                existingCompanyInfo.Country = companyInfo.Country;
                existingCompanyInfo.Phone = companyInfo.Phone;
                existingCompanyInfo.Fax = companyInfo.Fax;
                existingCompanyInfo.Email = companyInfo.Email;
                existingCompanyInfo.Website = companyInfo.Website;
                existingCompanyInfo.TaxId = companyInfo.TaxId;
                existingCompanyInfo.BusinessLicense = companyInfo.BusinessLicense;
                existingCompanyInfo.Description = companyInfo.Description;
                existingCompanyInfo.PrimaryContactName = companyInfo.PrimaryContactName;
                existingCompanyInfo.PrimaryContactTitle = companyInfo.PrimaryContactTitle;
                existingCompanyInfo.PrimaryContactEmail = companyInfo.PrimaryContactEmail;
                existingCompanyInfo.PrimaryContactPhone = companyInfo.PrimaryContactPhone;
                existingCompanyInfo.LastUpdated = DateTime.Now;

                // Only update logo if new data is provided
                if (companyInfo.LogoData != null)
                {
                    existingCompanyInfo.LogoData = companyInfo.LogoData;
                    existingCompanyInfo.LogoContentType = companyInfo.LogoContentType;
                    existingCompanyInfo.LogoFileName = companyInfo.LogoFileName;
                }

                companyInfo = existingCompanyInfo;
            }

            await _context.SaveChangesAsync();
            return companyInfo;
        }

        public async Task<CompanyInfo> UpdateCompanyLogoAsync(int id, byte[] logoData, string contentType, string fileName)
        {
            var companyInfo = await _context.CompanyInfo.FindAsync(id);
            if (companyInfo == null)
            {
                throw new InvalidOperationException("Company info not found");
            }

            companyInfo.LogoData = logoData;
            companyInfo.LogoContentType = contentType;
            companyInfo.LogoFileName = fileName;
            companyInfo.LastUpdated = DateTime.Now;

            await _context.SaveChangesAsync();
            return companyInfo;
        }

        public async Task<CompanyInfo> RemoveCompanyLogoAsync(int id)
        {
            var companyInfo = await _context.CompanyInfo.FindAsync(id);
            if (companyInfo == null)
            {
                throw new InvalidOperationException("Company info not found");
            }

            companyInfo.LogoData = null;
            companyInfo.LogoContentType = null;
            companyInfo.LogoFileName = null;
            companyInfo.LastUpdated = DateTime.Now;

            await _context.SaveChangesAsync();
            return companyInfo;
        }

        public async Task<bool> CompanyInfoExistsAsync()
        {
            return await _context.CompanyInfo.AnyAsync();
        }

        public async Task<CompanyInfo> CreateDefaultCompanyInfoAsync()
        {
            var defaultCompanyInfo = new CompanyInfo();
            _context.CompanyInfo.Add(defaultCompanyInfo);
            await _context.SaveChangesAsync();
            return defaultCompanyInfo;
        }
    }
}