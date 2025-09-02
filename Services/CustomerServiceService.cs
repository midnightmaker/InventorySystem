using InventorySystem.Data;
using InventorySystem.Models.CustomerService;
using InventorySystem.Models.Enums;
using InventorySystem.ViewModels.CustomerService;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
    public class CustomerServiceService : ICustomerServiceService
    {
        private readonly InventoryContext _context;
        private readonly ILogger<CustomerServiceService> _logger;

        public CustomerServiceService(InventoryContext context, ILogger<CustomerServiceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CustomerServiceDashboardViewModel> GetDashboardAsync()
        {
            var today = DateTime.Today;
            var cases = await _context.SupportCases.Include(c => c.Customer).ToListAsync();

            var dashboard = new CustomerServiceDashboardViewModel
            {
                TotalOpenCases = cases.Count(c => c.IsOpen),
                TotalCasesToday = cases.Count(c => c.CreatedDate.Date == today),
                TotalOverdueCases = cases.Count(c => c.IsOverdue),
                TotalEscalatedCases = cases.Count(c => c.Status == CaseStatus.Escalated),
                AverageResponseTimeHours = cases.Where(c => c.FirstResponseDate.HasValue)
                    .Select(c => (decimal)(c.ResponseTimeHours ?? 0))
                    .DefaultIfEmpty(0m)
                    .Average(),
                AverageResolutionTimeHours = cases.Where(c => c.ResolutionDate.HasValue)
                    .Select(c => (decimal)(c.ResolutionTimeHours ?? 0))
                    .DefaultIfEmpty(0m)
                    .Average(),
                CustomerSatisfactionAverage = cases.Where(c => c.CustomerSatisfactionRating.HasValue)
                    .Select(c => (decimal)(c.CustomerSatisfactionRating ?? 0))
                    .DefaultIfEmpty(0m)
                    .Average(),
                FirstCallResolutionRate = CalculateFirstCallResolutionRate(cases),
                RecentCases = cases.OrderByDescending(c => c.CreatedDate).Take(10).ToList(),
                HighPriorityCases = cases.Where(c => c.Priority == CasePriority.High || c.Priority == CasePriority.Critical)
                    .OrderByDescending(c => c.CreatedDate).Take(10).ToList(),
                CasesByStatus = cases.GroupBy(c => c.Status)
                    .ToDictionary(g => g.Key, g => g.Count()),
                CasesByPriority = cases.GroupBy(c => c.Priority)
                    .ToDictionary(g => g.Key, g => g.Count())
                
            };

            return dashboard;
        }

        public async Task<IEnumerable<SupportCase>> GetSupportCasesAsync(string? status = null, string? priority = null, string? search = null)
        {
            var query = _context.SupportCases.Include(c => c.Customer).AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<CaseStatus>(status, out var statusEnum))
            {
                query = query.Where(c => c.Status == statusEnum);
            }

            if (!string.IsNullOrEmpty(priority) && Enum.TryParse<CasePriority>(priority, out var priorityEnum))
            {
                query = query.Where(c => c.Priority == priorityEnum);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.CaseNumber.Contains(search) ||
                                        c.Subject.Contains(search) ||
                                        c.Customer.CustomerName.Contains(search));
            }

            return await query.OrderByDescending(c => c.CreatedDate).ToListAsync();
        }

        public async Task<SupportCase?> GetSupportCaseByIdAsync(int id)
        {
            return await _context.SupportCases
                .Include(c => c.Customer)
                .Include(c => c.CaseUpdates)
                .Include(c => c.CaseDocuments)
                .Include(c => c.RelatedSale)
                .Include(c => c.RelatedServiceOrder)
                .Include(c => c.RelatedProduct)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<SupportCase> CreateSupportCaseAsync(SupportCase supportCase)
        {
            _context.SupportCases.Add(supportCase);
            await _context.SaveChangesAsync();
            return supportCase;
        }

        public async Task<bool> UpdateSupportCaseAsync(SupportCase supportCase)
        {
            try
            {
                _context.SupportCases.Update(supportCase);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating support case {CaseId}", supportCase.Id);
                return false;
            }
        }

        public async Task<bool> DeleteSupportCaseAsync(int id)
        {
            try
            {
                var supportCase = await _context.SupportCases.FindAsync(id);
                if (supportCase != null)
                {
                    _context.SupportCases.Remove(supportCase);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting support case {CaseId}", id);
                return false;
            }
        }

        public async Task<IEnumerable<SupportCase>> GetAssignedCasesAsync(string? assignee)
        {
            if (string.IsNullOrEmpty(assignee))
                return new List<SupportCase>();

            return await _context.SupportCases
                .Include(c => c.Customer)
                .Where(c => c.AssignedTo == assignee && c.IsOpen)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<SupportCase>> GetOverdueCasesAsync()
        {
            return await _context.SupportCases
                .Include(c => c.Customer)
                .Where(c => c.IsOverdue)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();
        }

        public async Task<bool> AddCaseUpdateAsync(AddCaseUpdateViewModel model)
        {
            try
            {
                var update = new CaseUpdate
                {
                    SupportCaseId = model.SupportCaseId,
                    UpdateText = model.UpdateText,
                    UpdateType = model.UpdateType,
                    IsInternal = model.IsInternal,
                    TimeSpentHours = model.TimeSpentHours,
                    WorkCategory = model.WorkCategory,
                    UpdatedBy = model.UpdatedBy
                };

                _context.CaseUpdates.Add(update);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding case update");
                return false;
            }
        }

        public async Task<bool> UpdateCaseStatusAsync(int caseId, string newStatus, string? updatedBy)
        {
            try
            {
                var supportCase = await _context.SupportCases.FindAsync(caseId);
                if (supportCase == null) return false;

                if (Enum.TryParse<CaseStatus>(newStatus, out var statusEnum))
                {
                    var oldStatus = supportCase.Status;
                    supportCase.Status = statusEnum;
                    supportCase.LastModifiedBy = updatedBy;
                    supportCase.LastModifiedDate = DateTime.Now;

                    // Add status change update
                    var update = new CaseUpdate
                    {
                        SupportCaseId = caseId,
                        UpdateText = $"Status changed from {oldStatus} to {statusEnum}",
                        UpdateType = CaseUpdateType.StatusChange,
                        PreviousStatus = oldStatus,
                        NewStatus = statusEnum,
                        UpdatedBy = updatedBy ?? "System"
                    };

                    _context.CaseUpdates.Add(update);
                    await _context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating case status");
                return false;
            }
        }

        public async Task<bool> AssignCaseAsync(int caseId, string assignee, string? assignedBy)
        {
            try
            {
                var supportCase = await _context.SupportCases.FindAsync(caseId);
                if (supportCase == null) return false;

                var oldAssignee = supportCase.AssignedTo;
                supportCase.AssignedTo = assignee;
                supportCase.LastModifiedBy = assignedBy;
                supportCase.LastModifiedDate = DateTime.Now;

                // Add assignment update
                var update = new CaseUpdate
                {
                    SupportCaseId = caseId,
                    UpdateText = $"Case assigned to {assignee}",
                    UpdateType = CaseUpdateType.Assignment,
                    PreviousAssignee = oldAssignee,
                    NewAssignee = assignee,
                    UpdatedBy = assignedBy ?? "System"
                };

                _context.CaseUpdates.Add(update);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning case");
                return false;
            }
        }

        public async Task<CustomerServiceReportViewModel> GetReportsAsync()
        {
            // Implementation for reports
            return new CustomerServiceReportViewModel();
        }


        // Helper Methods
        private decimal CalculateFirstCallResolutionRate(List<SupportCase> cases)
        {
            var resolvedCases = cases.Where(c => c.IsResolved).ToList();
            if (!resolvedCases.Any()) return 0;

            var firstCallResolutions = resolvedCases.Count(c => c.UpdateCount <= 1);
            return (decimal)firstCallResolutions / resolvedCases.Count * 100;
        }

        
    }
}