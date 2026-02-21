using InventorySystem.Models;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels
{
    /// <summary>
    /// Represents a single customer payment record extracted from sales data
    /// </summary>
    public class CustomerPaymentRecord
    {
        public int SaleId { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        
        [Display(Name = "Payment Date")]
        public DateTime PaymentDate { get; set; }
        
        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }
        
        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }
        
        [Display(Name = "Notes")]
        public string? PaymentNotes { get; set; }

        // Navigation properties for convenience
        public Sale? Sale { get; set; }
        public Customer? Customer { get; set; }
    }

    /// <summary>
    /// ViewModel for customer payment details page
    /// </summary>
    public class CustomerPaymentDetailsViewModel
    {
        public Sale Sale { get; set; } = null!;
        public List<CustomerPaymentRecord> Payments { get; set; } = new();
        
        [Display(Name = "Total Paid")]
        [DataType(DataType.Currency)]
        public decimal TotalPaid { get; set; }
        
        [Display(Name = "Remaining Balance")]
        [DataType(DataType.Currency)]
        public decimal RemainingBalance { get; set; }

        // Helper properties
        public bool IsFullyPaid => RemainingBalance <= 0;
        public int PaymentCount => Payments.Count;
        public DateTime? LastPaymentDate => Payments.Any() ? Payments.Max(p => p.PaymentDate) : null;
        public string? MostRecentPaymentMethod => Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.PaymentMethod;
    }

    /// <summary>
    /// ViewModel for customer payments reports
    /// </summary>
    public class CustomerPaymentsReportViewModel
    {
        [Display(Name = "Report Start Date")]
        public DateTime StartDate { get; set; }
        
        [Display(Name = "Report End Date")]
        public DateTime EndDate { get; set; }
        
        [Display(Name = "Total Payments")]
        [DataType(DataType.Currency)]
        public decimal TotalPayments { get; set; }
        
        [Display(Name = "Payment Count")]
        public int PaymentCount { get; set; }
        
        [Display(Name = "Average Payment")]
        [DataType(DataType.Currency)]
        public decimal AveragePayment { get; set; }

        public List<PaymentMethodSummary> PaymentsByMethod { get; set; } = new();
        public List<CustomerPaymentSummary> TopCustomers { get; set; } = new();
        public List<MonthlyPaymentTrend> MonthlyTrends { get; set; } = new();

        // Helper properties
        public string DateRangeDisplay => $"{StartDate:MM/dd/yyyy} - {EndDate:MM/dd/yyyy}";
        public bool HasData => PaymentCount > 0;
    }

    /// <summary>
    /// Summary of payments by payment method
    /// </summary>
    public class PaymentMethodSummary
    {
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = string.Empty;
        
        [Display(Name = "Count")]
        public int Count { get; set; }
        
        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }
        
        [Display(Name = "Percentage")]
        public decimal Percentage { get; set; }

        public string PercentageDisplay => $"{Percentage:F1}%";
    }

    /// <summary>
    /// Summary of payments by customer
    /// </summary>
    public class CustomerPaymentSummary
    {
        public int CustomerId { get; set; }
        
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;
        
        [Display(Name = "Payment Count")]
        public int PaymentCount { get; set; }
        
        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }
        
        [Display(Name = "Average Payment")]
        [DataType(DataType.Currency)]
        public decimal AveragePayment { get; set; }
        
        [Display(Name = "Last Payment Date")]
        public DateTime LastPaymentDate { get; set; }
    }

    /// <summary>
    /// Monthly payment trend data
    /// </summary>
    public class MonthlyPaymentTrend
    {
        public int Year { get; set; }
        public int Month { get; set; }
        
        [Display(Name = "Month")]
        public string MonthName { get; set; } = string.Empty;
        
        [Display(Name = "Payment Count")]
        public int PaymentCount { get; set; }
        
        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }
        
        [Display(Name = "Average Payment")]
        [DataType(DataType.Currency)]
        public decimal AveragePayment { get; set; }
    }

    /// <summary>
    /// Search filters for customer payments
    /// </summary>
    public class CustomerPaymentSearchFilters
    {
        [Display(Name = "Search")]
        public string? Search { get; set; }
        
        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }
        
        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }
        
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }
        
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }
        
        [Display(Name = "Sort Order")]
        public string SortOrder { get; set; } = "date_desc";
        
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }
}