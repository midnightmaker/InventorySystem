// Services/ServiceOrderService.cs
using InventorySystem.Data;
using InventorySystem.Models;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
    public class ServiceOrderService : IServiceOrderService
    {
        private readonly InventoryContext _context;
        private readonly ILogger<ServiceOrderService> _logger;

        public ServiceOrderService(InventoryContext context, ILogger<ServiceOrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceOrder?> GetServiceOrderByIdAsync(int id)
        {
            return await _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Include(so => so.Sale)
                .Include(so => so.TimeLogs)
                .Include(so => so.Materials)
                    .ThenInclude(m => m.Item)
                .Include(so => so.Documents)
                .FirstOrDefaultAsync(so => so.Id == id);
        }

        public async Task<ServiceOrder?> GetServiceOrderByNumberAsync(string serviceOrderNumber)
        {
            return await _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Include(so => so.Sale)
                .FirstOrDefaultAsync(so => so.ServiceOrderNumber == serviceOrderNumber);
        }

        public async Task<IEnumerable<ServiceOrder>> GetAllServiceOrdersAsync()
        {
            return await _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Include(so => so.Sale)
                .OrderByDescending(so => so.RequestDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceOrder>> GetServiceOrdersByCustomerAsync(int customerId)
        {
            return await _context.ServiceOrders
                .Include(so => so.ServiceType)
                .Include(so => so.Sale)
                .Where(so => so.CustomerId == customerId)
                .OrderByDescending(so => so.RequestDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceOrder>> GetServiceOrdersByStatusAsync(ServiceOrderStatus status)
        {
            return await _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Where(so => so.Status == status)
                .OrderBy(so => so.PromisedDate ?? so.RequestDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceOrder>> GetOverdueServiceOrdersAsync()
        {
            var today = DateTime.Today;
            return await _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Where(so => so.PromisedDate.HasValue && 
                           so.PromisedDate.Value.Date < today &&
                           so.Status != ServiceOrderStatus.Completed &&
                           so.Status != ServiceOrderStatus.Delivered &&
                           so.Status != ServiceOrderStatus.Cancelled)
                .OrderBy(so => so.PromisedDate)
                .ToListAsync();
        }

        public async Task<ServiceOrder> CreateServiceOrderAsync(ServiceOrder serviceOrder)
        {
            try
            {
                // ? ADD: Validate required foreign keys exist
                var customer = await _context.Customers.FindAsync(serviceOrder.CustomerId);
                if (customer == null)
                {
                    throw new ArgumentException($"Customer with ID {serviceOrder.CustomerId} not found", nameof(serviceOrder.CustomerId));
                }

                var serviceType = await _context.ServiceTypes.FindAsync(serviceOrder.ServiceTypeId);
                if (serviceType == null)
                {
                    throw new ArgumentException($"ServiceType with ID {serviceOrder.ServiceTypeId} not found", nameof(serviceOrder.ServiceTypeId));
                }

                // ? ADD: Validate optional Sale foreign key if provided
                if (serviceOrder.SaleId.HasValue)
                {
                    var sale = await _context.Sales.FindAsync(serviceOrder.SaleId.Value);
                    if (sale == null)
                    {
                        throw new ArgumentException($"Sale with ID {serviceOrder.SaleId.Value} not found", nameof(serviceOrder.SaleId));
                    }
                }

                // Generate service order number if not provided
                if (string.IsNullOrEmpty(serviceOrder.ServiceOrderNumber))
                {
                    serviceOrder.ServiceOrderNumber = await GenerateServiceOrderNumberAsync();
                }

                // Set default values
                serviceOrder.CreatedDate = DateTime.Now;
                if (serviceOrder.Status == 0) // Default enum value
                {
                    serviceOrder.Status = ServiceOrderStatus.Requested;
                }

                // Get service type defaults if not already set
                if (serviceOrder.EstimatedHours == 0)
                    serviceOrder.EstimatedHours = serviceType.StandardHours;
                
                if (serviceOrder.HourlyRate == 0)
                    serviceOrder.HourlyRate = serviceType.StandardRate;
                
                if (serviceOrder.EstimatedCost == 0)
                    serviceOrder.EstimatedCost = serviceOrder.EstimatedHours * serviceOrder.HourlyRate;
                
                serviceOrder.QcRequired = serviceType.QcRequired;
                serviceOrder.CertificateRequired = serviceType.CertificateRequired;

                _context.ServiceOrders.Add(serviceOrder);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created service order {ServiceOrderNumber} for customer {CustomerId}", 
                    serviceOrder.ServiceOrderNumber, serviceOrder.CustomerId);

                return serviceOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service order for customer {CustomerId}, service type {ServiceTypeId}", 
                    serviceOrder.CustomerId, serviceOrder.ServiceTypeId);
                throw;
            }
        }

        public async Task<ServiceOrder> UpdateServiceOrderAsync(ServiceOrder serviceOrder)
        {
            serviceOrder.LastModifiedDate = DateTime.Now;
            _context.ServiceOrders.Update(serviceOrder);
            await _context.SaveChangesAsync();
            return serviceOrder;
        }

        public async Task DeleteServiceOrderAsync(int id)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(id);
            if (serviceOrder != null)
            {
                if (serviceOrder.Status == ServiceOrderStatus.InProgress)
                {
                    throw new InvalidOperationException("Cannot delete a service order that is in progress");
                }

                _context.ServiceOrders.Remove(serviceOrder);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ServiceType>> GetActiveServiceTypesAsync()
        {
            return await _context.ServiceTypes
                .Where(st => st.IsActive)
                .OrderBy(st => st.ServiceName)
                .ToListAsync();
        }

        public async Task<ServiceType?> GetServiceTypeByIdAsync(int id)
        {
            return await _context.ServiceTypes.FindAsync(id);
        }

        public async Task<ServiceType> CreateServiceTypeAsync(ServiceType serviceType)
        {
            _context.ServiceTypes.Add(serviceType);
            await _context.SaveChangesAsync();
            return serviceType;
        }

        public async Task<ServiceType> UpdateServiceTypeAsync(ServiceType serviceType)
        {
            _context.ServiceTypes.Update(serviceType);
            await _context.SaveChangesAsync();
            return serviceType;
        }

        public async Task<string> GenerateServiceOrderNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"SO-{today:yyyyMMdd}";
            
            var lastOrder = await _context.ServiceOrders
                .Where(so => so.ServiceOrderNumber.StartsWith(prefix))
                .OrderByDescending(so => so.ServiceOrderNumber)
                .FirstOrDefaultAsync();

            if (lastOrder == null)
            {
                return $"{prefix}-001";
            }

            var lastNumber = lastOrder.ServiceOrderNumber.Split('-').LastOrDefault();
            if (int.TryParse(lastNumber, out var number))
            {
                return $"{prefix}-{(number + 1):D3}";
            }

            return $"{prefix}-001";
        }

        public async Task<decimal> CalculateEstimatedCostAsync(int serviceTypeId, decimal? customHours = null)
        {
            var serviceType = await _context.ServiceTypes.FindAsync(serviceTypeId);
            if (serviceType == null)
                return 0;

            var hours = customHours ?? serviceType.StandardHours;
            return hours * serviceType.StandardRate;
        }

        public async Task<ServiceOrder> UpdateServiceStatusAsync(int serviceOrderId, ServiceOrderStatus newStatus, string? reason = null, string? user = null)
        {
            var serviceOrder = await GetServiceOrderByIdAsync(serviceOrderId);
            if (serviceOrder == null)
                throw new ArgumentException("Service order not found");

            var oldStatus = serviceOrder.Status;
            serviceOrder.UpdateStatus(newStatus, user);

            // Add status change logic
            switch (newStatus)
            {
                case ServiceOrderStatus.InProgress:
                    serviceOrder.StartedDate = DateTime.Now;
                    break;
                case ServiceOrderStatus.Completed:
                    serviceOrder.CompletedDate = DateTime.Now;
                    break;
                case ServiceOrderStatus.Cancelled:
                    // Handle cancellation logic
                    break;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Service order {ServiceOrderNumber} status changed from {OldStatus} to {NewStatus} by {User}. Reason: {Reason}",
                serviceOrder.ServiceOrderNumber, oldStatus, newStatus, user, reason);

            return serviceOrder;
        }

        public async Task<ServiceTimeLog> AddTimeLogAsync(ServiceTimeLog timeLog)
        {
            _context.ServiceTimeLogs.Add(timeLog);
            await _context.SaveChangesAsync();

            // Update service order actual hours
            var serviceOrder = await _context.ServiceOrders.FindAsync(timeLog.ServiceOrderId);
            if (serviceOrder != null)
            {
                serviceOrder.ActualHours = await _context.ServiceTimeLogs
                    .Where(tl => tl.ServiceOrderId == timeLog.ServiceOrderId)
                    .SumAsync(tl => tl.Hours);
                await _context.SaveChangesAsync();
            }

            return timeLog;
        }

        public async Task<ServiceMaterial> AddMaterialAsync(ServiceMaterial material)
        {
            _context.ServiceMaterials.Add(material);
            await _context.SaveChangesAsync();

            // Update inventory if item is tracked
            var item = await _context.Items.FindAsync(material.ItemId);
            if (item != null && item.ItemType == ItemType.Inventoried)
            {
                item.CurrentStock -= (int)material.QuantityUsed;
                await _context.SaveChangesAsync();
            }

            return material;
        }

        public async Task<ServiceDashboardViewModel> GetServiceDashboardAsync()
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            var activeServices = await _context.ServiceOrders
                .Where(so => so.Status != ServiceOrderStatus.Completed &&
                           so.Status != ServiceOrderStatus.Delivered &&
                           so.Status != ServiceOrderStatus.Cancelled)
                .ToListAsync();

            var dashboard = new ServiceDashboardViewModel
            {
                TotalActiveServices = activeServices.Count,
                ServicesScheduledToday = activeServices.Count(so => so.ScheduledDate?.Date == today),
                OverdueServices = activeServices.Count(so => so.IsOverdue),
                EmergencyServices = activeServices.Count(so => so.Priority == ServicePriority.Emergency),
                MonthlyRevenue = await _context.ServiceOrders
                    .Where(so => so.CompletedDate >= startOfMonth && so.CompletedDate <= today)
                    .SumAsync(so => so.TotalServiceCost),
                RecentServiceOrders = await _context.ServiceOrders
                    .Include(so => so.Customer)
                    .Include(so => so.ServiceType)
                    .OrderByDescending(so => so.RequestDate)
                    .Take(10)
                    .ToListAsync(),
                TodaysSchedule = activeServices
                    .Where(so => so.ScheduledDate?.Date == today)
                    .OrderBy(so => so.ScheduledDate)
                    .ToList(),
                OverdueList = activeServices
                    .Where(so => so.IsOverdue)
                    .OrderBy(so => so.PromisedDate)
                    .ToList()
            };

            return dashboard;
        }

        // Additional implementation methods would continue here...
        // For brevity, I'm showing the core structure

        public async Task<IEnumerable<ServiceOrder>> GetScheduledServicesAsync(DateTime date)
        {
            return await _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Where(so => so.ScheduledDate.HasValue && so.ScheduledDate.Value.Date == date.Date)
                .OrderBy(so => so.ScheduledDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceOrder>> GetUnscheduledServicesAsync()
        {
            return await _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Where(so => (so.Status == ServiceOrderStatus.Approved || so.Status == ServiceOrderStatus.Quoted) &&
                           !so.ScheduledDate.HasValue)
                .OrderBy(so => so.Priority)
                .ThenBy(so => so.RequestDate)
                .ToListAsync();
        }

        public async Task<ServiceOrder> ScheduleServiceAsync(int serviceOrderId, DateTime scheduledDate, string? technician = null)
        {
            var serviceOrder = await GetServiceOrderByIdAsync(serviceOrderId);
            if (serviceOrder == null)
                throw new ArgumentException("Service order not found");

            serviceOrder.ScheduledDate = scheduledDate;
            serviceOrder.AssignedTechnician = technician;
            serviceOrder.Status = ServiceOrderStatus.Scheduled;

            await _context.SaveChangesAsync();
            return serviceOrder;
        }

        public async Task<bool> CanChangeStatusAsync(int serviceOrderId, ServiceOrderStatus newStatus)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (serviceOrder == null) return false;

            // Implement business rules for status transitions
            return newStatus switch
            {
                ServiceOrderStatus.Scheduled => serviceOrder.Status == ServiceOrderStatus.Approved,
                ServiceOrderStatus.InProgress => serviceOrder.Status == ServiceOrderStatus.Scheduled,
                ServiceOrderStatus.Completed => serviceOrder.Status == ServiceOrderStatus.InProgress ||
                                              serviceOrder.Status == ServiceOrderStatus.QualityCheck,
                _ => true
            };
        }

        public async Task<IEnumerable<ServiceOrderStatus>> GetValidStatusChangesAsync(int serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (serviceOrder == null) return new List<ServiceOrderStatus>();

            return serviceOrder.Status switch
            {
                ServiceOrderStatus.Requested => new[] { ServiceOrderStatus.Quoted, ServiceOrderStatus.Approved, ServiceOrderStatus.Cancelled },
                ServiceOrderStatus.Quoted => new[] { ServiceOrderStatus.Approved, ServiceOrderStatus.Cancelled },
                ServiceOrderStatus.Approved => new[] { ServiceOrderStatus.Scheduled, ServiceOrderStatus.InProgress, ServiceOrderStatus.OnHold },
                ServiceOrderStatus.Scheduled => new[] { ServiceOrderStatus.InProgress, ServiceOrderStatus.OnHold },
                ServiceOrderStatus.InProgress => new[] { ServiceOrderStatus.AwaitingParts, ServiceOrderStatus.QualityCheck, ServiceOrderStatus.Completed },
                ServiceOrderStatus.AwaitingParts => new[] { ServiceOrderStatus.InProgress },
                ServiceOrderStatus.QualityCheck => new[] { ServiceOrderStatus.Completed, ServiceOrderStatus.InProgress },
                ServiceOrderStatus.Completed => new[] { ServiceOrderStatus.Delivered },
                _ => new List<ServiceOrderStatus>()
            };
        }

        // Additional methods would be implemented here...
        public async Task<IEnumerable<ServiceOrder>> GetServicesByTechnicianAsync(string technician, DateTime? date = null)
        {
            var query = _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Where(so => so.AssignedTechnician == technician);

            if (date.HasValue)
            {
                query = query.Where(so => so.ScheduledDate.HasValue && 
                                        so.ScheduledDate.Value.Date == date.Value.Date);
            }

            return await query.OrderBy(so => so.ScheduledDate).ToListAsync();
        }

        public async Task<IEnumerable<ServiceTimeLog>> GetTimeLogsByServiceAsync(int serviceOrderId)
        {
            return await _context.ServiceTimeLogs
                .Where(tl => tl.ServiceOrderId == serviceOrderId)
                .OrderByDescending(tl => tl.Date)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceMaterial>> GetMaterialsByServiceAsync(int serviceOrderId)
        {
            return await _context.ServiceMaterials
                .Include(sm => sm.Item)
                .Where(sm => sm.ServiceOrderId == serviceOrderId)
                .OrderBy(sm => sm.UsedDate)
                .ToListAsync();
        }

        public async Task<ServiceDocument> AddServiceDocumentAsync(ServiceDocument document)
        {
            _context.ServiceDocuments.Add(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<IEnumerable<ServiceDocument>> GetServiceDocumentsAsync(int serviceOrderId)
        {
            return await _context.ServiceDocuments
                .Where(sd => sd.ServiceOrderId == serviceOrderId)
                .OrderByDescending(sd => sd.UploadedDate)
                .ToListAsync();
        }

        public async Task<ServiceDocument?> GetServiceDocumentAsync(int documentId)
        {
            return await _context.ServiceDocuments.FindAsync(documentId);
        }

        public async Task DeleteServiceDocumentAsync(int documentId)
        {
            var document = await _context.ServiceDocuments.FindAsync(documentId);
            if (document != null)
            {
                _context.ServiceDocuments.Remove(document);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ServiceOrder>> GetServiceOrdersRequiringAttentionAsync()
        {
            var today = DateTime.Today;
            return await _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Where(so => so.IsOverdue || 
                           so.Priority == ServicePriority.Emergency ||
                           (so.Status == ServiceOrderStatus.AwaitingParts) ||
                           (so.QcRequired && !so.QcCompleted && so.Status == ServiceOrderStatus.QualityCheck))
                .OrderBy(so => so.Priority)
                .ThenBy(so => so.PromisedDate)
                .ToListAsync();
        }

        public async Task<bool> IsServiceCompletableAsync(int serviceOrderId)
        {
            var serviceOrder = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (serviceOrder == null) return false;

            // Check if QC is required and completed
            if (serviceOrder.QcRequired && !serviceOrder.QcCompleted)
                return false;

            // Check if status allows completion
            return serviceOrder.Status == ServiceOrderStatus.InProgress ||
                   serviceOrder.Status == ServiceOrderStatus.QualityCheck;
        }

        public async Task<ServiceReportViewModel> GetServiceReportAsync(DateTime startDate, DateTime endDate)
        {
            var services = await _context.ServiceOrders
                .Include(so => so.ServiceType)
                .Include(so => so.Customer)
                .Include(so => so.TimeLogs)
                .Include(so => so.Materials)
                .Where(so => so.RequestDate >= startDate && so.RequestDate <= endDate)
                .ToListAsync();

            return new ServiceReportViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalServiceOrders = services.Count,
                CompletedServiceOrders = services.Count(s => s.Status == ServiceOrderStatus.Completed),
                TotalRevenue = services.Sum(s => s.TotalServiceCost),
                TotalLaborHours = services.Sum(s => s.ActualHours),
                ServicesByType = services.GroupBy(s => s.ServiceType.ServiceName)
                                       .ToDictionary(g => g.Key, g => g.Count()),
                RevenueByType = services.GroupBy(s => s.ServiceType.ServiceName)
                                      .ToDictionary(g => g.Key, g => g.Sum(s => s.TotalServiceCost))
            };
        }

        public async Task<IEnumerable<ServiceOrder>> SearchServiceOrdersAsync(string searchTerm)
        {
            return await _context.ServiceOrders
                .Include(so => so.Customer)
                .Include(so => so.ServiceType)
                .Where(so => so.ServiceOrderNumber.Contains(searchTerm) ||
                           so.Customer.CustomerName.Contains(searchTerm) ||
                           so.ServiceType.ServiceName.Contains(searchTerm) ||
                           (so.EquipmentDetails != null && so.EquipmentDetails.Contains(searchTerm)) ||
                           (so.SerialNumber != null && so.SerialNumber.Contains(searchTerm)))
                .OrderByDescending(so => so.RequestDate)
                .ToListAsync();
        }

        public async Task<Dictionary<string, decimal>> GetTechnicianUtilizationAsync(DateTime startDate, DateTime endDate)
        {
            var timeLogs = await _context.ServiceTimeLogs
                .Where(tl => tl.Date >= startDate && tl.Date <= endDate)
                .GroupBy(tl => tl.Technician)
                .Select(g => new { Technician = g.Key, TotalHours = g.Sum(tl => tl.Hours) })
                .ToListAsync();

            return timeLogs.ToDictionary(t => t.Technician, t => t.TotalHours);
        }

        public async Task<ServiceOrder?> CreateServiceFromSaleAsync(int saleId, int serviceTypeId)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            if (sale == null) return null;

            var serviceOrder = new ServiceOrder
            {
                CustomerId = sale.CustomerId,
                SaleId = saleId,
                ServiceTypeId = serviceTypeId,
                RequestDate = DateTime.Today,
                IsPrepaid = true,
                PaymentMethod = "Credit Card", // Assuming from sale
                ServiceNotes = $"Service created from sale {sale.SaleNumber}"
            };

            return await CreateServiceOrderAsync(serviceOrder);
        }

        public async Task<bool> HasPendingServicesAsync(int customerId)
        {
            return await _context.ServiceOrders
                .AnyAsync(so => so.CustomerId == customerId &&
                              so.Status != ServiceOrderStatus.Completed &&
                              so.Status != ServiceOrderStatus.Delivered &&
                              so.Status != ServiceOrderStatus.Cancelled);
        }

        public async Task<decimal> GetCustomerServiceHistoryValueAsync(int customerId, DateTime? startDate = null)
        {
            var query = _context.ServiceOrders
                .Where(so => so.CustomerId == customerId &&
                           so.Status == ServiceOrderStatus.Completed);

            if (startDate.HasValue)
            {
                query = query.Where(so => so.CompletedDate >= startDate.Value);
            }

            return await query.SumAsync(so => so.TotalServiceCost);
        }

        public async Task<bool> CompleteServiceAsync(int serviceOrderId, string completedBy, string? notes = null)
        {
            var serviceOrder = await GetServiceOrderByIdAsync(serviceOrderId);
            if (serviceOrder == null || !serviceOrder.CanComplete())
                return false;

            serviceOrder.Status = ServiceOrderStatus.Completed;
            serviceOrder.CompletedDate = DateTime.Now;
            serviceOrder.LastModifiedBy = completedBy;
            
            if (!string.IsNullOrEmpty(notes))
            {
                serviceOrder.ServiceNotes = (serviceOrder.ServiceNotes + "\n" + notes).Trim();
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> StartServiceAsync(int serviceOrderId, string startedBy)
        {
            var serviceOrder = await GetServiceOrderByIdAsync(serviceOrderId);
            if (serviceOrder == null || !serviceOrder.CanStart())
                return false;

            serviceOrder.Status = ServiceOrderStatus.InProgress;
            serviceOrder.StartedDate = DateTime.Now;
            serviceOrder.LastModifiedBy = startedBy;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ServiceOrder> AssignTechnicianAsync(int serviceOrderId, string technician)
        {
            var serviceOrder = await GetServiceOrderByIdAsync(serviceOrderId);
            if (serviceOrder == null)
                throw new ArgumentException("Service order not found");

            serviceOrder.AssignedTechnician = technician;
            serviceOrder.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return serviceOrder;
        }
    }
}