using InventorySystem.Models.Accounting;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Accounting
{
   
    public class FinancialPeriodViewModel
    {
        public FinancialPeriod? CurrentPeriod { get; set; }
        public List<FinancialPeriod> AvailablePeriods { get; set; } = new();
        public CompanySettings CompanySettings { get; set; } = null!;
        
        // Quick period selections
        public (DateTime start, DateTime end) CurrentFinancialYear { get; set; }
        public (DateTime start, DateTime end) PreviousFinancialYear { get; set; }
        public (DateTime start, DateTime end) CurrentCalendarYear { get; set; }
    }

    public class CompanySettingsViewModel
    {
        [Required, StringLength(100)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Display(Name = "Financial Year Start Month")]
        [Range(1, 12)]
        public int FinancialYearStartMonth { get; set; } = 1;

        [Display(Name = "Financial Year Start Day")]
        [Range(1, 31)]
        public int FinancialYearStartDay { get; set; } = 1;

        [Display(Name = "Default Report Period")]
        public DefaultReportPeriod DefaultReportPeriod { get; set; } = DefaultReportPeriod.CurrentFinancialYear;

        [Display(Name = "Auto-Create Financial Periods")]
        public bool AutoCreateFinancialPeriods { get; set; } = true;

        // Helper properties
        public string FinancialYearStartDisplay => 
            new DateTime(DateTime.Today.Year, FinancialYearStartMonth, FinancialYearStartDay).ToString("MMMM dd");

        public bool IsCalendarYear => FinancialYearStartMonth == 1 && FinancialYearStartDay == 1;
    }

    
  
    
    
}