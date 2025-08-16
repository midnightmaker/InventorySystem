// Services/IServiceOrderService.cs
using InventorySystem.Models;
using InventorySystem.ViewModels;

namespace InventorySystem.Services
{
    public interface IServiceOrderService
    {
        // Service Order CRUD
        Task<ServiceOrder?> GetServiceOrderByIdAsync(int id);
        Task<ServiceOrder?> GetServiceOrderByNumberAsync(string serviceOrderNumber);
        Task<IEnumerable<ServiceOrder>> GetAllServiceOrdersAsync();
        Task<IEnumerable<ServiceOrder>> GetServiceOrdersByCustomerAsync(int customerId);
        Task<IEnumerable<ServiceOrder>> GetServiceOrdersByStatusAsync(ServiceOrderStatus status);
        Task<IEnumerable<ServiceOrder>> GetOverdueServiceOrdersAsync();
        Task<ServiceOrder> CreateServiceOrderAsync(ServiceOrder serviceOrder);
        Task<ServiceOrder> UpdateServiceOrderAsync(ServiceOrder serviceOrder);
        Task DeleteServiceOrderAsync(int id);

        // Service Types
        Task<IEnumerable<ServiceType>> GetActiveServiceTypesAsync();
        Task<ServiceType?> GetServiceTypeByIdAsync(int id);
        Task<ServiceType> CreateServiceTypeAsync(ServiceType serviceType);
        Task<ServiceType> UpdateServiceTypeAsync(ServiceType serviceType);

        // Scheduling
        Task<IEnumerable<ServiceOrder>> GetScheduledServicesAsync(DateTime date);
        Task<IEnumerable<ServiceOrder>> GetUnscheduledServicesAsync();
        Task<ServiceOrder> ScheduleServiceAsync(int serviceOrderId, DateTime scheduledDate, string? technician = null);
        Task<IEnumerable<ServiceOrder>> GetServicesByTechnicianAsync(string technician, DateTime? date = null);

        // Time & Material Tracking
        Task<ServiceTimeLog> AddTimeLogAsync(ServiceTimeLog timeLog);
        Task<ServiceMaterial> AddMaterialAsync(ServiceMaterial material);
        Task<IEnumerable<ServiceTimeLog>> GetTimeLogsByServiceAsync(int serviceOrderId);
        Task<IEnumerable<ServiceMaterial>> GetMaterialsByServiceAsync(int serviceOrderId);

        // Status Management
        Task<ServiceOrder> UpdateServiceStatusAsync(int serviceOrderId, ServiceOrderStatus newStatus, string? reason = null, string? user = null);
        Task<bool> CanChangeStatusAsync(int serviceOrderId, ServiceOrderStatus newStatus);
        Task<IEnumerable<ServiceOrderStatus>> GetValidStatusChangesAsync(int serviceOrderId);

        // Document Management
        Task<ServiceDocument> AddServiceDocumentAsync(ServiceDocument document);
        Task<IEnumerable<ServiceDocument>> GetServiceDocumentsAsync(int serviceOrderId);
        Task<ServiceDocument?> GetServiceDocumentAsync(int documentId);
        Task DeleteServiceDocumentAsync(int documentId);

        // Business Logic
        Task<string> GenerateServiceOrderNumberAsync();
        Task<decimal> CalculateEstimatedCostAsync(int serviceTypeId, decimal? customHours = null);
        Task<IEnumerable<ServiceOrder>> GetServiceOrdersRequiringAttentionAsync();
        Task<bool> IsServiceCompletableAsync(int serviceOrderId);

        // Reporting & Analytics
        Task<ServiceDashboardViewModel> GetServiceDashboardAsync();
        Task<ServiceReportViewModel> GetServiceReportAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<ServiceOrder>> SearchServiceOrdersAsync(string searchTerm);
        Task<Dictionary<string, decimal>> GetTechnicianUtilizationAsync(DateTime startDate, DateTime endDate);

        // Integration Methods
        Task<ServiceOrder?> CreateServiceFromSaleAsync(int saleId, int serviceTypeId);
        Task<bool> HasPendingServicesAsync(int customerId);
        Task<decimal> GetCustomerServiceHistoryValueAsync(int customerId, DateTime? startDate = null);

        // Workflow Methods
        Task<bool> CompleteServiceAsync(int serviceOrderId, string completedBy, string? notes = null);
        Task<bool> StartServiceAsync(int serviceOrderId, string startedBy);
        Task<ServiceOrder> AssignTechnicianAsync(int serviceOrderId, string technician);
    }
}