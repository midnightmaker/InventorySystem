using InventorySystem.Data;
using InventorySystem.Models.Accounting;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Services
{
    public class FinancialPeriodService : IFinancialPeriodService
    {
        private readonly InventoryContext _context;
        private readonly ILogger<FinancialPeriodService> _logger;

        public FinancialPeriodService(InventoryContext context, ILogger<FinancialPeriodService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CompanySettings> GetCompanySettingsAsync()
        {
            var settings = await _context.CompanySettings
                .Include(s => s.CurrentFinancialPeriod)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                // Create default settings
                settings = new CompanySettings
                {
                    CompanyName = "My Company",
                    FinancialYearStartMonth = 1, // January (calendar year)
                    FinancialYearStartDay = 1,
                    DefaultReportPeriod = DefaultReportPeriod.CurrentFinancialYear,
                    AutoCreateFinancialPeriods = true
                };

                _context.CompanySettings.Add(settings);
                await _context.SaveChangesAsync();

                // Create current financial year — pass the already-saved settings so
                // CreateCurrentFinancialYearIfNeeded does NOT call GetCompanySettingsAsync again.
                await CreateCurrentFinancialYearIfNeeded(settings);
            }

            return settings;
        }

        public async Task<CompanySettings> UpdateCompanySettingsAsync(CompanySettings settings)
        {
            var existing = await _context.CompanySettings.FirstOrDefaultAsync();
            if (existing != null)
            {
                existing.CompanyName = settings.CompanyName;
                existing.FinancialYearStartMonth = settings.FinancialYearStartMonth;
                existing.FinancialYearStartDay = settings.FinancialYearStartDay;
                existing.DefaultReportPeriod = settings.DefaultReportPeriod;
                existing.AutoCreateFinancialPeriods = settings.AutoCreateFinancialPeriods;
                existing.LastUpdated = DateTime.Now;
                existing.UpdatedBy = settings.UpdatedBy;

                await _context.SaveChangesAsync();
                return existing;
            }

            _context.CompanySettings.Add(settings);
            await _context.SaveChangesAsync();
            return settings;
        }

        public async Task<FinancialPeriod?> GetCurrentFinancialPeriodAsync()
        {
            var settings = await GetCompanySettingsAsync();
            
            if (settings.CurrentFinancialPeriodId.HasValue)
            {
                return await _context.FinancialPeriods
                    .FirstOrDefaultAsync(p => p.Id == settings.CurrentFinancialPeriodId.Value);
            }

            // Find current period by date
            var today = DateTime.Today;
            return await _context.FinancialPeriods
                .FirstOrDefaultAsync(p => p.StartDate <= today && p.EndDate >= today && !p.IsClosed);
        }

        public async Task<(DateTime start, DateTime end)> GetDefaultReportDateRangeAsync()
        {
            var settings = await GetCompanySettingsAsync();
            
            return settings.DefaultReportPeriod switch
            {
                DefaultReportPeriod.CurrentFinancialYear => await GetCurrentFinancialYearRangeAsync(),
                DefaultReportPeriod.CalendarYear => await GetCalendarYearRangeAsync(),
                DefaultReportPeriod.LastMonth => (DateTime.Today.AddMonths(-1), DateTime.Today),
                DefaultReportPeriod.Last90Days => (DateTime.Today.AddDays(-90), DateTime.Today),
                DefaultReportPeriod.AllTime => (new DateTime(2020, 1, 1), DateTime.Today),
                _ => await GetCurrentFinancialYearRangeAsync()
            };
        }

        public async Task<(DateTime start, DateTime end)> GetCurrentFinancialYearRangeAsync()
        {
            var settings = await GetCompanySettingsAsync();

            // Prefer the explicitly-set current period from the database — it is the
            // authoritative source and may differ from the computed range when the
            // financial year has been manually rolled over.
            if (settings.CurrentFinancialPeriodId.HasValue)
            {
                var currentPeriod = await _context.FinancialPeriods
                    .FirstOrDefaultAsync(p => p.Id == settings.CurrentFinancialPeriodId.Value);

                if (currentPeriod != null)
                {
                    return (currentPeriod.StartDate, currentPeriod.EndDate);
                }
            }

            // Fall back: find any open period that contains today
            var today = DateTime.Today;
            var activePeriod = await _context.FinancialPeriods
                .Where(p => p.StartDate <= today && p.EndDate >= today && !p.IsClosed)
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            if (activePeriod != null)
            {
                return (activePeriod.StartDate, activePeriod.EndDate);
            }

            // Last resort: compute from CompanySettings (handles case where no period
            // records exist yet)
            var start = settings.GetCurrentFinancialYearStart();
            var end = settings.GetCurrentFinancialYearEnd();
            return (start, end);
        }

        public async Task<(DateTime start, DateTime end)> GetPreviousFinancialYearRangeAsync()
        {
            var (currentStart, _) = await GetCurrentFinancialYearRangeAsync();
            var previousStart = currentStart.AddYears(-1);
            var previousEnd = currentStart.AddDays(-1);
            return (previousStart, previousEnd);
        }

        public async Task<(DateTime start, DateTime end)> GetCalendarYearRangeAsync(int? year = null)
        {
            var targetYear = year ?? DateTime.Today.Year;
            return (new DateTime(targetYear, 1, 1), new DateTime(targetYear, 12, 31));
        }

        private async Task CreateCurrentFinancialYearIfNeeded(CompanySettings settings)
        {
            // Check for an existing period without going through GetCurrentFinancialPeriodAsync,
            // which would call GetCompanySettingsAsync again and create a second insert.
            FinancialPeriod? currentPeriod = null;

            if (settings.CurrentFinancialPeriodId.HasValue)
            {
                currentPeriod = await _context.FinancialPeriods
                    .FirstOrDefaultAsync(p => p.Id == settings.CurrentFinancialPeriodId.Value);
            }

            if (currentPeriod == null)
            {
                var today = DateTime.Today;
                currentPeriod = await _context.FinancialPeriods
                    .FirstOrDefaultAsync(p => p.StartDate <= today && p.EndDate >= today && !p.IsClosed);
            }

            if (currentPeriod == null)
            {
                await CreateNextFinancialYearCoreAsync(settings);
            }
        }

        public async Task<FinancialPeriod> CreateNextFinancialYearAsync()
        {
            // Public entry point: load settings normally then delegate.
            var settings = await GetCompanySettingsAsync();
            return await CreateNextFinancialYearCoreAsync(settings);
        }

        // ?? Private helpers ???????????????????????????????????????????????????

        /// <summary>
        /// Core logic for creating the next financial year period.
        /// Accepts an already-loaded CompanySettings to avoid any recursive
        /// call back into GetCompanySettingsAsync.
        /// </summary>
        private async Task<FinancialPeriod> CreateNextFinancialYearCoreAsync(CompanySettings settings)
        {
            var start = settings.GetCurrentFinancialYearStart();
            var end = settings.GetCurrentFinancialYearEnd();

            var period = new FinancialPeriod
            {
                PeriodName = settings.FinancialYearStartMonth == 1 ?
                    $"Calendar Year {start.Year}" :
                    $"FY {start.Year}-{end.Year}",
                StartDate = start,
                EndDate = end,
                IsCurrentPeriod = true,
                PeriodType = FinancialPeriodType.Annual,
                Description = $"Financial year from {start:MMM dd, yyyy} to {end:MMM dd, yyyy}"
            };

            _context.FinancialPeriods.Add(period);
            await _context.SaveChangesAsync();

            // Set as current period
            settings.CurrentFinancialPeriodId = period.Id;
            await _context.SaveChangesAsync();

            return period;
        }

        // Implement other interface methods...
        public async Task<IEnumerable<FinancialPeriod>> GetAllFinancialPeriodsAsync()
        {
            return await _context.FinancialPeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
        }

        public async Task<FinancialPeriod?> GetFinancialPeriodByIdAsync(int id)
        {
            return await _context.FinancialPeriods.FindAsync(id);
        }

        public async Task<FinancialPeriod?> GetFinancialPeriodForDateAsync(DateTime date)
        {
            return await _context.FinancialPeriods
                .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date);
        }

        public async Task<FinancialPeriod> CreateFinancialPeriodAsync(FinancialPeriod period)
        {
            _context.FinancialPeriods.Add(period);
            await _context.SaveChangesAsync();
            return period;
        }

        public async Task<FinancialPeriod> UpdateFinancialPeriodAsync(FinancialPeriod period)
        {
            _context.FinancialPeriods.Update(period);
            await _context.SaveChangesAsync();
            return period;
        }

        public async Task<bool> DeleteFinancialPeriodAsync(int id)
        {
            var period = await _context.FinancialPeriods.FindAsync(id);
            if (period != null && !period.IsClosed)
            {
                _context.FinancialPeriods.Remove(period);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> SetCurrentFinancialPeriodAsync(int periodId)
        {
            var settings = await GetCompanySettingsAsync();
            settings.CurrentFinancialPeriodId = periodId;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CloseFinancialPeriodAsync(int periodId)
        {
            var period = await GetFinancialPeriodByIdAsync(periodId);
            if (period != null)
            {
                period.IsClosed = true;
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<IEnumerable<FinancialPeriod>> CreateQuarterlyPeriodsAsync(int financialYear)
        {
            var settings = await GetCompanySettingsAsync();
            var (yearStart, yearEnd) = settings.GetFinancialYear(financialYear);
            
            var quarters = new List<FinancialPeriod>();
            
            for (int q = 1; q <= 4; q++)
            {
                var quarterStart = yearStart.AddMonths((q - 1) * 3);
                var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
                
                if (quarterEnd > yearEnd) quarterEnd = yearEnd;
                
                var quarter = new FinancialPeriod
                {
                    PeriodName = $"Q{q} {financialYear}",
                    StartDate = quarterStart,
                    EndDate = quarterEnd,
                    PeriodType = FinancialPeriodType.Quarterly,
                    Description = $"Quarter {q} of financial year {financialYear}"
                };
                
                quarters.Add(quarter);
            }
            
            _context.FinancialPeriods.AddRange(quarters);
            await _context.SaveChangesAsync();
            
            return quarters;
        }

        public async Task<bool> IsDateInCurrentFinancialYearAsync(DateTime date)
        {
            var (start, end) = await GetCurrentFinancialYearRangeAsync();
            return date >= start && date <= end;
        }

        public async Task<bool> CanCreateTransactionForDateAsync(DateTime date)
        {
            var period = await GetFinancialPeriodForDateAsync(date);
            return period != null && !period.IsClosed;
        }

        // NEW: Financial Year Closing Implementation
        public async Task<bool> CloseFinancialYearAsync(int periodId, string closingNotes, string? closedBy = null)
        {
            try
            {
                var period = await GetFinancialPeriodByIdAsync(periodId);
                if (period == null || period.IsClosed)
                {
                    return false;
                }

                // Perform final validations
                if (!await CanCloseFinancialYearAsync(periodId))
                {
                    return false;
                }

                // Close the period
                period.IsClosed = true;
                period.ClosedDate = DateTime.Now;
                period.ClosedBy = closedBy;
                period.ClosingNotes = closingNotes;
                period.LastModified = DateTime.Now;
                period.LastModifiedBy = closedBy;

                // If this was the current period, clear the current period setting
                var settings = await GetCompanySettingsAsync();
                if (settings.CurrentFinancialPeriodId == periodId)
                {
                    settings.CurrentFinancialPeriodId = null;
                    settings.LastUpdated = DateTime.Now;
                    settings.UpdatedBy = closedBy;
                }

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Financial year {PeriodName} (ID: {PeriodId}) closed by {ClosedBy}", 
                    period.PeriodName, periodId, closedBy);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing financial year {PeriodId}", periodId);
                return false;
            }
        }

        public async Task<bool> CanCloseFinancialYearAsync(int periodId)
        {
            try
            {
                var period = await GetFinancialPeriodByIdAsync(periodId);
                if (period == null || period.IsClosed)
                {
                    return false;
                }

                // Check if trial balance is balanced
                var entries = await _context.GeneralLedgerEntries
                    .Where(e => e.TransactionDate >= period.StartDate && e.TransactionDate <= period.EndDate)
                    .ToListAsync();

                var totalDebits = entries.Sum(e => e.DebitAmount);
                var totalCredits = entries.Sum(e => e.CreditAmount);

                // Must be balanced within 1 cent
                var isBalanced = Math.Abs(totalDebits - totalCredits) < 0.01m;

                return isBalanced;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if financial year {PeriodId} can be closed", periodId);
                return false;
            }
        }

        public async Task<bool> ReopenFinancialYearAsync(int periodId, string? reopenedBy = null)
        {
            try
            {
                var period = await GetFinancialPeriodByIdAsync(periodId);
                if (period == null || !period.IsClosed)
                {
                    return false;
                }

                // Reopen the period
                period.IsClosed = false;
                period.ClosedDate = null;
                period.ClosedBy = null;
                period.ClosingNotes = null;
                period.LastModified = DateTime.Now;
                period.LastModifiedBy = reopenedBy;

                await _context.SaveChangesAsync();
                
                _logger.LogWarning("Financial year {PeriodName} (ID: {PeriodId}) reopened by {ReopenedBy}", 
                    period.PeriodName, periodId, reopenedBy);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reopening financial year {PeriodId}", periodId);
                return false;
            }
        }

        public async Task<bool> IsFinancialYearBalancedAsync(int periodId)
        {
            try
            {
                var period = await GetFinancialPeriodByIdAsync(periodId);
                if (period == null) return false;

                var entries = await _context.GeneralLedgerEntries
                    .Where(e => e.TransactionDate >= period.StartDate && e.TransactionDate <= period.EndDate)
                    .ToListAsync();

                var totalDebits = entries.Sum(e => e.DebitAmount);
                var totalCredits = entries.Sum(e => e.CreditAmount);

                return Math.Abs(totalDebits - totalCredits) < 0.01m;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetFinancialYearCloseValidationErrorsAsync(int periodId)
        {
            var errors = new List<string>();

            try
            {
                var period = await GetFinancialPeriodByIdAsync(periodId);
                if (period == null)
                {
                    errors.Add("Financial period not found");
                    return errors;
                }

                if (period.IsClosed)
                {
                    errors.Add("Financial period is already closed");
                    return errors;
                }

                // Check trial balance
                if (!await IsFinancialYearBalancedAsync(periodId))
                {
                    errors.Add("Trial balance is not balanced - debits must equal credits");
                }

                // Check for transactions after period end
                var transactionsAfterPeriod = await _context.GeneralLedgerEntries
                    .AnyAsync(e => e.TransactionDate > period.EndDate && 
                                  e.TransactionDate >= period.StartDate);

                if (transactionsAfterPeriod)
                {
                    errors.Add("There are transactions dated after the period end date");
                }

                // Add other validation checks as needed
                // - Pending approvals
                // - Unreconciled accounts
                // - Missing required adjustments
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating financial year close for period {PeriodId}", periodId);
                errors.Add("Error performing validation checks");
            }

            return errors;
        }
    }
}