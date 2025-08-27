using InventorySystem.Models.Accounting;

namespace InventorySystem.Services
{
    public interface IFinancialPeriodService
    {
        // Company Settings
        Task<CompanySettings> GetCompanySettingsAsync();
        Task<CompanySettings> UpdateCompanySettingsAsync(CompanySettings settings);

        // Financial Periods
        Task<IEnumerable<FinancialPeriod>> GetAllFinancialPeriodsAsync();
        Task<FinancialPeriod?> GetCurrentFinancialPeriodAsync();
        Task<FinancialPeriod?> GetFinancialPeriodByIdAsync(int id);
        Task<FinancialPeriod?> GetFinancialPeriodForDateAsync(DateTime date);
        Task<FinancialPeriod> CreateFinancialPeriodAsync(FinancialPeriod period);
        Task<FinancialPeriod> UpdateFinancialPeriodAsync(FinancialPeriod period);
        Task<bool> DeleteFinancialPeriodAsync(int id);

        // Period Management
        Task<bool> SetCurrentFinancialPeriodAsync(int periodId);
        Task<bool> CloseFinancialPeriodAsync(int periodId);
        Task<FinancialPeriod> CreateNextFinancialYearAsync();
        Task<IEnumerable<FinancialPeriod>> CreateQuarterlyPeriodsAsync(int financialYear);

        // NEW: Financial Year Closing Methods
        Task<bool> CloseFinancialYearAsync(int periodId, string closingNotes, string? closedBy = null);
        Task<bool> CanCloseFinancialYearAsync(int periodId);
        Task<bool> ReopenFinancialYearAsync(int periodId, string? reopenedBy = null);

        // Default Date Ranges
        Task<(DateTime start, DateTime end)> GetDefaultReportDateRangeAsync();
        Task<(DateTime start, DateTime end)> GetCurrentFinancialYearRangeAsync();
        Task<(DateTime start, DateTime end)> GetPreviousFinancialYearRangeAsync();
        Task<(DateTime start, DateTime end)> GetCalendarYearRangeAsync(int? year = null);

        // Validation
        Task<bool> IsDateInCurrentFinancialYearAsync(DateTime date);
        Task<bool> CanCreateTransactionForDateAsync(DateTime date);

        // NEW: Year-end validation methods
        Task<bool> IsFinancialYearBalancedAsync(int periodId);
        Task<List<string>> GetFinancialYearCloseValidationErrorsAsync(int periodId);
    }
}