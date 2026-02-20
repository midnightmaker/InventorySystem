using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Models.Accounting
{
    public class FinancialPeriod
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Period Name")]
        public string PeriodName { get; set; } = string.Empty; // e.g., "FY 2024", "2024 Calendar Year"

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Is Current Period")]
        public bool IsCurrentPeriod { get; set; } = false;

        [Display(Name = "Is Closed")]
        public bool IsClosed { get; set; } = false;

        [Display(Name = "Period Type")]
        public FinancialPeriodType PeriodType { get; set; } = FinancialPeriodType.Annual;

        [StringLength(200)]
        public string? Description { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }

        // NEW: Financial Year Closing Properties
        [Display(Name = "Closed Date")]
        public DateTime? ClosedDate { get; set; }

        [Display(Name = "Closed By")]
        [StringLength(100)]
        public string? ClosedBy { get; set; }

        [Display(Name = "Closing Notes")]
        [StringLength(1000)]
        public string? ClosingNotes { get; set; }

        [Display(Name = "Last Modified")]
        public DateTime? LastModified { get; set; }

        [Display(Name = "Last Modified By")]
        [StringLength(100)]
        public string? LastModifiedBy { get; set; }

        // Computed properties
        [Display(Name = "Duration (Days)")]
        public int DurationInDays => (EndDate - StartDate).Days + 1;

        [Display(Name = "Is Active")]
        public bool IsActive => DateTime.Today >= StartDate && DateTime.Today <= EndDate;

        public string FormattedPeriod => $"{StartDate:MMM dd, yyyy} - {EndDate:MMM dd, yyyy}";

        // Helper methods
        public bool ContainsDate(DateTime date) => date >= StartDate && date <= EndDate;

        public bool IsCalendarYear => 
            StartDate.Month == 1 && StartDate.Day == 1 && 
            EndDate.Month == 12 && EndDate.Day == 31 &&
            StartDate.Year == EndDate.Year;

        // NEW: Year-end closing validation
        public bool CanBeClosed => !IsClosed && DateTime.Today >= EndDate;

        public string StatusDescription
        {
            get
            {
                if (IsClosed)
                    return $"Closed on {ClosedDate?.ToString("MMM dd, yyyy")} by {ClosedBy}";
                if (IsActive)
                    return "Currently Active";
                if (DateTime.Today < StartDate)
                    return "Future Period";
                return "Past Period";
            }
        }
    }

    public enum FinancialPeriodType
    {
        Annual = 1,
        Quarterly = 2,
        Monthly = 3,
        Custom = 4
    }

    public class CompanySettings
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Display(Name = "Financial Year Start Month")]
        [Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
        public int FinancialYearStartMonth { get; set; } = 1; // Default: January (calendar year)

        [Display(Name = "Financial Year Start Day")]
        [Range(1, 31, ErrorMessage = "Day must be between 1 and 31")]
        public int FinancialYearStartDay { get; set; } = 1; // Default: 1st of month

        [Display(Name = "Current Financial Period")]
        public int? CurrentFinancialPeriodId { get; set; }
        public virtual FinancialPeriod? CurrentFinancialPeriod { get; set; }

        [Display(Name = "Default Report Period")]
        public DefaultReportPeriod DefaultReportPeriod { get; set; } = DefaultReportPeriod.CurrentFinancialYear;

        [Display(Name = "Auto-Create Financial Periods")]
        public bool AutoCreateFinancialPeriods { get; set; } = true;

        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public string? UpdatedBy { get; set; }

        // Helper methods
        public DateTime GetCurrentFinancialYearStart()
        {
            var today = DateTime.Today;
            var currentYear = today.Year;
            
            var fyStart = new DateTime(currentYear, FinancialYearStartMonth, FinancialYearStartDay);
            
            // If we're before the FY start date, use previous year
            if (today < fyStart)
            {
                fyStart = fyStart.AddYears(-1);
            }
            
            return fyStart;
        }

        public DateTime GetCurrentFinancialYearEnd()
        {
            return GetCurrentFinancialYearStart().AddYears(1).AddDays(-1);
        }

        public (DateTime start, DateTime end) GetFinancialYear(int year)
        {
            var start = new DateTime(year, FinancialYearStartMonth, FinancialYearStartDay);
            var end = start.AddYears(1).AddDays(-1);
            return (start, end);
        }
    }

    public enum DefaultReportPeriod
    {
        CurrentFinancialYear = 1,
        CalendarYear = 2,
        LastMonth = 3,
        Last90Days = 4,
        AllTime = 5
    }
}