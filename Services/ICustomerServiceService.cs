using InventorySystem.Models.CustomerService;
using InventorySystem.ViewModels.CustomerService;

namespace InventorySystem.Services
{
    public interface ICustomerServiceService
    {
        Task<CustomerServiceDashboardViewModel> GetDashboardAsync();
        Task<IEnumerable<SupportCase>> GetSupportCasesAsync(string? status = null, string? priority = null, string? search = null);
        Task<SupportCase?> GetSupportCaseByIdAsync(int id);
        Task<SupportCase> CreateSupportCaseAsync(SupportCase supportCase);
        Task<bool> UpdateSupportCaseAsync(SupportCase supportCase);
        Task<bool> DeleteSupportCaseAsync(int id);
        Task<IEnumerable<SupportCase>> GetAssignedCasesAsync(string? assignee);
        Task<IEnumerable<SupportCase>> GetOverdueCasesAsync();
        Task<bool> AddCaseUpdateAsync(AddCaseUpdateViewModel model);
        Task<bool> UpdateCaseStatusAsync(int caseId, string newStatus, string? updatedBy);
        Task<bool> AssignCaseAsync(int caseId, string assignee, string? assignedBy);
        
    }
}