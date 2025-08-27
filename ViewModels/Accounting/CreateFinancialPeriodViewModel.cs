using InventorySystem.Models.Accounting;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Accounting
{
    public class CreateFinancialPeriodViewModel
    {
        [Required(ErrorMessage = "Period name is required")]
        [StringLength(50, ErrorMessage = "Period name cannot exceed 50 characters")]
        [Display(Name = "Period Name")]
        public string PeriodName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required")]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "End date is required")]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1).AddDays(-1);

        [Display(Name = "Period Type")]
        public FinancialPeriodType PeriodType { get; set; } = FinancialPeriodType.Annual;

        [StringLength(200, ErrorMessage = "Description cannot exceed 200 characters")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Set as Current Period")]
        public bool IsCurrentPeriod { get; set; } = false;

        // Validation methods
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // Validate that end date is after start date
            if (EndDate <= StartDate)
            {
                results.Add(new ValidationResult(
                    "End date must be after start date", 
                    new[] { nameof(EndDate) }));
            }

            // Validate date range is reasonable
            var daysDifference = (EndDate - StartDate).Days;
            
            switch (PeriodType)
            {
                case FinancialPeriodType.Annual:
                    if (daysDifference < 300 || daysDifference > 400)
                    {
                        results.Add(new ValidationResult(
                            "Annual periods should be approximately one year (300-400 days)",
                            new[] { nameof(EndDate) }));
                    }
                    break;
                
                case FinancialPeriodType.Quarterly:
                    if (daysDifference < 80 || daysDifference > 100)
                    {
                        results.Add(new ValidationResult(
                            "Quarterly periods should be approximately 3 months (80-100 days)",
                            new[] { nameof(EndDate) }));
                    }
                    break;
                
                case FinancialPeriodType.Monthly:
                    if (daysDifference < 25 || daysDifference > 35)
                    {
                        results.Add(new ValidationResult(
                            "Monthly periods should be approximately one month (25-35 days)",
                            new[] { nameof(EndDate) }));
                    }
                    break;
            }

            // Validate that start date is not too far in the past
            if (StartDate < DateTime.Today.AddYears(-5))
            {
                results.Add(new ValidationResult(
                    "Start date cannot be more than 5 years in the past",
                    new[] { nameof(StartDate) }));
            }

            // Validate that end date is not too far in the future
            if (EndDate > DateTime.Today.AddYears(5))
            {
                results.Add(new ValidationResult(
                    "End date cannot be more than 5 years in the future",
                    new[] { nameof(EndDate) }));
            }

            return results;
        }

        // Helper properties for UI display
        public int DurationInDays => (EndDate - StartDate).Days + 1;

        public string FormattedPeriod => $"{StartDate:MMM dd, yyyy} - {EndDate:MMM dd, yyyy}";

        public bool IsCalendarYear => 
            StartDate.Month == 1 && StartDate.Day == 1 && 
            EndDate.Month == 12 && EndDate.Day == 31 &&
            StartDate.Year == EndDate.Year;

        // Helper method to generate suggested period name
        public string GetSuggestedPeriodName()
        {
            return PeriodType switch
            {
                FinancialPeriodType.Annual when IsCalendarYear => $"Calendar Year {StartDate.Year}",
                FinancialPeriodType.Annual => $"FY {StartDate.Year}-{EndDate.Year}",
                FinancialPeriodType.Quarterly => $"Q{GetQuarterNumber()} {StartDate.Year}",
                FinancialPeriodType.Monthly => $"{StartDate:MMMM yyyy}",
                FinancialPeriodType.Custom => $"Custom Period {StartDate:MMM yyyy}",
                _ => $"Period {StartDate:MMM yyyy}"
            };
        }

        private int GetQuarterNumber()
        {
            return StartDate.Month switch
            {
                1 or 2 or 3 => 1,
                4 or 5 or 6 => 2,
                7 or 8 or 9 => 3,
                10 or 11 or 12 => 4,
                _ => 1
            };
        }

        // Method to convert to FinancialPeriod entity
        public FinancialPeriod ToFinancialPeriod(string? createdBy = null)
        {
            return new FinancialPeriod
            {
                PeriodName = PeriodName,
                StartDate = StartDate,
                EndDate = EndDate,
                PeriodType = PeriodType,
                Description = Description,
                IsCurrentPeriod = IsCurrentPeriod,
                IsClosed = false,
                CreatedDate = DateTime.Now,
                CreatedBy = createdBy
            };
        }

        // Static method to create from existing period (for copying/templating)
        public static CreateFinancialPeriodViewModel FromExistingPeriod(FinancialPeriod existingPeriod)
        {
            return new CreateFinancialPeriodViewModel
            {
                PeriodName = existingPeriod.PeriodName,
                StartDate = existingPeriod.StartDate,
                EndDate = existingPeriod.EndDate,
                PeriodType = existingPeriod.PeriodType,
                Description = existingPeriod.Description,
                IsCurrentPeriod = false // Never copy this flag
            };
        }

        // Static method to create next financial year template
        public static CreateFinancialPeriodViewModel CreateNextFinancialYearTemplate(
            CompanySettings companySettings, 
            FinancialPeriod? currentPeriod = null)
        {
            DateTime startDate;
            DateTime endDate;

            if (currentPeriod != null)
            {
                // Create the next year based on current period
                startDate = currentPeriod.EndDate.AddDays(1);
                endDate = startDate.AddYears(1).AddDays(-1);
            }
            else
            {
                // Create based on company settings
                var currentYear = DateTime.Today.Year;
                startDate = new DateTime(currentYear, companySettings.FinancialYearStartMonth, companySettings.FinancialYearStartDay);
                
                // If we're past the start date, use next year
                if (DateTime.Today >= startDate)
                {
                    startDate = startDate.AddYears(1);
                }
                
                endDate = startDate.AddYears(1).AddDays(-1);
            }

            var isCalendarYear = companySettings.FinancialYearStartMonth == 1 && 
                               companySettings.FinancialYearStartDay == 1;

            return new CreateFinancialPeriodViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                PeriodType = FinancialPeriodType.Annual,
                PeriodName = isCalendarYear ? 
                    $"Calendar Year {startDate.Year}" : 
                    $"FY {startDate.Year}-{endDate.Year}",
                Description = $"Financial year from {startDate:MMM dd, yyyy} to {endDate:MMM dd, yyyy}",
                IsCurrentPeriod = currentPeriod == null // Set as current if no current period exists
            };
        }
    }
}